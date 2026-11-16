using System.Numerics;
namespace ACAI2.Rynth.Combat.Core;

// Translucent archer afterimages, drawn with open strokes only. No filled silhouette or window.
public static class EchoArt
{
    public static void Draw(Vector2 p,Vector2 text,double age,float intensity,float scale,bool renewal,Action<Vector2,Vector2,Vector4,float> line)
    {
        if(age<0||age>=2||scale<=0)return;
        float t=(float)age,fade=MathF.Exp(-t*2)*Math.Clamp(intensity,0,1);
        if(renewal)
        {
            for(int copy=2;copy>=0;copy--)
            {
                Vector2 o=p+new Vector2(-25-copy*(7+Math.Min(t,1)*6),text.Y*.5f-12)*scale;
                Vector4 c=new(.6f-copy*.08f,.85f,1,fade*(.7f-copy*.19f));
                void L(float x,float y,float a,float b)=>line(o+new Vector2(x,y)*scale,o+new Vector2(a,b)*scale,c,scale);
                for(int n=0;n<12;n++){float a=n*MathF.Tau/12,b=(n+1)*MathF.Tau/12;L(MathF.Cos(a)*3,MathF.Sin(a)*3-4,MathF.Cos(b)*3,MathF.Sin(b)*3-4);}
                L(0,0,0,14);L(0,14,-5,23);L(0,14,5,23);L(0,3,10,7);L(0,3,4,8);L(4,8,10,7);
                L(10,0,14,7);L(14,7,10,15);L(10,0,10,15);L(4,7,20,7);L(20,7,17,4);L(20,7,17,10);
            }
        }
        else
        {
            for(int copy=0;copy<2;copy++)
            {
                float offset=(9+copy*8+t*9)*scale;
                Vector4 c=new(.56f,.8f,1,fade*(copy==0?.4f:.2f));
                line(p+new Vector2(-offset,4*scale),p+new Vector2(-offset-4*scale,text.Y-4*scale),c,scale);
                line(p+new Vector2(text.X+offset,4*scale),p+new Vector2(text.X+offset+4*scale,text.Y-4*scale),c,scale);
            }
        }
    }
    public static Vector2 GhostOffset(int copy,double age,float scale)=>new(-(copy+1)*(4+6*Math.Clamp((float)age,0,1))*scale,-(copy+1)*2*scale);
    public static float GhostAlpha(int copy,double age)=>(copy==0?.2f:.09f)*MathF.Exp(-(float)Math.Max(0,age)*4);
}
