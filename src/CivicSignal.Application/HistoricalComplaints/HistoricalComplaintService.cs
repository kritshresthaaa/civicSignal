using CivicSignal.Application.Abstractions.OpenData;
using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Application.Common;
using CivicSignal.Application.HistoricalComplaints.Models;
using CivicSignal.Domain.HistoricalComplaints;
using CivicSignal.Domain.Incidents.ValueObjects;
using FluentValidation;

namespace CivicSignal.Application.HistoricalComplaints;

internal sealed class HistoricalComplaintService(
    IHistoricalComplaintRepository historicalComplaints,
    INyc311ComplaintClient nyc311Complaints,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<HistoricalComplaintSearchInput> searchValidator,
    IValidator<ImportNyc311ComplaintsInput> importValidator) : IHistoricalComplaintService
{
    private const int DefaultImportLimit = 1_000;
    private const int DefaultImportDaysBack = 30;

    public async Task<IReadOnlyCollection<HistoricalComplaintDto>> SearchAsync(
        HistoricalComplaintSearchInput input,
        CancellationToken cancellationToken = default)
    {
        await searchValidator.ValidateAndThrowAsync(input, cancellationToken);

        var criteria = ToCriteria(input);
        var results = await historicalComplaints.SearchAsync(criteria, cancellationToken);

        return results.Select(complaint => complaint.ToDto()).ToArray();
    }

    public async Task<HistoricalComplaintSummaryDto> GetSummaryAsync(
        HistoricalComplaintSearchInput input,
        CancellationToken cancellationToken = default)
    {
        await searchValidator.ValidateAndThrowAsync(input, cancellationToken);

        var summary = await historicalComplaints.GetSummaryAsync(ToCriteria(input), cancellationToken);

        return summary.ToDto();
    }

    public async Task<HistoricalComplaintImportResultDto> ImportNyc311Async(
        ImportNyc311ComplaintsInput input,
        CancellationToken cancellationToken = default)
    {
        await importValidator.ValidateAndThrowAsync(input, cancellationToken);

        var importedAt = clock.UtcNow;
        var records = await nyc311Complaints.GetComplaintsAsync(
            new Nyc311ComplaintQuery(
                input.Limit ?? DefaultImportLimit,
                input.DaysBack ?? DefaultImportDaysBack,
                input.ComplaintType,
                input.Borough),
            cancellationToken);

        var createdCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;

        foreach (var record in records)
        {
            if (!TryNormalize(record, out var normalized))
            {
                skippedCount++;
                continue;
            }

            var existing = await historicalComplaints.GetBySourceExternalIdAsync(
                HistoricalComplaint.Nyc311Source,
                normalized.ExternalId,
                cancellationToken);

            if (existing is null)
            {
                await historicalComplaints.AddAsync(
                    HistoricalComplaint.Create(
                        HistoricalComplaint.Nyc311Source,
                        normalized.ExternalId,
                        normalized.Category,
                        normalized.ComplaintType,
                        normalized.Descriptor,
                        normalized.Agency,
                        normalized.AgencyName,
                        normalized.Status,
                        normalized.Borough,
                        normalized.IncidentAddress,
                        normalized.ResolutionDescription,
                        normalized.Location,
                        normalized.CreatedAt,
                        normalized.ClosedAt,
                        importedAt),
                    cancellationToken);
                createdCount++;
            }
            else
            {
                existing.UpdateFromImport(
                    normalized.Category,
                    normalized.ComplaintType,
                    normalized.Descriptor,
                    normalized.Agency,
                    normalized.AgencyName,
                    normalized.Status,
                    normalized.Borough,
                    normalized.IncidentAddress,
                    normalized.ResolutionDescription,
                    normalized.Location,
                    normalized.CreatedAt,
                    normalized.ClosedAt,
                    importedAt);
                updatedCount++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new HistoricalComplaintImportResultDto(
            importedAt,
            records.Count,
            createdCount,
            updatedCount,
            skippedCount);
    }

    private static HistoricalComplaintSearchCriteria ToCriteria(HistoricalComplaintSearchInput input)
    {
        return new HistoricalComplaintSearchCriteria(
            TrimToNull(input.Query),
            TrimToNull(input.Category),
            TrimToNull(input.ComplaintType),
            TrimToNull(input.Agency)?.ToUpperInvariant(),
            TrimToNull(input.Status),
            TrimToNull(input.Borough)?.ToUpperInvariant(),
            input.Latitude,
            input.Longitude,
            input.RadiusMeters,
            input.CreatedFrom,
            input.CreatedTo,
            input.Page,
            input.PageSize);
    }

    private static bool TryNormalize(
        Nyc311ComplaintRecord record,
        out NormalizedComplaint normalized)
    {
        normalized = default;

        if (string.IsNullOrWhiteSpace(record.ExternalId)
            || string.IsNullOrWhiteSpace(record.ComplaintType)
            || record.CreatedAt is null
            || record.Latitude is null
            || record.Longitude is null)
        {
            return false;
        }

        try
        {
            var location = new GeoPoint(record.Latitude.Value, record.Longitude.Value);
            var closedAt = record.ClosedAt >= record.CreatedAt ? record.ClosedAt : null;
            normalized = new NormalizedComplaint(
                record.ExternalId.Trim(),
                Categorize(record.ComplaintType, record.Descriptor),
                record.ComplaintType.Trim(),
                TrimToNull(record.Descriptor),
                TrimToNull(record.Agency),
                TrimToNull(record.AgencyName),
                TrimToNull(record.Status),
                TrimToNull(record.Borough),
                TrimToNull(record.IncidentAddress),
                TrimToNull(record.ResolutionDescription),
                location,
                record.CreatedAt.Value,
                closedAt);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Categorize(string complaintType, string? descriptor)
    {
        var text = $"{complaintType} {descriptor}".ToLowerInvariant();

        if (ContainsAny(text, "pothole", "street defect", "road", "sidewalk", "curb", "street condition"))
        {
            return "RoadDamage";
        }

        if (ContainsAny(text, "catch basin", "flood", "sewer", "water leak", "hydrant"))
        {
            return "Flooding";
        }

        if (ContainsAny(text, "street light", "traffic signal", "streetlight", "lamp"))
        {
            return "Streetlight";
        }

        if (ContainsAny(text, "noise", "loud", "construction noise"))
        {
            return "Noise";
        }

        if (ContainsAny(text, "sanitation", "dirty", "trash", "garbage", "missed collection"))
        {
            return "Sanitation";
        }

        if (ContainsAny(text, "graffiti"))
        {
            return "Graffiti";
        }

        if (ContainsAny(text, "tree", "branch"))
        {
            return "TreeHazard";
        }

        return "GeneralIncident";
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private readonly record struct NormalizedComplaint(
        string ExternalId,
        string Category,
        string ComplaintType,
        string? Descriptor,
        string? Agency,
        string? AgencyName,
        string? Status,
        string? Borough,
        string? IncidentAddress,
        string? ResolutionDescription,
        GeoPoint Location,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ClosedAt);
}
