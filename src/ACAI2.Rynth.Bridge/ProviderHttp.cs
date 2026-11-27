using System.Net;
using System.Text.Json;

namespace ACAI2.Bridge;

// Provider messages may echo credentials, prompts or player text. Never log them.
internal sealed class ProviderRequestException(HttpStatusCode status, string code, string parameter)
    : HttpRequestException("AI provider request was rejected.", null, status)
{
    internal string Diagnostic => $"HTTP {(int)StatusCode!.Value}; code={code}; parameter={parameter}. " +
        (code == "model_not_found" ? "The selected model is unavailable or this API project does not have access." :
         code == "insufficient_quota" ? "Check API project credit and spending limits." :
         StatusCode == HttpStatusCode.Unauthorized ? "Check the API key used by this Bridge process." :
         StatusCode == HttpStatusCode.Forbidden ? "Check this API project's model and endpoint permissions." :
         StatusCode == HttpStatusCode.TooManyRequests ? "The API rejected the request for a rate or quota limit." :
         StatusCode == HttpStatusCode.BadRequest ? "The API rejected a request parameter or format." :
         (int)StatusCode.Value >= 500 ? "The AI provider is temporarily unavailable." :
         "The AI provider rejected the request.");
}

internal static class ProviderHttp
{
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "model_not_found", "invalid_api_key", "insufficient_quota", "rate_limit_exceeded",
        "unsupported_parameter", "unsupported_value", "invalid_value", "invalid_request_error",
        "context_length_exceeded", "permission_denied", "server_error", "model_access_denied"
    };
    private static readonly HashSet<string> Parameters = new(StringComparer.Ordinal)
    {
        "model", "reasoning_effort", "max_completion_tokens", "max_tokens", "tools", "tool_choice",
        "messages", "response_format", "temperature", "top_p", "stream"
    };

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string code = "unavailable", parameter = "unavailable";
        try
        {
            // Bound both reads and parsing even for HTML errors or a hostile upstream.
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            byte[] bytes = new byte[8193];
            int length = 0;
            while (length < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes.AsMemory(length), ct);
                if (read == 0) break;
                length += read;
            }
            if (length <= 8192)
            {
                using var doc = JsonDocument.Parse(bytes.AsMemory(0, length));
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String && Codes.Contains(c.GetString()!))
                        code = c.GetString()!;
                    if (error.TryGetProperty("param", out var p) && p.ValueKind == JsonValueKind.String && Parameters.Contains(p.GetString()!))
                        parameter = p.GetString()!;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or HttpRequestException or InvalidOperationException)
        { /* The HTTP status is still useful; omit unrecognized response data. */ }
        throw new ProviderRequestException(response.StatusCode, code, parameter);
    }

    internal static string Describe(Exception ex) => ex switch
    {
        ProviderRequestException provider => provider.Diagnostic,
        HttpRequestException { StatusCode: { } status } => $"HTTP {(int)status}; provider details unavailable.",
        HttpRequestException => "HTTP transport failure; check network, DNS, TLS and proxy connectivity.",
        OperationCanceledException => "Request timed out or was cancelled.",
        _ => ex.GetType().Name
    };
}
