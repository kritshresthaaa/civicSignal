using System.Diagnostics;
using CivicSignal.Application.Abstractions.Ai;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class HeuristicIncidentAnalyzer : IAiIncidentAnalyzer
{
    public Task<IncidentAnalysisResult> AnalyzeAsync(
        IncidentAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var analysisText = BuildAnalysisText(request).ToLowerInvariant();
        var category = DetermineCategory(analysisText);
        var severity = DetermineSeverity(analysisText);
        var agency = DetermineAgency(category);
        var confidence = DetermineConfidence(category, request.Media);
        var summary = BuildSummary(category, severity, agency, request.Description);
        stopwatch.Stop();

        return Task.FromResult(new IncidentAnalysisResult(
            category,
            severity,
            confidence,
            summary,
            agency,
            "civicsignal-heuristic-analyzer",
            "0",
            "heuristic-triage-v1",
            stopwatch.ElapsedMilliseconds,
            BuildEvidence(analysisText, category, severity, agency, request.Media)));
    }

    private static string DetermineCategory(string description)
    {
        if (ContainsAny(description, "pothole", "road crack", "sinkhole", "street damage", "asphalt"))
        {
            return "RoadDamage";
        }

        if (ContainsAny(description, "flood", "water leak", "standing water", "drain"))
        {
            return "Flooding";
        }

        if (ContainsAny(description, "streetlight", "traffic light", "signal light", "lamp"))
        {
            return "Streetlight";
        }

        if (ContainsAny(description, "trash", "debris", "dumping", "garbage"))
        {
            return "Sanitation";
        }

        return "GeneralIncident";
    }

    private static string DetermineSeverity(string description)
    {
        if (ContainsAny(description, "injury", "injured", "emergency", "sinkhole", "collapsed"))
        {
            return "Critical";
        }

        if (ContainsAny(description, "large", "dangerous", "blocking", "blocked", "deep", "major"))
        {
            return "High";
        }

        if (ContainsAny(description, "small", "minor", "low"))
        {
            return "Low";
        }

        return "Medium";
    }

    private static string DetermineAgency(string category)
    {
        return category switch
        {
            "RoadDamage" => "DOT",
            "Flooding" => "WATER",
            "Streetlight" => "UTILITIES",
            "Sanitation" => "SANITATION",
            _ => "CITYOPS"
        };
    }

    private static double DetermineConfidence(
        string category,
        IReadOnlyCollection<IncidentMediaDescriptor> media)
    {
        var baseConfidence = category == "GeneralIncident" ? 0.66 : 0.86;
        var analyzedMediaCount = media.Count(item =>
            string.Equals(item.AnalysisStatus, "Succeeded", StringComparison.OrdinalIgnoreCase));
        var mediaBoost = Math.Min(0.1, (media.Count * 0.025) + (analyzedMediaCount * 0.035));

        return Math.Round(Math.Min(0.97, baseConfidence + mediaBoost), 2);
    }

    private static string BuildSummary(string category, string severity, string agency, string description)
    {
        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length > 180)
        {
            trimmedDescription = $"{trimmedDescription[..177]}...";
        }

        return $"{severity} {category} report routed to {agency}: {trimmedDescription}";
    }

    private static IReadOnlyCollection<IncidentAnalysisEvidence> BuildEvidence(
        string description,
        string category,
        string severity,
        string agency,
        IReadOnlyCollection<IncidentMediaDescriptor> media)
    {
        var evidence = new List<IncidentAnalysisEvidence>
        {
            new(
                "Text",
                "Category keyword match",
                BuildCategoryEvidenceDetail(description, category),
                category == "GeneralIncident" ? 0.62 : 0.86),
            new(
                "Text",
                "Severity keyword match",
                BuildSeverityEvidenceDetail(description, severity),
                severity is "High" or "Critical" ? 0.84 : 0.72),
            new(
                "Routing",
                "Agency routing rule",
                $"{category} incidents are routed to {agency}.",
                0.8)
        };

        foreach (var item in media.Where(item =>
                     string.Equals(item.AnalysisStatus, "Succeeded", StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(item.Transcript))
            {
                evidence.Add(new IncidentAnalysisEvidence(
                    "Audio",
                    "Audio transcript used",
                    TrimForEvidence(item.Transcript),
                    0.72));
            }

            var labels = item.DetectedLabels?
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => label.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
            if (labels.Length > 0)
            {
                evidence.Add(new IncidentAnalysisEvidence(
                    "Image",
                    "Image labels used",
                    $"Detected label(s): {string.Join(", ", labels)}.",
                    0.72));
            }
        }

        if (media.Count > 0 && evidence.All(item => item.Kind is not "Image" and not "Audio"))
        {
            evidence.Add(new IncidentAnalysisEvidence(
                "Media",
                "Uploaded media present",
                $"{media.Count} media item(s) were attached for reviewer inspection.",
                0.7));
        }

        return evidence;
    }

    private static string BuildAnalysisText(IncidentAnalysisRequest request)
    {
        var mediaContext = request.Media
            .SelectMany(item => new[]
            {
                item.FileName,
                item.AnalysisSummary,
                item.Transcript,
                item.DetectedLabels is null ? null : string.Join(" ", item.DetectedLabels)
            })
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" ", new[] { request.Description }.Concat(mediaContext!));
    }

    private static string BuildCategoryEvidenceDetail(string description, string category)
    {
        var matchedTerms = category switch
        {
            "RoadDamage" => MatchingTerms(description, "pothole", "road crack", "sinkhole", "street damage", "asphalt"),
            "Flooding" => MatchingTerms(description, "flood", "water leak", "standing water", "drain"),
            "Streetlight" => MatchingTerms(description, "streetlight", "traffic light", "signal light", "lamp"),
            "Sanitation" => MatchingTerms(description, "trash", "debris", "dumping", "garbage"),
            _ => []
        };

        return matchedTerms.Length == 0
            ? "No specific category keyword was detected, so the incident stayed general."
            : $"Matched category term(s): {string.Join(", ", matchedTerms)}.";
    }

    private static string BuildSeverityEvidenceDetail(string description, string severity)
    {
        var matchedTerms = severity switch
        {
            "Critical" => MatchingTerms(description, "injury", "injured", "emergency", "sinkhole", "collapsed"),
            "High" => MatchingTerms(description, "large", "dangerous", "blocking", "blocked", "deep", "major"),
            "Low" => MatchingTerms(description, "small", "minor", "low"),
            _ => []
        };

        return matchedTerms.Length == 0
            ? "No high-risk or low-risk keyword was detected, so severity defaulted to medium."
            : $"Matched severity term(s): {string.Join(", ", matchedTerms)}.";
    }

    private static string[] MatchingTerms(string value, params string[] keywords)
    {
        return keywords
            .Where(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string TrimForEvidence(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 240 ? trimmed : $"{trimmed[..237]}...";
    }
}
