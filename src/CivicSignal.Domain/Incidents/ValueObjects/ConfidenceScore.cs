namespace CivicSignal.Domain.Incidents.ValueObjects;

public readonly record struct ConfidenceScore
{
    public ConfidenceScore(double value)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Confidence score must be between 0 and 1.");
        }

        Value = value;
    }

    public double Value { get; }
}
