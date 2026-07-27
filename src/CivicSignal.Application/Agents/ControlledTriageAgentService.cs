using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Abstractions.Weather;
using CivicSignal.Application.Agents.Models;
using CivicSignal.Application.Common;
using CivicSignal.Domain.Incidents;

namespace CivicSignal.Application.Agents;

internal sealed class ControlledTriageAgentService(
    IIncidentRepository incidents,
    IWeatherService weather,
    IUnitOfWork unitOfWork,
    IClock clock) : IControlledTriageAgentService
{
    private const string WorkflowStepName = "ControlledAgentWorkflow";
    private const double HighConfidenceThreshold = 0.75;

    public async Task<ControlledTriageWorkflowDto> RunAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Incident), incidentId);

        var stepAlreadyStarted = incident.ProcessingSteps.Any(step =>
            string.Equals(step.Name, WorkflowStepName, StringComparison.OrdinalIgnoreCase)
            && step.Status is ProcessingStepStatus.InProgress);
        var startedStep = false;
        try
        {
            if (!stepAlreadyStarted)
            {
                incident.StartProcessingStep(WorkflowStepName, clock.UtcNow);
                startedStep = true;
            }

            var workflow = await BuildWorkflowAsync(incident, cancellationToken);
            PersistWorkflowEvidence(incident, workflow);
            if (workflow.RequiresHumanReview)
            {
                incident.RequireHumanReview(clock.UtcNow);
            }

            if (!stepAlreadyStarted)
            {
                incident.CompleteProcessingStep(WorkflowStepName, clock.UtcNow);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return workflow;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (startedStep)
            {
                incident.FailProcessingStep(WorkflowStepName, exception.Message, clock.UtcNow);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private async Task<ControlledTriageWorkflowDto> BuildWorkflowAsync(
        Incident incident,
        CancellationToken cancellationToken)
    {
        var completedAt = clock.UtcNow;
        var toolRuns = new List<AgentToolRunDto>();
        var latestPrediction = incident.TriagePredictions
            .OrderByDescending(prediction => prediction.CreatedAt)
            .FirstOrDefault();
        var category = latestPrediction?.Category.Value
            ?? incident.CorrectedCategory
            ?? "GeneralIncident";
        var agencyCode = latestPrediction?.SuggestedAgency.Value
            ?? incident.CorrectedAgencyCode
            ?? "CITYOPS";
        var severity = latestPrediction?.Severity
            ?? incident.CorrectedSeverity
            ?? IncidentSeverity.Medium;

        toolRuns.Add(new AgentToolRunDto(
            "understand_complaint",
            "Succeeded",
            "Read stored incident description and coordinates.",
            $"Incident has {NormalizeCategory(category)} category signal at {incident.Location.Latitude:0.00000}, {incident.Location.Longitude:0.00000}.",
            latestPrediction?.Confidence.Value,
            completedAt));

        toolRuns.Add(new AgentToolRunDto(
            "collect_available_evidence",
            "Succeeded",
            "Inspect stored text, media analysis results, and latest prediction.",
            BuildEvidenceSummary(incident, latestPrediction),
            latestPrediction?.Confidence.Value,
            completedAt));

        var weatherResult = await weather.GetCurrentConditionsAsync(
            incident.Location.Latitude,
            incident.Location.Longitude,
            incident.CreatedAt,
            cancellationToken);
        var weatherContext = weatherResult.ToDto();
        toolRuns.Add(new AgentToolRunDto(
            "get_weather",
            weatherResult.IsAvailable ? "Succeeded" : "Unavailable",
            "Fetch current or latest available public weather context for incident coordinates.",
            BuildWeatherSummary(weatherResult),
            weatherResult.IsAvailable ? 0.85 : null,
            weatherResult.RetrievedAt));

        var duplicateCandidates = incident.DuplicateCandidates
            .OrderByDescending(candidate => candidate.SimilarityScore.Value)
            .ThenByDescending(candidate => candidate.UpdatedAt)
            .ToArray();
        var topDuplicate = duplicateCandidates.FirstOrDefault();
        toolRuns.Add(new AgentToolRunDto(
            "search_nearby_cases",
            "Succeeded",
            "Use stored pgvector/PostGIS duplicate candidates.",
            topDuplicate is null
                ? "No stored duplicate candidates are above the configured threshold."
                : $"Top candidate {topDuplicate.CandidateIncidentId} scored {topDuplicate.SimilarityScore.Value:0.00}.",
            topDuplicate?.SimilarityScore.Value,
            completedAt));

        var policy = RetrieveServicePolicy(category);
        toolRuns.Add(new AgentToolRunDto(
            "retrieve_service_policy",
            policy.IsKnown ? "Succeeded" : "NeedsReview",
            $"Lookup local routing policy for {NormalizeCategory(category)}.",
            policy.Description,
            policy.IsKnown ? 0.9 : 0.35,
            completedAt));

        var agencyPrediction = PredictResponsibleAgency(category, agencyCode, policy);
        toolRuns.Add(new AgentToolRunDto(
            "predict_responsible_agency",
            agencyPrediction.IsKnown ? "Succeeded" : "NeedsReview",
            "Use latest model output and controlled routing policy.",
            agencyPrediction.Description,
            agencyPrediction.IsKnown ? latestPrediction?.Confidence.Value ?? 0.7 : 0.35,
            completedAt));

        var slaRisk = CalculateSlaRisk(severity, latestPrediction?.Confidence.Value, topDuplicate?.SimilarityScore.Value, weatherResult);
        toolRuns.Add(new AgentToolRunDto(
            "calculate_sla_risk",
            "Succeeded",
            "Combine severity, model confidence, duplicate pressure, and weather context.",
            $"SLA risk score is {slaRisk:0.00}.",
            slaRisk,
            completedAt));

        var reviewReason = DetermineReviewReason(latestPrediction, policy, agencyPrediction, topDuplicate);
        var draftWorkOrder = reviewReason is null
            ? CreateDraftWorkOrder(incident, category, agencyPrediction.AgencyCode, severity, slaRisk, toolRuns)
            : null;
        toolRuns.Add(new AgentToolRunDto(
            "create_draft_work_order",
            draftWorkOrder is null ? "Skipped" : "Succeeded",
            "Create a draft work order only when confidence and policy checks pass.",
            draftWorkOrder is null
                ? $"Draft was not created: {reviewReason}"
                : $"Draft work order prepared for {draftWorkOrder.AgencyCode} with {draftWorkOrder.Priority} priority.",
            draftWorkOrder is null ? null : 0.82,
            completedAt));

        return new ControlledTriageWorkflowDto(
            incident.Id,
            draftWorkOrder is null ? "human_review_required" : "draft_work_order_ready",
            draftWorkOrder is null,
            reviewReason,
            slaRisk,
            weatherContext,
            draftWorkOrder,
            toolRuns);
    }

    private void PersistWorkflowEvidence(Incident incident, ControlledTriageWorkflowDto workflow)
    {
        var prediction = incident.TriagePredictions
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        if (prediction is null)
        {
            return;
        }

        foreach (var toolRun in workflow.ToolRuns)
        {
            var detail = TrimEvidence($"{toolRun.Status}: {toolRun.OutputSummary}");
            if (prediction.EvidenceItems.Any(item =>
                    string.Equals(item.Kind, "AgentTool", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Title, toolRun.ToolName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Detail, detail, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            prediction.AddEvidence(
                "AgentTool",
                toolRun.ToolName,
                detail,
                toolRun.Confidence,
                clock.UtcNow);
        }
    }

    private static string BuildEvidenceSummary(Incident incident, TriagePrediction? latestPrediction)
    {
        var mediaCount = incident.MediaItems.Count;
        var analyzedMediaCount = incident.MediaItems.Count(media =>
            string.Equals(media.AnalysisStatus.ToString(), "Succeeded", StringComparison.OrdinalIgnoreCase));

        if (latestPrediction is null)
        {
            return $"Text is available; {mediaCount} media item(s), {analyzedMediaCount} analyzed; no model prediction exists yet.";
        }

        return $"Latest prediction is {latestPrediction.Category.Value}/{latestPrediction.Severity} for {latestPrediction.SuggestedAgency.Value} at {latestPrediction.Confidence.Value:0.00}; {mediaCount} media item(s), {analyzedMediaCount} analyzed.";
    }

    private static string BuildWeatherSummary(WeatherObservationResult weatherResult)
    {
        if (!weatherResult.IsAvailable)
        {
            return weatherResult.UnavailableReason ?? "Weather context is unavailable.";
        }

        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(weatherResult.Summary))
        {
            pieces.Add(weatherResult.Summary);
        }

        if (weatherResult.TemperatureCelsius is not null)
        {
            pieces.Add($"{weatherResult.TemperatureCelsius:0.#}C");
        }

        if (weatherResult.WindSpeedKph is not null)
        {
            pieces.Add($"wind {weatherResult.WindSpeedKph:0.#} kph");
        }

        if (weatherResult.PrecipitationLastHourMillimeters is not null)
        {
            pieces.Add($"precipitation {weatherResult.PrecipitationLastHourMillimeters:0.#} mm last hour");
        }

        if (!string.IsNullOrWhiteSpace(weatherResult.SevereAlertSummary))
        {
            pieces.Add(weatherResult.SevereAlertSummary);
        }

        return pieces.Count == 0
            ? "Weather provider returned an observation without detailed conditions."
            : string.Join("; ", pieces);
    }

    private static ServicePolicy RetrieveServicePolicy(string category)
    {
        return NormalizeCategory(category) switch
        {
            "RoadDamage" => new ServicePolicy("DOT", "Road damage reports route to DOT street maintenance. Critical or lane-blocking cases require urgent review.", true),
            "Flooding" => new ServicePolicy("WATER", "Flooding and blocked drainage reports route to WATER operations, especially during rain or flood alerts.", true),
            "Streetlight" => new ServicePolicy("UTILITIES", "Streetlight and signal-light outages route to UTILITIES for electrical field response.", true),
            "Sanitation" => new ServicePolicy("SANITATION", "Illegal dumping, trash, and debris route to SANITATION for cleanup scheduling.", true),
            "Graffiti" => new ServicePolicy("PUBLICWORKS", "Graffiti and public vandalism route to PUBLICWORKS unless safety-sensitive content requires review.", true),
            "TreeHazard" => new ServicePolicy("PARKS", "Tree limbs, fallen branches, and park-path obstructions route to PARKS forestry response.", true),
            _ => new ServicePolicy("CITYOPS", "No category-specific routing policy is available; send to city operations review.", false)
        };
    }

    private static AgencyPrediction PredictResponsibleAgency(string category, string agencyCode, ServicePolicy policy)
    {
        var normalizedAgency = string.IsNullOrWhiteSpace(agencyCode)
            ? policy.AgencyCode
            : agencyCode.Trim().ToUpperInvariant();

        if (!policy.IsKnown)
        {
            return new AgencyPrediction(
                normalizedAgency,
                false,
                $"Agency prediction remains {normalizedAgency}, but no controlled routing policy exists for {NormalizeCategory(category)}.");
        }

        if (!string.Equals(normalizedAgency, policy.AgencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return new AgencyPrediction(
                policy.AgencyCode,
                true,
                $"Policy corrected agency from {normalizedAgency} to {policy.AgencyCode}.");
        }

        return new AgencyPrediction(
            normalizedAgency,
            true,
            $"Agency {normalizedAgency} matches controlled routing policy.");
    }

    private static double CalculateSlaRisk(
        IncidentSeverity severity,
        double? confidence,
        double? duplicateScore,
        WeatherObservationResult weatherResult)
    {
        var baseRisk = severity switch
        {
            IncidentSeverity.Critical => 0.92,
            IncidentSeverity.High => 0.72,
            IncidentSeverity.Medium => 0.48,
            IncidentSeverity.Low => 0.24,
            _ => 0.4
        };
        var confidenceRisk = confidence is null ? 0.08 : Math.Max(0, 0.7 - confidence.Value) * 0.25;
        var duplicateRisk = duplicateScore >= 0.85 ? 0.08 : 0;
        var weatherRisk = WeatherIncreasesRisk(weatherResult) ? 0.12 : 0;

        return Math.Round(Math.Clamp(baseRisk + confidenceRisk + duplicateRisk + weatherRisk, 0, 1), 4);
    }

    private static bool WeatherIncreasesRisk(WeatherObservationResult weatherResult)
    {
        if (!weatherResult.IsAvailable)
        {
            return false;
        }

        return weatherResult.PrecipitationLastHourMillimeters >= 5
            || weatherResult.WindSpeedKph >= 50
            || (!string.IsNullOrWhiteSpace(weatherResult.SevereAlertSummary)
                && !weatherResult.SevereAlertSummary.Contains("none", StringComparison.OrdinalIgnoreCase));
    }

    private static string? DetermineReviewReason(
        TriagePrediction? latestPrediction,
        ServicePolicy policy,
        AgencyPrediction agencyPrediction,
        DuplicateCandidate? topDuplicate)
    {
        if (latestPrediction is null)
        {
            return "No model prediction exists yet.";
        }

        if (latestPrediction.Confidence.Value < HighConfidenceThreshold)
        {
            return $"Model confidence {latestPrediction.Confidence.Value:0.00} is below {HighConfidenceThreshold:0.00}.";
        }

        if (!policy.IsKnown || !agencyPrediction.IsKnown)
        {
            return "Controlled routing policy could not verify the responsible agency.";
        }

        if (topDuplicate?.SimilarityScore.Value >= 0.9)
        {
            return "A very likely duplicate should be reviewed before creating a new work order.";
        }

        return null;
    }

    private static DraftWorkOrderDto CreateDraftWorkOrder(
        Incident incident,
        string category,
        string agencyCode,
        IncidentSeverity severity,
        double slaRisk,
        IReadOnlyCollection<AgentToolRunDto> toolRuns)
    {
        return new DraftWorkOrderDto(
            $"{NormalizeCategory(category)} response for incident {incident.PublicTrackingCode}",
            agencyCode,
            severity.ToString(),
            TrimEvidence($"{incident.Description} Recommended SLA risk is {slaRisk:P0}."),
            toolRuns
                .Where(run => run.Status is "Succeeded")
                .Select(run => $"{run.ToolName}: {run.OutputSummary}")
                .Take(6)
                .ToArray());
    }

    private static string NormalizeCategory(string category)
    {
        return string.IsNullOrWhiteSpace(category)
            ? "GeneralIncident"
            : category.Trim();
    }

    private static string TrimEvidence(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "No output." : value.Trim();
        return normalized.Length <= 950 ? normalized : $"{normalized[..947]}...";
    }

    private sealed record ServicePolicy(string AgencyCode, string Description, bool IsKnown);

    private sealed record AgencyPrediction(string AgencyCode, bool IsKnown, string Description);
}
