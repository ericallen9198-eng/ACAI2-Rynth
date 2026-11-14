using System.Globalization;
using System.Numerics;
using System.Security;
using System.Text;
using System.Text.Json;
using ACAI2.Rynth.Combat.Core;

CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
string output=args.Length>0?args[0]:"/tmp/acai2-combat-design";Directory.CreateDirectory(output);
string[] frames=[Frame(.04),Frame(.18),Frame(.55),Frame(1.1)];
File.WriteAllText(Path.Combine(output,"combat-design.svg"),frames[1]);
File.WriteAllText(Path.Combine(output,"notice-scales.svg"),GroupReview());
string html="""
<!doctype html><html lang="en"><meta charset="utf-8"><title>ACAI2 Combat · vector lettering review</title>
<style>body{margin:0;background:#0c141a;color:#d6e4ec;font:15px Arial,sans-serif}header{padding:18px 24px}h1{font-size:21px;margin:0 0 8px}button{background:#172b35;border:1px solid #34515f;color:#d6e4ec;padding:9px 16px;margin:4px;cursor:pointer}button.active{border-color:#68e5bf}#frame{width:1156px;height:754px}svg{display:block}p{margin:6px 0;color:#a9bdc8}.note{max-width:1100px;font-size:13px}</style>
<header><h1>ACAI2 COMBAT / working vector design</h1><p>Native size: 1156 × 754, matching the supplied clip. Browser zoom 100%. Synthetic values.</p>
<button onclick="show(0)">Arrival · 40ms</button><button onclick="show(1)">Punch · 180ms</button><button onclick="show(2)">Hold / drift · 550ms</button><button onclick="show(3)">Fade · 1100ms</button>
<p><label>Buffs scale <input id="Buff" type="range" min="0.5" max="2" step="0.05" value="1" oninput="scales()"></label> <label>Debuffs scale <input id="Debuff" type="range" min="0.5" max="2" step="0.05" value="1" oninput="scales()"></label> <label>Skills scale <input id="Skill" type="range" min="0.5" max="2" step="0.05" value="1" oninput="scales()"></label></p>
<p class="note">Digits, crit brackets/shards and open notice accents use the same C# vector geometry as the plugin. Browser label fonts and antialiasing differ from Rynth. This is a renderer design review, not a screenshot of the game. Slider transforms preview sizes; live collision placement is not simulated.</p></header><main id="frame"></main><script>
"""+"const frames="+JsonSerializer.Serialize(frames)+";function show(i){document.getElementById('frame').innerHTML=frames[i];document.querySelectorAll('button').forEach((b,j)=>b.classList.toggle('active',j===i));scales();}function scales(){document.querySelectorAll('g[data-group]').forEach(g=>{const s=Number(document.getElementById(g.dataset.group).value),p=g.dataset.pivot.split(',').map(Number);g.setAttribute('transform',`translate(${p[0]} ${p[1]}) scale(${s}) translate(${-p[0]} ${-p[1]})`);});}show(1);</script></html>";
File.WriteAllText(Path.Combine(output,"COMBAT-PREVIEW.html"),html);
Console.WriteLine("Shared vector preview: "+Path.Combine(output,"COMBAT-PREVIEW.html"));

