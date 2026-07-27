using System.Security.Cryptography;
using System.Text;
using CivicSignal.Application.Abstractions.Ai;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class HashingTextEmbeddingGenerator(IOptions<TextEmbeddingOptions> options) : ITextEmbeddingGenerator
{
    private static readonly HashSet<string> StopWords =
    [
        "a",
        "an",
        "and",
        "are",
        "around",
        "at",
        "by",
        "for",
        "from",
        "in",
        "is",
        "near",
        "of",
        "on",
        "the",
        "there",
        "to",
        "with"
    ];

    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dimensions = options.Value.NormalizedDimensions;
        var embedding = new float[dimensions];

        foreach (var token in Tokenize(text))
        {
            AddFeature(embedding, token, 1.0f);
            AddFeature(embedding, $"kind:{NormalizeIncidentConcept(token)}", 0.45f);
        }

        Normalize(embedding);

        return Task.FromResult(embedding);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '!', '?', '/', '\\', '-', '_', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeToken)
            .Where(token => token.Length > 2)
            .Where(token => !StopWords.Contains(token));
    }

    private static string NormalizeToken(string token)
    {
        var normalized = token.Trim();

        return normalized switch
        {
            "potholes" => "pothole",
            "cracks" => "crack",
            "streets" => "street",
            "roads" => "road",
            "blocked" => "blocking",
            "swerving" => "swerve",
            "garbage" => "trash",
            "dumping" => "dump",
            "flooded" => "flood",
            "flooding" => "flood",
            "lights" => "light",
            "streetlights" => "streetlight",
            _ => normalized
        };
    }

    private static string NormalizeIncidentConcept(string token)
    {
        return token switch
        {
            "asphalt" or "crack" or "pavement" or "pothole" or "road" or "street" or "swerve" => "road-damage",
            "drain" or "flood" or "water" => "flooding",
            "lamp" or "light" or "signal" or "streetlight" => "streetlight",
            "debris" or "dump" or "trash" => "sanitation",
            _ => token
        };
    }

    private static void AddFeature(float[] embedding, string feature, float weight)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(feature));
        var index = BitConverter.ToUInt32(bytes, 0) % embedding.Length;
        var sign = (bytes[4] & 1) == 0 ? 1 : -1;

        embedding[index] += sign * weight;
    }

    private static void Normalize(float[] embedding)
    {
        var magnitude = Math.Sqrt(embedding.Sum(value => value * value));
        if (magnitude == 0)
        {
            return;
        }

        for (var index = 0; index < embedding.Length; index++)
        {
            embedding[index] = (float)(embedding[index] / magnitude);
        }
    }
}
