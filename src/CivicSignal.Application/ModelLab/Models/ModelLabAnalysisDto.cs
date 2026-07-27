namespace CivicSignal.Application.ModelLab.Models;

public sealed record ModelLabAnalysisDto(
    string Input,
    string NormalizedText,
    string ModelName,
    string ModelVersion,
    IReadOnlyCollection<ModelLabTokenDto> Tokens,
    IReadOnlyCollection<double> EmbeddingPreview,
    IReadOnlyCollection<ModelLabEmbeddingFeatureDto> EmbeddingFeatures,
    IReadOnlyCollection<ModelLabClassScoreDto> ClassScores,
    string PredictedCategory,
    string SuggestedAgencyCode,
    string Severity,
    double Confidence,
    string Explanation);
