namespace ACAI2.Rynth.Marae.Core;

// Session-only world IDs: never carry landscape ownership guesses through logout.
public sealed class CacheWatcher
{
    public const int Limit=128;
    public const double Radius=55,RetrySeconds=60,AttemptSeconds=60;
    private sealed class Entry(uint id,double seen){public uint Id=id;public double Seen=seen,Due=seen;public int Attempts;public bool Done;}
    private readonly Dictionary<uint,Entry> entries=new();
    public uint Active {get;private set;}
    public double Deadline {get;private set;}
    public int Count=>entries.Count;
    public void Observe(IReadOnlyDictionary<uint,double> nearby,IReadOnlySet<uint> visible,double now)
    {
        foreach(uint id in entries.Keys.ToArray())if(!visible.Contains(id)&&id!=Active)entries.Remove(id);
        foreach(var pair in nearby)
            if(pair.Key!=0&&double.IsFinite(pair.Value)&&pair.Value<=Radius&&pair.Value>=0&&!entries.ContainsKey(pair.Key)&&entries.Count<Limit)entries.Add(pair.Key,new(pair.Key,now));
    }
    public uint Start(IReadOnlyDictionary<uint,double> nearby,double now,bool allowed)
    {
        if(!allowed||Active!=0)return 0;
        var next=entries.Values.Where(e=>!e.Done&&e.Attempts<2&&now>=e.Due&&now-e.Seen<=600&&nearby.TryGetValue(e.Id,out double d)&&d<=Radius).OrderBy(e=>nearby[e.Id]).ThenBy(e=>e.Id).FirstOrDefault();
        if(next==null)return 0;
        next.Attempts++;Active=next.Id;Deadline=now+AttemptSeconds;return Active;
    }
    public void Handled(uint id){if(entries.TryGetValue(id,out var e))e.Done=true;}
    public void Finish(double now,bool completed,bool refused)
    {
        if(entries.TryGetValue(Active,out var e)){e.Done=completed||refused||e.Attempts>=2;e.Due=now+RetrySeconds;}
        Active=0;Deadline=0;
    }
    public static bool HuntState(string state)=>state is "T180_HUNT" or "T225_HUNT" or "T275_HUNT" or "T180_FARM_RUN" or "T225_FARM_RUN" or "T275_FARM_RUN";
    public static bool MayStart(string state,bool enabled,bool running,bool recovery,bool portal,bool mapActive,bool unresolved,bool idle)=>HuntState(state)&&enabled&&running&&!recovery&&!portal&&!mapActive&&!unresolved&&idle;
    public static bool OwnershipRefusal(string text)
    {
        if(text.Length>512||text.Contains('"')||text.Contains(" says",StringComparison.OrdinalIgnoreCase)||text.Contains(" tells",StringComparison.OrdinalIgnoreCase))return false;
        if(!(text.StartsWith("You ",StringComparison.Ordinal)||text.StartsWith("This ",StringComparison.Ordinal)||text.StartsWith("That ",StringComparison.Ordinal)))return false;
        return text.Contains("not yours",StringComparison.OrdinalIgnoreCase)||text.Contains("does not belong to you",StringComparison.OrdinalIgnoreCase)||text.Contains("not allowed to open",StringComparison.OrdinalIgnoreCase)||text.Contains("not permitted to open",StringComparison.OrdinalIgnoreCase);
    }
}
