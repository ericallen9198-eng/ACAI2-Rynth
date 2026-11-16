using ACAI2.Protocol;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACAI2.Rynth.Core.Keyfer;

public sealed record KeptLoot(DateTime Utc,string Name,string Rule,double? Attack,double? MeleeDefense,double? ProfileAttackEstimate,double? ProfileMeleeDefenseEstimate,string[] Cantrips,string? Chest,bool ChestConfirmed);
public sealed record OwnLootSnapshot(string Server,string Character,string InstanceId,DateTime CapturedUtc,DateTime SessionStartedUtc,string Chest,bool Running,DateTime? ActivityUtc,int? ReportedCompletedCycles,int ObservedCompletedCycles,int ObservedNoMatchCycles,int ConfirmedKeepPickups,KeptLoot[] RecentKeeps,KeptLoot[] NotableKeeps,string Evidence);

// Own-client observations only. Never store ordinary chat, rule previews or salvage items.
public sealed class OwnLootLedger
{
    private static Regex Rx(string p)=>new(p,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
    private static readonly Regex Complete=Rx(@"^\[RynthAi\] Chest looter: looted (?<chest>[^\r\n]{1,120}) \((?<n>\d{1,8}) so far\)\.$");
    private static readonly Regex NoMatch=Rx(@"^\[RynthAi\] Nothing in (?<chest>[^\r\n]{1,120}) matches the loot profile\.$");
    private static readonly Regex Pickup=Rx(@"^\[RynthAi\] Looted \[(?:Keep|KeepUpTo)\] (?<name>[^\r\n]{1,160}?)(?: \(rule ""(?<rule>[^""\r\n]{1,180})""\))?(?:: (?<details>[^\r\n]{0,3200}))?$");
    private readonly string server,character,instance,file;
    private readonly DateTime started;
    private readonly List<KeptLoot> recent=[],notable=[];
    private readonly Dictionary<uint,(string Name,string Chest,DateTime Utc)> receipts=[];
    private string chest="";private bool running;private DateTime? activity;private int? reported;
    private int cycles,noMatches,keeps;private bool pendingNoMatch;private long revision,savedRevision;
    public OwnLootLedger(string server,string character,string instance,string root,DateTime now)
    {
        this.server=server;this.character=character;this.instance=instance;started=now;
        file=Path.Combine(root,"KeyferLoot",IdentityPaths.SafeSegment(server),IdentityPaths.SafeSegment(character),"notable-loot.json");
        try
        {
            if(File.Exists(file)&&new FileInfo(file).Length<=49152)
            {
                var old=JsonSerializer.Deserialize<KeptLoot[]>(File.ReadAllText(file));
                if(old!=null)notable.AddRange(old.Where(x=>ValidItem(x)&&now-x.Utc<=TimeSpan.FromDays(30)&&x.Utc<=now).TakeLast(24));
            }
        }catch{ /* Bad history must not break conversation or be overwritten without new evidence. */ }
    }
    public static bool ValidItem(KeptLoot x)=>x!=null&&x.Name is{Length:>0 and <=160}&&x.Rule is{Length:<=180}&&x.Cantrips is {Length:<=8}&&x.Cantrips.All(s=>s is{Length:<=100}&&!s.Any(char.IsControl))&&!x.Name.Any(char.IsControl)&&!x.Rule.Any(char.IsControl);
    public void NativeReceipt(uint id,string name,string fromChest,DateTime now)
    {
        if(id==0||name.Length is <1 or >160||fromChest.Length is <1 or >120)return;
        foreach(var k in receipts.Where(x=>now-x.Value.Utc>TimeSpan.FromSeconds(15)).Select(x=>x.Key).ToArray())receipts.Remove(k);
        if(receipts.Count<128)receipts.TryAdd(id,(name,fromChest,now));
        // Chat may precede the native ownership update. Enrich, never add a second pickup.
        var candidates=recent.Select((x,i)=>(x,i)).Where(p=>!p.x.ChestConfirmed&&p.x.Name==name&&Math.Abs((now-p.x.Utc).TotalSeconds)<=5).ToArray();
        int match=candidates.Length==1?candidates[0].i:-1;
        if(match>=0)
        {
            var old=recent[match];var updated=old with{Chest=fromChest,ChestConfirmed=true};recent[match]=updated;
            int n=notable.FindLastIndex(x=>x==old);if(n>=0){notable[n]=updated;revision++;}receipts.Remove(id);
        }
    }
    private void SetCount(int n,DateTime now,bool isCompletion)
    {
        if(reported.HasValue&&n>=reported.Value){cycles+=n-reported.Value;if(n>reported.Value)activity=now;}
        else if(isCompletion)cycles++;
        reported=n;
    }
    public void Poll(string? json,DateTime now)
    {
        if(string.IsNullOrWhiteSpace(json)||json.Length>16384)return;
        try
        {
            using var doc=JsonDocument.Parse(json);var c=doc.RootElement.GetProperty("chest");
            string name=c.GetProperty("name").GetString()??"";if(name.Length>120||name.Any(char.IsControl))return;
            chest=name;running=c.GetProperty("running").GetBoolean();activity=now;
            int count=c.GetProperty("looted").GetInt32();if(count>=0)SetCount(count,now,false);
        }catch(JsonException){}catch(InvalidOperationException){}catch(KeyNotFoundException){}
    }
    public int KeepCount=>keeps;
    public KeptLoot? LastKeep=>recent.LastOrDefault();
    public bool Observe(string raw,DateTime now)
    {
        if(raw.Length>4096||ACAI2.Protocol.GameChat.Parse(raw)!=null)return false;
        var m=NoMatch.Match(raw);if(m.Success){chest=m.Groups["chest"].Value;pendingNoMatch=true;activity=now;return true;}
        m=Complete.Match(raw);if(m.Success)
        {
            chest=m.Groups["chest"].Value;SetCount(int.Parse(m.Groups["n"].Value,CultureInfo.InvariantCulture),now,true);
            if(pendingNoMatch){noMatches++;pendingNoMatch=false;}activity=now;return true;
        }
        m=Pickup.Match(raw);if(!m.Success)return false;
        string name=m.Groups["name"].Value,rule=m.Groups["rule"].Value,details=m.Groups["details"].Value;
        double? Stat(string suffix){var s=Regex.Match(details,@"(?:^|, )(?<n>[+-]?\d+(?:\.\d+)?)%"+suffix+@"(?:,|$)",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));return s.Success?double.Parse(s.Groups["n"].Value,CultureInfo.InvariantCulture):null;}
        double? attack=Stat("a"),defense=Stat("md");
        var spells=details.Split(", ").Where(s=>Regex.IsMatch(s,@"^(?:Minor|Major|Epic|Legendary) ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50))).Where(s=>s.Length<=100).Take(8).ToArray();
        double Bonus(string stem)=>spells.Contains("Legendary "+stem)?9:spells.Contains("Epic "+stem)?7:spells.Contains("Major "+stem)?5:spells.Contains("Minor "+stem)?3:0;
        double? adjustedAttack=attack+Bonus("Heart Thirst"),adjustedDefense=defense+Bonus("Defender");
        var matches=receipts.Where(x=>x.Value.Name==name&&Math.Abs((now-x.Value.Utc).TotalSeconds)<=5).ToArray();
        var receipt=matches.Length==1?matches[0]:default;
        bool confirmed=receipt.Key!=0;if(confirmed)receipts.Remove(receipt.Key);
        var item=new KeptLoot(now,name,rule,attack,defense,adjustedAttack,adjustedDefense,spells,confirmed?receipt.Value.Chest:null,confirmed);
        keeps++;pendingNoMatch=false;recent.Add(item);if(recent.Count>12)recent.RemoveAt(0);
        if(adjustedAttack>=25||adjustedDefense>=25||rule.StartsWith("(MOD25)",StringComparison.Ordinal))
        {notable.Add(item);if(notable.Count>24)notable.RemoveAt(0);revision++;}
        return true;
    }
    public OwnLootSnapshot Snapshot(DateTime now)=>new(server,character,instance,now,started,chest,running&&activity.HasValue&&now-activity.Value<TimeSpan.FromSeconds(5),activity,reported,cycles,noMatches,keeps,recent.ToArray(),notable.Where(x=>now-x.Utc<=TimeSpan.FromDays(30)).ToArray(),"RynthAi Item Tool read-only snapshot and own Looted [Keep] confirmations. Completed cycles are NOT item counts. No-match means nothing satisfied the loaded profile, NOT an empty chest. Native chest-to-inventory correlation alone sets ChestConfirmed. Profile modifier estimates use the printed base plus only known item cantrips; displayed base modifiers remain separate. Historical notables are dated and do not prove current ownership.");
    public bool Save()
    {
        if(savedRevision==revision)return true;
        try
        {
            string json=JsonSerializer.Serialize(notable.ToArray());
            while(System.Text.Encoding.UTF8.GetByteCount(json)>49152&&notable.Count>1){notable.RemoveAt(0);json=JsonSerializer.Serialize(notable.ToArray());}
            if(System.Text.Encoding.UTF8.GetByteCount(json)>49152)return false;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file+".tmp",json);File.Move(file+".tmp",file,true);savedRevision=revision;return true;
        }catch{return false;}
    }
}
