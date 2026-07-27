namespace CivicSignal.Application.ModelLab;

public sealed record ModelLabAnalysisInput(
    string Text,
    int EmbeddingDimensions = 16);
