using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Duplicates;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CivicSignal.Infrastructure.Duplicates;

internal sealed class HeuristicDuplicateIncidentSearchService(CivicSignalDbContext dbContext)
    : IDuplicateIncidentSearchService
{
    private static readonly HashSet<string> StopWords =
    [
        "a",
        "an",
        "and",
        "at",
        "by",
        "for",
        "in",
        "near",
        "of",
        "on",
        "the",
        "to",
        "with"
    ];

    public async Task<IReadOnlyCollection<DuplicateIncidentCandidateResult>> FindDuplicatesAsync(
        IncidentAnalysisRequest request,
        IncidentAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        var requestTokens = Tokenize(request.Description);
        if (requestTokens.Count == 0)
        {
            return [];
        }

        var candidates = await dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id != request.IncidentId)
            .OrderByDescending(incident => incident.CreatedAt)
            .Take(50)
            .Select(incident => new CandidateIncident(
                incident.Id,
                incident.Description,
                incident.Location.Latitude,
                incident.Location.Longitude))
            .ToArrayAsync(cancellationToken);

        return candidates
            .Select(candidate => BuildCandidate(request, requestTokens, candidate))
            .Where(candidate => candidate.SimilarityScore >= 0.7)
            .OrderByDescending(candidate => candidate.SimilarityScore)
            .Take(5)
            .ToArray();
    }

    private static DuplicateIncidentCandidateResult BuildCandidate(
        IncidentAnalysisRequest request,
        HashSet<string> requestTokens,
        CandidateIncident candidate)
    {
        var tokenScore = CalculateTokenSimilarity(requestTokens, Tokenize(candidate.Description));
        var distanceScore = CalculateDistanceScore(
            request.Latitude,
            request.Longitude,
            candidate.Latitude,
            candidate.Longitude);

        var score = Math.Round(Math.Clamp((tokenScore * 0.65) + (distanceScore * 0.35), 0, 0.99), 2);
        var reason = distanceScore > 0.7
            ? "Similar report text near the same coordinates."
            : "Similar report text from a recent incident.";

        return new DuplicateIncidentCandidateResult(candidate.Id, score, reason);
    }

    private static HashSet<string> Tokenize(string value)
    {
        var tokens = value
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '!', '?', '/', '\\', '-', '_', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 2)
            .Where(token => !StopWords.Contains(token));

        return new HashSet<string>(tokens);
    }

    private static double CalculateTokenSimilarity(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Intersect(right).Count();
        var union = left.Union(right).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    private static double CalculateDistanceScore(
        double leftLatitude,
        double leftLongitude,
        double rightLatitude,
        double rightLongitude)
    {
        var distanceKm = CalculateDistanceInKilometers(leftLatitude, leftLongitude, rightLatitude, rightLongitude);
        if (distanceKm <= 0.1)
        {
            return 1;
        }

        if (distanceKm >= 2)
        {
            return 0;
        }

        return 1 - (distanceKm / 2);
    }

    private static double CalculateDistanceInKilometers(
        double leftLatitude,
        double leftLongitude,
        double rightLatitude,
        double rightLongitude)
    {
        const double earthRadiusKm = 6371;
        var latitudeDelta = DegreesToRadians(rightLatitude - leftLatitude);
        var longitudeDelta = DegreesToRadians(rightLongitude - leftLongitude);
        var leftRadians = DegreesToRadians(leftLatitude);
        var rightRadians = DegreesToRadians(rightLatitude);

        var haversine = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(leftRadians) * Math.Cos(rightRadians)
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);

        var angularDistance = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));

        return earthRadiusKm * angularDistance;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180);
    }

    private sealed record CandidateIncident(
        Guid Id,
        string Description,
        double Latitude,
        double Longitude);
}
