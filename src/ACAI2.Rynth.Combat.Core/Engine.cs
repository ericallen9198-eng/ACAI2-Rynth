using System.Text.RegularExpressions;
namespace ACAI2.Rynth.Combat.Core;

public sealed record FloatEvent(long Id,double Start,string Kind,string Label,string Target,long Amount,int Count,int Slot,bool Incoming=false,bool Critical=false);
public sealed class FloatStream
{
    private readonly List<FloatEvent> events=[];
    private long serial;
    public void Add(double now,string kind,string label,string target="",long amount=0,bool incoming=false,bool critical=false)
    {
        events.RemoveAll(e=>now-e.Start>5);
        int index=events.FindLastIndex(e=>e.Kind==kind&&e.Target==target&&e.Label==label&&e.Incoming==incoming&&e.Critical==critical&&now>=e.Start&&now-e.Start<.15);
        if(index>=0){var old=events[index];events[index]=old with{Amount=old.Amount+amount,Count=old.Count+1};return;}
        events.Add(new(++serial,now,kind,label,target,amount,1,(int)(serial%4),incoming,critical));if(events.Count>48)events.RemoveAt(0);
    }
    public FloatEvent[] Read(double now)=>events.Where(e=>now>=e.Start&&now-e.Start<5).ToArray();
    public static (float X,float Y,float Alpha) Arc(FloatEvent e,double now,Appearance a)
    {
        float age=(float)Math.Max(0,now-e.Start),p=Math.Clamp(age/a.Lifetime,0,1);
        bool left=e.Incoming||e.Kind is "Healing" or "Ward" or "Evade" or "Resist";
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
    private readonly CombatMessageParser parser=new();
    public Totals Session {get;private set;}=new();
    public List<RunRecord> Runs {get;}=[];
    public RunRecord? Current {get;private set;}
    public bool Inside {get;private set;}
    public bool EntryConfirmed {get;private set;}
    public long Revision {get;private set;}
    public FloatStream Floats {get;}=new();
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
    private static int Number(string s)=>int.TryParse(s.Replace(",",""),out int n)&&n>=0?n:0;
    public bool Observe(string raw,int type,DateTime utc,double now)
    {
        if(!Native(raw,type))return false;string text=Regex.Replace(raw.Trim(),@"^\d{2}:\d{2}:\d{2}\s+","");var open=Opener.Match(text);
        if(open.Success)
        {
            Pause(utc);Current=new(){Server=server,Character=character,Name=open.Groups["name"].Value,Opened=utc};Runs.Add(Current);if(Runs.Count>50)Runs.RemoveAt(0);Revision++;return true;
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
            var killer=Regex.Match(text,@"^(?<mob>.+?)'s attack stops you cold!");
            AddDeath(new(utc,killer.Success?killer.Groups["mob"].Value:"Unspecified killer",damage,hp,max,Math.Max(0,damage-hp),text),now);return true;
        }
        var ward=WardReady.Match(text);if(ward.Success){Utility("Ward",Number(ward.Groups["n"].Value),"WARD READY",now,true);return true;}
        ward=WardSpent.Match(text);if(ward.Success){Utility("Ward",Number(ward.Groups["n"].Value),"ABSORBED",now,false);return true;}
        if(text=="You catch your second wind."){Utility("Second Wind",0,"SECOND WIND",now);return true;}
        if(text is "Your frenzy subsides." or "Your killer instinct fades."){Utility("Class status",0,text.TrimEnd('.').ToUpperInvariant(),now);return true;}
        if(!parser.TryParse(text,character,out var e))
        {
            var hit=SimpleIncoming.Match(text);if(!hit.Success)return false;
            e=new(){Direction="Incoming",Result="Damage",AttackType="Melee/Missile",Component="Defense",Mob=hit.Groups["mob"].Value,Damage=Number(hit.Groups["damage"].Value),Element=hit.Groups["element"].Value};
        }
        e.Raw=""; // Only explicit server death notices survive as message text.
        if(e.Result=="Death"){if(lastDeath==null||(utc-lastDeath.Utc).TotalSeconds>2)AddDeath(new(utc,"Unspecified killer",null,null,null,null,text),now);return true;}
        Add(Session,e);sessionRate.Add(e,now);
        if(Inside&&Current is {Cleared:null} active){Add(active.Stats,e);runRate.Add(e,now);Revision++;}
        string kind=e.Direction=="Healing"?"Healing":e.Direction=="Incoming"?e.Result=="Damage"?"Taken":e.Result:e.SneakAttack?"Sneak":e.Critical?"Critical":e.Component=="Direct"?e.Element:e.Component;
        if(e.Result=="Damage"&&e.Damage>0)Floats.Add(now,kind,e.Direction=="Incoming"?"HIT":e.Component=="Direct"?e.SneakAttack?"SNEAK":"":e.Component.ToUpperInvariant(),e.Mob,e.Damage,e.Direction=="Incoming",e.Critical);
        else if(e.Result=="Healing"&&e.Amount>0)Floats.Add(now,"Healing",e.Component=="PeriodicHeal"?"PERIODIC HEAL":"HEAL",e.Recipient,e.Amount);
        else if(e.Result is "Evade" or "Resist"&&e.Direction=="Incoming")Floats.Add(now,kind,e.Result.ToUpperInvariant(),e.Mob,0,true);
        else if(e.Result=="Kill")Floats.Add(now,"Kill","DOWN",e.Mob);
        else if(e.Direction=="Utility")Floats.Add(now,"Utility",e.Ability.Length>0?e.Ability:e.Result,e.Mob);
        return true;
    }
    private static void Add(Totals t,CombatEventRecord e)
    {
        MobStats? m=e.Mob.Length>0?t.Mob(e.Mob):null;
        if(e.Result=="Damage"&&e.Direction=="Outgoing")
        {
            t.Damage+=e.Damage;if(m!=null)m.Dealt+=e.Damage;t.Components[e.Component]=t.Components.GetValueOrDefault(e.Component)+e.Damage;
            if(CombatComponents.IsDirect(e)&&e.AttackType=="Melee/Missile"){t.DirectHits++;if(e.Critical)t.Crits++;if(e.SneakAttack){t.Sneaks++;t.SneakDamage+=e.Damage;}}
        }
        else if(e.Result=="Damage"&&e.Direction=="Incoming")
        {
            t.Taken+=e.Damage;
            if(e.AttackType=="Magic"){t.MagicDamage+=e.Damage;t.MagicHits++;t.MagicHigh=Math.Max(t.MagicHigh,e.Damage);if(m!=null){m.MagicDamage+=e.Damage;m.MagicHits++;m.MagicHigh=Math.Max(m.MagicHigh,e.Damage);}}
            else {t.PhysicalDamage+=e.Damage;t.PhysicalHits++;t.PhysicalHigh=Math.Max(t.PhysicalHigh,e.Damage);if(m!=null){m.PhysicalDamage+=e.Damage;m.PhysicalHits++;m.PhysicalHigh=Math.Max(m.PhysicalHigh,e.Damage);}}
        }
        else if(e.Result=="Healing"){if(e.Recipient==""||e.Component!="Summons")t.Healing+=e.Amount;string source=e.Component=="PeriodicHeal"?"Periodic healing (source unspecified)":e.Component=="Recovery"?"Healing kit":e.Ability.Length>0?e.Ability:e.Component;t.HealingSources[source]=t.HealingSources.GetValueOrDefault(source)+e.Amount;}
        else if(e.Direction=="Incoming"&&e.Result=="Evade"){t.Evades++;if(m!=null)m.Evades++;}
        else if(e.Direction=="Incoming"&&e.Result=="Resist"){t.Resists++;if(m!=null)m.Resists++;}
        else if(e.Result=="Block")t.Blocks++;
        else if(e.Result=="Kill"){t.Kills++;if(m!=null)m.Kills++;}
        else if(e.Direction=="Utility")t.Procs[e.Component]=t.Procs.GetValueOrDefault(e.Component)+1;
    }
    private void Utility(string kind,int n,string label,double now,bool ready=false)
    {
        void AddTo(Totals t){t.Procs[kind]=t.Procs.GetValueOrDefault(kind)+1;if(kind=="Ward"){if(ready)t.WardCapacity=n;else{t.WardAbsorbed+=n;t.WardCapacity=0;}}}
        AddTo(Session);if(Inside&&Current is {Cleared:null} r){AddTo(r.Stats);Revision++;}Floats.Add(now,kind=="Ward"?"Ward":"Utility",label,amount:n);
    }
    private void AddDeath(DeathRecord death,double now)
    {
        if(lastDeath!=null&&(death.Utc-lastDeath.Utc).TotalSeconds<2&&lastDeath.Message==death.Message)return;
        void Save(Totals t){if(t.Deaths.Count>0&&(death.Utc-t.Deaths[^1].Utc).TotalSeconds<2&&t.Deaths[^1].Blow==null)t.Deaths.RemoveAt(t.Deaths.Count-1);else t.DeathCount++;t.Deaths.Add(death);if(t.Deaths.Count>64){t.Deaths.RemoveAt(0);t.Truncated=true;}}
        Save(Session);if(Inside&&Current is {Cleared:null} r){Save(r.Stats);Revision++;}
        lastDeath=death;Floats.Add(now,"Death","DOWN"+(death.Overkill.HasValue?" · "+death.Overkill+" OVERKILL":""));Tick(death.Utc);
    }
    public void Enter(DateTime utc)
    {if(Current is not {Ended:null} r)return;Inside=true;EntryConfirmed=true;tick=utc;r.Entered??=utc;Revision++;}
    public void Pause(DateTime utc,bool transient=false){Tick(utc);Inside=false;if(!transient)EntryConfirmed=false;tick=null;}
    public void Tick(DateTime utc)
    {
        if(Inside&&Current is {Cleared:null} r&&tick.HasValue){double dt=(utc-tick.Value).TotalSeconds;if(dt is >=0 and <=2)r.Observed+=dt;else r.Gaps=true;Revision++;}
        tick=utc;
    }
    public void Gap(){if(Current!=null){Current.Gaps=true;Revision++;}}
    public void Seen(uint id,string name)
    {if(!Inside||Current is not {Cleared:null} r||seen.Contains(id))return;if(seen.Count>=4096){r.Stats.Truncated=true;return;}seen.Add(id);r.Stats.Mob(name).Seen++;Session.Mob(name).Seen++;Revision++;}
    public (double Dps,double Taken) Rates(double now,bool run)=>run?runRate.Read(now):sessionRate.Read(now);
    public void RestoreRuns(IEnumerable<RunRecord> runs)
    {
        foreach(var r in runs.Take(50))if(r.Server==server&&r.Character==character&&!Runs.Any(x=>x.Id==r.Id)){if(r.Ended==null)r.Gaps=true;Runs.Add(r);}
        Current??=Runs.Where(r=>r.Ended==null).OrderByDescending(r=>r.Opened).FirstOrDefault();
        Revision++;
    }
    public void NewSession(){Session=new();sessionRate=new();lastDeath=null;}
}
