using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CivicSignal.Application.Abstractions.Ai;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class AiServiceTextEmbeddingGenerator(
    HttpClient httpClient,
    IOptions<TextEmbeddingOptions> options) : ITextEmbeddingGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dimensions = options.Value.NormalizedDimensions;
        using var response = await httpClient.PostAsJsonAsync(
            "v1/text/embeddings",
            new AiServiceEmbeddingRequest(text, dimensions),
            JsonOptions,
            cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AI service embedding failed with {(int)response.StatusCode}: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<AiServiceEmbeddingResponse>(responseContent, JsonOptions)
            ?? throw new InvalidOperationException("AI service returned an empty embedding response.");

        if (result.Embedding.Count != dimensions)
        {
            throw new InvalidOperationException(
                $"AI service returned {result.Embedding.Count} embedding dimensions; expected {dimensions}.");
        }

        return result.Embedding.ToArray();
    }

    private sealed record AiServiceEmbeddingRequest(string Text, int Dimensions);

    private sealed record AiServiceEmbeddingResponse(IReadOnlyCollection<float> Embedding);
}
