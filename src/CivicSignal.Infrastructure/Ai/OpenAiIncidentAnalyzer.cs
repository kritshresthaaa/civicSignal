using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CivicSignal.Application.Abstractions.Ai;
using CivicSignal.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Ai;

internal sealed class OpenAiIncidentAnalyzer(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    IFileStorageService storage) : IAiIncidentAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IncidentAnalysisResult> AnalyzeAsync(
        IncidentAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var openAiOptions = options.Value;
        if (string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is required when OpenAI analysis is enabled.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, openAiOptions.Endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAiOptions.ApiKey);
        httpRequest.Content = JsonContent.Create(
            await BuildRequestBodyAsync(request, openAiOptions.Model, cancellationToken),
            options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI analysis failed with {(int)response.StatusCode}: {responseContent}");
        }

        var modelOutput = ExtractOutputText(responseContent);
        var result = JsonSerializer.Deserialize<OpenAiTriageResult>(modelOutput, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI analysis returned an empty triage result.");

        return new IncidentAnalysisResult(
            result.Category,
            result.Severity,
            result.Confidence,
            result.Summary,
            result.SuggestedAgencyCode,
            openAiOptions.Model,
            openAiOptions.Model,
            "openai-incident-triage-v1",
            stopwatch.ElapsedMilliseconds,
            result.Evidence
                .Select(evidence => new IncidentAnalysisEvidence(
                    evidence.Kind,
                    evidence.Title,
                    evidence.Detail,
                    evidence.Confidence))
                .ToArray());
    }

    private async Task<object> BuildRequestBodyAsync(
        IncidentAnalysisRequest request,
        string model,
        CancellationToken cancellationToken)
    {
        var userContent = new List<object>
        {
            new
            {
                type = "input_text",
                text = $"""
Analyze this city incident for operations triage.

Incident ID: {request.IncidentId}
Description: {request.Description}
Latitude: {request.Latitude}
Longitude: {request.Longitude}

Return category, severity, confidence, summary, suggested agency code, and concise supporting evidence.
Prefer categories like RoadDamage, Flooding, Streetlight, Sanitation, SafetyHazard, or GeneralIncident.
Agency code examples: DOT, WATER, UTILITIES, SANITATION, PUBLICSAFETY, CITYOPS.
"""
            }
        };

        foreach (var media in request.Media.Where(media =>
                     media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            var imageUrl = await ResolveImageUrlAsync(media, cancellationToken);
            if (imageUrl is null)
            {
                continue;
            }

            userContent.Add(new
            {
                type = "input_image",
                image_url = imageUrl,
                detail = "low"
            });
        }

        return new
        {
            model,
            store = false,
            input = new object[]
            {
                new
                {
                    role = "developer",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = "You are a city incident triage assistant. Return only the requested structured JSON."
                        }
                    }
                },
                new
                {
                    role = "user",
                    content = userContent
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "incident_triage",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            category = new
                            {
                                type = "string"
                            },
                            severity = new
                            {
                                type = "string",
                                @enum = new[] { "Low", "Medium", "High", "Critical" }
                            },
                            confidence = new
                            {
                                type = "number"
                            },
                            summary = new
                            {
                                type = "string"
                            },
                            suggestedAgencyCode = new
                            {
                                type = "string"
                            },
                            evidence = new
                            {
                                type = "array",
                                minItems = 1,
                                maxItems = 6,
                                items = new
                                {
                                    type = "object",
                                    additionalProperties = false,
                                    properties = new
                                    {
                                        kind = new
                                        {
                                            type = "string"
                                        },
                                        title = new
                                        {
                                            type = "string"
                                        },
                                        detail = new
                                        {
                                            type = "string"
                                        },
                                        confidence = new
                                        {
                                            type = new[] { "number", "null" }
                                        }
                                    },
                                    required = new[]
                                    {
                                        "kind",
                                        "title",
                                        "detail",
                                        "confidence"
                                    }
                                }
                            }
                        },
                        required = new[]
                        {
                            "category",
                            "severity",
                            "confidence",
                            "summary",
                            "suggestedAgencyCode",
                            "evidence"
                        }
                    }
                }
            }
        };
    }

    private async Task<string?> ResolveImageUrlAsync(
        IncidentMediaDescriptor media,
        CancellationToken cancellationToken)
    {
        if (media.StorageUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || media.StorageUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || media.StorageUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return media.StorageUri;
        }

        await using var stream = await storage.OpenReadAsync(media.StorageUri, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        return $"data:{media.ContentType};base64,{Convert.ToBase64String(memory.ToArray())}";
    }

    private static string ExtractOutputText(string responseContent)
    {
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText)
            && outputText.ValueKind is JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("output", out var output)
            && output.ValueKind is JsonValueKind.Array)
        {
            foreach (var outputItem in output.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out var content)
                    || content.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text)
                        && text.ValueKind is JsonValueKind.String)
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }
            }
        }

        throw new InvalidOperationException("OpenAI analysis response did not include output text.");
    }

    private sealed record OpenAiTriageResult(
        string Category,
        string Severity,
        double Confidence,
        string Summary,
        string SuggestedAgencyCode,
        OpenAiEvidenceItem[] Evidence);

    private sealed record OpenAiEvidenceItem(
        string Kind,
        string Title,
        string Detail,
        double? Confidence);
}
