using System.Text.Json;
using ACAI2.Rynth.Marae.Core;
using RynthCore.Plugin.RynthAi.Meta;
using RynthCore.Plugin.RynthAi.LegacyUi;
var meta=AfFileParser.Load(args[0]);
if(meta.Warnings.Count>0)throw new Exception(string.Join(";",meta.Warnings));
if(meta.EmbeddedNavs.Count!=20)throw new Exception("Expected 20 embedded routes.");
var states=meta.Rules.Select(r=>r.State).ToHashSet();int refs=0,requests=0,portals=0,expressions=0;
var extra=new HashSet<string>();
void Rule(MetaRule r)
{
    if(r.Action==MetaActionType.SetMetaState&&!states.Contains(r.ActionData))throw new Exception("Missing state "+r.ActionData);
    if(r.Action==MetaActionType.EmbeddedNavRoute){if(!meta.EmbeddedNavs.ContainsKey(r.ActionData))throw new Exception("Missing nav "+r.ActionData);refs++;}
    foreach(var c in r.Children.Concat(r.ActionChildren))Rule(c);
}
foreach(var r in meta.Rules)Rule(r);
foreach(var pair in meta.EmbeddedNavs)
{
    var nav=NavRouteParser.LoadFromLines(pair.Value);if(!string.IsNullOrEmpty(nav.LoadWarning))throw new Exception(nav.LoadWarning);
    if(nav.Points.Any(p=>p.Type==NavPointType.Npc))throw new Exception("Unconverted NPC waypoint.");
    foreach(var p in nav.Points)
    {
        if(p.Type==NavPointType.PortalNPC)portals++;
        if(p.Type!=NavPointType.Chat)continue;
        if(p.ChatCommand.Contains("mm_stock_npc_request"))requests++;
        if(p.ChatCommand.StartsWith("/ra mexec ")){extra.Add(p.ChatCommand[10..]);expressions++;}
    }
}
if(requests!=27||portals!=2)throw new Exception($"Expected 27 NPC gates and 2 unchanged portals, got {requests}/{portals}.");
File.WriteAllText(args[1],JsonSerializer.Serialize(meta.Rules));
File.WriteAllText(args[2],JsonSerializer.Serialize(extra));
Console.WriteLine($"PASS: stock RynthSuite parsers: {meta.Rules.Count} rules, {states.Count} states, 20 routes, {refs} references, 27 confirmed-use requests, 2 native portals, {expressions} nav expressions.");
// Replay the route serializer through the genuine stock nav parser: no guessed trailers.
foreach(var name in new[]{"180_-_Hunt_Loop","225_-_Hunt_Loop","275_-_Hunt_East","180_Continuous_Hunt","225_Continuous_Hunt","275_-_Treasure_Circuit"})
{
    var n=NavRouteParser.LoadFromLines(meta.EmbeddedNavs[name]);
    foreach(int active in new[]{0,n.Points.Count/2,n.Points.Count-1})
    {
        var snapshot=new RouteSnapshot(name,(int)n.RouteType,active,n.Points.Select(p=>new RoutePoint(p.Type.ToString(),p.NS,p.EW,p.Z,p.ChatCommand)).ToArray());
        using var reader=new StringReader(snapshot.ResumeFile());var lines=new List<string>();while(reader.ReadLine() is string line)lines.Add(line);
        var restored=NavRouteParser.LoadFromLines(lines);
        if(!string.IsNullOrEmpty(restored.LoadWarning)||restored.Points.Count!=(n.RouteType==NavRouteType.Circular?n.Points.Count:n.Points.Count-active)||restored.Points[0].NS!=n.Points[active].NS||restored.Points[^1].ChatCommand!=(n.RouteType==NavRouteType.Circular?n.Points[(active+n.Points.Count-1)%n.Points.Count].ChatCommand:n.Points[^1].ChatCommand))throw new Exception("Resume route lost order or completion action.");
    }
}
Console.WriteLine("PASS: 18 actual-parser hunt route save/restore round trips, including final completion commands.");
