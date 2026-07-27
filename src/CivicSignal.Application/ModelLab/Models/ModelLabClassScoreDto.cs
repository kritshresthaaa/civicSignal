namespace CivicSignal.Application.ModelLab.Models;

public sealed record ModelLabClassScoreDto(
    string Category,
    string AgencyCode,
    string Severity,
    double Logit,
    double Probability,
    IReadOnlyCollection<string> EvidenceTerms);
