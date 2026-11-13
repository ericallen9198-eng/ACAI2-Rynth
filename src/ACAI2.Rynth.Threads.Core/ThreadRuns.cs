using System.Text.RegularExpressions;
namespace ACAI2.Rynth.Threads.Core;

public sealed class ThreadRuns(ThreadGemJournal data)
{
    private static readonly Regex Realm=new(@"^\[REALM\]\s+id=(\d+)\s+inst=(\d+)\s+eph=([01])\s+name=.+$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
    private static readonly Regex Progress=new(@"^Monsters killed (\d+)/(\d+)\.(?: (Boss still remaining\.|Boss (?:defeated|slain)\.))?$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
    private DateTime? last;
    private bool dead;
    public bool InThread {get;private set;}
    public ThreadRunRecord? Current=>data.Runs.FirstOrDefault(r=>r.Id==data.ActiveRunId);
    public void Resume(){if(Current is {EndedUtc:null} run){run.ObservationGap=true;run.Status=run.ClearedUtc.HasValue?"Cleared — entry unconfirmed":"Entry unconfirmed after restart";}}
    public bool Enter(DateTime now,string basis="Manual entry confirmation")
    {
        if(Current is not {EndedUtc:null} run)return false;
        Tick(now);InThread=true;last=now;dead=false;run.EnteredUtc??=now;
        run.Status=run.ClearedUtc.HasValue?"Cleared":"In progress";
        return true;
    }
    public void Pause(DateTime now,bool gap=false)
    {
        Tick(now);InThread=false;last=null;dead=false;
        if(Current is {EndedUtc:null} run){run.ObservationGap|=gap;run.Status=run.ClearedUtc.HasValue?"Cleared — outside":"Outside / awaiting return";}
    }
    public bool Tick(DateTime now)
    {
        if(!InThread||Current is not {EndedUtc:null} run)return false;
        if(last.HasValue)
        {
            double seconds=(now-last.Value).TotalSeconds;
            if(seconds is >=0 and <=2)run.ObservedSeconds+=seconds;
            else run.ObservationGap=true;
        }
        last=now;return true;
    }
    public bool Observe(string raw,DateTime now)
    {
        if(!ThreadText.Native(raw))return false;
        string text=raw.Trim();var realm=Realm.Match(text);
        if(realm.Success)
        {
            if(realm.Groups[3].Value=="0"){Pause(now);return true;}
            string key=realm.Groups[1].Value+":"+realm.Groups[2].Value;
            if(realm.Groups[2].Value=="0")return false;
            var pending=Current;
            var known=data.Runs.LastOrDefault(r=>r.RealmInstance==key&&!r.EndedUtc.HasValue);
            if(known!=null)
            {
                if(pending!=null&&pending!=known&&pending.EnteredUtc==null&&pending.RealmInstance.Length==0)
                {known.RemainingReturns=pending.RemainingReturns;pending.Status="Superseded by known instance";pending.EndedUtc=now;}
                data.ActiveRunId=known.Id;
            }
            else if(pending is {EndedUtc:null,RealmInstance:""})pending.RealmInstance=key;
            else return false; // An ephemeral realm without a Thread opener is not proof of a Thread.
            Enter(now,"Server realm/instance");return true;
        }
        var run=Current;if(run==null||run.EndedUtc.HasValue)return false;
        if(text=="Your Thread Gem crumbles to dust.")
        {Pause(now);run.EndedUtc=now;run.Status=run.ClearedUtc.HasValue?"Finished — cleared":"Ended — clear not observed";return true;}
        if(!InThread)return false;
        if(text.StartsWith(run.Dungeon+" is cleared.",StringComparison.Ordinal)&&run.Dungeon.Length>0)
        {Tick(now);run.ClearedUtc??=now;run.Status="Cleared";return true;}
        var progress=Progress.Match(text);
        if(progress.Success&&int.TryParse(progress.Groups[1].Value,out int killed)&&int.TryParse(progress.Groups[2].Value,out int total)&&total>0&&killed<=total)
        {
            if(run.MonstersKilled.HasValue&&killed<run.MonstersKilled)return false;
            run.MonstersKilled=killed;run.MonstersTotal=total;
            if(progress.Groups[3].Success)run.BossRemaining=progress.Groups[3].Value.StartsWith("Boss still",StringComparison.Ordinal);
            return true;
        }
        if(text is "You are dead!" or "You have died!")
        {if(!dead){run.Deaths++;dead=true;}return true;}
        return false;
    }
}
