using System.Text.Json;
using System.Text.RegularExpressions;
namespace ACAI2.Rynth.Combat.Core;

public sealed record ActiveEffect(uint SpellId,DateTime? ExpiresUtc);
public sealed record HitContext(DateTime? SampleUtc,double? AgeMilliseconds,bool Available,bool? Imperiled,string[] Vulnerabilities,uint[] UnknownSpellIds,string[] ChangesSinceSample,string Status)
{
    public static HitContext Unknown(string reason)=>new(null,null,false,null,[],[],[],reason);
}
public sealed record IncomingHit(DateTime Utc,string Attacker,int Damage,string Delivery,string Element,string Spell,bool Critical,bool RecklessTagged,bool KillingBlow,int? HealthBefore,int? Overkill,HitContext Context,string Evidence="Parsed damage message");
public static class EffectCatalog
{
    private sealed class Data{public uint[] KnownIds {get;set;}=[];public Dictionary<uint,string> Effects {get;set;}=[];}
    private static readonly Data Catalog=Load();
    private static readonly HashSet<uint> Known=Catalog.KnownIds.ToHashSet();
    private static Data Load(){using var stream=typeof(EffectCatalog).Assembly.GetManifestResourceStream("ACAI2.Rynth.Combat.Core.Data.EffectCatalog.json")??throw new InvalidDataException("Effect metadata missing");return JsonSerializer.Deserialize<Data>(stream)??throw new InvalidDataException("Effect metadata unreadable");}
    public static string? Name(uint id)=>Catalog.Effects.GetValueOrDefault(id);
    public static bool IsKnown(uint id)=>Known.Contains(id);
    public static string? Label(uint id)=>Catalog.Effects.TryGetValue(id,out string? name)?LabelName(name):null;
    public static string? LabelName(string name)
    {
        if(Regex.IsMatch(name,@"\bImperil Other(?: [IVX]+)?$",RegexOptions.IgnoreCase))return "IMPERIL";
        var match=Regex.Match(name,@"\b(Lightning|Electrical|Cold|Frost|Fire|Flame|Acid|Nether|Bludgeoning|Slashing|Piercing) Vulnerability Other(?: [IVX]+)?$",RegexOptions.IgnoreCase);
        return !match.Success?null:match.Groups[1].Value.ToLowerInvariant() switch{"lightning" or "electrical"=>"LIGHT VULN","cold" or "frost"=>"FROST VULN","fire" or "flame"=>"FIRE VULN","acid"=>"ACID VULN","nether"=>"NETHER VULN","bludgeoning"=>"BLUDGE VULN","slashing"=>"SLASH VULN",_=>"PIERCE VULN"};
    }
}
public sealed class EffectHistory
{
    private sealed record Sample(DateTime Utc,bool Available,ActiveEffect[] Effects,string Status);
    private readonly List<Sample> samples=[];
    private DateTime? lost;
    private sealed record Delta(DateTime Utc,uint Id,bool Added,double Duration);
    private readonly List<Delta> deltas=[];
    private readonly List<(DateTime Utc,string Label)> changes=[];
    public void SampleAt(DateTime utc,bool available,ActiveEffect[] effects,string status="Native enchantment sample")
    {samples.Add(new(utc,available,effects.Take(512).ToArray(),status));if(samples.Count>32)samples.RemoveAt(0);}
    public void Lost(DateTime utc){if(!lost.HasValue||utc>lost.Value)lost=utc;}
    public void Changed(DateTime utc,uint id,bool added,double duration){deltas.Add(new(utc,id,added,duration));if(deltas.Count>256){Lost(deltas[0].Utc);deltas.RemoveAt(0);}}
    public void Notice(DateTime utc,string label){changes.Add((utc,label));if(changes.Count>32){Lost(changes[0].Utc);changes.RemoveAt(0);}}
    public HitContext At(DateTime utc)
    {
        var s=samples.LastOrDefault(x=>x.Utc<=utc);
        if(s==null)return HitContext.Unknown("No pre-hit enchantment sample");
        if(lost.HasValue&&s.Utc<=lost.Value&&utc>=lost.Value)return HitContext.Unknown("Enchantment/event observation gap before this hit");
        double age=(utc-s.Utc).TotalMilliseconds;
        if(!s.Available||age>500)return HitContext.Unknown(!s.Available?s.Status:"Pre-hit enchantment sample is stale");
        var activeList=s.Effects.Where(e=>!e.ExpiresUtc.HasValue||e.ExpiresUtc>utc).ToList();
        var applied=deltas.Where(d=>d.Utc>s.Utc&&d.Utc<=utc).OrderBy(d=>d.Utc).ToArray();
        var possiblyLayered=new HashSet<uint>();
        foreach(var d in applied)
        {
            int existing=activeList.Count(e=>e.SpellId==d.Id);
            if(existing>1||!d.Added&&possiblyLayered.Contains(d.Id))return HitContext.Unknown("Layered enchantment changed after sample; a fresh snapshot is needed");
            if(d.Added&&existing>0)possiblyLayered.Add(d.Id);
            activeList.RemoveAll(e=>e.SpellId==d.Id);
            if(d.Added){DateTime? expiry=d.Duration>0&&d.Duration<31536000?d.Utc.AddSeconds(d.Duration):null;if(expiry==null||expiry>utc)activeList.Add(new(d.Id,expiry));}
        }
        var active=activeList.ToArray();
        var labels=active.Select(e=>EffectCatalog.Label(e.SpellId)).Where(x=>x!=null).Distinct().Cast<string>().ToArray();
        var unknown=active.Where(e=>!EffectCatalog.IsKnown(e.SpellId)).Select(e=>e.SpellId).Distinct().ToArray();
        var changed=changes.Where(c=>c.Utc>s.Utc&&c.Utc<=utc).Where(c=>!applied.Any(d=>EffectCatalog.Label(d.Id)==c.Label.Replace("Expired ",""))).Select(c=>c.Label).Distinct().Take(8).ToArray();
        bool? imp=labels.Contains("IMPERIL")?true:unknown.Length==0?false:null;
        if(changed.Any(c=>c.Contains("IMPERIL",StringComparison.Ordinal)))imp=null;
        return new(s.Utc,Math.Round(age,1),true,imp,labels.Where(l=>l!="IMPERIL").ToArray(),unknown.Take(24).ToArray(),changed,changed.Length>0?"Effects changed after the pre-hit sample; state uncertain":unknown.Length>0?"Pre-hit sample; unclassified spell IDs present":applied.Length>0?"Pre-hit native sample plus confirmed enchantment events":"Pre-hit native sample (last observed state, not an exact server-tick guarantee)");
    }
}
public static class DeathSource
{
    public static string Parse(string text)
    {
        text=Regex.Replace(text,"^"+CombatMessageParser.Prefix,"");
        string[] patterns=[@"^(?<mob>.+?)'s attack stops you cold!",@"^You are utterly destroyed by (?<mob>.+?)'s attack!",@"^Your body is shattered by (?<mob>.+?)'s attack!",@"^(?<mob>.+?) smites you mightily!",@"^(?<mob>.+?) reduces you to a sizzling, oozing mass!",@"^You catch (?<mob>.+?)'s attack, with dire consequences!",@"^You suffer a frozen fate at the hands of (?<mob>.+?)!"];
        foreach(string p in patterns){var m=Regex.Match(text,p,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));if(m.Success)return m.Groups["mob"].Value;}
        return "Unspecified killer";
    }
}
