using CivicSignal.Domain.DataImports;

namespace CivicSignal.Domain.Tests;

public sealed class DataImportJobTests
{
    [Fact]
    public void Request_nyc311_job_starts_pending()
    {
        var requestedAt = DateTimeOffset.Parse("2026-07-24T10:00:00Z");

        var job = DataImportJob.RequestNyc311HistoricalComplaints(
            """{"limit":100}""",
            Guid.Parse("019f8db8-01b9-72bc-b672-012ef3878a48"),
            requestedAt);

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(DataImportJob.Nyc311Source, job.Source);
        Assert.Equal(DataImportJob.HistoricalComplaintsImportType, job.ImportType);
        Assert.Equal(DataImportJobStatus.Pending, job.Status);
        Assert.Equal(requestedAt, job.RequestedAt);
    }

    [Fact]
    public void Start_and_complete_records_counts()
    {
        var job = DataImportJob.RequestNyc311HistoricalComplaints(
            """{"limit":100}""",
            null,
            DateTimeOffset.Parse("2026-07-24T10:00:00Z"));

        job.Start(DateTimeOffset.Parse("2026-07-24T10:01:00Z"));
        job.Complete(100, 70, 20, 10, DateTimeOffset.Parse("2026-07-24T10:02:00Z"));

        Assert.Equal(DataImportJobStatus.Succeeded, job.Status);
        Assert.Equal(100, job.ReceivedCount);
        Assert.Equal(70, job.CreatedCount);
        Assert.Equal(20, job.UpdatedCount);
        Assert.Equal(10, job.SkippedCount);
        Assert.Null(job.ErrorMessage);
    }

    [Fact]
    public void Fail_records_error_message()
    {
        var job = DataImportJob.RequestNyc311HistoricalComplaints(
            """{"limit":100}""",
            null,
            DateTimeOffset.Parse("2026-07-24T10:00:00Z"));

        job.Start(DateTimeOffset.Parse("2026-07-24T10:01:00Z"));
        job.Fail("Remote source timed out.", DateTimeOffset.Parse("2026-07-24T10:02:00Z"));

        Assert.Equal(DataImportJobStatus.Failed, job.Status);
        Assert.Equal("Remote source timed out.", job.ErrorMessage);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public void Retry_failed_job_returns_to_pending_and_clears_failure_state()
    {
        var job = DataImportJob.RequestNyc311HistoricalComplaints(
            """{"limit":100}""",
            null,
            DateTimeOffset.Parse("2026-07-24T10:00:00Z"));

        job.Start(DateTimeOffset.Parse("2026-07-24T10:01:00Z"));
        job.Fail("Remote import failed.", DateTimeOffset.Parse("2026-07-24T10:02:00Z"));
        job.Retry(DateTimeOffset.Parse("2026-07-24T10:03:00Z"));

        Assert.Equal(DataImportJobStatus.Pending, job.Status);
        Assert.Null(job.StartedAt);
        Assert.Null(job.FinishedAt);
        Assert.Null(job.ErrorMessage);
        Assert.Equal(0, job.ReceivedCount);
        Assert.Equal(DateTimeOffset.Parse("2026-07-24T10:03:00Z"), job.UpdatedAt);
    }
}
