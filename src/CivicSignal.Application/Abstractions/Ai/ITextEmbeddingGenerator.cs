namespace CivicSignal.Application.Abstractions.Ai;

public interface ITextEmbeddingGenerator
{
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}