static string Frame(double age)
{
    var b=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1156\" height=\"754\" viewBox=\"0 0 1156 754\"><rect width=\"1156\" height=\"754\" fill=\"#132128\"/>");
    string N(float f)=>f.ToString("0.##",CultureInfo.InvariantCulture);
    string C(Vector4 c)=>$"rgb({(int)(Math.Clamp(c.X,0,1)*255)},{(int)(Math.Clamp(c.Y,0,1)*255)},{(int)(Math.Clamp(c.Z,0,1)*255)})";
    void Poly(Vector2[] p,Vector4 c)=>b.Append($"<polygon points=\"{string.Join(' ',p.Select(q=>N(q.X)+','+N(q.Y)))}\" fill=\"{C(c)}\" opacity=\"{N(c.W)}\"/>");
    void Line(Vector2 p,Vector2 q,Vector4 c,float w)=>b.Append($"<path d=\"M{N(p.X)} {N(p.Y)} L{N(q.X)} {N(q.Y)}\" stroke=\"{C(c)}\" stroke-width=\"{N(w)}\" opacity=\"{N(c.W)}\"/>");
    void Text(string s,float x,float y,float size,Vector4 c)=>b.Append($"<text x=\"{N(x)}\" y=\"{N(y+size*.82f)}\" font-family=\"Arial,sans-serif\" font-size=\"{N(size)}\" fill=\"{C(c)}\" opacity=\"{N(c.W)}\" stroke=\"#05090c\" stroke-width=\"2\" paint-order=\"stroke\">{SecurityElement.Escape(s)}</text>");
    Vector4 gold=new(1,.83f,.3f,1),red=new(1,.32f,.29f,1),heal=new(.60f,.96f,.69f,1),muted=new(.57f,.69f,.74f,1),acid=new(.48f,.94f,.35f,1);
    // Reference silhouette is a spatial guide, never a camera-follow claim.
    for(int x=20;x<1156;x+=48)Line(new(x,0),new(x,754),new(1,1,1,.018f),1);
    Line(new(578,115),new(578,650),new(.4f,.6f,.7f,.12f),1);
    b.Append("<path d=\"M550 370 L605 370 L626 468 L610 605 L546 605 L530 468 Z\" fill=\"#243b43\" stroke=\"#35525d\"/><circle cx=\"578\" cy=\"346\" r=\"24\" fill=\"#243b43\" stroke=\"#35525d\"/>");
    Text("INCOMING",235,90,12,muted);Text("HEALING",540,90,12,muted);Text("OUTGOING",790,90,12,muted);
    var ready=new FloatEvent(1,0,"KineticReady","KINETIC READY","",0,1,0);float readyScale=NoticeStyle.Focus(ready,age);string readyLabel=NoticeStyle.Label(ready,age);
    Vector4 chargeColor=new(.44f,.85f,1,1);float readyFont=25*readyScale;Vector2 chargeP=new(730,175),chargeSize=new(ImpactLettering.Width(readyLabel,readyFont),readyFont);
    b.Append("<g data-group=\"Skill\" data-pivot=\"850,175\">");ImpactArt.Charge(chargeP,chargeSize,chargeColor,age,.85f,Line,readyScale);ImpactLettering.Draw(readyLabel,chargeP,readyFont,chargeColor,true,Poly);b.Append("</g>");
    // Sample meter uses the same digits as its actual plugin header.
    ImpactArt.Plate(new(24,22),new(285,137),new(.38f,.9f,.77f,1),age,Poly);
    Text("COMBAT / TOE",42,37,13,muted);
    ImpactLettering.Draw("12,480",new(42,63),30,new(.38f,.9f,.77f,1),true,Poly);Text("DPS",193,73,13,muted);
    Text("Sneak 43.1%    Reckless ON · power 65%",42,111,12,muted);Text("Physical evade 72.4%",42,132,12,muted);
    float drift=(float)Math.Max(0,age-.2)*32;
    void Crit(string number,string caption,Vector2 center,Vector4 color)
    {
        float size=MathF.Round(23*1.5f*1.3f*ImpactLettering.Punch(age)),width=ImpactLettering.Width(number,size);
        Vector2 p=center-new Vector2(width/2,drift);
        ImpactArt.Burst(p,new(width,size+21),color,age,.85f,Line,Poly);
        ImpactLettering.Draw(number,p,size,color,true,Poly);ImpactLettering.Draw(caption,p+new Vector2((width-ImpactLettering.Width(caption,18))/2,size+3),18,color,false,Poly);
    }
    Crit("2,313","CRIT",new(785,350),gold);Crit("-344","CRIT",new(343,416),red);
    float hw=ImpactLettering.Width("+118",23);ImpactLettering.Draw("+118",new(578-hw/2,232-drift),23,heal,true,Poly);ImpactLettering.Draw("HEAL",new(561,258-drift),12,heal,false,Poly);
    b.Append("<ellipse cx=\"578\" cy=\"283\" rx=\"26\" ry=\"7\" fill=\"none\" stroke=\"#99f5b0\" opacity=\".5\"/>");
    void Effect(string label,Vector2 p,Vector4 c)
    {
        var effect=new FloatEvent(1,0,"Imperil",label,"",0,1,0,true);float focus=NoticeStyle.Focus(effect,age);float font=25*focus;Vector2 size=new(ImpactLettering.Width(label,font),font);b.Append($"<g data-group=\"Debuff\" data-pivot=\"{N(p.X+size.X/2)},{N(p.Y)}\">");ImpactArt.Notice(p,size,c,age,.85f,Line,focus);ImpactLettering.Draw(label,p,font,c,true,Poly);b.Append("</g>");
    }
    Effect("LIGHT VULN",new(215,220-drift),new(.84f,.57f,1,1));Effect("FROST VULN",new(205,274-drift),new(.38f,.75f,1,1));Effect("IMPERIL",new(234,328-drift),new(1,.63f,.35f,1));
    void Quiet(string number,string caption,Vector2 center,Vector4 color)
    {
        float w=ImpactLettering.Width(number,18);ImpactLettering.Draw(number,center-new Vector2(w/2,drift),18,color,false,Poly);
        float cw=ImpactLettering.Width(caption,12);ImpactLettering.Draw(caption,center+new Vector2(-cw/2,21-drift),12,color,false,Poly);
    }
    Quiet("380","SNEAK ×3",new(785,455),new(.92f,.64f,1,.8f));
    Quiet("281","POISON ×3",new(785,510),acid);
    b.Append("<g data-group=\"Buff\" data-pivot=\"343,535\">");ImpactLettering.Draw("119",new(320,535),23,new(.45f,.78f,1,1),true,Poly);ImpactLettering.Draw("WARD READY",new(302,561),12,new(.45f,.78f,1,1),false,Poly);b.Append("</g>");
    Text("SPACE RESERVED FOR CHARACTER",459,650,11,muted);
    Text("COMPACT · DIGITS ONLY",24,681,11,muted);ImpactArt.Plate(new(24,700),new(145,46),new(.38f,.9f,.77f,1),age,Poly);ImpactLettering.Draw("1,473",new(32,705),36,new(.38f,.9f,.77f,1),true,Poly);
    Text("ULTRA · FLOAT LAYER · LOCK TO CLICK THROUGH",240,681,11,muted);ImpactLettering.Draw("1,473",new(240,705),36,new(.38f,.9f,.77f,1),true,Poly);
    Text("Synthetic design review · shared vector geometry · 100% scale",645,720,12,muted);
    return b.Append("</svg>").ToString();
}

