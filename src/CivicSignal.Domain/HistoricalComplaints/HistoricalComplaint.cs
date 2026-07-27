using CivicSignal.Domain.Incidents.ValueObjects;

namespace CivicSignal.Domain.HistoricalComplaints;

public sealed class HistoricalComplaint
{
    public const string Nyc311Source = "NYC311";

    private HistoricalComplaint()
    {
        Source = string.Empty;
        ExternalId = string.Empty;
        Category = string.Empty;
        ComplaintType = string.Empty;
        Location = new GeoPoint(0, 0);
    }

    private HistoricalComplaint(
        string source,
        string externalId,
        string category,
        string complaintType,
        string? descriptor,
        string? agency,
        string? agencyName,
        string? status,
        string? borough,
        string? incidentAddress,
        string? resolutionDescription,
        GeoPoint location,
        DateTimeOffset createdAt,
        DateTimeOffset? closedAt,
        DateTimeOffset importedAt)
    {
        Id = Guid.NewGuid();
        Source = NormalizeRequired(source, nameof(source), 40);
        ExternalId = NormalizeRequired(externalId, nameof(externalId), 80);
        Category = NormalizeRequired(category, nameof(category), 80);
        ComplaintType = NormalizeRequired(complaintType, nameof(complaintType), 200);
        Descriptor = NormalizeOptional(descriptor, 300);
        Agency = NormalizeOptional(agency, 40)?.ToUpperInvariant();
        AgencyName = NormalizeOptional(agencyName, 200);
        Status = NormalizeOptional(status, 80);
        Borough = NormalizeOptional(borough, 80)?.ToUpperInvariant();
        IncidentAddress = NormalizeOptional(incidentAddress, 300);
        ResolutionDescription = NormalizeOptional(resolutionDescription, 2_000);
        Location = location;
        CreatedAt = createdAt;
        ClosedAt = closedAt;
        ImportedAt = importedAt;
        UpdatedAt = importedAt;
    }

    public Guid Id { get; private set; }

    public string Source { get; private set; }

    public string ExternalId { get; private set; }

    public string Category { get; private set; }

    public string ComplaintType { get; private set; }

    public string? Descriptor { get; private set; }

    public string? Agency { get; private set; }

    public string? AgencyName { get; private set; }

    public string? Status { get; private set; }

    public string? Borough { get; private set; }

    public string? IncidentAddress { get; private set; }

    public string? ResolutionDescription { get; private set; }

    public GeoPoint Location { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset ImportedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static HistoricalComplaint Create(
        string source,
        string externalId,
        string category,
        string complaintType,
        string? descriptor,
        string? agency,
        string? agencyName,
        string? status,
        string? borough,
        string? incidentAddress,
        string? resolutionDescription,
        GeoPoint location,
        DateTimeOffset createdAt,
        DateTimeOffset? closedAt,
        DateTimeOffset importedAt)
    {
        if (closedAt is not null && closedAt < createdAt)
        {
            throw new ArgumentException("Closed date cannot be before complaint creation.", nameof(closedAt));
        }

        return new HistoricalComplaint(
            source,
            externalId,
            category,
            complaintType,
            descriptor,
            agency,
            agencyName,
            status,
            borough,
            incidentAddress,
            resolutionDescription,
            location,
            createdAt,
            closedAt,
            importedAt);
    }

    public void UpdateFromImport(
        string category,
        string complaintType,
        string? descriptor,
        string? agency,
        string? agencyName,
        string? status,
        string? borough,
        string? incidentAddress,
        string? resolutionDescription,
        GeoPoint location,
        DateTimeOffset createdAt,
        DateTimeOffset? closedAt,
        DateTimeOffset importedAt)
    {
        if (closedAt is not null && closedAt < createdAt)
        {
            throw new ArgumentException("Closed date cannot be before complaint creation.", nameof(closedAt));
        }

        Category = NormalizeRequired(category, nameof(category), 80);
        ComplaintType = NormalizeRequired(complaintType, nameof(complaintType), 200);
        Descriptor = NormalizeOptional(descriptor, 300);
        Agency = NormalizeOptional(agency, 40)?.ToUpperInvariant();
        AgencyName = NormalizeOptional(agencyName, 200);
        Status = NormalizeOptional(status, 80);
        Borough = NormalizeOptional(borough, 80)?.ToUpperInvariant();
        IncidentAddress = NormalizeOptional(incidentAddress, 300);
        ResolutionDescription = NormalizeOptional(resolutionDescription, 2_000);
        Location = location;
        CreatedAt = createdAt;
        ClosedAt = closedAt;
        UpdatedAt = importedAt;
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
