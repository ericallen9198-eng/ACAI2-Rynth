using System.Text.Json;
using RynthCore.Plugin.RynthAi.Meta;
using RynthCore.Plugin.RynthAi.LegacyUi;
if(args[0]=="--lua-test") {
 var vm=new MoonSharp.Interpreter.Script(MoonSharp.Interpreter.CoreModules.Preset_Complete);
 vm.DoString(File.ReadAllText(args[1]));vm.DoString(File.ReadAllText(args[2]));
 var count=vm.DoString(File.ReadAllText(args[3]));
 Console.WriteLine($"PASS: {count.Number} MapHunt controller checks through genuine MoonSharp; fake game only.");
 if(args.Length>4) {
  var seen=vm.Globals.Get("sim").Table.Get("exprSeen").Table.Keys.Select(k=>k.String).ToArray();
  File.WriteAllText(args[4],JsonSerializer.Serialize(seen));
 }
 return;
}
var meta=AfFileParser.Load(args[0]);
if(meta.Warnings.Count>0)throw new Exception(string.Join(";",meta.Warnings));
if(meta.EmbeddedNavs.Count!=0)throw new Exception("Generic meta must load external maphunt.nav");
var states=meta.Rules.Select(r=>r.State).ToHashSet();
void Walk(MetaRule r){
 if(r.Action==MetaActionType.SetMetaState&&!states.Contains(r.ActionData))throw new Exception("Missing state "+r.ActionData);
 if(r.Action==MetaActionType.ChatCommand&&(r.ActionData.Contains("nav load")&&r.ActionData!="/ra nav load maphunt.nav"||r.ActionData=="/ls"))throw new Exception("Area-specific travel leaked");
 foreach(var c in r.Children.Concat(r.ActionChildren))Walk(c);
}
foreach(var r in meta.Rules)Walk(r);
File.WriteAllText(args[1],JsonSerializer.Serialize(meta.Rules));
Console.WriteLine($"PASS: genuine Rynth AF parser: {meta.Rules.Count} rules, {states.Count} states; all state references resolve.");
