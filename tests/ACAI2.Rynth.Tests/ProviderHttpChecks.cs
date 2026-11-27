using System.Net;
using System.Text;
using System.Text.Json;
using ACAI2.Bridge;

internal static class ProviderHttpChecks
{
    internal static async Task Run(Action<bool, string> check)
    {
        foreach (var (status, code, param) in new[] {
            (401, "invalid_api_key", "unavailable"), (403, "permission_denied", "model"),
            (404, "model_not_found", "model"), (429, "insufficient_quota", "unavailable"),
            (429, "rate_limit_exceeded", "unavailable"), (400, "unsupported_parameter", "reasoning_effort"),
            (400, "unsupported_value", "tool_choice"), (500, "server_error", "unavailable") })
        {
            using var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(
                JsonSerializer.Serialize(new { error = new { code, param, message = "PRIVATE_PROMPT sk-secret PRIVATE_PLAYER_TEXT" } })) };
            try { await ProviderHttp.EnsureSuccessAsync(response, CancellationToken.None); throw new Exception("Expected rejection"); }
            catch (ProviderRequestException ex)
            {
                string safe = ProviderHttp.Describe(ex);
                check((int)ex.StatusCode! == status && safe.Contains("HTTP " + status) && safe.Contains("code=" + code),
                    "provider diagnostic preserves status and recognized code for HTTP " + status + "/" + code);
                check(!safe.Contains("PRIVATE_") && !safe.Contains("sk-secret") && !ex.Message.Contains("PRIVATE_"),
                    "provider diagnostic excludes echoed prompts, credentials and player text for " + code);
            }
        }
        foreach (string body in new[] {
            "<html>PRIVATE_PROMPT sk-secret</html>", "{\"error\":{\"code\":\"PRIVATE_PROMPT\",\"param\":\"sk-secret\",\"message\":\"PRIVATE_PLAYER\"}}",
            new string('X', 9000), "[]", "{\"error\":null}", "{\"error\":{\"code\":12,\"param\":true}}" })
        {
            using var response = new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent(body) };
            try { await ProviderHttp.EnsureSuccessAsync(response, CancellationToken.None); throw new Exception("Expected rejection"); }
            catch (ProviderRequestException ex)
            {
                check(ex.Diagnostic.Contains("HTTP 502") && ex.Diagnostic.Contains("code=unavailable") &&
                    !ex.Diagnostic.Contains("PRIVATE_") && !ex.Diagnostic.Contains("sk-secret"),
                    "malformed, oversized or unrecognized provider data yields status-only safe diagnostics");
            }
        }
        using var good = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[]}") };
        await ProviderHttp.EnsureSuccessAsync(good, CancellationToken.None);
        check(await good.Content.ReadAsStringAsync() == "{\"choices\":[]}", "successful provider content remains available to response parsing");
        check(!ProviderHttp.Describe(new HttpRequestException("sk-secret PRIVATE_PROMPT")).Contains("sk-secret"),
            "transport errors never print exception messages");
        check(ProviderHttp.Describe(new OperationCanceledException()).Contains("cancelled"), "cancelled provider work has an explicit safe diagnostic");
    }
}
