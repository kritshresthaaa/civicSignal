using CivicSignal.Application.Abstractions.OpenData;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Common;
using CivicSignal.Application.DependencyInjection;
using CivicSignal.Application.HistoricalComplaints;
using CivicSignal.Domain.HistoricalComplaints;
using CivicSignal.Domain.Incidents.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace CivicSignal.Application.Tests;

public sealed class HistoricalComplaintServiceTests
{
    [Fact]
    public async Task Import_nyc311_creates_categorized_historical_complaints_and_skips_incomplete_records()
    {
        var repository = new FakeHistoricalComplaintRepository();
        var client = new FakeNyc311ComplaintClient
        {
            Records =
            [
                new Nyc311ComplaintRecord(
                    "311-1",
                    "Street Condition",
                    "Pothole",
                    "dot",
                    "Department of Transportation",
                    "Open",
                    "manhattan",
                    "Main Street",
                    null,
                    40.7128,
                    -74.0060,
                    DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                    null),
                new Nyc311ComplaintRecord(
                    "311-2",
                    "Street Condition",
                    "Missing coordinates",
                    "DOT",
                    null,
                    "Open",
                    "MANHATTAN",
                    null,
                    null,
                    null,
                    null,
                    DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                    null)
            ]
        };
        using var provider = BuildProvider(repository, client);
        var service = provider.GetRequiredService<IHistoricalComplaintService>();

        var result = await service.ImportNyc311Async(new ImportNyc311ComplaintsInput(10, 7, null, null));

        Assert.Equal(2, result.ReceivedCount);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.SkippedCount);

