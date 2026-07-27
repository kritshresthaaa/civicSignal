namespace CivicSignal.Domain.Incidents;

public enum IncidentStatus
{
    Submitted = 1,
    Processing = 2,
    Triaged = 3,
    HumanReviewRequired = 4,
    Reviewed = 5,
    Closed = 6,
    Approved = 7,
    Rejected = 8,
    NeedsMoreInfo = 9,
    Dispatched = 10
}
