using System.Text.Json;
using System.Text.RegularExpressions;
using RynthCore.Loot.VTank;
static class LootChecks
{
 static readonly string[] Currency=["(DW) Trade Note","(DW) Marae Lassel Doubloon"];
 public static void Prepare(string source,string destination)
 {
  var p=VTankLootParser.Load(source);
  foreach(string name in Currency)
  {
   var r=p.Rules.Single(r=>r.Name==name);
   if(!r.Enabled||r.Action!=VTankLootAction.Keep)throw new Exception("Currency was not enabled Keep: "+name);
   if(r.Conditions.RemoveAll(c=>c.NodeType==VTankNodeTypes.CharacterMainPackEmptySlotsGE)!=1)throw new Exception("Unexpected slot condition: "+name);
  }
  VTankLootWriter.Save(p,destination);Verify(source,destination);
 }
 public static void Verify(string source,string destination)
 {
  var before=VTankLootParser.Load(source);var after=VTankLootParser.Load(destination);
  foreach(string name in Currency)before.Rules.Single(r=>r.Name==name).Conditions.RemoveAll(c=>c.NodeType==VTankNodeTypes.CharacterMainPackEmptySlotsGE);
  if(JsonSerializer.Serialize(before)!=JsonSerializer.Serialize(after))throw new Exception("Unintended profile change beyond the two currency slot conditions");
  foreach(string name in Currency)
  {var r=after.Rules.Single(r=>r.Name==name);if(!r.Enabled||r.Action!=VTankLootAction.Keep)throw new Exception("Currency is not Keep");}
  var notes=after.Rules.Single(r=>r.Name==Currency[0]);string pattern=notes.Conditions.Single(c=>c.NodeType==VTankNodeTypes.StringValueMatch).DataLines[0];
  foreach(string n in new[]{"Trade Note (250,000)","Trade Note (1,000,000)","Trade Note (100,000)"})if(!Regex.IsMatch(n,pattern))throw new Exception("Note denomination excluded: "+n);
  var doubloons=after.Rules.Single(r=>r.Name==Currency[1]);if(!doubloons.Conditions.Any(c=>c.NodeType==VTankNodeTypes.LongValKeyE&&c.DataLines[0]=="1004101"))throw new Exception("Doubloon template mismatch");
  Console.WriteLine($"PASS: {after.Rules.Count} loot rules verified with genuine Rynth LootSdk; only the two currency slot gates removed; notes and doubloons enabled Keep.");
 }
}
