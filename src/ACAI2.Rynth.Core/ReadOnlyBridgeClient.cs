using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
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
            RuntimeId = identity.RuntimeId, PluginVersion = "ACAI2.Rynth/0.3.1", StartedUtc = s.StartedUtc.ToString("O") };
        await PostAsync("v1/client/register",register,PilotJson.Default.ClientRegistration,deadline.Token);
        deadline.Token.ThrowIfCancellationRequested();
        var heartbeat = new ClientHeartbeat { ProtocolVersion = "2", InstanceId = s.InstanceId, Server = identity.Server, Character = identity.Character,
            RuntimeId = identity.RuntimeId, SentUtc = s.CapturedUtc.ToString("O") };
        await PostAsync("v1/client/heartbeat",heartbeat,PilotJson.Default.ClientHeartbeat,deadline.Token);
        deadline.Token.ThrowIfCancellationRequested();
        // No inferred player/monster classification or guessed distance is sent by this pilot.
        var perception = new WorldPerception { ProtocolVersion = "2", InstanceId = s.InstanceId, Server = identity.Server, Character = identity.Character,
            RuntimeId = identity.RuntimeId, CapturedUtc = s.CapturedUtc.ToString("O"), Objects = s.Objects.Select(x => x.Name).Distinct(StringComparer.Ordinal).ToList(),
            Players = s.Objects.Where(x=>x.Kind=="Player").Select(x=>x.Name).Distinct(StringComparer.Ordinal).ToList() };
        await PostAsync("v1/client/perception",perception,PilotJson.Default.WorldPerception,deadline.Token);
        return true;
    }
    // Rynth isolates System.Text.Json in each collectible plugin context, while
    // System.Net.Http.Json is shared. Passing JsonTypeInfo across that boundary
    // fails method resolution. Serialize/deserialize privately; HTTP sees only text.
    private async Task PostAsync<T>(string path,T payload,JsonTypeInfo<T> metadata,CancellationToken ct)
    {
        using var content=new StringContent(JsonSerializer.Serialize(payload,metadata),Encoding.UTF8,"application/json");
        using var response=await http.PostAsync(path,content,ct);
        await ConfirmAsync(response,ct);
    }
    private static async Task ConfirmAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var text=await response.Content.ReadAsStringAsync(ct);
        var ack=JsonSerializer.Deserialize(text,PilotJson.Default.BridgeAck);
        if (ack?.Ok != true) throw new InvalidDataException("The local Bridge did not acknowledge telemetry.");
    }
    public void Dispose() => http.Dispose();
}
