using System.Text.Json;
using RynthCore.Plugin.RynthAi.Meta;
using RynthCore.Plugin.RynthAi.LegacyUi;
string folder=args[0];string name="275 Plateau Treasure - Verified";
var af=AfFileParser.Load(Path.Combine(folder,name+".af"));
var met=MetFileParser.Load(Path.Combine(folder,name+".met"));
if(af.Warnings.Count+met.Warnings.Count>0)throw new Exception(string.Join(";",af.Warnings.Concat(met.Warnings)));
if(af.Rules.Count!=331||met.Rules.Count!=331)throw new Exception("Incomplete meta parse");
object Tree(MetaRule r)=>new{r.Condition,r.ConditionData,r.Action,r.ActionData,r.ChatColors,r.CallReturnState,r.Enabled,Children=r.Children.Select(Tree).ToArray(),ActionChildren=r.ActionChildren.Select(Tree).ToArray()};
foreach(var pair in af.Rules.Zip(met.Rules))
 if(pair.First.State!=pair.Second.State||JsonSerializer.Serialize(Tree(pair.First))!=JsonSerializer.Serialize(Tree(pair.Second)))throw new Exception("AF/MET differ: "+pair.First.State);
Console.WriteLine("331 AF/MET rules match through genuine pinned RynthSuite parsers.");
Checks.Run(met,folder);
