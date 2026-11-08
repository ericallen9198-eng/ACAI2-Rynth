using System.Collections.Concurrent;
using System.Text.Json;
using ACAI2.Rynth.Core;
using RynthCore.PluginCore;
using RynthCore.PluginSdk;

namespace ACAI2.Rynth.Plugin;

public sealed class PilotPlugin : RynthPluginBase
{
    public const string Version = "0.1.0";
    private PilotSettings settings = new();
    private PilotSession? observations;
    private BridgeWorker? bridge;
    private readonly ConcurrentQueue<string> commands = new();
    private int commandCount, loginPending, logoutPending;
    private volatile bool waitingForLogin;
    private DateTime nextProbe;
    private DateTime nextTelemetry;
    private string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ACAI2-Rynth");
    public override uint MinimumApiVersion => 92;

    public override int Initialize()
    {
        settings = PilotSettings.Load(Path.Combine(DataRoot, "pilot.json"));
        observations = new(settings); bridge = new(settings);
        Host.Log("[ACAI2.Rynth] v0.1.0 read-only pilot. Bridge " + (settings.BridgeEnabled ? "telemetry enabled" : "disabled") + ". No game commands, chat replies, or automation.");
        return 0;
    }
    // Lifecycle callbacks only signal state changes. Native reads and local UI writes happen on the pump.
    public override void OnLoginComplete() { Interlocked.Exchange(ref loginPending, 1); waitingForLogin = false; }
    public override void OnLogout() { waitingForLogin = true; Interlocked.Exchange(ref logoutPending, 1); }
    private bool AcceptCallbacks => !waitingForLogin && Volatile.Read(ref loginPending) == 0 && Volatile.Read(ref logoutPending) == 0;
    public override void OnChatWindowText(string? text, int chatType, ref int eat)
    {
        var lease = observations?.Current;
        if (AcceptCallbacks && lease != null) observations!.ObserveChat(lease, text, chatType, DateTime.UtcNow);
        // Leave incoming chat's eat flag untouched.
    }
    private void ObjectChanged() { var lease = observations?.Current; if (AcceptCallbacks && lease != null) observations!.ObserveObject(lease); }
    public override void OnCreateObject(uint id) => ObjectChanged();
    public override void OnDeleteObject(uint id) => ObjectChanged();
    public override void OnUpdateObject(uint id) => ObjectChanged();
    public override void OnUpdateObjectInventory(uint id) => ObjectChanged();
    public override void OnChatBarEnter(string? text, ref int eat)
    {
        string command = (text ?? "").Trim().ToLowerInvariant();
        if (command is not ("/acai2r status" or "/acai2r export")) return;
        eat = 1; // Only our two local diagnostic commands; never another plugin's /acai2 command.
        if (Interlocked.Increment(ref commandCount) > 8) { Interlocked.Decrement(ref commandCount); return; }
        commands.Enqueue(command);
    }
    public override void OnTick()
    {
        if (observations == null) return;
        if (Interlocked.Exchange(ref logoutPending, 0) != 0) observations.End();
        if (Interlocked.Exchange(ref loginPending, 0) != 0) { observations.End(); nextProbe = DateTime.MinValue; }
        DateTime now = DateTime.UtcNow;
        if (!waitingForLogin && now >= nextProbe)
        {
            nextProbe = now.AddSeconds(1);
            ProbeIdentity(now);
            if (observations.Current is { } lease)
            {
                observations.SetObjects(lease, ReadObjects(lease.Identity.PlayerId));
                if (now >= nextTelemetry)
                {
                    nextTelemetry = now.AddSeconds(settings.TelemetrySeconds);
                    if (observations.Capture(now) is { } frame) bridge?.Submit(frame);
                }
            }
        }
        while (commands.TryDequeue(out string? command))
        {
            Interlocked.Decrement(ref commandCount);
            if (command == "/acai2r status") Local(Status());
            else Export(now);
        }
    }
    private void ProbeIdentity(DateTime now)
    {
        // Do not synthesize UnknownServer/UnknownCharacter registrations.
        if (Host.HasUiHooks && Host.GetScreenMode(out _) != RynthScreenMode.World) { observations!.End(); return; }
        uint player = Host.GetPlayerId();
        if (player == 0 || !Host.TryGetWorldName(out string server) || !Host.TryGetObjectName(player, out string character)) { observations!.End(); return; }
        var identity = new GameIdentity(server, character, player);
        try { identity.Validate(); } catch (InvalidDataException) { observations!.End(); return; }
        if (observations!.Current?.Identity == identity) return;
        observations.Begin(identity, now); nextTelemetry = DateTime.MinValue;
        Host.Log("[ACAI2.Rynth] Observing " + identity.RuntimeId + " (read-only, isolated session).");
    }
    private IEnumerable<ObservedObject> ReadObjects(uint player)
    {
        if (!Host.HasGetLiveObjectIds || !Host.HasGetObjectPosition || !Host.TryGetObjectPosition(player, out uint cell, out _, out _, out _) || cell == 0) yield break;
        int count = 0;
        foreach (uint id in Host.GetLiveObjectIds())
        {
            if (id == player || !Host.TryGetObjectPosition(id, out uint objectCell, out float x, out float y, out float z) || objectCell == 0 || (objectCell >> 16) != (cell >> 16)) continue;
            if (!Host.TryGetObjectName(id, out string name)) continue;
            yield return new(id, name, objectCell, x, y, z, Host.HasHasAppraisalData && Host.HasAppraisalData(id));
            if (++count >= settings.MaxObjects) yield break;
        }
    }
    private string Status()
    {
        var frame = observations?.Capture(DateTime.UtcNow);
        return frame == null ? "[ACAI2.Rynth] Read-only pilot: waiting for a valid logged-in identity. " + bridge?.Status :
            $"[ACAI2.Rynth] {frame.Snapshot.Identity.RuntimeId} | read-only | objects {frame.Snapshot.Objects.Length} | chat events {frame.Snapshot.ChatEvents} | {bridge?.Status}";
    }
    private void Export(DateTime now)
    {
        var frame = observations?.Capture(now);
        if (frame == null) { Local("[ACAI2.Rynth] No active session to export."); return; }
        try
        {
            string root = Path.Combine(DataRoot, "Exports"); Directory.CreateDirectory(root);
            string path = Path.Combine(root, frame.Snapshot.InstanceId + "-" + now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(frame.Snapshot, PilotJson.Default.PilotSnapshot));
            Local("[ACAI2.Rynth] Local observation export: " + path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Local("[ACAI2.Rynth] Export unavailable: " + ex.GetType().Name); }
    }
    private void Local(string text) { Host.Log(text); if (observations?.Current != null && Host.HasWriteToChat) Host.WriteToChat(text, 1); }
    public override void Shutdown()
    {
        observations?.Dispose(); bridge?.Stop(); observations = null; bridge = null;
        Host.Log("[ACAI2.Rynth] Read-only pilot stopped.");
    }
}
