using CivicSignal.Domain.DataImports;

namespace CivicSignal.Application.DataImports.Models;

public static class DataImportJobMappings
{
    public static DataImportJobDto ToDto(this DataImportJob job)
    {
        return new DataImportJobDto(
            job.Id,
            job.Source,
            job.ImportType,
            job.ParametersJson,
            job.Status.ToString(),
            job.RequestedByUserId,
            job.RequestedAt,
            job.StartedAt,
            job.FinishedAt,
            job.ReceivedCount,
            job.CreatedCount,
            job.UpdatedCount,
            job.SkippedCount,
            job.ErrorMessage,
            job.UpdatedAt);
    }
}
