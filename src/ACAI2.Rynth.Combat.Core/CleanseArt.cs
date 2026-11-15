using System.Numerics;
namespace ACAI2.Rynth.Combat.Core;

// Open soap-film rings: shared by ImGui and the actual geometry preview. No fills/backings.
public static class CleanseArt
{
    public static void Draw(Vector2 p,Vector2 text,double age,float intensity,float scale,Action<Vector2,Vector2,Vector4,float> line)
    {
        if(age<0||age>=5||scale<=0)return;
        float t=(float)age,fade=Math.Clamp((2.5f-t)*2,0,1)*Math.Clamp(intensity,0,1);
        Vector2 center=p+text*.5f;
        for(int i=0;i<8;i++)
        {
            int side=i%2==0?-1:1;
            Vector2 q=new(center.X+side*(text.X*.5f+(13+MathF.Sin(t*1.8f+i)*4)*scale),p.Y+text.Y*(.15f+(i/2)*.22f)-(t*5+MathF.Sin(i+t)*3)*scale);
            float r=(4+i%3*2)*scale;
            float popStart=.75f+(i%4)*.27f,pop=Math.Clamp((t-popStart)/.24f,0,1);
            if(pop>=1)continue;
            float opacity=fade*(1-pop);
            for(int n=0;n<24;n++)
            {
                float a=n*MathF.Tau/24,b=(n+1)*MathF.Tau/24;
                Vector4 c=n<8?new(.65f,.94f,1,opacity*.8f):n<16?new(.78f,.72f,1,opacity*.5f):new(.57f,1,.82f,opacity*.65f);
                if(pop>.05f&&n%3!=0)continue;
                float radius=r*(1+pop*.65f);
                line(q+new Vector2(MathF.Cos(a),MathF.Sin(a))*radius,q+new Vector2(MathF.Cos(b),MathF.Sin(b))*radius,c,scale);
            }
            if(pop<.05f)
            {
                Vector4 white=new(.94f,1,1,opacity*.75f);
                line(q+new Vector2(-r*.5f,-r*.6f),q+new Vector2(-r*.1f,-r*.8f),white,scale);
            }
            else for(int n=0;n<4;n++)
            {
                float a=n*MathF.Tau/4+i;Vector2 d=new(MathF.Cos(a),MathF.Sin(a));
                line(q+d*r*(1+pop),q+d*r*(1.25f+pop*1.4f),new(.8f,1,.95f,opacity*.65f),scale);
            }
        }
    }
}
