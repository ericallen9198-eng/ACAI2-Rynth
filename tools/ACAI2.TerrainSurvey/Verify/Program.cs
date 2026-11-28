using System.Text.Json;
using RynthCore.Plugin.RynthAi.Meta;
using RynthCore.Plugin.RynthAi.LegacyUi;
var meta=AfFileParser.Load(args[0]);if(meta.Warnings.Count>0)throw new Exception(string.Join(';',meta.Warnings));
var states=meta.DeclaredStates.ToHashSet();int portals=0,points=0,refs=0;
void Check(MetaRule r){
 if(r.Action==MetaActionType.SetMetaState&&!states.Contains(r.ActionData))throw new Exception("Undefined state "+r.ActionData);
 if(r.Action==MetaActionType.EmbeddedNavRoute){if(!meta.EmbeddedNavs.ContainsKey(r.ActionData))throw new Exception("Undefined embedded nav");refs++;}
 if(r.State.StartsWith("WAIT_")&&r.ActionData.Contains("te_last_progress"))throw new Exception("One-shot movement timer returned");
 foreach(var c in r.Children.Concat(r.ActionChildren))Check(c);
}
foreach(var rule in meta.Rules)Check(rule);
foreach(var (name,lines)in meta.EmbeddedNavs){var nav=NavRouteParser.LoadFromLines(lines);if(nav.LoadWarning!=null||nav.RouteType!=NavRouteType.Once)throw new Exception("Invalid terrain nav "+name);foreach(var p in nav.Points){if(p.Type==NavPointType.Npc)throw new Exception("Unexpected NPC");if(p.Type==NavPointType.PortalNPC)portals++;if(!double.IsFinite(p.NS)||!double.IsFinite(p.EW)||!double.IsFinite(p.Z))throw new Exception("Nonfinite coordinate");points++;}}
if(portals!=2||meta.EmbeddedNavs.Count!=77)throw new Exception("Missing terrain/hub connections");
if(args.Length>1)File.WriteAllText(args[1],JsonSerializer.Serialize(new{rules=meta.Rules.Count,states=states.Count,navs=meta.EmbeddedNavs.Count,refs,points,portals,warnings=meta.Warnings.Count}));
Console.WriteLine($"PASS genuine upstream AF/nav parsers: {meta.Rules.Count} rules, {states.Count} states, {refs} refs, {points} points; 2 deliberate portals; no one-shot movement monitor.");
