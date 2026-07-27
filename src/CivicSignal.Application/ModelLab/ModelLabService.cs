using System.Text.RegularExpressions;
using CivicSignal.Application.ModelLab.Models;

namespace CivicSignal.Application.ModelLab;

internal sealed partial class ModelLabService : IModelLabService
{
    private const string ModelName = "civicsignal-learning-classifier";
    private const string ModelVersion = "model-lab-v1";

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "and",
        "are",
        "at",
        "by",
        "for",
        "from",
        "in",
        "is",
        "it",
        "near",
        "of",
        "on",
        "the",
        "there",
        "to",
        "with"
    };

    private static readonly IReadOnlyCollection<CategoryProfile> CategoryProfiles =
    [
        new CategoryProfile(
            "RoadDamage",
            "DOT",
            "High",
            [
                Term("pothole", 2.6),
                Term("road", 1.5),
                Term("street", 1.0),
                Term("asphalt", 1.7),
                Term("pavement", 1.7),
                Term("sidewalk", 1.4),
                Term("crack", 1.4),
                Term("curb", 1.0),
                Term("swerving", 1.2)
            ]),
        new CategoryProfile(
            "Flooding",
            "DPW",
            "High",
            [
                Term("flood", 2.5),
                Term("flooding", 2.5),
                Term("water", 1.4),
                Term("drain", 2.2),
                Term("storm", 1.4),
                Term("sewer", 1.7),
                Term("pooling", 1.7),
                Term("rain", 1.1)
            ]),
        new CategoryProfile(
            "Streetlight",
            "DOT",
            "Medium",
            [
                Term("streetlight", 2.5),
                Term("light", 1.5),
                Term("lamp", 1.6),
                Term("dark", 1.3),
                Term("signal", 1.8),
                Term("traffic", 1.4),
                Term("intersection", 1.0),
                Term("crosswalk", 1.0)
            ]),
        new CategoryProfile(
            "Sanitation",
            "DSNY",
            "Medium",
            [
                Term("trash", 2.2),
                Term("garbage", 2.2),
                Term("dumping", 2.4),
                Term("debris", 1.7),
                Term("litter", 1.6),
                Term("odor", 1.1),
                Term("waste", 1.4)
            ]),
        new CategoryProfile(
            "Graffiti",
            "DSNY",
            "Low",
            [
                Term("graffiti", 2.7),
                Term("tagged", 1.8),
                Term("spray", 1.6),
                Term("vandalism", 1.8),
                Term("paint", 1.2)
            ]),
        new CategoryProfile(
            "TreeHazard",
            "PARKS",
            "High",
            [
                Term("tree", 2.3),
                Term("branch", 2.0),
                Term("limb", 1.8),
                Term("fallen", 1.7),
                Term("blocked", 1.5),
                Term("hanging", 1.4)
            ]),
        new CategoryProfile(
            "GeneralIncident",
            "CITYOPS",
            "Low",
            [])
    ];

    public Task<ModelLabAnalysisDto> AnalyzeAsync(
        ModelLabAnalysisInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var text = input.Text.Trim();
        if (text.Length == 0)
        {
            throw new ArgumentException("Model Lab text is required.", nameof(input));
        }

        var dimensions = Math.Clamp(input.EmbeddingDimensions, 8, 64);
        var tokens = Tokenize(text).ToArray();
        var meaningfulTokens = tokens
            .Where(token => !token.IsStopWord)
            .Select(token => token.Normalized)
            .ToArray();
        var tokenSet = new HashSet<string>(meaningfulTokens, StringComparer.OrdinalIgnoreCase);
        var embedding = BuildEmbedding(meaningfulTokens, dimensions);
        var classScores = BuildClassScores(tokenSet);
        var probabilities = Softmax(classScores.Select(score => score.Logit).ToArray());
        var scoredClasses = classScores
            .Zip(probabilities, (score, probability) => score with
            {
                Probability = Math.Round(probability, 4)
            })
            .OrderByDescending(score => score.Probability)
            .ToArray();
        var predicted = scoredClasses[0];
        var embeddingFeatures = BuildEmbeddingFeatures(meaningfulTokens, dimensions)
            .OrderByDescending(feature => Math.Abs(feature.Value))
            .Take(12)
            .ToArray();

        return Task.FromResult(new ModelLabAnalysisDto(
            text,
            string.Join(' ', meaningfulTokens),
            ModelName,
            ModelVersion,
            tokens,
            embedding.Select(value => Math.Round(value, 4)).ToArray(),
            embeddingFeatures,
            scoredClasses,
            predicted.Category,
            predicted.AgencyCode,
            UpgradeSeverityForUrgency(predicted.Severity, tokenSet),
            Math.Round(predicted.Probability, 4),
            BuildExplanation(predicted)));
    }

    private static IReadOnlyCollection<ModelLabClassScoreDto> BuildClassScores(HashSet<string> tokenSet)
    {
        return CategoryProfiles
            .Select(profile =>
            {
                var matchedTerms = profile.Terms
                    .Where(term => tokenSet.Contains(term.Text))
                    .ToArray();
                var logit = -1.15 + matchedTerms.Sum(term => term.Weight);

                if (profile.Category == "GeneralIncident" && matchedTerms.Length == 0)
                {
                    logit = 0.15;
                }

                return new ModelLabClassScoreDto(
                    profile.Category,
                    profile.AgencyCode,
                    profile.Severity,
                    Math.Round(logit, 3),
                    0,
                    matchedTerms.Select(term => term.Text).ToArray());
            })
            .ToArray();
    }

    private static double[] BuildEmbedding(IReadOnlyCollection<string> tokens, int dimensions)
    {
        var embedding = new double[dimensions];

        foreach (var token in tokens)
        {
            var index = (int)(StableHash(token) % (uint)dimensions);
            var direction = StableHash($"direction:{token}") % 2 == 0 ? 1 : -1;
            embedding[index] += direction;
        }

        var magnitude = Math.Sqrt(embedding.Sum(value => value * value));
        if (magnitude == 0)
        {
            return embedding;
        }

        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }

    private static IReadOnlyCollection<ModelLabEmbeddingFeatureDto> BuildEmbeddingFeatures(
        IReadOnlyCollection<string> tokens,
        int dimensions)
    {
        return tokens
            .GroupBy(token => token, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var token = group.Key;
                var direction = StableHash($"direction:{token}") % 2 == 0 ? 1 : -1;

                return new ModelLabEmbeddingFeatureDto(
                    token,
                    (int)(StableHash(token) % (uint)dimensions),
                    direction * group.Count());
            })
            .ToArray();
    }

    private static IReadOnlyCollection<ModelLabTokenDto> Tokenize(string text)
    {
        return TokenPattern()
            .Matches(text)
            .Select(match =>
            {
                var normalized = NormalizeToken(match.Value);

                return new ModelLabTokenDto(
                    match.Value,
                    normalized,
                    (int)(StableHash(normalized) % 50_000u),
                    match.Index,
                    match.Length,
                    StopWords.Contains(normalized));
            })
            .ToArray();
    }

    private static double[] Softmax(IReadOnlyCollection<double> logits)
    {
        var max = logits.Max();
        var exps = logits.Select(logit => Math.Exp(logit - max)).ToArray();
        var sum = exps.Sum();

        return exps.Select(value => value / sum).ToArray();
    }

    private static string NormalizeToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();

        return normalized switch
        {
            "potholes" => "pothole",
            "roads" => "road",
            "streets" => "street",
            "drains" => "drain",
            "lights" => "light",
            "signals" => "signal",
            "branches" => "branch",
            "trees" => "tree",
            "cracks" => "crack",
            "flooded" => "flood",
            "floods" => "flood",
            "dumped" => "dumping",
            "tagging" => "tagged",
            _ => normalized
        };
    }

    private static string UpgradeSeverityForUrgency(string baseSeverity, HashSet<string> tokenSet)
    {
        var criticalTerms = new[] { "danger", "dangerous", "blocked", "swerving", "injury", "sparking", "sinkhole" };

        return criticalTerms.Any(tokenSet.Contains) && baseSeverity != "Low"
            ? "Critical"
            : baseSeverity;
    }

    private static string BuildExplanation(ModelLabClassScoreDto predicted)
    {
        if (predicted.EvidenceTerms.Count == 0)
        {
            return "No strong category keywords were found, so the classifier abstains to general city operations.";
        }

        return $"{predicted.Category} won because these normalized tokens matched its learned baseline weights: {string.Join(", ", predicted.EvidenceTerms)}.";
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;

        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static WeightedCategoryTerm Term(string text, double weight)
    {
        return new WeightedCategoryTerm(text, weight);
    }

    [GeneratedRegex("[A-Za-z0-9']+")]
    private static partial Regex TokenPattern();

    private sealed record CategoryProfile(
        string Category,
        string AgencyCode,
        string Severity,
        IReadOnlyCollection<WeightedCategoryTerm> Terms);

    private sealed record WeightedCategoryTerm(string Text, double Weight);
}
