using System.Net.Http.Json;
using ACAI2.Protocol;

namespace ACAI2.Rynth.Core;

// This transport deliberately exposes no chat/ask/command/prize route.
public sealed class ReadOnlyBridgeClient : IDisposable
{
    private readonly PilotSettings settings;
    private readonly HttpClient http;
    public ReadOnlyBridgeClient(PilotSettings settings, HttpMessageHandler? handler = null)
    {
        settings.Validate(); this.settings = settings;
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = settings.BridgeAddress(), Timeout = TimeSpan.FromSeconds(3) };
    }
    public async Task<bool> SendAsync(TelemetryFrame frame, CancellationToken stop = default)
    {
        if (!settings.BridgeEnabled || frame.Session.Token.IsCancellationRequested) return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop, frame.Session.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var s = frame.Snapshot; var identity = s.Identity;
        if (frame.Session.InstanceId != s.InstanceId || frame.Session.Identity != identity) throw new InvalidDataException("Telemetry session mismatch.");
        var register = new ClientRegistration { ProtocolVersion = "2", InstanceId = s.InstanceId, Server = identity.Server, Character = identity.Character,
            RuntimeId = identity.RuntimeId, PluginVersion = "ACAI2.Rynth/0.3.0", StartedUtc = s.StartedUtc.ToString("O") };
        using (var response = await http.PostAsJsonAsync("v1/client/register", register, PilotJson.Default.ClientRegistration, deadline.Token)) await ConfirmAsync(response, deadline.Token);
        deadline.Token.ThrowIfCancellationRequested();
        var heartbeat = new ClientHeartbeat { ProtocolVersion = "2", InstanceId = s.InstanceId, Server = identity.Server, Character = identity.Character,
            RuntimeId = identity.RuntimeId, SentUtc = s.CapturedUtc.ToString("O") };
        using (var response = await http.PostAsJsonAsync("v1/client/heartbeat", heartbeat, PilotJson.Default.ClientHeartbeat, deadline.Token)) await ConfirmAsync(response, deadline.Token);
        deadline.Token.ThrowIfCancellationRequested();
        // No inferred player/monster classification or guessed distance is sent by this pilot.
        var perception = new WorldPerception { ProtocolVersion = "2", InstanceId = s.InstanceId, Server = identity.Server, Character = identity.Character,
            RuntimeId = identity.RuntimeId, CapturedUtc = s.CapturedUtc.ToString("O"), Objects = s.Objects.Select(x => x.Name).Distinct(StringComparer.Ordinal).ToList(),
            Players = s.Objects.Where(x=>x.Kind=="Player").Select(x=>x.Name).Distinct(StringComparer.Ordinal).ToList() };
        using (var response = await http.PostAsJsonAsync("v1/client/perception", perception, PilotJson.Default.WorldPerception, deadline.Token)) await ConfirmAsync(response, deadline.Token);
        return true;
    }
    private static async Task ConfirmAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var ack = await response.Content.ReadFromJsonAsync(PilotJson.Default.BridgeAck, ct);
        if (ack?.Ok != true) throw new InvalidDataException("The local Bridge did not acknowledge telemetry.");
    }
    public void Dispose() => http.Dispose();
}
