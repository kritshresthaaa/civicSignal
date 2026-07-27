using CivicSignal.Application.Abstractions.Persistence;
using CivicSignal.Domain.HistoricalComplaints;

namespace CivicSignal.Application.HistoricalComplaints.Models;

public static class HistoricalComplaintMappings
{
    public static HistoricalComplaintDto ToDto(this HistoricalComplaint complaint)
    {
        return new HistoricalComplaintDto(
            complaint.Id,
            complaint.Source,
            complaint.ExternalId,
            complaint.Category,
            complaint.ComplaintType,
            complaint.Descriptor,
            complaint.Agency,
            complaint.AgencyName,
            complaint.Status,
            complaint.Borough,
            complaint.IncidentAddress,
            complaint.ResolutionDescription,
            complaint.Location.Latitude,
            complaint.Location.Longitude,
            complaint.CreatedAt,
            complaint.ClosedAt,
            complaint.ImportedAt,
            complaint.UpdatedAt);
    }

    public static HistoricalComplaintSummaryDto ToDto(this HistoricalComplaintSummaryResult summary)
    {
        return new HistoricalComplaintSummaryDto(
            summary.TotalCount,
            summary.OldestCreatedAt,
            summary.NewestCreatedAt,
            summary.TopCategories.Select(bucket => bucket.ToDto()).ToArray(),
            summary.TopAgencies.Select(bucket => bucket.ToDto()).ToArray(),
            summary.TopBoroughs.Select(bucket => bucket.ToDto()).ToArray());
    }

    private static HistoricalComplaintBucketDto ToDto(this HistoricalComplaintBucket bucket)
    {
        return new HistoricalComplaintBucketDto(bucket.Value, bucket.Count);
    }
}
