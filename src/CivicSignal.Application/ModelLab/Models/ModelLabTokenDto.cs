namespace CivicSignal.Application.ModelLab.Models;

public sealed record ModelLabTokenDto(
    string Text,
    string Normalized,
    int TokenId,
    int Start,
    int Length,
    bool IsStopWord);
