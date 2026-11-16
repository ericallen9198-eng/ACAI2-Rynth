using System.Numerics;
namespace ACAI2.Rynth.Combat.Core;

public static class SurgeArt
{
    public static float ShieldRadius(double age,float scale)=>(7+25*(1-MathF.Exp(-(float)Math.Max(0,age)*7)))*scale;
    public static float Fade(double age)=>MathF.Exp(-(float)Math.Max(0,age)*5);
    public static void Draw(Vector2 center,double age,float intensity,float scale,bool protection,Action<Vector2,Vector2,Vector4,float> line)
    {
        if(age<0||age>1.1||scale<=0)return;
        float t=(float)age,r=ShieldRadius(age,scale),fade=Fade(age)*Math.Clamp(intensity,0,1);
        if(protection)
        {
            Vector2[] shield=[new(0,-1),new(.8f,-.65f),new(.65f,.35f),new(0,1),new(-.65f,.35f),new(-.8f,-.65f),new(0,-1)];
            for(int pass=0;pass<2;pass++)for(int n=1;n<shield.Length;n++)
            {
                float size=r*(pass==0?1:.76f);Vector4 c=new(.6f,.87f,1,fade*(pass==0?.9f:.35f));
                line(center+shield[n-1]*size,center+shield[n]*size,c,(pass==0?1.7f:1)*scale);
            }
            line(center+new Vector2(-.3f,0)*r,center+new Vector2(-.05f,.25f)*r,new(.86f,1,1,fade),1.5f*scale);
            line(center+new Vector2(-.05f,.25f)*r,center+new Vector2(.4f,-.3f)*r,new(.86f,1,1,fade),1.5f*scale);
        }
        else
        {
            // Fractured impact rune: three open spearhead shards expand into embers.
            for(int i=0;i<3;i++)
            {
                float a=i*MathF.Tau/3-.6f+t*.6f;
                Vector2 d=new(MathF.Cos(a),MathF.Sin(a)),side=new(-d.Y,d.X);
                Vector2 tip=center+d*r,back=center+d*r*.25f;
                Vector4 c=i==0?new(1,.83f,.4f,fade):new(1,.4f,.2f,fade*.85f);
                line(tip,back+side*r*.2f,c,1.8f*scale);line(tip,back-side*r*.2f,c,1.8f*scale);
                line(center+d*r*1.15f,center+d*r*1.4f,new(1,.65f,.25f,fade*.6f),scale);
            }
        }
    }
}
