using System.Text.RegularExpressions;
namespace ACAI2.Rynth.Combat.Core;

public sealed record FloatEvent(long Id,double Start,string Kind,string Label,string Target,long Amount,int Count,int Slot,bool Incoming=false,bool Critical=false,string AttackType="");
public sealed class FloatStream
{
    private readonly List<FloatEvent> events=[];
    private long serial;
    public void Add(double now,string kind,string label,string target="",long amount=0,bool incoming=false,bool critical=false,string attackType="")
    {
        events.RemoveAll(e=>now-e.Start>5);
        int index=events.FindLastIndex(e=>e.Kind==kind&&e.Amount==amount&&e.AttackType==attackType&&e.Label==label&&e.Incoming==incoming&&e.Critical==critical&&now>=e.Start&&now-e.Start<=.6);
        if(index>=0){var old=events[index];events[index]=old with{Count=old.Count+1,Target=old.Target==target?target:"Multiple targets"};return;}
        events.Add(new(++serial,now,kind,label,target,amount,1,(int)(serial%4),incoming,critical,attackType));if(events.Count>48)events.RemoveAt(0);
    }
    public static bool HasNumber(FloatEvent e)=>e.Amount>0||e.Kind=="KineticRelease"&&e.AttackType.Length>0;
    public static string Format(FloatEvent e)
    {
        string tag=e.Critical?"CRIT"+(e.Label.Length>0?" "+e.Label:""):e.Label;
        string label=HasNumber(e)?(e.Kind=="Healing"?"+":e.Kind=="Taken"?"-":"")+e.Amount.ToString("N0")+(tag.Length>0?" "+tag:""):tag;
        return label+(e.Count>1?" ×"+e.Count:"");
    }
    public static bool Regular(FloatEvent e)=>!e.Critical&&e.Amount>0&&e.Kind is not ("Healing" or "Ward" or "KineticRelease");
    public FloatEvent[] Read(double now)=>events.Where(e=>now>=e.Start&&now-e.Start<5).ToArray();
    public static (float X,float Y,float Alpha) Arc(FloatEvent e,double now,Appearance a)
    {
        float age=(float)Math.Max(0,now-e.Start),p=Math.Clamp(age/a.Lifetime,0,1);
        if(e.Kind=="Healing")return(0,-a.HealOffset-a.Rise*p-e.Slot*8,Math.Clamp((1-p)*4,0,1));
        bool left=e.Incoming;
        float bend=Math.Min(a.Curve,a.Gap-24),spacing=44-26*a.Overlap;
        return ((left?-1:1)*(a.Gap-bend*p*p),-a.Rise*p-e.Slot*spacing,Math.Clamp((1-p)*4,0,1));
    }
}
internal sealed class RateRing
{
    private readonly long[] seconds=Enumerable.Repeat(long.MinValue,61).ToArray(),outgoing=new long[61],incoming=new long[61];
    private double? first;
    public void Add(CombatEventRecord e,double now)
    {
        if(e.Result!="Damage"||e.Damage<=0)return;first??=now;long second=(long)now;int i=(int)(second%61);
        if(seconds[i]!=second){seconds[i]=second;outgoing[i]=incoming[i]=0;}
        if(e.Direction=="Outgoing")outgoing[i]+=e.Damage;else if(e.Direction=="Incoming")incoming[i]+=e.Damage;
    }
    public (double Dps,double Taken) Read(double now)
    {long o=0,n=0,s=(long)now;for(int i=0;i<61;i++)if(seconds[i]>s-60&&seconds[i]<=s){o+=outgoing[i];n+=incoming[i];}double d=first.HasValue?Math.Clamp(now-first.Value,1,60):1;return(o/d,n/d);}
}
public sealed class CombatEngine(string server,string character)
{
    public const string ParserVersion="0.1.6";
    private readonly CombatMessageParser parser=new();
    public Totals Session {get;private set;}=new();
    public List<RunRecord> Runs {get;}=[];
    public RunRecord? Current {get;private set;}
    public bool Inside {get;private set;}
    public bool EntryConfirmed {get;private set;}
    public long Revision {get;private set;}
    public FloatStream Floats {get;}=new();
    public EffectHistory Effects {get;}=new();
    public PackTracker Pack {get;}=new();
    public RecklessActivity Reckless {get;}=new();
    public KineticActivity Kinetic {get;}=new();
    public int BigHitMinimum {get;set;}=100;
    private readonly List<IncomingHit> recentHits=[];
    private readonly HashSet<uint> seen=[];
    private RateRing sessionRate=new(),runRate=new();
    private DateTime? tick;
    private DeathRecord? lastDeath;
    public static bool Native(string text,int type=0)=>text.Length<=4096&&type is not (2 or 3 or 4 or 17 or 18)&&!Regex.IsMatch(text,@"^(?:\[(?:General|Allegiance|Fellowship|Trade|Tell)[^\]]*\])|\b(?:says|say|tells you|thinks),\s*""",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(50));
    private static Regex Rx(string p)=>new(p,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
    private static readonly Regex Opener=Rx(@"^The Thread Gem opens a way into (?<name>.+?)\. It will bring you back \d+ more time\(s\) while the dungeon stands\.$");
    private static readonly Regex Realm=Rx(@"^\[REALM\] id=(\d+) inst=(\d+) eph=([01]) name=.+$");
    private static readonly Regex Progress=Rx(@"^Monsters killed (\d+)/(\d+)\.");
    private static readonly Regex Death=Rx(@"^.+! \(Killing blow: (?<damage>[\d,]+) damage\. You had (?<hp>[\d,]+)/(?<max>[\d,]+) health\.\)$");
    private static readonly Regex WardReady=Rx(@"^Your runic ward is fully inscribed, absorbing your next (?<n>[\d,]+) points of damage\.$");
    private static readonly Regex WardSpent=Rx(@"^Your runic ward (?:shatters|fades) after absorbing (?<n>[\d,]+) points of damage\.$");
    private static readonly Regex SimpleIncoming=Rx(@"^(?:Critical hit! )?(?<mob>.+?) (?:hits|strikes|shoots) you for (?<damage>[\d,]+) points? of (?<element>[A-Za-z]+) damage!$");
    private static readonly Regex UnknownIncoming=Rx(@"^(?:Critical hit! )?(?<mob>.+?) \w+ you for (?<damage>[\d,]+) points? of (?<element>[A-Za-z]+) damage!$");
    private static int Number(string s)=>int.TryParse(s.Replace(",",""),out int n)&&n>=0?n:0;
    public bool Observe(string raw,int type,DateTime utc,double now)
    {
        if(!Native(raw,type))return false;string text=Regex.Replace(raw.Trim(),@"^\d{2}:\d{2}:\d{2}\s+","");var open=Opener.Match(text);
        if(open.Success)
        {
            Pause(utc);Current=new(){Server=server,Character=character,ParserVersion=ParserVersion,Name=open.Groups["name"].Value,Opened=utc};Runs.Add(Current);if(Runs.Count>50)Runs.RemoveAt(0);Revision++;return true;
        }
        var realm=Realm.Match(text);
        if(realm.Success)
        {
            if(realm.Groups[3].Value=="0"){Pause(utc);return true;}
            string key=realm.Groups[1].Value+":"+realm.Groups[2].Value;
            var known=Runs.LastOrDefault(r=>r.Realm==key&&r.Ended==null);
            if(known!=null){if(Current!=known&&Current?.Entered==null)Runs.Remove(Current!);Current=known;}
            if(Current is {Ended:null} r&&realm.Groups[2].Value!="0")
            {if(r.Realm.Length>0&&r.Realm!=key)return false;bool fresh=r.Entered==null;r.Realm=key;Enter(utc);if(fresh){seen.Clear();runRate=new();}return true;}
            return false;
        }
        if(Current is {Ended:null} run)
        {
            if(text=="Your Thread Gem crumbles to dust."){Pause(utc);run.Ended=utc;Revision++;return true;}
            if(Inside&&text.StartsWith(run.Name+" is cleared.",StringComparison.Ordinal)){Tick(utc);run.Cleared??=utc;Revision++;return true;}
            var p=Progress.Match(text);if(Inside&&p.Success){int killed=Number(p.Groups[1].Value),total=Number(p.Groups[2].Value);if(total>0&&killed<=total&&killed>=(run.KilledProgress??0)){run.KilledProgress=killed;run.MonsterTotal=total;Revision++;}return true;}
        }
        var death=Death.Match(text);
        if(death.Success)
        {
            int damage=Number(death.Groups["damage"].Value),hp=Number(death.Groups["hp"].Value),max=Number(death.Groups["max"].Value);
            if(damage<=0||max<=0||hp>max)return false;
            string killer=DeathSource.Parse(text);var context=Effects.At(utc);
            var matched=recentHits.LastOrDefault(h=>h.Damage==damage&&h.Attacker==killer&&(utc-h.Utc).TotalSeconds is >=0 and <=1);
            var lethal=matched!=null?matched with{KillingBlow=true,HealthBefore=hp,Overkill=Math.Max(0,damage-hp),Evidence="Correlated with preceding hit by attacker and damage within one second"}:new IncomingHit(utc,killer,damage,"Unknown","Unknown","",false,false,true,hp,Math.Max(0,damage-hp),context,"Server death notice; attack type unconfirmed");
            if(lastDeath==null||(utc-lastDeath.Utc).TotalSeconds>=2||lastDeath.Message!=text){SaveHit(Session,lethal);if(Inside&&Current is {Cleared:null} fatalRun)SaveHit(fatalRun.Stats,lethal);}
            AddDeath(new(utc,killer,damage,hp,max,Math.Max(0,damage-hp),text,lethal.Context),now);return true;
        }
        if(text.EndsWith(" has expired.",StringComparison.Ordinal)&&EffectCatalog.LabelName(text[..^13]) is {} expired)Effects.Notice(utc,"Expired "+expired);
        if(TryIncomingEffect(text,out string effectKind,out string effectLabel))
        {
            Effects.Notice(utc,effectLabel);
            Session.IncomingEffects[effectLabel]=Session.IncomingEffects.GetValueOrDefault(effectLabel)+1;
            if(Inside&&Current is {Cleared:null} affected){affected.Stats.IncomingEffects[effectLabel]=affected.Stats.IncomingEffects.GetValueOrDefault(effectLabel)+1;Revision++;}
            Floats.Add(now,effectKind,effectLabel,incoming:true);return true;
        }
        var ward=WardReady.Match(text);if(ward.Success){Utility("Ward",Number(ward.Groups["n"].Value),"WARD READY",now,true);return true;}
        ward=WardSpent.Match(text);if(ward.Success){Utility("Ward",Number(ward.Groups["n"].Value),"ABSORBED",now,false);return true;}
        if(text=="You catch your second wind."){Utility("Second Wind",0,"SECOND WIND",now);return true;}
        if(text is "Your frenzy subsides." or "Your killer instinct fades."){Utility("Class status",0,text.TrimEnd('.').ToUpperInvariant(),now);return true;}
        if(!parser.TryParse(text,character,out var e))
        {
            string prefix=Regex.Match(text,"^"+CombatMessageParser.Prefix).Value;string body=text[prefix.Length..];
            var hit=SimpleIncoming.Match(body);bool physical=hit.Success;if(!physical)hit=UnknownIncoming.Match(body);if(!hit.Success)return false;
            e=new(){Direction="Incoming",Result="Damage",AttackType=physical?"Melee/Missile":"Unknown",Component="Defense",Mob=hit.Groups["mob"].Value,Damage=Number(hit.Groups["damage"].Value),Element=hit.Groups["element"].Value,Critical=prefix.Contains("Critical hit!",StringComparison.Ordinal),RecklessTagged=Regex.IsMatch(prefix,@"Reckless(?:ness)?!")};
        }
        e.Raw=""; // Only explicit server death notices survive as message text.
        if(e.Result=="Death"){if(lastDeath==null||(utc-lastDeath.Utc).TotalSeconds>2)AddDeath(new(utc,"Unspecified killer",null,null,null,null,text),now);return true;}
        if(!Kinetic.Accept(e,now))return true;
        Pack.Observe(e,now);Reckless.Observe(e,now);
        Add(Session,e);sessionRate.Add(e,now);
        if(Inside&&Current is {Cleared:null} active){Add(active.Stats,e);runRate.Add(e,now);Revision++;}
        if(e.Result=="Damage"&&e.Direction=="Incoming")
        {
            string spell=e.AttackType=="Magic"?Regex.Match(text,@"\bwith (?<spell>[^.!]+)").Groups["spell"].Value:"";
            var hit=new IncomingHit(utc,e.Mob.Length>0?e.Mob:"Unattributed periodic damage",e.Damage,e.AttackType,e.Element,spell,e.Critical,false,false,null,null,Effects.At(utc));
            recentHits.Add(hit);if(recentHits.Count>16)recentHits.RemoveAt(0);
            if(e.Damage>=BigHitMinimum){SaveHit(Session,hit);if(Inside&&Current is {Cleared:null} hitRun)SaveHit(hitRun.Stats,hit);}
        }
        bool kineticHit=Kinetic.ClaimHit(e,now);
        string kind=kineticHit?"KineticRelease":e.Direction=="Healing"?"Healing":e.Direction=="Incoming"?e.Result=="Damage"?"Taken":e.Result:e.Component!="Direct"?e.Component:e.SneakAttack?"Sneak":e.Critical?"Critical":e.Element;
        if(e.Result=="Damage"&&(e.Damage>0||kineticHit))Floats.Add(now,kind,kineticHit?"KINETIC RELEASE":e.Direction=="Incoming"?e.AttackType=="Periodic"?"DOT":"HIT":e.Component=="Direct"?e.SneakAttack?"SNEAK":"":e.Component.ToUpperInvariant(),e.Mob,e.Damage,e.Direction=="Incoming",e.Critical,e.AttackType);
        else if(e.Result=="Healing"&&e.Amount>0)Floats.Add(now,"Healing",e.Component=="Summons"?"PET HEAL":"HEAL",e.Recipient,e.Amount,incoming:true,critical:e.Critical);
        else if(e.Result is "Evade" or "Resist"&&e.Direction=="Incoming")Floats.Add(now,kind,e.Result.ToUpperInvariant(),e.Mob,0,true);
        else if(e.Result=="Block")Floats.Add(now,"Ward","BLOCK",e.Mob,incoming:true);
        else if(e.Result is "Evade" or "Resist"&&e.Direction=="Outgoing")Floats.Add(now,"Utility",e.Result=="Evade"?"EVADED":"RESISTED",e.Mob);
        else if(e.Result=="Kill")Floats.Add(now,"Kill","DOWN",e.Mob);
        else if(e.Component=="Kinetic"){if(e.Result=="ChargeReady")Floats.Add(now,"KineticReady","KINETIC READY");else if(e.Result=="ChargeExpired")Floats.Add(now,"Utility","KINETIC FADED");}
        else if(e.Direction=="Utility")Floats.Add(now,"Utility",e.Ability.Length>0?e.Ability:e.Result,e.Mob);
        return true;
    }
    private static bool TryIncomingEffect(string text,out string kind,out string label)
    {
        kind=label="";
        var hit=Regex.Match(text,@"^.+? casts? (?<spell>.+?) on you(?:, refreshing .+)?[.!]?$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
        if(!hit.Success||text.Contains("surpassed",StringComparison.OrdinalIgnoreCase))return false;
        string spell=hit.Groups["spell"].Value;
        if(Regex.IsMatch(spell,@"\bImperil Other(?: [IVX]+)?$")){kind="Imperil";label="IMPERIL";return true;}
        var vuln=Regex.Match(spell,@"\b(Lightning|Electrical|Cold|Frost|Fire|Flame|Acid|Nether|Bludgeoning|Slashing|Piercing) Vulnerability Other(?: [IVX]+)?$",RegexOptions.IgnoreCase);
        if(!vuln.Success)return false;
        (kind,label)=vuln.Groups[1].Value.ToLowerInvariant() switch
        {"lightning" or "electrical"=>("VulnLightning","LIGHT VULN"),"cold" or "frost"=>("VulnFrost","FROST VULN"),"fire" or "flame"=>("VulnFire","FIRE VULN"),"acid"=>("VulnAcid","ACID VULN"),"nether"=>("VulnNether","NETHER VULN"),"bludgeoning"=>("VulnBludgeon","BLUDGE VULN"),"slashing"=>("VulnSlash","SLASH VULN"),_=>("VulnPierce","PIERCE VULN")};return true;
    }
    private static void Add(Totals t,CombatEventRecord e)
    {
        MobStats? m=e.Mob.Length>0?t.Mob(e.Mob):null;
        if(e.Result=="Damage"&&e.Direction=="Outgoing")
        {
            t.Damage+=e.Damage;if(m!=null)m.Dealt+=e.Damage;t.Components[e.Component]=t.Components.GetValueOrDefault(e.Component)+e.Damage;
            if(CombatComponents.IsDirect(e)&&e.AttackType=="Melee/Missile"){t.DirectHits++;if(e.RecklessTagged)t.RecklessOutgoing.Add(e.Damage);if(e.Critical)t.Crits++;if(e.SneakAttack){t.Sneaks++;t.SneakDamage+=e.Damage;}}
        }
        else if(e.Result=="Damage"&&e.Direction=="Incoming")
        {
            t.Taken+=e.Damage;
            if(e.AttackType=="Periodic"){t.PeriodicTaken+=e.Damage;t.PeriodicTicks++;t.PeriodicHigh=Math.Max(t.PeriodicHigh,e.Damage);}
            else if(e.AttackType=="Magic"){t.MagicDamage+=e.Damage;t.MagicHits++;t.MagicHigh=Math.Max(t.MagicHigh,e.Damage);if(m!=null){m.MagicDamage+=e.Damage;m.MagicHits++;m.MagicHigh=Math.Max(m.MagicHigh,e.Damage);}}
            else if(e.AttackType=="Melee/Missile") {t.PhysicalDamage+=e.Damage;t.PhysicalHits++;t.PhysicalHigh=Math.Max(t.PhysicalHigh,e.Damage);if(m!=null){m.PhysicalDamage+=e.Damage;m.PhysicalHits++;m.PhysicalHigh=Math.Max(m.PhysicalHigh,e.Damage);}}
            else {t.UnclassifiedDamage+=e.Damage;t.UnclassifiedHits++;t.UnclassifiedHigh=Math.Max(t.UnclassifiedHigh,e.Damage);if(m!=null){m.UnclassifiedDamage+=e.Damage;m.UnclassifiedHits++;m.UnclassifiedHigh=Math.Max(m.UnclassifiedHigh,e.Damage);}}
        }
        else if(e.Result=="Healing"){if(e.Recipient==""||e.Component!="Summons")t.Healing+=e.Amount;string source=e.Component=="PeriodicHeal"?"Periodic healing (source unspecified)":e.Component=="Recovery"?"Healing kit":e.Ability.Length>0?e.Ability:e.Component;t.HealingSources[source]=t.HealingSources.GetValueOrDefault(source)+e.Amount;}
        else if(e.Direction=="Incoming"&&e.Result=="Evade"){t.Evades++;if(m!=null)m.Evades++;}
        else if(e.Direction=="Incoming"&&e.Result=="Resist"){t.Resists++;if(m!=null)m.Resists++;}
        else if(e.Result=="Block")t.Blocks++;
        else if(e.Result=="Kill"){t.Kills++;if(m!=null)m.Kills++;}
        else if(e.Component=="Kinetic")
        {if(e.Result=="ChargeReady")t.KineticReady++;else if(e.Result=="ChargeReleased")t.KineticReleases++;else if(e.Result=="ChargeExpired")t.KineticExpired++;}
        else if(e.Direction=="Utility")t.Procs[e.Component]=t.Procs.GetValueOrDefault(e.Component)+1;
    }
    private static void SaveHit(Totals totals,IncomingHit hit)
    {
        int found=totals.BigHits.FindIndex(h=>h.Utc==hit.Utc&&h.Attacker==hit.Attacker&&h.Damage==hit.Damage);
        if(found>=0){if(hit.KillingBlow)totals.BigHits[found]=hit;return;}
        totals.BigHits.Add(hit);totals.BigHits=totals.BigHits.OrderByDescending(h=>h.Damage).ThenByDescending(h=>h.KillingBlow).ThenByDescending(h=>h.Utc).Take(24).ToList();
    }
    private void Utility(string kind,int n,string label,double now,bool ready=false)
    {
        void AddTo(Totals t){t.Procs[kind]=t.Procs.GetValueOrDefault(kind)+1;if(kind=="Ward"){if(ready)t.WardCapacity=n;else{t.WardAbsorbed+=n;t.WardCapacity=0;}}}
        AddTo(Session);if(Inside&&Current is {Cleared:null} r){AddTo(r.Stats);Revision++;}Floats.Add(now,kind=="Ward"?"Ward":"Utility",label,amount:n,incoming:true);
    }
    private void AddDeath(DeathRecord death,double now)
    {
        if(lastDeath!=null&&(death.Utc-lastDeath.Utc).TotalSeconds<2&&lastDeath.Message==death.Message)return;
        void Save(Totals t){if(t.Deaths.Count>0&&(death.Utc-t.Deaths[^1].Utc).TotalSeconds<2&&t.Deaths[^1].Blow==null)t.Deaths.RemoveAt(t.Deaths.Count-1);else t.DeathCount++;t.Deaths.Add(death);if(t.Deaths.Count>64){t.Deaths.RemoveAt(0);t.Truncated=true;}}
        Save(Session);if(Inside&&Current is {Cleared:null} r){Save(r.Stats);Revision++;}
        Reckless.Stop();
        lastDeath=death;Floats.Add(now,"Death","DOWN"+(death.Overkill.HasValue?" · "+death.Overkill+" OVERKILL":""),incoming:true);Tick(death.Utc);
    }
    public void Enter(DateTime utc)
    {if(Current is not {Ended:null} r)return;Inside=true;EntryConfirmed=true;tick=utc;r.Entered??=utc;Revision++;}
    public void Pause(DateTime utc,bool transient=false){Pack.Finish();Reckless.Stop();Kinetic.Stop();Tick(utc);Inside=false;if(!transient)EntryConfirmed=false;tick=null;}
    public void Tick(DateTime utc)
    {
        if(Inside&&Current is {Cleared:null} r&&tick.HasValue){double dt=(utc-tick.Value).TotalSeconds;if(dt is >=0 and <=2)r.Observed+=dt;else r.Gaps=true;Revision++;}
        tick=utc;
    }
    public void Advance(double now){if(Kinetic.UnmatchedRelease(now))Floats.Add(now,"KineticRelease","KINETIC RELEASE");}
    public void Gap(){if(Current!=null){Current.Gaps=true;Revision++;}}
    public void Seen(uint id,string name)
    {if(!Inside||Current is not {Cleared:null} r||seen.Contains(id))return;if(seen.Count>=4096){r.Stats.Truncated=true;return;}seen.Add(id);r.Stats.Mob(name).Seen++;Session.Mob(name).Seen++;Revision++;}
    public (double Dps,double Taken) Rates(double now,bool run)=>run?runRate.Read(now):sessionRate.Read(now);
    public void RestoreRuns(IEnumerable<RunRecord> runs)
    {
        foreach(var r in runs.Take(50).OrderBy(r=>r.Opened))if(r.Server==server&&r.Character==character&&!Runs.Any(x=>x.Id==r.Id)){if(r.Ended==null)r.Gaps=true;Runs.Add(r);}
        Current??=Runs.Where(r=>r.Ended==null).OrderByDescending(r=>r.Opened).FirstOrDefault();
        Revision++;
    }
    public void NewSession(){Session=new();sessionRate=new();lastDeath=null;recentHits.Clear();Pack.Reset();Reckless.Reset();Kinetic.Reset();}
}
