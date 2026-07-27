using System.Globalization;
using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Duplicates;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;

namespace CivicSignal.Infrastructure.Duplicates;

internal sealed class PgvectorDuplicateIncidentSearchService(
    CivicSignalDbContext dbContext,
    ITextEmbeddingGenerator textEmbeddings,
    IOptions<DuplicateDetectionOptions> options) : IDuplicateIncidentSearchService
{
    public async Task<IReadOnlyCollection<DuplicateIncidentCandidateResult>> FindDuplicatesAsync(
        IncidentAnalysisRequest request,
        IncidentAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        var embeddingValues = await textEmbeddings.GenerateEmbeddingAsync(request.Description, cancellationToken);
        if (embeddingValues.Length == 0)
        {
            return [];
        }

        var duplicateOptions = options.Value;
        var radiusMeters = duplicateOptions.NormalizedSearchRadiusMeters;
        var sourceCreatedAt = await GetSourceCreatedAtAsync(request.IncidentId, cancellationToken)
            ?? DateTimeOffset.UtcNow;

        var queryEmbedding = new Vector(embeddingValues);
        var rows = await QueryCandidatesAsync(
            request,
            queryEmbedding,
            radiusMeters,
            duplicateOptions.NormalizedCandidatePoolSize,
            cancellationToken);

        return rows
            .Select(row => BuildResult(row, analysis, sourceCreatedAt, duplicateOptions))
            .Where(candidate => candidate.SimilarityScore >= duplicateOptions.NormalizedMinimumScore)
            .OrderByDescending(candidate => candidate.SimilarityScore)
            .Take(duplicateOptions.NormalizedMaxResults)
            .ToArray();
    }

    private Task<DateTimeOffset?> GetSourceCreatedAtAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == incidentId)
            .Select(incident => (DateTimeOffset?)incident.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<CandidateRow[]> QueryCandidatesAsync(
        IncidentAnalysisRequest request,
        Vector queryEmbedding,
        double radiusMeters,
        int candidatePoolSize,
        CancellationToken cancellationToken)
    {
        return await dbContext.Database
            .SqlQuery<CandidateRow>($"""
                SELECT
                    i.id AS "CandidateIncidentId",
                    i.created_at AS "CreatedAt",
                    i.text_embedding <=> {queryEmbedding} AS "TextDistance",
                    ST_Distance(
                        i.location,
                        ST_SetSRID(ST_MakePoint({request.Longitude}, {request.Latitude}), 4326)::geography
                    ) AS "DistanceMeters"
                FROM incidents AS i
                WHERE i.id <> {request.IncidentId}
                  AND i.text_embedding IS NOT NULL
                  AND ST_DWithin(
                        i.location,
                        ST_SetSRID(ST_MakePoint({request.Longitude}, {request.Latitude}), 4326)::geography,
                        {radiusMeters}
                  )
                ORDER BY
                    i.text_embedding <=> {queryEmbedding},
                    ST_Distance(
                        i.location,
                        ST_SetSRID(ST_MakePoint({request.Longitude}, {request.Latitude}), 4326)::geography
                    )
                LIMIT {candidatePoolSize}
                """)
            .ToArrayAsync(cancellationToken);
    }

    private static DuplicateIncidentCandidateResult BuildResult(
        CandidateRow row,
        IncidentAnalysisResult analysis,
        DateTimeOffset sourceCreatedAt,
        DuplicateDetectionOptions options)
    {
        var textSimilarity = 1 - Math.Clamp(row.TextDistance, 0, 1);
        var geographySimilarity = 1 - Math.Clamp(row.DistanceMeters / options.NormalizedSearchRadiusMeters, 0, 1);
        var ageDifferenceHours = Math.Abs((sourceCreatedAt - row.CreatedAt).TotalHours);
        var timeSimilarity = 1 - Math.Clamp(ageDifferenceHours / options.NormalizedTimeWindowHours, 0, 1);

        var score = Math.Round(
            Math.Clamp(
                (options.TextWeight * textSimilarity)
                + (options.GeographyWeight * geographySimilarity)
                + (options.TimeWeight * timeSimilarity),
                0,
                0.99),
            2);

        var reason = string.Create(
            CultureInfo.InvariantCulture,
            $"Vector/geospatial match for {analysis.Category}: text {textSimilarity * 100:0}%, distance {row.DistanceMeters:0}m, time {timeSimilarity * 100:0}%.");

        return new DuplicateIncidentCandidateResult(row.CandidateIncidentId, score, reason);
    }

    private sealed class CandidateRow
    {
        public Guid CandidateIncidentId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public double TextDistance { get; set; }

        public double DistanceMeters { get; set; }
    }
}
