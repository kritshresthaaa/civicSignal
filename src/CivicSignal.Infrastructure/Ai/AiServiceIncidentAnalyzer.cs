using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CivicSignal.Application.Abstractions.Ai;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class AiServiceIncidentAnalyzer(HttpClient httpClient) : IAiIncidentAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IncidentAnalysisResult> AnalyzeAsync(
        IncidentAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var serviceRequest = new AiServiceIncidentAnalysisRequest(
            request.IncidentId,
            request.Description,
            request.Latitude,
            request.Longitude,
            request.Media
                .Select(media => new AiServiceMediaDescriptor(
                    media.Id,
                    media.FileName,
                    media.ContentType,
                    media.StorageUri,
                    media.MediaType,
                    media.AnalysisStatus,
                    media.AnalysisSummary,
                    media.Transcript,
                    media.DetectedLabels ?? []))
                .ToArray());

        using var response = await httpClient.PostAsJsonAsync(
            "v1/incidents/analyze",
            serviceRequest,
            JsonOptions,
            cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AI service analysis failed with {(int)response.StatusCode}: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<AiServiceIncidentAnalysisResponse>(responseContent, JsonOptions)
            ?? throw new InvalidOperationException("AI service returned an empty analysis response.");

        return new IncidentAnalysisResult(
            result.Category,
            result.Severity,
            Math.Clamp(result.Confidence, 0, 1),
            result.Summary,
            result.SuggestedAgencyCode,
            result.ModelName,
            result.ModelVersion,
            result.PromptVersion,
            result.ProcessingTimeMilliseconds ?? stopwatch.ElapsedMilliseconds,
            result.Evidence
                .Select(evidence => new IncidentAnalysisEvidence(
                    evidence.Kind,
                    evidence.Title,
                    evidence.Detail,
                    evidence.Confidence is null ? null : Math.Clamp(evidence.Confidence.Value, 0, 1)))
                .ToArray());
    }

    private sealed record AiServiceIncidentAnalysisRequest(
        Guid IncidentId,
        string Description,
        double Latitude,
        double Longitude,
        IReadOnlyCollection<AiServiceMediaDescriptor> Media);

    private sealed record AiServiceMediaDescriptor(
        Guid Id,
        string FileName,
        string ContentType,
        string StorageUri,
        string MediaType,
        string AnalysisStatus,
        string? AnalysisSummary,
        string? Transcript,
        IReadOnlyCollection<string> DetectedLabels);

    private sealed record AiServiceIncidentAnalysisResponse(
        string Category,
        string Severity,
        double Confidence,
        string Summary,
        string SuggestedAgencyCode,
        string ModelName,
        string? ModelVersion,
        string? PromptVersion,
        long? ProcessingTimeMilliseconds,
        IReadOnlyCollection<AiServiceEvidenceItem> Evidence);

    private sealed record AiServiceEvidenceItem(
        string Kind,
        string Title,
        string Detail,
        double? Confidence);
}
