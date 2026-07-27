namespace CivicSignal.Domain.Incidents.ValueObjects;

public readonly record struct AgencyCode
{
    public AgencyCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Agency code is required.", nameof(value));
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 32)
        {
            throw new ArgumentException("Agency code cannot exceed 32 characters.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}
