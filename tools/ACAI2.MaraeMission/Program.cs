using System.Text.Json;
using RynthCore.Plugin.RynthAi.Meta;
using RynthCore.Plugin.RynthAi.LegacyUi;
if(args[0]=="--lua-test") {
 var vm=new MoonSharp.Interpreter.Script(MoonSharp.Interpreter.CoreModules.Preset_Complete);
 vm.DoString(File.ReadAllText(args[1]));vm.DoString(File.ReadAllText(args[2]));
 var count=vm.DoString(File.ReadAllText(args[3]));
 Console.WriteLine($"PASS: {count.Number} controller checks through genuine MoonSharp; fake game interfaces only.");
 if(args.Length>4) {
  var allocationRun=vm.LoadString(File.ReadAllText(args[4]));
  long before=GC.GetAllocatedBytesForCurrentThread();
  vm.Call(allocationRun);
  long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
  Console.WriteLine($"Synthetic 5-second high-tick Lua allocation test: {bytes / 1048576.0:F2} MiB (fake native interfaces; excludes live engine costs).");
  if(bytes>16*1024*1024)throw new Exception("Lua allocation regression exceeds 16 MiB synthetic budget");
 }
 return;
}
var meta=AfFileParser.Load(args[0]);
if(meta.Warnings.Count>0)throw new Exception(string.Join(";",meta.Warnings));
if(meta.EmbeddedNavs.Count!=18)throw new Exception("Expected 18 embedded routes");
var states=meta.Rules.Select(r=>r.State).ToHashSet();int refs=0,marks=0;
void Walk(MetaRule r){
 if(r.Action==MetaActionType.SetMetaState&&!states.Contains(r.ActionData))throw new Exception("Missing state "+r.ActionData);
 if(r.Action==MetaActionType.ChatCommand&&r.ActionData.Contains("nav load"))throw new Exception("External route load");
 if(r.Action==MetaActionType.EmbeddedNavRoute){if(!meta.EmbeddedNavs.ContainsKey(r.ActionData))throw new Exception("Missing nav "+r.ActionData);refs++;}
 foreach(var c in r.Children.Concat(r.ActionChildren))Walk(c);
}
foreach(var r in meta.Rules)Walk(r);
foreach(var pair in meta.EmbeddedNavs){var n=NavRouteParser.LoadFromLines(pair.Value);if(!string.IsNullOrEmpty(n.LoadWarning))throw new Exception(n.LoadWarning);marks+=n.Points.Count(p=>p.ConfirmNpcReply);}
if(marks!=27)throw new Exception("Missing hunter confirmation markers");
File.WriteAllText(args[1],JsonSerializer.Serialize(meta.Rules));
Console.WriteLine($"PASS: {meta.Rules.Count} rules, {states.Count} states, {meta.EmbeddedNavs.Count} routes, {refs} embedded references, {marks} confirmed hunter waypoints; genuine Rynth parsers.");
