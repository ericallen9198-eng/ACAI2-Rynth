using RynthCore.Plugin.RynthAi;
using RynthCore.Plugin.RynthAi.LegacyUi;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
var root = args[0];
int markers = 0, routes = 0;
foreach (var path in Directory.GetFiles(Path.Combine(root, "NavProfiles"), "*.nav"))
{
    var route = NavRouteParser.Load(path);
    Check(string.IsNullOrEmpty(route.LoadWarning), "genuine parser accepts " + path);
    var expected = File.ReadAllLines(path).Where(l => l.StartsWith("#rynth-talk:")).Select(l => int.Parse(l[12..])).ToHashSet();
    var actual = route.Points.Select((p,i) => (p,i)).Where(x => x.p.ConfirmNpcReply).Select(x => x.i).ToHashSet();
    Check(actual.SetEquals(expected), "markers identify exactly their recorded NPC indices");
    Check(route.Points.Where(p => p.ConfirmNpcReply).All(p => p.Type == NavPointType.Npc), "only NPCs are marked");
    var again = NavRouteParser.LoadFromLines(route.ToLines());
    Check(again.ToLines().SequenceEqual(route.ToLines()), "save/load preserves full route including markers");
    markers += actual.Count; routes++;
}
Check(routes == 19 && markers == 27, "all 19 routes and 27 hunter waypoints inspected");
var synthetic = new NavRouteParser { RouteType = NavRouteType.Once };
synthetic.Points.Add(new() { Type = NavPointType.PortalNPC, TargetName = "Portal", ObjectClass = 37 });
synthetic.Points.Add(new() { Type = NavPointType.Npc, TargetName = "Hunter", ObjectClass = 37 });
var lines = synthetic.ToLines();
lines.AddRange(new[] { "#rynth-talk:0", "#rynth-talk:1", "#rynth-talk:1", "#rynth-talk:-1", "#rynth-talk:2", "#rynth-talk:bad", "#rynth-talk:99999999999999999" });
var parsed = NavRouteParser.LoadFromLines(lines);
Check(!parsed.Points[0].ConfirmNpcReply && parsed.Points[1].ConfirmNpcReply, "invalid/duplicate markers do not mark portals or break valid marker");
Check(!NavRouteParser.LoadFromLines(synthetic.ToLines()).Points[1].ConfirmNpcReply, "ordinary NPC remains unmarked");

const string name = "Hea Rukenua the Zefir Hunter";
var gate = new NpcReplyGate();var r1 = new object(); var r2 = new object();
gate.Bind(r1,1,name);
gate.Observe(name + " tells you, \"Old reply\"", 17);
Check(!gate.HasReply, "reply before a use is ignored");
Check(gate.Due(100), "initial use is due");
gate.BeginUse(100);
Check(gate.Waiting(r1,1) && !gate.Waiting(r2,1), "pending action is bound to exact route and index");
Check(!gate.Due(8099) && gate.Due(8100), "no rapid duplicate uses; retry after eight seconds");
gate.Observe("Other hunter tells you, \"Task ready\"",17);
Check(!gate.HasReply, "another hunter cannot advance the waypoint");
gate.Observe("Player says, \"" + name + " tells you, ...\"", 2);
Check(!gate.HasReply, "quoted NPC reply cannot advance");
gate.Observe(name + " tells you, \"Task ready\"",18);
Check(!gate.HasReply, "allegiance copy cannot acknowledge use");
gate.BeginUse(8100);
gate.Observe(name + " tells you, \"You have defeated 0 of 50 Drumtaken Enku Zefir.\"",17);
Check(gate.HasReply && !gate.Due(20000), "real progress reply confirms and stops retries");
gate.Bind(r1,3,"Hea Tamarau the Ursuin Hunter");
Check(!gate.HasReply, "next NPC does not inherit the last reply");
gate.BeginUse(10000);gate.Observe(name + " tells you, \"Late second line\"",17);
Check(!gate.HasReply, "late reply from previous hunter is ignored");
gate.Observe("Hea Tamarau the Ursuin Hunter says, \"Slay 50 Ursuin.\"",2);
Check(gate.HasReply, "named NPC speech reply also confirms");
gate.Bind(r2,3,"Hea Tamarau the Ursuin Hunter");
Check(!gate.HasReply && !gate.Waiting(r1,3), "route swap clears old acknowledgement");
gate.BeginUse(20000);gate.NoUse(20000);
gate.Observe("Hea Tamarau the Ursuin Hunter tells you, \"old\"",17);
Check(!gate.HasReply && !gate.Due(21999) && gate.Due(22000), "unsent action is disarmed and uses two-second retry backoff");
gate.BeginUse(22000);gate.Observe("Hea Tamarau the Ursuin Hunter tells you, \"Synchronous reply\"",3);
Check(gate.HasReply, "reply during native dispatch can be captured");
gate.Reset();Check(!gate.HasReply && !gate.Waiting(r2,3), "reset/logout/restart clears pending gate");
Console.WriteLine($"PASS: {checks} checks; {routes} beta routes and {markers} marked NPC waypoints. No native/game actions executed.");
