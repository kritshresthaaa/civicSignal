using Microsoft.AspNetCore.Http;

namespace CivicSignal.Api.Contracts.Incidents;

public sealed class UploadIncidentMediaRequest
{
    public IFormFile? File { get; set; }
}
