using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CivicSignal.Application.Abstractions.Ai;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class AiServiceIncidentMediaAnalyzer(HttpClient httpClient) : IIncidentMediaAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IncidentMediaAnalysisResult> AnalyzeAsync(
        IncidentMediaAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return await AnalyzeImageAsync(request, cancellationToken);
        }

        if (request.Media.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return await TranscribeAudioAsync(request, cancellationToken);
        }

        return new IncidentMediaAnalysisResult(
            $"Media type '{request.Media.ContentType}' is stored for review but is not supported by the AI media analyzer.",
            null,
            [],
            null,
            "civicsignal-ai-service-media-analyzer",
            "not-applicable");
    }

    private async Task<IncidentMediaAnalysisResult> AnalyzeImageAsync(
        IncidentMediaAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var form = CreateMultipartContent(request);
        using var response = await httpClient.PostAsync("v1/images/analyze", form, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AI service image analysis failed with {(int)response.StatusCode}: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<AiServiceImageAnalysisResponse>(responseContent, JsonOptions)
            ?? throw new InvalidOperationException("AI service returned an empty image analysis response.");

        var labels = result.Labels
            .Where(label => !string.IsNullOrWhiteSpace(label.Name))
            .OrderByDescending(label => label.Confidence)
            .Select(label => label.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var topLabel = result.Labels
            .OrderByDescending(label => label.Confidence)
            .FirstOrDefault();

        var summary = labels.Length == 0
            ? $"Image '{request.Media.FileName}' was analyzed, but no hazard labels were returned."
            : $"Image '{request.Media.FileName}' suggests {string.Join(", ", labels)}.";

        return new IncidentMediaAnalysisResult(
            summary,
            null,
            labels,
            topLabel?.Confidence,
            result.ModelName,
            result.ModelVersion,
            result.ProcessingTimeMilliseconds ?? stopwatch.ElapsedMilliseconds,
            MapEvidence(result.Evidence));
    }

    private async Task<IncidentMediaAnalysisResult> TranscribeAudioAsync(
        IncidentMediaAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var form = CreateMultipartContent(request);
        using var response = await httpClient.PostAsync("v1/audio/transcriptions", form, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AI service audio transcription failed with {(int)response.StatusCode}: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<AiServiceAudioTranscriptionResponse>(responseContent, JsonOptions)
            ?? throw new InvalidOperationException("AI service returned an empty audio transcription response.");

        var transcript = string.IsNullOrWhiteSpace(result.Text) ? null : result.Text.Trim();
        var summary = transcript is null
            ? $"Audio '{request.Media.FileName}' was accepted, but no transcript was returned by the current ASR model."
            : $"Audio '{request.Media.FileName}' transcript: {TrimForSummary(transcript)}";

        return new IncidentMediaAnalysisResult(
            summary,
            transcript,
            [],
            result.Confidence,
            result.ModelName,
            result.ModelVersion,
            result.ProcessingTimeMilliseconds ?? stopwatch.ElapsedMilliseconds,
            MapEvidence(result.Evidence));
    }

    private static MultipartFormDataContent CreateMultipartContent(IncidentMediaAnalysisRequest request)
    {
        var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(request.Content);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.Media.ContentType);
        form.Add(streamContent, "file", request.Media.FileName);

        return form;
    }

    private static IReadOnlyCollection<IncidentAnalysisEvidence> MapEvidence(
        IReadOnlyCollection<AiServiceEvidenceItem>? evidence)
    {
        return evidence?
            .Select(item => new IncidentAnalysisEvidence(
                item.Kind,
                item.Title,
                item.Detail,
                item.Confidence is null ? null : Math.Clamp(item.Confidence.Value, 0, 1)))
            .ToArray() ?? [];
    }

    private static string TrimForSummary(string value)
    {
        return value.Length <= 180 ? value : $"{value[..177]}...";
    }

    private sealed record AiServiceImageAnalysisResponse(
        IReadOnlyCollection<AiServiceImageLabel> Labels,
        string ModelName,
        string? ModelVersion,
        long? ProcessingTimeMilliseconds,
        IReadOnlyCollection<AiServiceEvidenceItem>? Evidence);

    private sealed record AiServiceImageLabel(
        string Name,
        double Confidence);

    private sealed record AiServiceAudioTranscriptionResponse(
        string Text,
        string? Language,
        double? Confidence,
        string ModelName,
        string? ModelVersion,
        long? ProcessingTimeMilliseconds,
        IReadOnlyCollection<AiServiceEvidenceItem>? Evidence);

    private sealed record AiServiceEvidenceItem(
        string Kind,
        string Title,
        string Detail,
        double? Confidence);
}
