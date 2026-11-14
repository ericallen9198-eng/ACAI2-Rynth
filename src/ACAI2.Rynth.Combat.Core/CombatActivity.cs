namespace ACAI2.Rynth.Combat.Core;

public sealed record PackView(int Number,long Damage,double Seconds,double Dps,bool Active,bool HasCombat)
{
    public static PackView Empty=new(0,0,0,0,false,false);
}
/// <summary>Chat-observed packs. Event-span DPS does not decay through the idle tail.
/// A gap separates packs; heals and class notices never start or prolong a pack.</summary>
public sealed class PackTracker
{
    public double IdleSeconds {get;set;}=4;
    private int number;
    private long damage;
    private double first,last;
    private bool started,finished;
    public void Observe(CombatEventRecord e,double now)
    {
        if(!double.IsFinite(now))return;
        bool attack=e.Result is "Damage" or "Evade" or "Resist" or "Block"&&e.Direction is "Incoming" or "Outgoing";
        if(!attack){if(e.Result=="Kill"&&started&&!finished&&now>=last&&now-last<IdleSeconds)last=now;return;}
        if(!started||finished||now-last>=IdleSeconds){number++;damage=0;first=last=now;started=true;finished=false;}
        if(now<first)return;
        last=Math.Max(last,now);if(e.Result=="Damage"&&e.Direction=="Outgoing")damage+=e.Damage;
    }
    public PackView Read(double now)
    {
        if(!started)return PackView.Empty;
        double elapsed=Math.Max(1,last-first);
        return new(number,damage,elapsed,damage/elapsed,!finished&&now>=last&&now-last<IdleSeconds,true);
    }
    public void Finish()=>finished=true;
    public void Reset(){number=0;damage=0;first=last=0;started=finished=false;}
}

public sealed record RecklessView(bool Discovered,bool On,float? PowerPercent,double? PowerAgeSeconds)
{
    public static RecklessView Empty=new(false,false,null,null);
}
/// <summary>Only the player's outgoing direct physical tags activate Recklessness.</summary>
public sealed class RecklessActivity
{
    private readonly Queue<(double Time,float Power)> power=[];
    private double? lastTag,sampleTime;
    private float? sample;
    private bool discovered;
    public void Sample(double now,float? percent)
    {
        if(!double.IsFinite(now))return;
        power.Enqueue((now,percent is >=0 and <=100&&float.IsFinite(percent.Value)?percent.Value:float.NaN));
        while(power.Count>16)power.Dequeue();
    }
    public void Observe(CombatEventRecord e,double now)
    {
        if(e.Direction!="Outgoing"||e.Result!="Damage"||e.AttackType!="Melee/Missile"||!CombatComponents.IsDirect(e)||!e.RecklessTagged)return;
        discovered=true;lastTag=now;sample=null;sampleTime=null;
        var candidates=power.Where(p=>p.Time<=now&&now-p.Time<=.5).ToArray();
        if(candidates.Length>0&&float.IsFinite(candidates[^1].Power)){sample=candidates[^1].Power;sampleTime=candidates[^1].Time;}
    }
    public RecklessView Read(double now)=>new(discovered,lastTag.HasValue&&now>=lastTag&&now-lastTag<4,sample,sampleTime.HasValue&&lastTag.HasValue?lastTag-sampleTime:null);
    public void Stop()=>lastTag=null;
    public void Reset(){Stop();power.Clear();sample=null;sampleTime=null;discovered=false;}
}

/// <summary>Ready/released/expired are states of one charge, not three procs.
/// Only identical release/expiry notices within 250ms of their accepted transition
/// are suppressed, unless a new ready transition intervenes. Damage is never deduplicated.</summary>
public sealed class KineticActivity
{
    private bool ready;
    private double? pendingRelease;
    public bool Ready=>ready;
    private string lastResult="",lastDetail="";
    private double lastTime=double.NegativeInfinity;
    public int Suppressed {get;private set;}
    public bool Accept(CombatEventRecord e,double now)
    {
        if(e.Component!="Kinetic")return true;
        bool duplicate=e.Result=="ChargeReady"&&ready||e.Result==lastResult&&e.Detail==lastDetail&&now>=lastTime&&now-lastTime<=.25;
        if(duplicate){Suppressed++;return false;}
        ready=e.Result=="ChargeReady";lastResult=e.Result;lastDetail=e.Detail;lastTime=now;if(e.Result=="ChargeReleased")pendingRelease=now;return true;
    }
    public bool ClaimHit(CombatEventRecord e,double now)
    {
        if(!pendingRelease.HasValue||now<pendingRelease||now-pendingRelease>1.5||e.Direction!="Outgoing"||e.Result!="Damage"||e.AttackType!="Melee/Missile"||!CombatComponents.IsDirect(e))return false;
        pendingRelease=null;return true;
    }
    public bool UnmatchedRelease(double now){if(!pendingRelease.HasValue||now-pendingRelease<=1.5)return false;pendingRelease=null;return true;}
    public void Stop(){ready=false;pendingRelease=null;lastResult=lastDetail="";lastTime=double.NegativeInfinity;}
    public void Reset(){Stop();ready=false;lastResult=lastDetail="";lastTime=double.NegativeInfinity;Suppressed=0;}
}
