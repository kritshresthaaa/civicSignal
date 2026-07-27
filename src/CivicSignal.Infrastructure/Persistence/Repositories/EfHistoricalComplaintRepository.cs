using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Domain.HistoricalComplaints;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CivicSignal.Infrastructure.Persistence.Repositories;

internal sealed class EfHistoricalComplaintRepository(CivicSignalDbContext dbContext)
    : EfGenericRepository<HistoricalComplaint>(dbContext), IHistoricalComplaintRepository
{
    public Task<HistoricalComplaint?> GetBySourceExternalIdAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken)
    {
        return DbContext.HistoricalComplaints
            .FirstOrDefaultAsync(
                complaint => complaint.Source == source && complaint.ExternalId == externalId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<HistoricalComplaint>> SearchAsync(
        HistoricalComplaintSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        return await ApplyFilters(DbContext.HistoricalComplaints.AsNoTracking(), criteria)
            .OrderByDescending(complaint => complaint.CreatedAt)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<HistoricalComplaintSummaryResult> GetSummaryAsync(
        HistoricalComplaintSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var filtered = ApplyFilters(DbContext.HistoricalComplaints.AsNoTracking(), criteria);
        var totalCount = await filtered.CountAsync(cancellationToken);
        var oldestCreatedAt = await filtered
            .Select(complaint => (DateTimeOffset?)complaint.CreatedAt)
            .MinAsync(cancellationToken);
        var newestCreatedAt = await filtered
            .Select(complaint => (DateTimeOffset?)complaint.CreatedAt)
            .MaxAsync(cancellationToken);

        var topCategories = await BuildBucketsAsync(
            filtered,
            complaint => complaint.Category,
            cancellationToken);
        var topAgencies = await BuildBucketsAsync(
            filtered.Where(complaint => complaint.Agency != null),
            complaint => complaint.Agency!,
            cancellationToken);
        var topBoroughs = await BuildBucketsAsync(
            filtered.Where(complaint => complaint.Borough != null),
            complaint => complaint.Borough!,
            cancellationToken);

        return new HistoricalComplaintSummaryResult(
            totalCount,
            oldestCreatedAt,
            newestCreatedAt,
            topCategories,
            topAgencies,
            topBoroughs);
    }

    private static IQueryable<HistoricalComplaint> ApplyFilters(
        IQueryable<HistoricalComplaint> query,
        HistoricalComplaintSearchCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var pattern = BuildLikePattern(criteria.Query);
            query = query.Where(complaint =>
                EF.Functions.ILike(complaint.ComplaintType, pattern)
                || (complaint.Descriptor != null && EF.Functions.ILike(complaint.Descriptor, pattern))
                || (complaint.IncidentAddress != null && EF.Functions.ILike(complaint.IncidentAddress, pattern))
                || (complaint.ResolutionDescription != null && EF.Functions.ILike(complaint.ResolutionDescription, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            query = query.Where(complaint => complaint.Category == criteria.Category);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ComplaintType))
        {
            query = query.Where(complaint => complaint.ComplaintType == criteria.ComplaintType);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Agency))
        {
            query = query.Where(complaint => complaint.Agency == criteria.Agency);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Status))
        {
            query = query.Where(complaint => complaint.Status == criteria.Status);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Borough))
        {
            query = query.Where(complaint => complaint.Borough == criteria.Borough);
        }

        if (criteria.CreatedFrom is not null)
        {
            query = query.Where(complaint => complaint.CreatedAt >= criteria.CreatedFrom);
        }

        if (criteria.CreatedTo is not null)
        {
            query = query.Where(complaint => complaint.CreatedAt <= criteria.CreatedTo);
        }

        if (criteria.Latitude is not null && criteria.Longitude is not null && criteria.RadiusMeters is not null)
        {
            var searchPoint = new Point(criteria.Longitude.Value, criteria.Latitude.Value)
            {
                SRID = 4326
            };
            query = query.Where(complaint =>
                EF.Property<Point>(complaint, "LocationPoint").Distance(searchPoint) <= criteria.RadiusMeters);
        }

        return query;
    }

    private static async Task<IReadOnlyCollection<HistoricalComplaintBucket>> BuildBucketsAsync(
        IQueryable<HistoricalComplaint> query,
        System.Linq.Expressions.Expression<Func<HistoricalComplaint, string>> selector,
        CancellationToken cancellationToken)
    {
        var buckets = await query
            .GroupBy(selector)
            .Select(group => new
            {
                Value = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Value)
            .Take(8)
            .ToArrayAsync(cancellationToken);

        return buckets
            .Select(bucket => new HistoricalComplaintBucket(bucket.Value, bucket.Count))
            .ToArray();
    }

    private static string BuildLikePattern(string value)
    {
        var escaped = value
            .Trim()
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
