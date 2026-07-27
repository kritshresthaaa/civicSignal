using CivicSignal.Application.Agents;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.Incidents.Models;
using CivicSignal.Worker.Options;
using Microsoft.Extensions.Options;

namespace CivicSignal.Worker.Processing;

public sealed class IncidentProcessingPipeline(
    IIncidentService incidents,
    IIncidentIntelligenceService intelligence,
    IControlledTriageAgentService agentWorkflow,
    IOptions<IncidentProcessingWorkerOptions> options,
    ILogger<IncidentProcessingPipeline> logger)
{
    public Task ProcessAsync(
        IncidentDto incident,
        CancellationToken cancellationToken = default)
    {
        return ProcessAsync(incident, "Polling", cancellationToken);
    }

    public async Task ProcessAsync(
        IncidentDto incident,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldProcessIncident(incident.Status, trigger))
        {
            logger.LogDebug(
                "Skipping incident {IncidentId} for trigger {Trigger} because its status is {IncidentStatus}.",
                incident.Id,
                trigger,
                incident.Status);
            return;
        }

        foreach (var stepName in options.Value.NormalizedSteps)
        {
            await RunStepAsync(incident.Id, stepName, cancellationToken);
        }

        logger.LogInformation("Completed processing pipeline for incident {IncidentId}.", incident.Id);
    }

    private static bool ShouldProcessIncident(string status, string trigger)
    {
        if (string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(trigger, "IncidentMediaUploaded", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return status.Equals("Triaged", StringComparison.OrdinalIgnoreCase)
            || status.Equals("HumanReviewRequired", StringComparison.OrdinalIgnoreCase)
            || status.Equals("NeedsMoreInfo", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunStepAsync(Guid incidentId, string stepName, CancellationToken cancellationToken)
    {
        var stepStarted = false;

        try
        {
            await incidents.UpdateProcessingStatusAsync(
                incidentId,
                new UpdateProcessingStatusInput(stepName, "InProgress", null),
                cancellationToken);
            stepStarted = true;

            if (options.Value.StepDelay > TimeSpan.Zero)
            {
                await Task.Delay(options.Value.StepDelay, cancellationToken);
            }

            if (IsMediaAnalysisStep(stepName))
            {
                await AnalyzeMediaAsync(incidentId, cancellationToken);
            }
            else if (IsTriageDraftStep(stepName))
            {
                await intelligence.AnalyzeAsync(incidentId, cancellationToken);
            }
            else if (IsControlledAgentWorkflowStep(stepName))
            {
                await agentWorkflow.RunAsync(incidentId, cancellationToken);
            }

            await incidents.UpdateProcessingStatusAsync(
                incidentId,
                new UpdateProcessingStatusInput(stepName, "Succeeded", null),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Processing step {StepName} failed for incident {IncidentId}.",
                stepName,
                incidentId);

            if (stepStarted)
            {
                await TryMarkFailedAsync(incidentId, stepName, exception.Message, cancellationToken);
            }

            throw;
        }
    }

    private static bool IsTriageDraftStep(string stepName)
    {
        return string.Equals(stepName, "TriageDraft", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMediaAnalysisStep(string stepName)
    {
        return string.Equals(stepName, "MediaAnalysis", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsControlledAgentWorkflowStep(string stepName)
    {
        return string.Equals(stepName, "ControlledAgentWorkflow", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AnalyzeMediaAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var mediaItems = await intelligence.GetMediaAsync(incidentId, cancellationToken);
        if (mediaItems is null || mediaItems.Count == 0)
        {
            logger.LogDebug("No media found for incident {IncidentId}; media analysis step is complete.", incidentId);
            return;
        }

        foreach (var media in mediaItems
                     .Where(ShouldAnalyzeMedia)
                     .OrderBy(media => media.CreatedAt))
        {
            await intelligence.AnalyzeMediaAsync(incidentId, media.Id, cancellationToken);
        }
    }

    private static bool ShouldAnalyzeMedia(IncidentMediaDto media)
    {
        if (string.Equals(media.AnalysisStatus, "Succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(media.AnalysisStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || media.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryMarkFailedAsync(
        Guid incidentId,
        string stepName,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await incidents.UpdateProcessingStatusAsync(
                incidentId,
                new UpdateProcessingStatusInput(stepName, "Failed", errorMessage),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not mark processing step {StepName} as failed for incident {IncidentId}.",
                stepName,
                incidentId);
        }
    }
}
