using System.Globalization;
using System.Text;

namespace ACAI2.Rynth.Terrain.Plugin;

// Host-free policy. All units are native world units (yards), including height.
public readonly record struct Ground(double X,double Y,double Z)
{
    public double Flat(Ground b)=>Math.Sqrt((X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y));
    public bool Finite=>double.IsFinite(X)&&double.IsFinite(Y)&&double.IsFinite(Z);
}
public static class MeshPlan
{
    public static Ground[] Build(Ground start,Ground goal,int variant,
        Func<Ground,Ground,Ground[]> find,Func<Ground,Ground,bool> clear,IReadOnlyList<Ground> portals)
    {
        if(!start.Finite||!goal.Finite||variant is <0 or >4||start.Flat(goal)>600)
            throw new InvalidDataException("Invalid or overlong mesh request");
        Ground[] result;
        if(variant==0)result=Leg(start,goal,find);
        else
        {
            double length=start.Flat(goal);
            if(length<3)throw new InvalidDataException("Too close for a side approach");
            double dx=(goal.X-start.X)/length,dy=(goal.Y-start.Y)/length;
            double side=(variant<=2?10:18)*(variant%2==1?1:-1);
            var via=new Ground(start.X+dx*6-dy*side,start.Y+dy*6+dx*side,start.Z);
            var first=Leg(start,via,find);var second=Leg(first[^1],goal,find);
            result=first.Concat(second.Skip(1)).ToArray();
        }
        if(result.Length>256)throw new InvalidDataException("Too many mesh corners");
        double distance=0;
        for(int i=1;i<result.Length;i++)
        {
            var a=result[i-1];var b=result[i];distance+=a.Flat(b);
            if(!clear(a,b))throw new InvalidDataException("Mesh raycast blocked a planned segment");
            foreach(var portal in portals)
                if(portal.Z>=Math.Min(a.Z,b.Z)-15&&portal.Z<=Math.Max(a.Z,b.Z)+15)
                    if(SegmentDistance(portal,a,b)<15)
                        throw new InvalidDataException("Planned path enters a camp portal exclusion");
        }
        if(distance>1500)throw new InvalidDataException("Mesh detour is too long for one survey step");
        return result;
    }
    private static Ground[] Leg(Ground a,Ground b,Func<Ground,Ground,Ground[]> find)
    {
        var points=find(a,b);
        if(points.Length is <2 or >128||points.Any(p=>!p.Finite)||points[0].Flat(a)>2||Math.Abs(points[0].Z-a.Z)>4
            ||points[^1].Flat(b)>2||Math.Abs(points[^1].Z-b.Z)>4)
            throw new InvalidDataException("Mesh path endpoints do not match the requested ground layer");
        // Include the actual pose, so the walk from it onto the snapped mesh is checked too.
        return new[]{a}.Concat(points.Skip(1)).ToArray();
    }
    public static double SegmentDistance(Ground p,Ground a,Ground b)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,l=dx*dx+dy*dy;
        double f=l>0?Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/l,0,1):0;
        return Math.Sqrt(Math.Pow(p.X-a.X-dx*f,2)+Math.Pow(p.Y-a.Y-dy*f,2));
    }
    public static string Serialize(IReadOnlyList<Ground> points)
    {
        var s=new StringBuilder("uTank2 NAV 1.2\n4\n");s.Append(points.Count).Append('\n');
        foreach(var p in points)
        {
            if(!p.Finite)throw new InvalidDataException("Invalid route point");
            s.Append("0\n").Append(((p.X-24468)/240).ToString("R",CultureInfo.InvariantCulture)).Append('\n')
                .Append(((p.Y-24468)/240).ToString("R",CultureInfo.InvariantCulture)).Append('\n')
                .Append((p.Z/240).ToString("R",CultureInfo.InvariantCulture)).Append("\n0\n");
        }
        return s.ToString();
    }
}
