using System.Collections.Concurrent;
using System.Text.Json;
using ACAI2.Rynth.Core.Keyfer;
using ACAI2.Rynth.Core;
using RynthCore.PluginCore;
using RynthCore.PluginSdk;

namespace ACAI2.Rynth.Plugin;

public sealed class PilotPlugin : RynthPluginBase
{
    public const string Version = "0.3.1";
    private readonly object sessionGate=new();
    private KeyferSession? keyfer;
    private KeyferSettings keyferSettings=new();
    private string keyferStatus="Keyfer disabled";
    private string KeyferPath=>Path.Combine(DataRoot,"keyfer.json");
    private PilotSettings settings = new();
    private PilotSession? observations;
    private BridgeWorker? bridge;
    private readonly ConcurrentQueue<string> commands = new();
    private int commandCount, loginPending, logoutPending;
    private volatile bool waitingForLogin;
    private DateTime nextProbe;
    private DateTime nextTelemetry,nextKeyferRetry,nextKeyferError;
    private string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ACAI2-Rynth");
    public override uint MinimumApiVersion => 92;

    public override int Initialize()
    {
        settings = PilotSettings.Load(Path.Combine(DataRoot, "pilot.json"));
        keyferSettings=KeyferSettings.Load(KeyferPath);keyferSettings.DataRoot=DataRoot;
        observations = new(settings); bridge = new(settings);
        Host.Log("[ACAI2.Rynth] v0.3.1 Keyfer adapter. Combat UI excluded. Keyfer "+(keyferSettings.keyferEnabled?"enabled for "+keyferSettings.keyferServer+" / "+keyferSettings.keyferCharacter:"disabled; configure keyfer.json"));
        return 0;
    }
    // Lifecycle callbacks only signal state changes. Native reads and local UI writes happen on the pump.
    public override void OnLoginComplete() { observations?.Current?.Dispose(); Interlocked.Exchange(ref loginPending, 1); waitingForLogin = false; }
    public override void OnLogout() { observations?.Current?.Dispose(); waitingForLogin = true; Interlocked.Exchange(ref logoutPending, 1); }
    private bool AcceptCallbacks => !waitingForLogin && Volatile.Read(ref loginPending) == 0 && Volatile.Read(ref logoutPending) == 0;
    public override void OnChatWindowText(string? text, int chatType, ref int eat)
    {
        var lease = observations?.Current;
        if (AcceptCallbacks && lease != null)
        {
            observations!.ObserveChat(lease, text, chatType, DateTime.UtcNow);
            lock(sessionGate)if(observations.Current==lease)keyfer?.Capture(text,chatType,DateTime.UtcNow);
        }
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
        if (command is not ("/acai2r status" or "/acai2r export" or "/acai2r ui") && command!="/acai2r keyfer" && !command.StartsWith("/acai2r keyfer ",StringComparison.Ordinal)) return;
        eat = 1; // Only our local diagnostic/UI commands; never another plugin's /acai2 command.
        if (Interlocked.Increment(ref commandCount) > 8) { Interlocked.Decrement(ref commandCount); return; }
        commands.Enqueue(command);
    }
    public override void OnTick()
    {
        if (observations == null) return;
        if (Interlocked.Exchange(ref logoutPending, 0) != 0) EndSession();
        if (Interlocked.Exchange(ref loginPending, 0) != 0) { EndSession(); nextProbe = DateTime.MinValue; }
        DateTime now = DateTime.UtcNow;
        if (!waitingForLogin && now >= nextProbe)
        {
            nextProbe = now.AddSeconds(1);
            ProbeIdentity(now);
            if (observations.Current is { } lease)
            {
                if(keyfer==null&&keyferSettings.Matches(lease.Identity)&&now>=nextKeyferRetry)
                {nextKeyferRetry=now.AddSeconds(5);lock(sessionGate)StartKeyfer();}
                observations.SetObjects(lease, ReadObjects(lease.Identity.PlayerId));
                if(observations.Capture(now) is {} perception)keyfer?.UpdatePerception(perception);
                if (now >= nextTelemetry)
                {
                    nextTelemetry = now.AddSeconds(settings.TelemetrySeconds);
                    if (observations.Capture(now) is { } frame) bridge?.Submit(frame);
                }
            }
        }
        lock(sessionGate)
        {
            var active=observations.Current;
            if(AcceptCallbacks&&active!=null&&keyfer!=null)
            {
                try { keyfer.Pump(command=>{
                    if(!AcceptCallbacks||observations.Current!=active||active.Token.IsCancellationRequested||!Host.HasInvokeChatParser)throw new InvalidOperationException("Keyfer native send unavailable.");
                    if(!Host.InvokeChatParser(command))throw new InvalidOperationException("Rynth rejected the chat command.");
                },Local); }
                catch(Exception ex){keyferStatus="Keyfer pump: "+ex.GetType().Name;if(now>=nextKeyferError){nextKeyferError=now.AddSeconds(15);Local("[ACAI2.Rynth] "+keyferStatus+" "+ex.Message);}}
            }
        }
        while (commands.TryDequeue(out string? command))
        {
            Interlocked.Decrement(ref commandCount);
            if (command == "/acai2r status") Local(Status());
            else if(command=="/acai2r ui") Local("[ACAI2.Rynth] Combat UI is excluded from the Keyfer rollout.");
            else if(command.StartsWith("/acai2r keyfer",StringComparison.Ordinal))
            {
                string option=command.Length>14?command[14..].Trim():"status";if(option.Length==0)option="status";
                if(option=="reload")
                {
                    try {var updated=KeyferSettings.Load(KeyferPath);updated.DataRoot=DataRoot;lock(sessionGate){keyfer?.Dispose();keyfer=null;keyferSettings=updated;StartKeyfer();}Local("[ACAI2.Rynth] Keyfer settings reloaded. "+keyferStatus);}
                    catch(Exception ex){Local("[ACAI2.Rynth] Keyfer reload failed: "+ex.GetType().Name+". Existing files preserved.");}
                }
                else Local("[ACAI2.Rynth] "+(keyfer?.Control(option)??keyferStatus));
            }
            else Export(now);
        }
    }
    private void EndSession()
    {
        lock(sessionGate){keyfer?.Dispose();keyfer=null;observations?.End();keyferStatus="Waiting for Keyfer login";}
    }
    private void StartKeyfer()
    {
        var lease=observations?.Current;
        if(lease==null||!keyferSettings.Matches(lease.Identity)){keyferStatus="Keyfer disabled for this identity";return;}
        try {keyfer=new KeyferSession(keyferSettings,lease);keyferStatus="Keyfer connecting";}
        catch(IOException){keyferStatus="Keyfer already owned by another Rynth session, or local data is unavailable. Reload after it closes.";}
        catch(Exception ex){keyferStatus="Keyfer unavailable: "+ex.GetType().Name;}
    }
    private void ProbeIdentity(DateTime now)
    {
        // Do not synthesize UnknownServer/UnknownCharacter registrations.
        if (Host.HasUiHooks && Host.GetScreenMode(out _) != RynthScreenMode.World) { EndSession(); return; }
        uint player = Host.GetPlayerId();
        if (player == 0 || !Host.TryGetWorldName(out string server) || !Host.TryGetObjectName(player, out string character)) { EndSession(); return; }
        var identity = new GameIdentity(server, character, player);
        try { identity.Validate(); } catch (InvalidDataException) { EndSession(); return; }
        if (observations!.Current?.Identity == identity) return;
        EndSession();
        lock(sessionGate){observations.Begin(identity,now);StartKeyfer();}
        nextTelemetry = DateTime.MinValue;
        Host.Log("[ACAI2.Rynth] Observing " + identity.RuntimeId + " (isolated session).");
    }
    private IEnumerable<ObservedObject> ReadObjects(uint player)
    {
        if (!Host.HasGetLiveObjectIds || !Host.HasGetObjectPosition || !Host.TryGetObjectPosition(player, out uint cell, out _, out _, out _) || cell == 0) yield break;
        int count = 0;
        foreach (uint id in Host.GetLiveObjectIds())
        {
            if (id == player || !Host.TryGetObjectPosition(id, out uint objectCell, out float x, out float y, out float z) || objectCell == 0 || (objectCell >> 16) != (cell >> 16)) continue;
            if (!Host.TryGetObjectName(id, out string name)) continue;
            yield return new(id, name, objectCell, x, y, z, Host.HasHasAppraisalData && Host.HasAppraisalData(id),Host.HasGetObjectBitfield&&Host.TryGetObjectBitfield(id,out uint bits)&&(bits&0x8)!=0?"Player":"Object");
            if (++count >= settings.MaxObjects) yield break;
        }
    }
    private string Status()
    {
        var frame = observations?.Capture(DateTime.UtcNow);
        return frame == null ? "[ACAI2.Rynth] Waiting for a valid logged-in identity. " + bridge?.Status :
            $"[ACAI2.Rynth] {frame.Snapshot.Identity.RuntimeId} | objects {frame.Snapshot.Objects.Length} | chat events {frame.Snapshot.ChatEvents} | {bridge?.Status} | {keyfer?.Status??keyferStatus}";
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
        EndSession();
        observations?.Dispose(); bridge?.Stop(); observations = null; bridge = null;
        Host.Log("[ACAI2.Rynth] Keyfer adapter stopped.");
    }
}
