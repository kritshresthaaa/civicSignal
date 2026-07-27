using System.Text.Json;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Common;
using CivicSignal.Application.DataImports.Models;
using CivicSignal.Application.HistoricalComplaints;
using CivicSignal.Domain.DataImports;
using FluentValidation;

namespace CivicSignal.Application.DataImports;

internal sealed class DataImportJobService(
    IDataImportJobRepository jobs,
    IHistoricalComplaintService historicalComplaints,
    IDataImportJobQueue queue,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateNyc311ImportJobInput> createNyc311Validator,
    IValidator<DataImportJobSearchInput> searchValidator) : IDataImportJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DataImportJobDto> QueueNyc311ImportAsync(
        CreateNyc311ImportJobInput input,
        CancellationToken cancellationToken = default)
    {
        await createNyc311Validator.ValidateAndThrowAsync(input, cancellationToken);

        var parameters = new ImportNyc311ComplaintsInput(
            input.Limit,
            input.DaysBack,
            input.ComplaintType,
            input.Borough);
        var job = DataImportJob.RequestNyc311HistoricalComplaints(
            JsonSerializer.Serialize(parameters, JsonOptions),
            input.RequestedByUserId,
            clock.UtcNow);

        await jobs.AddAsync(job, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(job.Id, job.Source, cancellationToken);

        return job.ToDto();
    }

    public async Task<DataImportJobDto?> GetByIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetByIdAsync(jobId, cancellationToken);
        return job?.ToDto();
    }

    public async Task<IReadOnlyCollection<DataImportJobDto>> SearchAsync(
        DataImportJobSearchInput input,
        CancellationToken cancellationToken = default)
    {
        await searchValidator.ValidateAndThrowAsync(input, cancellationToken);

        var results = await jobs.SearchAsync(
            new DataImportJobSearchCriteria(
                TrimToNull(input.Source)?.ToUpperInvariant(),
                TrimToNull(input.Status),
                Math.Max(1, input.Page),
                Math.Clamp(input.PageSize, 1, 200)),
            cancellationToken);

        return results.Select(job => job.ToDto()).ToArray();
    }

    public async Task<DataImportJobDto> RetryAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException(nameof(DataImportJob), jobId);

        if (job.Status is not DataImportJobStatus.Failed)
        {
            throw new ArgumentException("Only failed import jobs can be retried.", nameof(jobId));
        }

        job.Retry(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(job.Id, job.Source, cancellationToken);

        return job.ToDto();
    }

    public async Task<int> RunPendingAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var pending = await jobs.GetPendingAsync(Math.Clamp(count, 1, 100), cancellationToken);
        var processedCount = 0;

        foreach (var job in pending)
        {
            try
            {
                await RunJobCoreAsync(job, cancellationToken);
            }
            catch
            {
                // RunJobCoreAsync records the failure; keep the worker moving through the batch.
            }

            processedCount++;
        }

        return processedCount;
    }

    public async Task<DataImportJobDto> RunJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException(nameof(DataImportJob), jobId);

        return await RunJobCoreAsync(job, cancellationToken);
    }

    private async Task<DataImportJobDto> RunJobCoreAsync(
        DataImportJob job,
        CancellationToken cancellationToken)
    {
        if (job.Status is DataImportJobStatus.Succeeded)
        {
            return job.ToDto();
        }

        job.Start(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            if (job.Source == DataImportJob.Nyc311Source
                && job.ImportType == DataImportJob.HistoricalComplaintsImportType)
            {
                var input = DeserializeNyc311Input(job);
                var result = await historicalComplaints.ImportNyc311Async(input, cancellationToken);
                job.Complete(
                    result.ReceivedCount,
                    result.CreatedCount,
                    result.UpdatedCount,
                    result.SkippedCount,
                    clock.UtcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return job.ToDto();
            }

            throw new InvalidOperationException($"Unsupported data import job {job.Source}/{job.ImportType}.");
        }
        catch (Exception exception)
        {
            job.Fail(exception.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static ImportNyc311ComplaintsInput DeserializeNyc311Input(DataImportJob job)
    {
        return JsonSerializer.Deserialize<ImportNyc311ComplaintsInput>(job.ParametersJson, JsonOptions)
            ?? new ImportNyc311ComplaintsInput(null, null, null, null);
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