        var complaint = Assert.Single(repository.Complaints);
        Assert.Equal("RoadDamage", complaint.Category);
        Assert.Equal("DOT", complaint.Agency);
        Assert.Equal("MANHATTAN", complaint.Borough);
    }

    [Fact]
    public async Task Import_nyc311_updates_existing_source_record()
    {
        var repository = new FakeHistoricalComplaintRepository();
        await repository.AddAsync(
            HistoricalComplaint.Create(
                HistoricalComplaint.Nyc311Source,
                "311-1",
                "GeneralIncident",
                "Street Condition",
                null,
                "DOT",
                null,
                "Open",
                "BROOKLYN",
                null,
                null,
                new GeoPoint(40.7128, -74.0060),
                DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                null,
                DateTimeOffset.Parse("2026-07-23T12:00:00Z")),
            CancellationToken.None);
        var client = new FakeNyc311ComplaintClient
        {
            Records =
            [
                new Nyc311ComplaintRecord(
                    "311-1",
                    "Street Condition",
                    "Pothole",
                    "dot",
                    "Department of Transportation",
                    "Closed",
                    "manhattan",
                    "Main Street",
                    "Work completed.",
                    40.7130,
                    -74.0062,
                    DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                    DateTimeOffset.Parse("2026-07-23T10:00:00Z"))
            ]
        };
        using var provider = BuildProvider(repository, client);
        var service = provider.GetRequiredService<IHistoricalComplaintService>();

        var result = await service.ImportNyc311Async(new ImportNyc311ComplaintsInput(10, 7, null, null));

        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.UpdatedCount);
        var complaint = Assert.Single(repository.Complaints);
        Assert.Equal("RoadDamage", complaint.Category);
        Assert.Equal("Closed", complaint.Status);
        Assert.Equal("MANHATTAN", complaint.Borough);
        Assert.Equal("Work completed.", complaint.ResolutionDescription);
    }

    [Fact]
    public async Task Search_and_summary_use_repository_filters()
    {
        var repository = new FakeHistoricalComplaintRepository();
        await repository.AddAsync(CreateComplaint("311-1", "RoadDamage", "Street Condition", "DOT", "MANHATTAN"), CancellationToken.None);
        await repository.AddAsync(CreateComplaint("311-2", "Sanitation", "Dirty Condition", "DSNY", "BROOKLYN"), CancellationToken.None);
        using var provider = BuildProvider(repository, new FakeNyc311ComplaintClient());
        var service = provider.GetRequiredService<IHistoricalComplaintService>();

        var results = await service.SearchAsync(
            new HistoricalComplaintSearchInput(
                Query: null,
                Category: "RoadDamage",
                ComplaintType: null,
                Agency: "dot",
                Status: null,
                Borough: null,
                Latitude: null,
                Longitude: null,
                RadiusMeters: null,
                CreatedFrom: null,
                CreatedTo: null,
                Page: 1,
                PageSize: 100),
            CancellationToken.None);
        var summary = await service.GetSummaryAsync(
            new HistoricalComplaintSearchInput(null, null, null, null, null, null, null, null, null, null, null, 1, 100),
            CancellationToken.None);

        Assert.Equal("311-1", Assert.Single(results).ExternalId);
        Assert.Equal(2, summary.TotalCount);
        Assert.Contains(summary.TopCategories, bucket => bucket.Value == "RoadDamage" && bucket.Count == 1);
        Assert.Contains(summary.TopAgencies, bucket => bucket.Value == "DOT" && bucket.Count == 1);
    }

    private static ServiceProvider BuildProvider(
        FakeHistoricalComplaintRepository repository,
        FakeNyc311ComplaintClient client)
    {
        return new ServiceCollection()
            .AddApplication()
            .AddSingleton<IHistoricalComplaintRepository>(repository)
            .AddSingleton<INyc311ComplaintClient>(client)
            .AddSingleton<IUnitOfWork, FakeUnitOfWork>()
            .AddSingleton<IClock>(new FixedClock(DateTimeOffset.Parse("2026-07-23T12:00:00Z")))
            .BuildServiceProvider();
    }

    private static HistoricalComplaint CreateComplaint(
        string externalId,
        string category,
        string complaintType,
        string agency,
        string borough)
    {
        return HistoricalComplaint.Create(
            HistoricalComplaint.Nyc311Source,
            externalId,
            category,
            complaintType,
            null,
            agency,
            null,
            "Open",
            borough,
            null,
            null,
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
            null,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
    }

    private sealed class FakeHistoricalComplaintRepository : IHistoricalComplaintRepository
    {
        private readonly Dictionary<Guid, HistoricalComplaint> _complaints = [];

        public IReadOnlyCollection<HistoricalComplaint> Complaints => _complaints.Values.ToArray();

        public Task AddAsync(HistoricalComplaint entity, CancellationToken cancellationToken)
        {
            _complaints[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task<HistoricalComplaint?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            _complaints.TryGetValue(id, out var complaint);
            return Task.FromResult(complaint);
        }

        public Task<IReadOnlyCollection<HistoricalComplaint>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Complaints);
        }

        public void Update(HistoricalComplaint entity)
        {
            _complaints[entity.Id] = entity;
        }

        public void Remove(HistoricalComplaint entity)
        {
            _complaints.Remove(entity.Id);
        }

        public Task<HistoricalComplaint?> GetBySourceExternalIdAsync(
            string source,
            string externalId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_complaints.Values.FirstOrDefault(complaint =>
                complaint.Source == source && complaint.ExternalId == externalId));
        }

        public Task<IReadOnlyCollection<HistoricalComplaint>> SearchAsync(
            HistoricalComplaintSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            var query = ApplyFilters(criteria);
            var results = query
                .OrderByDescending(complaint => complaint.CreatedAt)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<HistoricalComplaint>>(results);
        }

        public Task<HistoricalComplaintSummaryResult> GetSummaryAsync(
            HistoricalComplaintSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            var filtered = ApplyFilters(criteria).ToArray();

            return Task.FromResult(new HistoricalComplaintSummaryResult(
                filtered.Length,
                filtered.Select(complaint => (DateTimeOffset?)complaint.CreatedAt).Min(),
                filtered.Select(complaint => (DateTimeOffset?)complaint.CreatedAt).Max(),
                BuildBuckets(filtered, complaint => complaint.Category),
                BuildBuckets(filtered.Where(complaint => complaint.Agency is not null), complaint => complaint.Agency!),
                BuildBuckets(filtered.Where(complaint => complaint.Borough is not null), complaint => complaint.Borough!)));
        }

        private IEnumerable<HistoricalComplaint> ApplyFilters(HistoricalComplaintSearchCriteria criteria)
        {
            var query = _complaints.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(criteria.Category))
            {
                query = query.Where(complaint => complaint.Category == criteria.Category);
            }

            if (!string.IsNullOrWhiteSpace(criteria.Agency))
            {
                query = query.Where(complaint => complaint.Agency == criteria.Agency);
            }

            return query;
        }

        private static IReadOnlyCollection<HistoricalComplaintBucket> BuildBuckets(
            IEnumerable<HistoricalComplaint> complaints,
            Func<HistoricalComplaint, string> selector)
        {
            return complaints
                .GroupBy(selector)
                .Select(group => new HistoricalComplaintBucket(group.Key, group.Count()))
                .OrderByDescending(bucket => bucket.Count)
                .ThenBy(bucket => bucket.Value)
                .ToArray();
        }
    }

    private sealed class FakeNyc311ComplaintClient : INyc311ComplaintClient
    {
        public IReadOnlyCollection<Nyc311ComplaintRecord> Records { get; init; } = [];

        public Task<IReadOnlyCollection<Nyc311ComplaintRecord>> GetComplaintsAsync(
            Nyc311ComplaintQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Records);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
