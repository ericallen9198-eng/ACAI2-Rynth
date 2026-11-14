using System.Numerics;
namespace ACAI2.Rynth.Combat.Core;

/// <summary>Original display digits shared by the live ImGui renderer and SVG review.
/// Vector strokes stay sharp at any size without modifying the engine font atlas.</summary>
public static class ImpactLettering
{
    // Original centerlines on a 6 x 10 grid, with heavy bevel joins and forward lean.
    private static readonly Dictionary<char,Vector2[][]> Paths=new()
    {
        ['0']=[[new(1,0),new(5,0),new(6,1),new(6,9),new(5,10),new(1,10),new(0,9),new(0,1),new(1,0)]],
        ['1']=[[new(1,2),new(3,0),new(3,10)],[new(0,10),new(6,10)]],
        ['2']=[[new(0,1),new(1,0),new(5,0),new(6,1),new(6,3),new(0,9),new(0,10),new(6,10)]],
        ['3']=[[new(0,0),new(5,0),new(6,1),new(6,4),new(5,5),new(2,5)],[new(5,5),new(6,6),new(6,9),new(5,10),new(0,10)]],
        ['4']=[[new(0,0),new(0,5),new(6,5)],[new(5,0),new(5,10)]],
        ['5']=[[new(6,0),new(0,0),new(0,5),new(5,5),new(6,6),new(6,9),new(5,10),new(0,10)]],
        ['6']=[[new(6,0),new(1,0),new(0,1),new(0,9),new(1,10),new(5,10),new(6,9),new(6,6),new(5,5),new(0,5)]],
        ['7']=[[new(0,0),new(6,0),new(6,1),new(1,10)]],
        ['8']=[[new(1,5),new(0,4),new(0,1),new(1,0),new(5,0),new(6,1),new(6,4),new(5,5),new(1,5),new(0,6),new(0,9),new(1,10),new(5,10),new(6,9),new(6,6),new(5,5)]],
        ['9']=[[new(6,5),new(1,5),new(0,4),new(0,1),new(1,0),new(5,0),new(6,1),new(6,9),new(5,10),new(0,10)]],
        ['A']=[[new(0,10),new(0,2),new(2,0),new(4,0),new(6,2),new(6,10)],[new(0,6),new(6,6)]],
        ['B']=[[new(0,10),new(0,0),new(4,0),new(6,2),new(6,3),new(4,5),new(0,5)],[new(4,5),new(6,7),new(6,8),new(4,10),new(0,10)]],
        ['C']=[[new(6,0),new(1,0),new(0,1),new(0,9),new(1,10),new(6,10)]],
        ['D']=[[new(0,10),new(0,0),new(3,0),new(6,3),new(6,7),new(3,10),new(0,10)]],
        ['E']=[[new(6,0),new(0,0),new(0,10),new(6,10)],[new(0,5),new(5,5)]],
        ['F']=[[new(6,0),new(0,0),new(0,10)],[new(0,5),new(5,5)]],
        ['G']=[[new(6,0),new(1,0),new(0,1),new(0,9),new(1,10),new(6,10),new(6,5),new(3,5)]],
        ['H']=[[new(0,0),new(0,10)],[new(6,0),new(6,10)],[new(0,5),new(6,5)]],
        ['I']=[[new(0,0),new(6,0)],[new(3,0),new(3,10)],[new(0,10),new(6,10)]],
        ['J']=[[new(0,0),new(6,0),new(6,9),new(5,10),new(1,10),new(0,9),new(0,7)]],
        ['K']=[[new(0,0),new(0,10)],[new(6,0),new(0,5),new(6,10)]],
        ['L']=[[new(0,0),new(0,10),new(6,10)]],
        ['M']=[[new(0,10),new(0,0),new(3,4),new(6,0),new(6,10)]],
        ['N']=[[new(0,10),new(0,0),new(6,10),new(6,0)]],
        ['O']=[[new(1,0),new(5,0),new(6,1),new(6,9),new(5,10),new(1,10),new(0,9),new(0,1),new(1,0)]],
        ['P']=[[new(0,10),new(0,0),new(5,0),new(6,1),new(6,4),new(5,5),new(0,5)]],
        ['Q']=[[new(1,0),new(5,0),new(6,1),new(6,8),new(4,10),new(1,10),new(0,9),new(0,1),new(1,0)],[new(3,7),new(6,10)]],
        ['R']=[[new(0,10),new(0,0),new(5,0),new(6,1),new(6,4),new(5,5),new(0,5)],[new(3,5),new(6,10)]],
        ['S']=[[new(6,0),new(1,0),new(0,1),new(0,4),new(1,5),new(5,5),new(6,6),new(6,9),new(5,10),new(0,10)]],
        ['T']=[[new(0,0),new(6,0)],[new(3,0),new(3,10)]],
        ['U']=[[new(0,0),new(0,9),new(1,10),new(5,10),new(6,9),new(6,0)]],
        ['V']=[[new(0,0),new(0,4),new(3,10),new(6,4),new(6,0)]],
        ['W']=[[new(0,0),new(0,10),new(3,6),new(6,10),new(6,0)]],
        ['X']=[[new(0,0),new(6,10)],[new(6,0),new(0,10)]],
        ['Y']=[[new(0,0),new(3,5),new(6,0)],[new(3,5),new(3,10)]],
        ['Z']=[[new(0,0),new(6,0),new(0,10),new(6,10)]],
        ['×']=[[new(1,3),new(5,7)],[new(5,3),new(1,7)]],
        ['·']=[[new(2,5),new(2.15f,5)]],
        ['+']=[[new(0,5),new(6,5)],[new(3,2),new(3,8)]],
        ['-']=[[new(1,5),new(5,5)]], [',']=[[new(1,9),new(0,11)]],
        ['.']=[[new(0,10),new(.15f,10)]]
    };
    private static float Advance(char c)=>c is ',' or '.'?3.6f:c==' '?3:9.6f;
    public static float Width(string value,float height)=>Math.Max(0,value.Sum(Advance))*height/13;
    public static bool Supports(string value)=>value.All(c=>c==' '||Paths.ContainsKey(c));
    private sealed record Shape(Vector2[] Points,int Layer);
    private static readonly Dictionary<char,Shape[]> Shapes=Paths.ToDictionary(p=>p.Key,p=>Compile(p.Value));
    private static Shape[] Compile(Vector2[][] paths)
    {
        List<Shape> shapes=[];
        Vector2 Map(Vector2 q)=>new(q.X+1.4f+(10-q.Y)*.10f,q.Y+1.4f);
        foreach(var path in paths)Stroke(path,1.15f,0);
        foreach(var path in paths)Stroke(path,.79f,1);
        foreach(var path in paths)for(int i=1;i<path.Length;i++)
            if(MathF.Abs(path[i].Y-path[i-1].Y)<.1f&&path[i].X>path[i-1].X)
            {
                Vector2 a=Map(path[i-1]+new Vector2(0,-.53f)),b=Map(path[i]+new Vector2(0,-.53f));
                shapes.Add(new([a,b,b+new Vector2(0,.36f),a+new Vector2(0,.36f)],2));
            }
        return shapes.ToArray();
        void Stroke(Vector2[] path,float radius,int layer)
        {
            for(int i=1;i<path.Length;i++)
            {
                Vector2 delta=path[i]-path[i-1];if(delta.LengthSquared()<.001f)continue;
                Vector2 normal=Vector2.Normalize(new(-delta.Y,delta.X))*radius;
                shapes.Add(new([Map(path[i-1]+normal),Map(path[i]+normal),Map(path[i]-normal),Map(path[i-1]-normal)],layer));
            }
            foreach(var q in path)shapes.Add(new([Map(q+new Vector2(-radius,0)),Map(q+new Vector2(0,-radius)),Map(q+new Vector2(radius,0)),Map(q+new Vector2(0,radius))],layer));
        }
    }
    /// <remarks>The callback consumes a reused four-point buffer synchronously; copy it if retaining geometry.</remarks>
    public static void Draw(string value,Vector2 origin,float height,Vector4 color,bool highlight,Action<Vector2[],Vector4> polygon)
    {
        float scale=height/13,cursor=0;Vector2[] mapped=new Vector2[4];
        foreach(char glyph in value)
        {
            if(Shapes.TryGetValue(glyph,out var shapes))foreach(var shape in shapes)
            {
                if(shape.Layer==2&&!highlight)continue;
                Vector4 tint=shape.Layer switch{0=>new(.012f,.017f,.022f,color.W),2=>new(1,1,.95f,color.W*.68f),_=>color};
                for(int i=0;i<4;i++)mapped[i]=origin+(shape.Points[i]+new Vector2(cursor,0))*scale;
                polygon(mapped,tint);
            }
            cursor+=Advance(glyph);
        }
    }
    public static float Punch(double age)=>age<.10?1.18f:age<.28?1.08f:1;
    public static string Number(FloatEvent e)=>(e.Kind=="Healing"?"+":e.Kind=="Taken"?"-":"")+e.Amount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
    public static string Caption(FloatEvent e)
    {
        string label=e.Label.Trim();
        if(label is "CRITICAL" or "CRIT" or "TAKEN" or "HIT")label="";
        return e.Critical||e.Kind=="Critical"?"CRIT"+(label.Length>0?" · "+label:""):label;
    }
}

