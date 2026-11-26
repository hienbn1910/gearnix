using System.Net;
using System.Text.Json;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A ProblemDetails response read from the API, with the fields the error contract promises (Workflow.md §9).
/// </summary>
public sealed record ProblemResponse(HttpStatusCode Status, string? ContentType, JsonElement Body)
{
    public string? Code => Body.TryGetProperty("code", out var code) ? code.GetString() : null;

    public string? TraceId => Body.TryGetProperty("traceId", out var traceId) ? traceId.GetString() : null;

    public string RawBody => Body.GetRawText();

    public static async Task<ProblemResponse> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = json.Length == 0 ? default : JsonDocument.Parse(json).RootElement.Clone();
        return new ProblemResponse(response.StatusCode, response.Content.Headers.ContentType?.MediaType, body);
    }
}
