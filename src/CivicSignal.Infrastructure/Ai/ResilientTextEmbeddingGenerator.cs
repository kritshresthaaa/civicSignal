using CivicSignal.Application.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class ResilientTextEmbeddingGenerator(
    AiServiceTextEmbeddingGenerator primary,
    HashingTextEmbeddingGenerator fallback,
    ILogger<ResilientTextEmbeddingGenerator> logger) : ITextEmbeddingGenerator
{
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await primary.GenerateEmbeddingAsync(text, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "AI service embedding failed; using local hashing fallback.");

            return await fallback.GenerateEmbeddingAsync(text, cancellationToken);
        }
    }
}
