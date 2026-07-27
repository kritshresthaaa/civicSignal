using CivicSignal.Application.Incidents.Models;

namespace CivicSignal.Application.Incidents;

public interface IIncidentService
{
    Task<IncidentDto> CreateAsync(CreateIncidentInput input, CancellationToken cancellationToken = default);

    Task<IncidentDto?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken = default);

    Task<IncidentDto?> GetByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IncidentDto>> SearchAsync(IncidentSearchInput input, CancellationToken cancellationToken = default);

    Task<IncidentDto> ReviewAsync(Guid incidentId, ReviewIncidentInput input, CancellationToken cancellationToken = default);

    Task<IncidentDto> AssignAsync(Guid incidentId, AssignIncidentInput input, CancellationToken cancellationToken = default);

    Task<IncidentDto> DispatchAsync(Guid incidentId, DispatchIncidentInput input, CancellationToken cancellationToken = default);

    Task<IncidentDto> LinkDuplicateAsync(Guid incidentId, LinkDuplicateIncidentInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IncidentReviewDto>?> GetReviewHistoryAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<IncidentProcessingStatusDto?> GetProcessingStatusAsync(Guid incidentId, CancellationToken cancellationToken = default);

    Task<IncidentProcessingStatusDto> UpdateProcessingStatusAsync(
        Guid incidentId,
        UpdateProcessingStatusInput input,
        CancellationToken cancellationToken = default);

    Task<IncidentUpdateRequestDto> RequestUpdateAsync(
        Guid incidentId,
        CreateIncidentUpdateRequestInput input,
        CancellationToken cancellationToken = default);

    Task<IncidentNotificationPreferenceDto> UpdateNotificationPreferenceAsync(
        Guid incidentId,
        UpdateNotificationPreferenceInput input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IncidentFeedbackDto>?> GetFeedbackAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<IncidentFeedbackDto> AddFeedbackAsync(
        Guid incidentId,
        CreateIncidentFeedbackInput input,
        CancellationToken cancellationToken = default);
}
