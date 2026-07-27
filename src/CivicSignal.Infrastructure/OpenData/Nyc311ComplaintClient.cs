using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CivicSignal.Application.Abstractions.OpenData;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.OpenData;

internal sealed class Nyc311ComplaintClient(
    HttpClient httpClient,
    IOptions<Nyc311Options> options) : INyc311ComplaintClient
{
    private const string SelectColumns = "unique_key,created_date,closed_date,agency,agency_name,complaint_type,descriptor,status,borough,incident_address,resolution_description,latitude,longitude";

    public async Task<IReadOnlyCollection<Nyc311ComplaintRecord>> GetComplaintsAsync(
        Nyc311ComplaintQuery query,
        CancellationToken cancellationToken = default)
    {
        var configured = options.Value;
        var limit = Math.Clamp(query.Limit, 1, Math.Max(1, configured.MaxLimit));
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestPath(configured, query, limit));

        if (!string.IsNullOrWhiteSpace(configured.AppToken))
        {
            request.Headers.TryAddWithoutValidation("X-App-Token", configured.AppToken.Trim());
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawRecords = await response.Content.ReadFromJsonAsync<Nyc311RawComplaint[]>(
            cancellationToken: cancellationToken) ?? [];

        return rawRecords
            .Select(ToRecord)
            .ToArray();
    }

    private static string BuildRequestPath(
        Nyc311Options options,
        Nyc311ComplaintQuery query,
        int limit)
    {
        var parameters = new Dictionary<string, string>
        {
            ["$select"] = SelectColumns,
            ["$order"] = "created_date DESC",
            ["$limit"] = limit.ToString(CultureInfo.InvariantCulture),
            ["$where"] = BuildWhereClause(query)
        };

        var queryString = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

        return $"{EnsureLeadingSlash(options.ResourcePath)}?{queryString}";
    }

    private static string BuildWhereClause(Nyc311ComplaintQuery query)
    {
        var clauses = new List<string>
        {
            "latitude IS NOT NULL",
            "longitude IS NOT NULL"
        };

        if (query.DaysBack is not null)
        {
            var fromDate = DateTimeOffset.UtcNow.AddDays(-query.DaysBack.Value).UtcDateTime;
            clauses.Add($"created_date >= '{fromDate:yyyy-MM-ddTHH:mm:ss}'");
        }

        if (!string.IsNullOrWhiteSpace(query.ComplaintType))
        {
            clauses.Add($"complaint_type = '{EscapeSoqlString(query.ComplaintType)}'");
        }

        if (!string.IsNullOrWhiteSpace(query.Borough))
        {
            clauses.Add($"borough = '{EscapeSoqlString(query.Borough.Trim().ToUpperInvariant())}'");
        }

        return string.Join(" AND ", clauses);
    }

    private static Nyc311ComplaintRecord ToRecord(Nyc311RawComplaint raw)
    {
        return new Nyc311ComplaintRecord(
            raw.UniqueKey ?? string.Empty,
            raw.ComplaintType ?? string.Empty,
            raw.Descriptor,
            raw.Agency,
            raw.AgencyName,
            raw.Status,
            raw.Borough,
            raw.IncidentAddress,
            raw.ResolutionDescription,
            ParseDouble(raw.Latitude),
            ParseDouble(raw.Longitude),
            ParseDate(raw.CreatedDate),
            ParseDate(raw.ClosedDate));
    }

    private static double? ParseDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string EscapeSoqlString(string value)
    {
        return value.Trim().Replace("'", "''", StringComparison.Ordinal);
    }

    private static string EnsureLeadingSlash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/resource/erm2-nwe9.json";
        }

        return value.StartsWith("/", StringComparison.Ordinal)
            ? value
            : $"/{value}";
    }

    private sealed class Nyc311RawComplaint
    {
        [JsonPropertyName("unique_key")]
        public string? UniqueKey { get; init; }

        [JsonPropertyName("created_date")]
        public string? CreatedDate { get; init; }

        [JsonPropertyName("closed_date")]
        public string? ClosedDate { get; init; }

        [JsonPropertyName("agency")]
        public string? Agency { get; init; }

        [JsonPropertyName("agency_name")]
        public string? AgencyName { get; init; }

        [JsonPropertyName("complaint_type")]
        public string? ComplaintType { get; init; }

        [JsonPropertyName("descriptor")]
        public string? Descriptor { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("borough")]
        public string? Borough { get; init; }

        [JsonPropertyName("incident_address")]
        public string? IncidentAddress { get; init; }

        [JsonPropertyName("resolution_description")]
        public string? ResolutionDescription { get; init; }

        [JsonPropertyName("latitude")]
        public string? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public string? Longitude { get; init; }
    }
}
