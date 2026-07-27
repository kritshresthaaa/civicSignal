using CivicSignal.Domain.HistoricalComplaints;
using CivicSignal.Domain.Incidents.ValueObjects;

namespace CivicSignal.Domain.Tests;

public sealed class HistoricalComplaintTests
{
    [Fact]
    public void Create_normalizes_source_agency_and_borough()
    {
        var complaint = HistoricalComplaint.Create(
            " nyc311 ",
            " 12345 ",
            "RoadDamage",
            "Street Condition",
            "Pothole",
            "dot",
            "Department of Transportation",
            "Open",
            "manhattan",
            "Main Street",
            null,
            new GeoPoint(40.7128, -74.0060),
            DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
            null,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        Assert.NotEqual(Guid.Empty, complaint.Id);
        Assert.Equal("nyc311", complaint.Source);
        Assert.Equal("12345", complaint.ExternalId);
        Assert.Equal("DOT", complaint.Agency);
        Assert.Equal("MANHATTAN", complaint.Borough);
    }

    [Fact]
    public void Update_from_import_replaces_latest_operational_fields()
    {
        var complaint = HistoricalComplaint.Create(
            HistoricalComplaint.Nyc311Source,
            "12345",
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
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        complaint.UpdateFromImport(
            "RoadDamage",
            "Street Condition",
            "Pothole",
            "dot",
            "Department of Transportation",
            "Closed",
            "manhattan",
            "Main Street",
            "Work completed.",
            new GeoPoint(40.7130, -74.0062),
            DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-23T10:00:00Z"),
            DateTimeOffset.Parse("2026-07-23T12:30:00Z"));

        Assert.Equal("RoadDamage", complaint.Category);
        Assert.Equal("Pothole", complaint.Descriptor);
        Assert.Equal("DOT", complaint.Agency);
        Assert.Equal("MANHATTAN", complaint.Borough);
        Assert.Equal("Closed", complaint.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-07-23T12:30:00Z"), complaint.UpdatedAt);
    }

    [Fact]
    public void Create_rejects_closed_date_before_created_date()
    {
        Assert.Throws<ArgumentException>(() =>
            HistoricalComplaint.Create(
                HistoricalComplaint.Nyc311Source,
                "12345",
                "RoadDamage",
                "Street Condition",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                new GeoPoint(40.7128, -74.0060),
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-23T12:30:00Z")));
    }
}
