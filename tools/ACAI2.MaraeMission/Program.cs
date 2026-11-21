using System.Text.Json;
using RynthCore.Plugin.RynthAi.Meta;
using RynthCore.Plugin.RynthAi.LegacyUi;
if(args[0]=="--map-goal-test") {
 int checks=0;
 foreach(double heading in new[]{0d,45,90,135,180,225,270,315}) {
  if(!MaraeMapWalkMath.TryGoal(45,-75,heading,12,out double ns,out double ew))throw new Exception("Goal refused");
  double dn=(ns-45)*240,de=(ew+75)*240;
  if(Math.Abs(dn-12*Math.Cos(heading*Math.PI/180))>1e-8 || Math.Abs(de-12*Math.Sin(heading*Math.PI/180))>1e-8)throw new Exception("Clue bearing inverted");
  checks++;
 }
 if(MaraeMapWalkMath.TryGoal(double.NaN,0,0,10,out _,out _) || MaraeMapWalkMath.TryGoal(0,0,0,-1,out _,out _))throw new Exception("Invalid goal accepted");checks+=2;
 if(!MaraeMapWalkMath.TryGoal(0,0,0,1000,out double limited,out _) || Math.Abs(limited*240-120)>1e-9)throw new Exception("Unbounded clue step");checks++;
 if(MaraeMapWalkMath.TryGoal(102.85,0,0,24,out _,out _))throw new Exception("Out-of-world goal accepted");checks++;
 foreach(double stride in new[]{8d,30d,60d,120d}) {
  if(!MaraeMapWalkMath.TryGoal(0,0,90,stride,out _,out double east) || Math.Abs(east*240-stride)>1e-8)throw new Exception("Configured stride was silently shortened");checks++;
 }
 Console.WriteLine($"PASS: {checks} map-clue geometry checks using the native helper source.");return;
}
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
if(meta.EmbeddedNavs.Count!=20)throw new Exception("Expected 20 embedded routes");
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
