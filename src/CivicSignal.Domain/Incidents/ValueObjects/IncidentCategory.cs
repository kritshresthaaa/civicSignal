namespace CivicSignal.Domain.Incidents.ValueObjects;

public readonly record struct IncidentCategory
{
    public IncidentCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Incident category is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 80)
        {
            throw new ArgumentException("Incident category cannot exceed 80 characters.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}
