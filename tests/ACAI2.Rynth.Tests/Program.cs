using System.Net;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ACAI2.Rynth.Core;

int count = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
var now = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
var identity = new GameIdentity("DreamWeave", "Toe", 0x50000001);
var options = new PilotSettings { CapturePublicChat = true, MaxChatRecords = 2, MaxObjects = 2 };
foreach (string url in new[] { "https://example.com", "http://example.com", "http://127.0.0.1/path", "http://user@127.0.0.1", "http://127.0.0.1?key=x" })
{
    bool rejected = false; try { (options with { BridgeUrl = url }).Validate(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Bridge settings reject non-loopback or unexpected URL components: " + url);
}
Check(new PilotSettings { BridgeUrl = "http://[::1]:5052" }.BridgeAddress().IsLoopback, "IPv6 loopback is accepted");
Check(!new PilotSettings().BridgeEnabled && !new PilotSettings().CapturePublicChat, "Bridge telemetry and raw chat capture default off");
bool invalid = false; try { new GameIdentity("UnknownServer", "Toe", 1).Validate(); } catch (InvalidDataException) { invalid = true; }
Check(invalid, "placeholder identities cannot register");
new GameIdentity("DreamWeave", "Unknown Hero", 1).Validate();
Check(true, "a legitimate character name beginning with Unknown is not banned");
using var session = new PilotSession(options);
Check(session.Capture(now) == null, "no snapshot exists before a valid login");
var first = session.Begin(identity, now);
session.ObserveChat(first, "Someone tells you, \"PRIVATE-SECRET [General] fake\"", 3, now);
session.ObserveChat(first, "[General] Toe says, \"hello\"", 1, now);
session.ObserveChat(first, "[Allegiance] Samm says, \"test\"", 1, now);
session.ObserveChat(first, "[General] Toe says, \"third\"", 1, now);
var snapshot = session.Capture(now)!.Snapshot;
Check(snapshot.ChatEvents == 4 && snapshot.PublicChat.Length == 2 && snapshot.DroppedChatRecords == 1, "public chat capture is bounded and retains an accurate event/drop count");
Check(snapshot.PublicChat.All(x => !x.Text.Contains("PRIVATE-SECRET")), "tells and an embedded channel label are never captured as public chat");
session.ObserveObject(first);
session.SetObjects(first, new[] { new ObservedObject(1, "One", 0x12340100, 1, 2, 3, true), new ObservedObject(1, "Duplicate", 0x12340100, 1, 2, 3, true),
    new ObservedObject(2, "Two", 0x12340100, 4, 5, 6, false), new ObservedObject(3, "Too many", 0x12340100, 0, 0, 0, false) });
var firstFrame = session.Capture(now)!;
Check(firstFrame.Snapshot.Objects.Length == 2 && firstFrame.Snapshot.Objects[1].Name == "Two" && firstFrame.Snapshot.ObjectEvents == 1, "object samples are deduplicated, bounded and copied");
firstFrame.Snapshot.Objects[0] = new(99,"Changed",1,0,0,0,false);
Check(session.Capture(now)!.Snapshot.Objects[0].Name == "One", "snapshot arrays cannot mutate the current observation store");
var second = session.Begin(new("Other Server", "Samm", 2), now.AddSeconds(1));
Check(first.Token.IsCancellationRequested && second.InstanceId != first.InstanceId, "switching characters cancels the old session and rotates the instance ID");
session.ObserveChat(first,"[General] Toe says, \"STALE\"",1,now); session.ObserveObject(first);
session.SetObjects(first,new[] { new ObservedObject(55,"STALE",1,0,0,0,false) });
Check(session.Capture(now)!.Snapshot is { ChatEvents: 0, ObjectEvents: 0, Objects.Length: 0 }, "delayed callbacks cannot pollute another character's session");
session.End();
Check(second.Token.IsCancellationRequested && session.Capture(now) == null, "logout clears observations and cancels queued work");
using (var defaults = new PilotSession(new()))
{
    var lease = defaults.Begin(identity, now); defaults.ObserveChat(lease, "[General] secret", 1, now);
    Check(defaults.Capture(now)!.Snapshot.PublicChat.Length == 0, "default mode counts chat without keeping its text");
}
bool badBounds=false;try { using var bad=new PilotSession(new(){MaxObjects=0}); }catch(InvalidDataException){badBounds=true;}
Check(badBounds,"invalid buffer settings fail before observing game data");
var settingsPath=Path.Combine(Path.GetTempPath(),"acai2-rynth-settings-"+Guid.NewGuid().ToString("N")+".json");
try {
    File.WriteAllText(settingsPath,"{\"bridgeEnabled\":true,\"bridgeUrl\":\"http://127.0.0.1:5052\"}");
    Check(PilotSettings.Load(settingsPath).BridgeEnabled,"source-generated settings loading accepts the documented case-insensitive JSON contract");
} finally { File.Delete(settingsPath); }
var active = session.Begin(identity, now);
session.SetObjects(active,new[] { new ObservedObject(42,"Dread Revenant Lord",0x12340100,1,2,3,true) });
var frame = session.Capture(now)!;
var networkOptions = options with { BridgeEnabled = true };
var handler = new RecordingHandler();
using (var client = new ReadOnlyBridgeClient(networkOptions, handler))
{
    Check(await client.SendAsync(frame), "opt-in telemetry completes against a fake Bridge");
    Check(handler.Calls.Select(x => x.Path).SequenceEqual(new[] { "/v1/client/register", "/v1/client/heartbeat", "/v1/client/perception" }), "transport only uses the three read-only observation routes");
    using var registration = JsonDocument.Parse(handler.Calls[0].Json);
    Check(registration.RootElement.GetProperty("ProtocolVersion").GetString() == "2" && registration.RootElement.GetProperty("InstanceId").GetString()!.StartsWith("rynth-") && registration.RootElement.GetProperty("Server").GetString() == "DreamWeave", "registration preserves protocol 2 with a distinct Rynth instance and actual identity");
    using var perception = JsonDocument.Parse(handler.Calls[2].Json);
    Check(perception.RootElement.GetProperty("Objects")[0].GetString() == "Dread Revenant Lord" && perception.RootElement.GetProperty("Monsters").GetArrayLength() == 0, "unclassified observations do not invent monster labels or distances");
    Check(handler.Calls.All(x => !x.Json.Contains("PRIVATE-SECRET") && !x.Path.Contains("keyfer") && x.Method == "POST"), "telemetry never forwards raw chat to Keyfer, asks AI, claims prizes or sends commands");
    bool mismatch = false;
    try { await client.SendAsync(frame with { Snapshot = frame.Snapshot with { Identity = new("Wrong", "Wrong", 5) } }); } catch (InvalidDataException) { mismatch = true; }
    Check(mismatch && handler.Calls.Count == 3, "identity-mismatched frames are rejected before HTTP");
}
var disabledHandler = new RecordingHandler();
using (var disabled = new ReadOnlyBridgeClient(new(), disabledHandler)) Check(!await disabled.SendAsync(frame) && disabledHandler.Calls.Count == 0, "disabled telemetry produces zero network calls");
var negativeAck = new RecordingHandler { Reply = "{\"ok\":false}" };
using (var rejecting = new ReadOnlyBridgeClient(networkOptions, negativeAck))
{
    bool failed=false;try { await rejecting.SendAsync(frame); } catch(InvalidDataException) { failed=true; }
    Check(failed && negativeAck.Calls.Count==1,"a successful HTTP status without a positive Bridge acknowledgment is not reported as telemetry success");
}
var staleHandler = new RecordingHandler(); active.Dispose();
using (var stale = new ReadOnlyBridgeClient(networkOptions, staleHandler)) Check(!await stale.SendAsync(frame) && staleHandler.Calls.Count == 0, "cancelled login frames cannot register after logout");
active = session.Begin(identity,now); frame = session.Capture(now)!;
var redirectHandler = new RecordingHandler { Code = HttpStatusCode.Found };
using (var redirect = new ReadOnlyBridgeClient(networkOptions, redirectHandler))
{
    bool rejected = false; try { await redirect.SendAsync(frame); } catch (HttpRequestException) { rejected = true; }
    Check(rejected && redirectHandler.Calls.Count == 1, "redirects do not redirect observation traffic to another service");
}
var logoutHandler = new RecordingHandler { OnFirst = () => session.End() };
using (var logout = new ReadOnlyBridgeClient(networkOptions, logoutHandler))
{
    bool cancelled = false; try { await logout.SendAsync(frame); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled && logoutHandler.Calls.Count == 1, "logout during registration cancels later heartbeat/perception delivery");
}
active=session.Begin(identity,now); frame=session.Capture(now)!;
var blocked = new BlockingHandler();
await using (var worker = new BridgeWorker(networkOptions, blocked))
{
    worker.Submit(frame); await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
    for(int i=1;i<=20;i++) worker.Submit(frame with { Snapshot=frame.Snapshot with { CapturedUtc=now.AddSeconds(i), Objects=[new((uint)i,"Latest-"+i,1,0,0,0,false)] } });
    blocked.Release.TrySetResult();
    await blocked.SixCalls.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Check(blocked.Calls.Count==6 && blocked.Calls.Last().Json.Contains("Latest-20") && !blocked.Calls.Last().Json.Contains("Latest-19"), "slow HTTP retains only the latest pending frame instead of growing an event backlog");
    worker.Stop(); Check(!worker.Submit(frame), "stopped workers refuse new frames");
}
// Combat aggregation and profiles are tested without loading ImGui or a game client.
Check(!new CombatAppearance().Floating,"custom proc text defaults off to avoid duplicating Rynth Vision numbers");
Check(CombatDisplay.Arc(2,2,true,50,100,100).X<0 && CombatDisplay.Arc(2,2,false,50,100,100).X>0,"maximum curvature cannot cross lanes through the character");
var meter=new CombatDisplay();
Check(meter.Observe("Your poison deals 1,221 additional points of damage to Dread Plaguefang!","Toe",100),"real poison log parses into combat totals");
meter.Observe("Your poison deals 173 additional points of damage to Dread Plaguefang!","Toe",100.1);
meter.Observe("Sneak Attack! You graze Dread Plaguefang for 69 points of bludgeoning damage!","Toe",100.2);
var combatView=meter.Read(101,2.4f);
Check(combatView.Damage==1463 && combatView.Sneaks==1 && combatView.SneakDamage==69 && combatView.DirectHits==1,"poison and sneak totals preserve actual damage and direct-hit denominator");
Check(combatView.Components["Poison"]==1394 && combatView.Text.Any(x=>x.Kind=="Poison" && x.Amount==1394 && x.Count==2),"same-target poison burst groups with an explicit tick count");
meter.Observe("Your poison deals 20 additional points of damage to Other Target!","Toe",100.12);
Check(meter.Read(101,2.4f).Text.Count(x=>x.Kind=="Poison")==2,"different targets never merge into a fabricated single proc");
Check(!meter.Observe("[Allegiance] Samm says, \"Your poison deals 9,999 additional points of damage to Dread Plaguefang!\"","Toe",101),"quoted chat is excluded from combat parsing");
Check(meter.Read(161,2.4f).Dps==0 && meter.Read(161,2.4f).Damage==1483,"rolling damage expires while session totals remain intact");
Check(meter.Read(161,2.4f).Text.Length==0,"expired floating events are not rendered");
var freshMeter=new CombatDisplay();
Check(freshMeter.Read(200,2.4f).Damage==0,"new character/session starts with clean combat totals");
for(int i=0;i<100;i++)freshMeter.Observe($"Your poison deals 1 additional points of damage to Target {i}!","Toe",200+i*.001);
Check(freshMeter.Read(201,2.4f).Text.Length<=16,"floating event retention remains bounded during a dense burst");
var leftArc=CombatDisplay.Arc(1,2,true,100,40,100);var rightArc=CombatDisplay.Arc(1,2,false,100,40,100);
Check(leftArc.X==-rightArc.X && Math.Abs(leftArc.X)<100 && leftArc.Y<0,"paired arcs rise and curve symmetrically inward");
string profileRoot=Path.Combine(Path.GetTempPath(),"acai2-ui-tests-"+Guid.NewGuid().ToString("N"));
try
{
    var toe=new GameIdentity("DreamWeave","Toe",1);var other=new GameIdentity("DreamWeave","Samm",2);
    string toePath=CombatProfiles.PathFor(profileRoot,toe);
    Check(toePath!=CombatProfiles.PathFor(profileRoot,other) && toePath==CombatProfiles.PathFor(profileRoot,toe with {PlayerId=999}),"profiles isolate server/character and survive changing object IDs");
    var appearance=new CombatAppearance{Locked=true,Opacity=.25f,Curvature=62,X=424};
    appearance.Colors["Poison"]=0x8844cc22;
    CombatProfiles.Save(toePath,appearance);var restored=CombatProfiles.Load(toePath);
    Check(restored.Locked && restored.X==424 && restored.Curvature==62 && restored.Colors["Poison"]==0x8844cc22,"atomic profile roundtrip retains lock, position, arc and RGBA customization");
    Check(!Directory.GetFiles(Path.GetDirectoryName(toePath)!,"*.tmp").Any(),"successful profile save leaves no temporary files");
    restored.Scale=float.NaN;restored.TextSize=900;restored.Validate();
    Check(restored.Scale==1 && restored.TextSize==40,"invalid appearance values are normalized before rendering");
    File.WriteAllText(toePath,"{\"Schema\":2}");
    bool futureRejected=false;try{CombatProfiles.Load(toePath);}catch(InvalidDataException){futureRejected=true;}
    Check(futureRejected && File.ReadAllText(toePath).Contains("2"),"future profile versions are rejected without overwriting their contents");
}
finally{if(Directory.Exists(profileRoot))Directory.Delete(profileRoot,true);}
string? pluginPath = args.Length > 0 ? args[0] : null;
if (pluginPath != null)
{
    using var file=File.OpenRead(pluginPath); using var pe=new PEReader(file); var reader=pe.GetMetadataReader();
    Check(pe.PEHeaders.CoffHeader.Machine==Machine.I386 && pe.PEHeaders.CorHeader!=null,"actual compiled plugin is a managed x86 assembly for the CoreCLR host");
    var assemblyRefs=reader.AssemblyReferences.Select(x=>reader.GetString(reader.GetAssemblyReference(x).Name)).ToArray();
    Check(!assemblyRefs.Any(x=>x.Contains("ImGui")||x.Contains("Combat.Future")),"shipped Keyfer plugin excludes combat UI and future combat assembly references");
    Check(!assemblyRefs.Any(x=>x.Contains("Decal") || x.Contains("UtilityBelt") || x.Contains("uTank")),"compiled plugin has no legacy framework assembly dependency");
    using(var coreFile=File.OpenRead(Path.Combine(Path.GetDirectoryName(pluginPath)!,"ACAI2.Rynth.Core.dll")))
    using(var corePe=new PEReader(coreFile))
    {
        var coreReader=corePe.GetMetadataReader();
        Check(!coreReader.TypeDefinitions.Any(t=>coreReader.GetString(coreReader.GetTypeDefinition(t).Name).StartsWith("Combat",StringComparison.Ordinal)),"published Core assembly contains no combat parser, aggregation or rendering types");
    }
    var exports=new List<string>(); uint minimumApi=0; bool cdecl=true;
    foreach(var handle in reader.CustomAttributes)
    {
        var attribute=reader.GetCustomAttribute(handle);
        if(attribute.Constructor.Kind!=HandleKind.MemberReference) continue;
        var member=reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        if(member.Parent.Kind!=HandleKind.TypeReference) continue;
        var type=reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if(reader.GetString(type.Name)=="RynthPluginManifestAttribute")
        { minimumApi=(uint)attribute.DecodeValue(new AttributeTypes()).FixedArguments.Single().Value!; continue; }
        if(reader.GetString(type.Name)!="UnmanagedCallersOnlyAttribute") continue;
        var value=attribute.DecodeValue(new AttributeTypes());
        var name=value.NamedArguments.First(x=>x.Name=="EntryPoint").Value as string;
        if(name!=null) exports.Add(name);
        var conventions=value.NamedArguments.FirstOrDefault(x=>x.Name=="CallConvs").Value;
        cdecl &= conventions is ImmutableArray<CustomAttributeTypedArgument<string>> values && values.Length==1 && values[0].Value?.ToString()?.Contains("CallConvCdecl")==true;
    }
    var expected=new[]{"RynthPluginRender","RynthPluginRenderOverlay","RynthPluginInit","RynthPluginShutdown","RynthPluginName","RynthPluginVersion","RynthPluginOnLoginComplete","RynthPluginOnLogout","RynthPluginTick","RynthPluginOnChatWindowText","RynthPluginOnChatBarEnter","RynthPluginOnCreateObject","RynthPluginOnDeleteObject","RynthPluginOnUpdateObject","RynthPluginOnUpdateObjectInventory"};
    Check(exports.Order().SequenceEqual(expected.Order()),"compiled lifecycle/chat/object exports match the real RynthCore ABI");
    Check(cdecl,"every compiled native entry point declares the required Cdecl calling convention");
    Check(minimumApi==92,"compiled manifest attribute requires the reviewed API 92 rather than silently loading an older host");
}
string sourceRoot=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"src","ACAI2.Rynth.Plugin"));
if(Directory.Exists(sourceRoot)) {
    var allowed=new HashSet<string>{"Log","WriteToChat","GetScreenMode","GetPlayerId","TryGetWorldName","TryGetObjectName","TryGetObjectPosition","HasAppraisalData","GetLiveObjectIds","SetPanelOpen","InvokeChatParser","TryGetObjectBitfield"};
    var used=Directory.GetFiles(sourceRoot,"*.cs").SelectMany(path=>System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path),@"[Hh]ost\.(\w+)\s*\(").Select(m=>m.Groups[1].Value)).ToArray();
    Check(used.Length>0 && used.All(allowed.Contains),"plugin native calls are read APIs, local diagnostics and the guarded Keyfer chat dispatcher only");
}
await KeyferChecks.Run(Check);
Console.WriteLine($"All {count} checks passed. Only fake HTTP handlers were used; no AI, game chat or production Bridge calls.");