static string GroupReview()
{
    var b=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"680\"><rect width=\"1200\" height=\"680\" fill=\"#18272d\"/>");
    string N(float n)=>n.ToString("0.##",CultureInfo.InvariantCulture);
    string C(Vector4 c)=>$"rgb({c.X*255:F0},{c.Y*255:F0},{c.Z*255:F0})";
    void Poly(Vector2[] p,Vector4 c)=>b.Append($"<polygon points=\"{string.Join(' ',p.Select(q=>N(q.X)+','+N(q.Y)))}\" fill=\"{C(c)}\" opacity=\"{N(c.W)}\"/>");
    void Line(Vector2 p,Vector2 q,Vector4 c,float w)=>b.Append($"<path d=\"M{N(p.X)} {N(p.Y)} L{N(q.X)} {N(q.Y)}\" stroke=\"{C(c)}\" stroke-width=\"{N(w)}\" opacity=\"{N(c.W)}\"/>");
    void Text(string s,float x,float y)=>b.Append($"<text x=\"{N(x)}\" y=\"{N(y)}\" font-size=\"14\" font-family=\"Arial,sans-serif\" fill=\"#9eb7bf\">{SecurityElement.Escape(s)}</text>");
    Text("ACTUAL VECTOR GEOMETRY / OPEN NOTICES / no notice backings",24,28);
    Text("Synthetic comparison. Background grid stays visible between glyphs. Each column scales text AND accents together.",24,52);
    for(int x=0;x<1200;x+=24)Line(new(x,75),new(x,635),new(.7f,.85f,.9f,.055f),1);
    for(int y=75;y<635;y+=24)Line(new(0,y),new(1200,y),new(.7f,.85f,.9f,.055f),1);
    float[] scales=[.65f,1,1.35f];
    for(int col=0;col<3;col++)
    {
        float scale=scales[col],cx=200+400*col;var a=new Appearance{BuffsScale=scale,DebuffsScale=scale,SkillsScale=scale};
        Text($"{scale:P0} group size",cx-65,95);
        var samples=new[]{new FloatEvent(1,0,"Ward","WARD READY","",119,1,0),new FloatEvent(2,0,"VulnFrost","FROST VULN","",0,1,0,true),new FloatEvent(3,0,"KineticReady","KINETIC READY","",0,1,0),new FloatEvent(4,0,"KineticRelease","KINETIC RELEASE","",558,1,0,false,false,"Melee/Missile")};
        for(int row=0;row<samples.Length;row++)
        {
            var e=samples[row];float m=NoticeStyle.Scale(e,a)*NoticeStyle.Focus(e,.18),baseSize=row==3?48:row==0?23:25,font=baseSize*m,y=135+row*130;
            Vector4 color=row switch{0=>new(.45f,.78f,1,1),1=>new(.38f,.75f,1,1),2=>new(.44f,.85f,1,1),_=>new(1,.83f,.3f,1)};
            string label=FloatStream.HasNumber(e)?ImpactLettering.Number(e):NoticeStyle.Label(e,.18);float w=ImpactLettering.Width(label,font);Vector2 p=new(cx-w/2,y),size=new(w,font);
            if(row==1)ImpactArt.Notice(p,size,color,.18,.85f,Line,m);
            if(row==2)ImpactArt.Charge(p,size,color,.18,.85f,Line,m);
            if(row==3)ImpactArt.Burst(p,size+new Vector2(0,21*m),color,.18,.85f,Line,Poly,m);
            ImpactLettering.Draw(label,p,font,color,true,Poly);
            if(FloatStream.HasNumber(e)){float cap=(row==3?18:12)*m,cw=ImpactLettering.Width(e.Label,cap);ImpactLettering.Draw(e.Label,new(cx-cw/2,y+font+3*m),cap,color,false,Poly);}
        }
    }
    Text("Rows: Buff / Debuff / Skill ready / Skill release. Ordinary damage, crits and healing retain their own size controls.",24,662);
    return b.Append("</svg>").ToString();
}