/// <summary>Shape vocabulary shared by the live renderer and the design review exporter.</summary>
public static class ImpactArt
{
    public static void Plate(Vector2 p,Vector2 size,Vector4 color,double age,Action<Vector2[],Vector4> polygon)
    {
        float cut=7;
        polygon([p+new Vector2(cut,0),p+new Vector2(size.X,0),p+size-new Vector2(0,cut),p+size-new Vector2(cut,0),p+new Vector2(0,size.Y),p+new Vector2(0,cut)],new(.015f,.025f,.035f,color.W*.86f));
        polygon([p+new Vector2(0,7),p+new Vector2(5,2),p+new Vector2(5,size.Y-2),p+new Vector2(0,size.Y)],color);
        float length=size.X*(.28f+.20f*MathF.Exp(-(float)age*3));
        polygon([p+new Vector2(8,size.Y-2),p+new Vector2(length,size.Y-2),p+new Vector2(length-3,size.Y),p+new Vector2(8,size.Y)],new(color.X,color.Y,color.Z,color.W*.8f));
    }
    public static void Burst(Vector2 p,Vector2 size,Vector4 color,double age,float strength,Action<Vector2,Vector2,Vector4,float> line,Action<Vector2[],Vector4> polygon)
    {
        float t=(float)age,fade=MathF.Exp(-t*2.2f)*strength,travel=Math.Min(t,.6f)*22;
        Vector4 Tint(float alpha)=>new(color.X,color.Y,color.Z,color.W*fade*alpha);
        Vector2 center=p+size*.5f;
        for(int side=-1;side<=1;side+=2)
        {
            float x=center.X+side*(size.X*.5f+7+travel);
            line(new(x+side*9,p.Y+9),new(x,p.Y-3),Tint(1),2);
            line(new(x,p.Y-3),new(x-side*13,p.Y-3),Tint(1),2);
            line(new(x,p.Y+size.Y),new(x-side*10,p.Y+size.Y),Tint(.7f),2);
            for(int i=0;i<3;i++)
            {
                Vector2 q=new(x+side*(10+i*6+travel),center.Y+(i-1)*(13+travel));
                polygon([q,q+new Vector2(side*(8+i*2),(i-1)*6),q+new Vector2(side*2,4)],Tint(.85f));
            }
        }
        line(new(center.X-size.X*.28f,p.Y+size.Y+5),new(center.X+size.X*.28f,p.Y+size.Y+5),Tint(.8f),2);
    }
}

