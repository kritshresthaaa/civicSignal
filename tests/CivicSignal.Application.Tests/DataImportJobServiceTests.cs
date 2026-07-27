using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Common;
using CivicSignal.Application.DataImports;
using CivicSignal.Application.DependencyInjection;
using CivicSignal.Application.HistoricalComplaints;
using CivicSignal.Application.HistoricalComplaints.Models;
using CivicSignal.Domain.DataImports;
using Microsoft.Extensions.DependencyInjection;

namespace CivicSignal.Application.Tests;

public sealed class DataImportJobServiceTests
{
    [Fact]
    public async Task Queue_nyc311_import_creates_pending_job_and_enqueues_message()
    {
        var repository = new FakeDataImportJobRepository();
        var queue = new RecordingDataImportJobQueue();
        using var provider = BuildProvider(repository, queue, new FakeHistoricalComplaintService());
        var service = provider.GetRequiredService<IDataImportJobService>();

        var job = await service.QueueNyc311ImportAsync(
            new CreateNyc311ImportJobInput(100, 7, "Street Condition", "MANHATTAN", Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48")),
            CancellationToken.None);

        Assert.Equal("Pending", job.Status);
        Assert.Contains("\"limit\":100", job.ParametersJson);
        var queued = Assert.Single(queue.Messages);
        Assert.Equal(job.Id, queued.JobId);
        Assert.Equal(DataImportJob.Nyc311Source, queued.Source);
    }

    [Fact]
    public async Task Run_job_imports_nyc311_and_records_counts()
    {
        var repository = new FakeDataImportJobRepository();
        var historical = new FakeHistoricalComplaintService
        {
            ImportResult = new HistoricalComplaintImportResultDto(
                DateTimeOffset.Parse("2026-07-24T10:10:00Z"),
                100,
                80,
                10,
                10)
        };
        using var provider = BuildProvider(repository, new RecordingDataImportJobQueue(), historical);
        var service = provider.GetRequiredService<IDataImportJobService>();
        var job = await service.QueueNyc311ImportAsync(
            new CreateNyc311ImportJobInput(100, 7, null, null, null),
            CancellationToken.None);

        var completed = await service.RunJobAsync(job.Id, CancellationToken.None);

        Assert.Equal("Succeeded", completed.Status);
        Assert.Equal(100, completed.ReceivedCount);
        Assert.Equal(80, completed.CreatedCount);
        Assert.Equal(10, completed.UpdatedCount);
        Assert.Equal(10, completed.SkippedCount);
        Assert.Equal(100, historical.LastInput?.Limit);
    }

    [Fact]
    public async Task Run_job_failure_marks_job_failed()
    {
        var repository = new FakeDataImportJobRepository();
        using var provider = BuildProvider(
            repository,
            new RecordingDataImportJobQueue(),
            new FakeHistoricalComplaintService { ImportException = new InvalidOperationException("Remote import failed.") });
        var service = provider.GetRequiredService<IDataImportJobService>();
        var job = await service.QueueNyc311ImportAsync(
            new CreateNyc311ImportJobInput(100, 7, null, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunJobAsync(job.Id, CancellationToken.None));
        var failed = await service.GetByIdAsync(job.Id, CancellationToken.None);

        Assert.NotNull(failed);
        Assert.Equal("Failed", failed.Status);
        Assert.Equal("Remote import failed.", failed.ErrorMessage);
    }

    [Fact]
    public async Task Retry_failed_job_returns_to_pending_and_enqueues_message()
    {
        var repository = new FakeDataImportJobRepository();
        var queue = new RecordingDataImportJobQueue();
        using var provider = BuildProvider(
            repository,
            queue,
            new FakeHistoricalComplaintService { ImportException = new InvalidOperationException("Remote import failed.") });
        var service = provider.GetRequiredService<IDataImportJobService>();
        var job = await service.QueueNyc311ImportAsync(
            new CreateNyc311ImportJobInput(100, 7, null, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunJobAsync(job.Id, CancellationToken.None));
        var retried = await service.RetryAsync(job.Id, CancellationToken.None);

        Assert.Equal("Pending", retried.Status);
        Assert.Null(retried.ErrorMessage);
        Assert.Null(retried.StartedAt);
        Assert.Null(retried.FinishedAt);
        Assert.Equal(2, queue.Messages.Count);
        Assert.Equal(job.Id, queue.Messages[^1].JobId);
    }

    private static ServiceProvider BuildProvider(
        FakeDataImportJobRepository repository,
        RecordingDataImportJobQueue queue,
        FakeHistoricalComplaintService historical)
    {
        return new ServiceCollection()
            .AddApplication()
            .AddSingleton<IDataImportJobRepository>(repository)
            .AddSingleton<IDataImportJobQueue>(queue)
            .AddSingleton<IHistoricalComplaintService>(historical)
            .AddSingleton<IUnitOfWork, FakeUnitOfWork>()
            .AddSingleton<IClock>(new FixedClock(DateTimeOffset.Parse("2026-07-24T10:00:00Z")))
            .BuildServiceProvider();
    }

    private sealed class FakeDataImportJobRepository : IDataImportJobRepository
    {
        private readonly Dictionary<Guid, DataImportJob> _jobs = [];

        public Task AddAsync(DataImportJob entity, CancellationToken cancellationToken)
        {
            _jobs[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task<DataImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            _jobs.TryGetValue(id, out var job);
            return Task.FromResult(job);
        }

        public Task<IReadOnlyCollection<DataImportJob>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<DataImportJob>>(_jobs.Values.ToArray());
        }

        public void Update(DataImportJob entity)
        {
            _jobs[entity.Id] = entity;
        }

        public void Remove(DataImportJob entity)
        {
            _jobs.Remove(entity.Id);
        }

        public Task<IReadOnlyCollection<DataImportJob>> SearchAsync(
            DataImportJobSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            var results = _jobs.Values
                .OrderByDescending(job => job.RequestedAt)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<DataImportJob>>(results);
        }

        public Task<IReadOnlyCollection<DataImportJob>> GetPendingAsync(
            int count,
            CancellationToken cancellationToken)
        {
            var results = _jobs.Values
                .Where(job => job.Status == DataImportJobStatus.Pending)
                .OrderBy(job => job.RequestedAt)
                .Take(count)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<DataImportJob>>(results);
        }
    }

    private sealed class RecordingDataImportJobQueue : IDataImportJobQueue
    {
        public List<QueuedDataImportJob> Messages { get; } = [];

        public Task EnqueueAsync(Guid jobId, string source, CancellationToken cancellationToken = default)
        {
            Messages.Add(new QueuedDataImportJob(jobId, source));
            return Task.CompletedTask;
        }
    }

    private sealed record QueuedDataImportJob(Guid JobId, string Source);

    private sealed class FakeHistoricalComplaintService : IHistoricalComplaintService
    {
        public ImportNyc311ComplaintsInput? LastInput { get; private set; }

        public HistoricalComplaintImportResultDto ImportResult { get; init; } =
            new(DateTimeOffset.Parse("2026-07-24T10:10:00Z"), 0, 0, 0, 0);

        public Exception? ImportException { get; init; }

        public Task<IReadOnlyCollection<HistoricalComplaintDto>> SearchAsync(
            HistoricalComplaintSearchInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<HistoricalComplaintSummaryDto> GetSummaryAsync(
            HistoricalComplaintSearchInput input,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<HistoricalComplaintImportResultDto> ImportNyc311Async(
            ImportNyc311ComplaintsInput input,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;

            if (ImportException is not null)
            {
                throw ImportException;
            }

            return Task.FromResult(ImportResult);
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
