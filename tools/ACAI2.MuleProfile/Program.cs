using System.Security.Cryptography;
using System.Text.Json;
using RynthCore.Loot;
using RynthCore.Loot.VTank;

// Never add eligibility conditions to Keep rules. A mule copy differs ONLY in salvage handling.
if(args.Length is <2 or >3)throw new ArgumentException("Usage: input.utl output-directory | --verify file.utl | --compare original.utl mule.utl");
const string name="Keyfer-Mule-NoSalvage";
void Verify(VTankLootProfile p)
{
    if(p.Rules.Count==0||p.Rules.Any(r=>r.Action==VTankLootAction.Salvage)||p.SalvageCombine?.Enabled!=false)throw new Exception("Salvage action/combining remains, or empty profile.");
    if(JsonSerializer.Serialize(p.Rules)!=JsonSerializer.Serialize(VTankLootParser.LoadFromText(VTankLootWriter.Serialize(p)).Rules))throw new Exception("Reparse mismatch.");
}
void Compare(VTankLootProfile before,VTankLootProfile after)
{
    var expected=before.Rules.Where(r=>r.Action!=VTankLootAction.Salvage).ToArray();
    if(JsonSerializer.Serialize(expected)!=JsonSerializer.Serialize(after.Rules))throw new Exception("A non-salvage rule changed: name, conditions, enabled state, action, priority, count, expression or order differs.");
    if(before.SalvageCombine!=null)
    {
        var expectedCombine=JsonSerializer.Deserialize<SalvageCombineSettings>(JsonSerializer.Serialize(before.SalvageCombine))!;
        expectedCombine.Enabled=false;
        if(JsonSerializer.Serialize(expectedCombine)!=JsonSerializer.Serialize(after.SalvageCombine))throw new Exception("Combine metadata changed beyond Enabled=false.");
    }
    Verify(after);
}
if(args[0]=="--verify")
{
    var check=VTankLootParser.Load(args[1]);Verify(check);
    Console.WriteLine($"Verified {check.Rules.Count} rules: salvage actions absent and combining off. Original-file comparison is also required before publication.");return;
}
if(args[0]=="--compare")
{
    if(args.Length!=3)throw new ArgumentException("--compare requires original and mule UTL paths.");
    Compare(VTankLootParser.Load(args[1]),VTankLootParser.Load(args[2]));
    Console.WriteLine("Verified exact semantic equality of EVERY non-salvage rule, including all conditions and ordering. Only salvage handling differs.");return;
}
var original=VTankLootParser.Load(args[0]);
if(original.Rules.Count==0)throw new InvalidDataException("Input profile contains no rules.");
var profile=VTankLootParser.LoadFromText(VTankLootWriter.Serialize(original));
int removed=profile.Rules.RemoveAll(r=>r.Action==VTankLootAction.Salvage);
profile.SalvageCombine??=new();profile.SalvageCombine.Enabled=false;
var root=Path.GetFullPath(args[1]);
var loot=Path.Combine(root,"LootProfiles",name+".utl");Directory.CreateDirectory(Path.GetDirectoryName(loot)!);
VTankLootWriter.Save(profile,loot);
Compare(original,VTankLootParser.Load(loot));
File.WriteAllText(Path.Combine(root,"audit.json"),JsonSerializer.Serialize(new{
    SourceFile=Path.GetFileName(args[0]),SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
    SourceRules=original.Rules.Count,RemovedSalvageRules=removed,RemovedEnabledSalvageRules=original.Rules.Count(r=>r.Action==VTankLootAction.Salvage&&r.Enabled),
    RetainedRules=profile.Rules.Count,SalvageActions=0,SalvageCombine=false,
    Validation="Genuine RynthSuite LootSdk parse/write/reparse; exact semantic equality of every non-salvage rule, ALL conditions, action, priority, count, enabled state, expression and ordering. No added Keep filters."
},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Verified: {removed} salvage rules removed, {profile.Rules.Count} other rules preserved EXACTLY; combining off. No added Keep filters.");
