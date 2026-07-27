using System.Diagnostics;
using CivicSignal.Application.Abstractions.Ai;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class HeuristicIncidentMediaAnalyzer : IIncidentMediaAnalyzer
{
    public Task<IncidentMediaAnalysisResult> AnalyzeAsync(
        IncidentMediaAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var fileName = request.Media.FileName.ToLowerInvariant();

        if (request.Media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var labels = DetermineImageLabels(fileName);
            stopwatch.Stop();

            return Task.FromResult(new IncidentMediaAnalysisResult(
                labels.Length == 0
                    ? $"Image '{request.Media.FileName}' was attached for reviewer inspection."
                    : $"Image '{request.Media.FileName}' has local visual hints: {string.Join(", ", labels)}.",
                null,
                labels,
                labels.Length == 0 ? 0.35 : 0.62,
                "civicsignal-heuristic-media-analyzer",
                "0",
                stopwatch.ElapsedMilliseconds,
                [
                    new IncidentAnalysisEvidence(
                        "Image",
                        "Local image analysis fallback",
                        "Filename and content type were used because model-backed image analysis is not enabled.",
                        labels.Length == 0 ? 0.35 : 0.62)
                ]));
        }

        if (request.Media.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            stopwatch.Stop();

            return Task.FromResult(new IncidentMediaAnalysisResult(
                $"Audio '{request.Media.FileName}' was attached and kept for reviewer playback.",
                "Audio evidence was attached, but local ASR is not configured.",
                [],
                0.25,
                "civicsignal-heuristic-media-analyzer",
                "0",
                stopwatch.ElapsedMilliseconds,
                [
                    new IncidentAnalysisEvidence(
                        "Audio",
                        "Local audio analysis fallback",
                        "Audio was preserved for review. Enable the Python AI service with ASR for real transcription.",
                        0.25)
                ]));
        }

        stopwatch.Stop();

        return Task.FromResult(new IncidentMediaAnalysisResult(
            $"Media '{request.Media.FileName}' was stored but not analyzed.",
            null,
            [],
            null,
            "civicsignal-heuristic-media-analyzer",
            "0",
            stopwatch.ElapsedMilliseconds));
    }

    private static string[] DetermineImageLabels(string fileName)
    {
        if (ContainsAny(fileName, "pothole", "road", "crack", "sinkhole", "asphalt"))
        {
            return ["road damage", "pothole"];
        }

        if (ContainsAny(fileName, "flood", "water", "drain"))
        {
            return ["standing water", "blocked drain"];
        }

        if (ContainsAny(fileName, "trash", "garbage", "debris", "dumping"))
        {
            return ["debris", "sanitation issue"];
        }

        if (ContainsAny(fileName, "graffiti", "vandal"))
        {
            return ["graffiti", "vandalism"];
        }

        if (ContainsAny(fileName, "tree", "branch", "limb"))
        {
            return ["tree hazard", "fallen branch"];
        }

        return [];
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
