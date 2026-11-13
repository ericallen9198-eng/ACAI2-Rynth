namespace ACAI2.Rynth.Threads.Core;

public sealed record StationPosition(uint Cell,double X,double Y,double Z);
public sealed record StationCandidate(uint Id,string Name,StationPosition? Position,double? UseRadius);
public sealed record StationMatch(uint Id,string Problem);
public static class StationLocator
{
    public const double LocalUseLimit=12;
    public static double? Distance(StationPosition a,StationPosition b)
    {
        if(a.Cell==0||b.Cell==0)return null;
        double dx=a.X-b.X,dy=a.Y-b.Y;
        if((a.Cell>>16)!=(b.Cell>>16))
        {
            // Outdoor coordinates are local to 192m landblocks. Interior blocks
            // cannot be compared without the engine's dungeon transforms.
            if((a.Cell&0xffff)>=0x100||(b.Cell&0xffff)>=0x100)return null;
            dx+=((int)(a.Cell>>24)-(int)(b.Cell>>24))*192;
            dy+=((int)((a.Cell>>16)&255)-(int)((b.Cell>>16)&255))*192;
        }
        double dz=a.Z-b.Z,d=Math.Sqrt(dx*dx+dy*dy+dz*dz);
        return double.IsFinite(d)?d:null;
    }
    public static bool ExpectedApproach(string state,StationPosition origin,StationPosition current)=>
        state is "Awaiting vendor" or "Awaiting press" && Distance(origin,current) is double d && d<=LocalUseLimit;
    public static StationMatch Find(string name,StationPosition player,IEnumerable<StationCandidate> objects)
    {
        var named=objects.Where(o=>o.Name==name).GroupBy(o=>o.Id).Select(g=>g.First()).ToArray();
        if(named.Length==0)return new(0,name+": no exact-name object in Rynth's live object list.");
        var measured=named.Where(o=>o.Position!=null).Select(o=>(Item:o,Distance:Distance(player,o.Position!),Radius:o.UseRadius is >0 and <=100&&double.IsFinite(o.UseRadius.Value)?o.UseRadius.Value:3d)).ToArray();
        var within=measured.Where(o=>o.Distance.HasValue&&o.Distance.Value<=LocalUseLimit).ToArray();
        if(within.Length==1)return new(within[0].Item.Id,"");
        if(within.Length>1)return new(0,name+": multiple exact-name objects inside the local-use area ("+string.Join(", ",within.Select(o=>o.Item.Id.ToString("X8")))+").");
        var nearest=measured.Where(o=>o.Distance.HasValue).OrderBy(o=>o.Distance).FirstOrDefault();
        if(nearest.Item!=null)return new(0,name+": nearest "+nearest.Item.Id.ToString("X8")+" is "+nearest.Distance!.Value.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"m away; local-use limit 12.00m; advertised use radius "+nearest.Radius.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"m"+(nearest.Item.UseRadius.HasValue?"":" (fallback; radius property unavailable)")+".");
        return new(0,name+": found "+named.Length+" exact-name object(s), but position snapshots are unavailable or in an incomparable interior landblock.");
    }
}
