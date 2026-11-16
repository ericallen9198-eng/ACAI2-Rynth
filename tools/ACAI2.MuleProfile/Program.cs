using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RynthCore.Loot;
using RynthCore.Loot.VTank;

if(args.Length!=2)throw new ArgumentException("Usage: input.utl output-directory (build with -p:LootSdkRoot=<genuine RynthSuite Shared/RynthCore.LootSdk>)");
const string name="Keyfer-Mule-NoSalvage";
const string bagGuard=@"^(?!.*\b(?:Salvaged|Salvage)\b).*$";
if(args[0]=="--verify")
{
    var check=VTankLootParser.Load(args[1]);
    if(check.Rules.Count==0||check.Rules.Any(r=>r.Action is not (VTankLootAction.Keep or VTankLootAction.KeepUpTo))||check.SalvageCombine?.Enabled!=false)throw new Exception("Unsafe mule actions/combine settings.");
    foreach(var rule in check.Rules.Where(r=>r.Enabled))
        if(!rule.Conditions.Any(c=>c.NodeType==1&&c.DataLines.SequenceEqual(new[]{bagGuard,"1"}))||!rule.Conditions.Any(c=>c.NodeType==13&&c.DataLines.SequenceEqual(new[]{"1073741824","1"})))throw new Exception("Bag exclusions missing.");
    if(JsonSerializer.Serialize(check.Rules)!=JsonSerializer.Serialize(VTankLootParser.LoadFromText(VTankLootWriter.Serialize(check)).Rules))throw new Exception("Reparse mismatch.");
    Console.WriteLine($"Verified {check.Rules.Count} rules with genuine RynthSuite LootSdk: Keep/KeepUpTo only; salvage bags excluded and combining off.");return;
}
var original=VTankLootParser.Load(args[0]);
if(original.Rules.Count==0)throw new InvalidDataException("Input profile contains no rules.");
var profile=VTankLootParser.LoadFromText(VTankLootWriter.Serialize(original));
int removed=profile.Rules.RemoveAll(r=>r.Action==VTankLootAction.Salvage);
if(profile.Rules.Any(r=>r.Action is not (VTankLootAction.Keep or VTankLootAction.KeepUpTo)))throw new InvalidDataException("Review non-Keep source actions before making a mule profile.");
VTankLootCondition Node(int type,params string[] data)=>new(type,data.Sum(x=>Encoding.UTF8.GetByteCount(x)+2).ToString(),data);
foreach(var rule in profile.Rules)
{
    // Editor metadata is deliberately disabled, not an item rule. Keep it intact.
    if(rule.Name.StartsWith("~~ Loot editor data",StringComparison.Ordinal)&&!rule.Enabled)continue;
    rule.Conditions.Add(Node(VTankNodeTypes.StringValueMatch,bagGuard,"1")); // name
    rule.Conditions.Add(Node(VTankNodeTypes.LongValKeyNE,"1073741824","1")); // ACE ItemType.TinkeringMaterial
}
profile.SalvageCombine=new(){Enabled=false,DefaultBands="",PerMaterial=new()};
var root=Path.GetFullPath(args[1]);
var loot=Path.Combine(root,"LootProfiles",name+".utl");Directory.CreateDirectory(Path.GetDirectoryName(loot)!);
VTankLootWriter.Save(profile,loot);
var roundtrip=VTankLootParser.Load(loot);
if(roundtrip.Rules.Count!=original.Rules.Count-removed||roundtrip.Rules.Any(r=>r.Action==VTankLootAction.Salvage)||roundtrip.SalvageCombine?.Enabled!=false)throw new Exception("No-salvage roundtrip failed.");
var oldKeeps=original.Rules.Where(r=>r.Action!=VTankLootAction.Salvage).ToArray();
for(int i=0;i<oldKeeps.Length;i++)
{
    var before=oldKeeps[i];var after=roundtrip.Rules[i];
    if(before.Name!=after.Name||before.Enabled!=after.Enabled||before.Action!=after.Action||before.KeepCount!=after.KeepCount||before.CustomExpression!=after.CustomExpression)throw new Exception("Keep criteria changed.");
    if(before.Conditions.Where((c,n)=>JsonSerializer.Serialize(c)!=JsonSerializer.Serialize(after.Conditions[n])).Any())throw new Exception("Existing condition changed.");
    if(after.Enabled&&(!after.Conditions.Any(c=>c.NodeType==1&&c.DataLines.SequenceEqual(new[]{bagGuard,"1"}))||!after.Conditions.Any(c=>c.NodeType==13&&c.DataLines.SequenceEqual(new[]{"1073741824","1"}))))throw new Exception("Enabled Keep rule lacks bag guards.");
}
foreach(var bag in new[]{"Salvaged Iron","Salvaged Steel (100)","Foolproof Salvage"})if(Regex.IsMatch(bag,bagGuard,RegexOptions.IgnoreCase))throw new Exception("Bag name not excluded.");
foreach(var keep in new[]{"Legendary Key","Trade Note (250,000)","Heavy Crossbow","Diamond"})if(!Regex.IsMatch(keep,bagGuard,RegexOptions.IgnoreCase))throw new Exception("Ordinary item name excluded.");
File.WriteAllText(Path.Combine(root,"audit.json"),JsonSerializer.Serialize(new{
    Source="User-supplied DWv_4.utl",SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
    SourceRules=original.Rules.Count,RemovedSalvageRules=removed,RemovedEnabledSalvageRules=original.Rules.Count(r=>r.Action==VTankLootAction.Salvage&&r.Enabled),
    KeptRules=roundtrip.Rules.Count,EnabledKeepRules=roundtrip.Rules.Count(r=>r.Enabled),SalvageActions=0,SalvageCombine=false,
    Validation="Genuine RynthSuite LootSdk parse/write/reparse; every source Keep rule/condition preserved in order, enabled Keep rules have name/item-type bag exclusions; regex fixture checks passed."
},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Verified: {original.Rules.Count} source rules, {removed} salvage rules removed, {roundtrip.Rules.Count} Keep/KeepUpTo rules retained, combining off.");