/// <summary>Bounded, per-frame placement. Text boxes never overlap or cross the center lane.
/// A crowded screen may omit a visual event; statistics do not depend on placement.</summary>
public sealed class FloatLayout(Vector2 viewport,Vector2 anchor)
{
    private readonly List<(Vector2 P,Vector2 Size)> occupied=[];
    public bool TryPlace(Vector2 desired,Vector2 size,int lane,out Vector2 placed)
    {
        placed=default;
        if(!float.IsFinite(size.X)||!float.IsFinite(size.Y)||size.X<=0||size.Y<=0)return false;
        float minX=18,maxX=viewport.X-size.X-18;
        if(lane<0)maxX=Math.Min(maxX,anchor.X-24-size.X);
        if(lane>0)minX=Math.Max(minX,anchor.X+24);
        if(maxX<minX||viewport.Y<size.Y+24)return false;
        Vector2 p=new(MathF.Round(Math.Clamp(desired.X-size.X/2,minX,maxX)),MathF.Round(Math.Clamp(desired.Y,12,viewport.Y-size.Y-12)));
        // At most one bounded pass through the 48-event stream for each placement.
        for(int attempt=0;attempt<49;attempt++)
        {
            var hits=occupied.Where(r=>p.X<r.P.X+r.Size.X+8&&p.X+size.X+8>r.P.X&&p.Y<r.P.Y+r.Size.Y+6&&p.Y+size.Y+6>r.P.Y).ToArray();
            if(hits.Length==0){occupied.Add((p,size));placed=p;return true;}
            p.Y=MathF.Floor(hits.Min(r=>r.P.Y)-size.Y-8);if(p.Y<12)return false;
        }
        return false;
    }
}
