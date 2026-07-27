namespace CivicSignal.Application.ModelLab.Models;

public sealed record ModelLabEmbeddingFeatureDto(
    string Token,
    int Index,
    double Value);
