namespace CivicSignal.Infrastructure.Ai;

internal sealed class TextEmbeddingOptions
{
    public const string SectionName = "TextEmbeddings";

    public const int DefaultDimensions = 1024;

    public int Dimensions { get; set; } = DefaultDimensions;

    public int NormalizedDimensions => Math.Clamp(Dimensions, 128, 2_000);
}