record Call(string Path,string Json,string Method);
class RecordingHandler : HttpMessageHandler
{
    public List<Call> Calls { get; }=[]; public HttpStatusCode Code=HttpStatusCode.OK; public Action? OnFirst; public string Reply="{\"Ok\":true}";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        Calls.Add(new(request.RequestUri!.AbsolutePath,await request.Content!.ReadAsStringAsync(ct),request.Method.Method));
        if(Calls.Count==1) OnFirst?.Invoke();
        return new(Code){Content=new StringContent(Reply),Headers={Location=new Uri("http://example.com/")}};
    }
}
sealed class BlockingHandler : RecordingHandler
{
    public TaskCompletionSource Entered { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource SixCalls { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        if(Calls.Count==0){Entered.TrySetResult();await Release.Task.WaitAsync(ct);}
        var response=await base.SendAsync(request,ct);if(Calls.Count>=6) SixCalls.TrySetResult();return response;
    }
}
sealed class AttributeTypes : ICustomAttributeTypeProvider<string>
{
    public string GetPrimitiveType(PrimitiveTypeCode code)=>code.ToString();
    public string GetSystemType()=>"System.Type";
    public bool IsSystemType(string type)=>type=="System.Type";
    public string GetSZArrayType(string type)=>type+"[]";
    public string GetTypeFromDefinition(MetadataReader reader,TypeDefinitionHandle handle,byte raw)=>reader.GetString(reader.GetTypeDefinition(handle).Name);
    public string GetTypeFromReference(MetadataReader reader,TypeReferenceHandle handle,byte raw)=>reader.GetString(reader.GetTypeReference(handle).Name);
    public string GetTypeFromSerializedName(string name)=>name;
    public PrimitiveTypeCode GetUnderlyingEnumType(string type)=>PrimitiveTypeCode.Int32;
}
