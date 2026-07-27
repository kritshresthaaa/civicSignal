namespace CivicSignal.Api.Contracts.ModelLab;

public sealed record AnalyzeTextRequest(
    string Text,
    int EmbeddingDimensions = 16);
