using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACAI2.Protocol;

namespace ACAI2.Rynth.Core;

public sealed class CombatAppearance
{
    public int Schema { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public bool Floating { get; set; }
    public float X { get; set; } = 30;
    public float Y { get; set; } = 120;
    public float Scale { get; set; } = 1;
    public float Opacity { get; set; } = .72f;
    public float TextSize { get; set; } = 22;
    public float Outline { get; set; } = 1;
    public float Curvature { get; set; } = 45;
    public float LaneGap { get; set; } = 100;
    public float Lifetime { get; set; } = 2.4f;
    public float Rise { get; set; } = 110;
    public float AnchorX { get; set; } = .5f;
    public float AnchorY { get; set; } = .55f;
    public Dictionary<string,uint> Colors { get; set; } = new()
    { ["Poison"]=0xff65d765, ["Acid"]=0xff52e6b5, ["Healing"]=0xffc8df60,
      ["Critical"]=0xff55dafa, ["Sneak"]=0xffff91d0, ["Direct"]=0xffe6e6e6 };
    public void Validate()
    {
        if(Schema!=1) throw new InvalidDataException("Unsupported combat UI profile version.");
        X=Clamp(X,0,16000,30); Y=Clamp(Y,0,16000,120);
        Scale=Clamp(Scale,.7f,2,1); Opacity=Clamp(Opacity,0,1,.72f);
        TextSize=Clamp(TextSize,14,40,22); Outline=Clamp(Outline,0,2,1);
        Curvature=Clamp(Curvature,0,100,45); LaneGap=Clamp(LaneGap,50,250,100);
        Lifetime=Clamp(Lifetime,1,5,2.4f); Rise=Clamp(Rise,30,250,110);
        AnchorX=Clamp(AnchorX,.05f,.95f,.5f); AnchorY=Clamp(AnchorY,.1f,.9f,.55f);
        var defaults=new CombatAppearance().Colors;
        Colors ??= defaults;
        foreach(var pair in defaults) if(!Colors.ContainsKey(pair.Key)) Colors[pair.Key]=pair.Value;
        Colors=Colors.Where(x=>defaults.ContainsKey(x.Key)).ToDictionary();
    }
    private static float Clamp(float v,float min,float max,float fallback)=>float.IsFinite(v)?Math.Clamp(v,min,max):fallback;
}

public static class CombatProfiles
{
    public static string PathFor(string root,GameIdentity identity)
    {
        identity.Validate();
        // Hash server and character separately; never treat a player name as a filesystem path.
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.Server.ToUpperInvariant()+"\0"+identity.Character.ToUpperInvariant())));
        return Path.Combine(root,"CombatProfiles",key+".json");
    }
    public static CombatAppearance Load(string path)
    {
        if(!File.Exists(path)) return new();
        if(new FileInfo(path).Length>32768) throw new InvalidDataException("Combat profile is too large.");
        var settings=JsonSerializer.Deserialize(File.ReadAllText(path),CombatJson.Default.CombatAppearance) ?? throw new InvalidDataException("Empty combat profile.");
        settings.Validate(); return settings;
    }
    public static void Save(string path,CombatAppearance settings)
    {
        settings.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temporary,JsonSerializer.Serialize(settings,CombatJson.Default.CombatAppearance)); File.Move(temporary,path,true); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed record CombatReading(double Dps,double Taken,long Damage,long Healing,long SneakDamage,int Sneaks,int DirectHits,
    Dictionary<string,long> Components,FloatEvent[] Text);
public sealed record FloatEvent(long Id,double Start,string Kind,string Target,long Amount,int Count,int Slot);

// Session totals are incremental; 60-second rates use a fixed bucket ring. No raw log history is kept.
public sealed class CombatDisplay
{
    private readonly CombatMessageParser parser=new();
    private readonly long[] seconds=Enumerable.Repeat(long.MinValue,61).ToArray(), damage=new long[61], taken=new long[61];
    private readonly Dictionary<string,long> components=new();
    private readonly List<FloatEvent> text=new();
    private long total,healing,sneakDamage,serial;
    private int sneaks,directHits,slot;
    private double? first;
    public bool Observe(string raw,string character,double now)
    {
        if(raw.Length>4096 || !double.IsFinite(now) || !parser.TryParse(raw,character,out var e)) return false;
        Add(e,now); return true;
    }
    public void Add(CombatEventRecord e,double now)
    {
        if(!double.IsFinite(now)||now<0) return;
        if(e.Result=="Damage" && e.Damage>0)
        {
            bool outgoing=e.Direction=="Outgoing";
            if(!outgoing && e.Direction!="Incoming") return;
            first??=now;
            long second=(long)now; int i=(int)(second%61);
            if(seconds[i]!=second) {seconds[i]=second;damage[i]=taken[i]=0;}
            if(outgoing)
            {
                damage[i]+=e.Damage; total+=e.Damage;
                components[e.Component]=components.GetValueOrDefault(e.Component)+e.Damage;
                if(CombatComponents.IsDirect(e)) {directHits++;if(e.SneakAttack){sneaks++;sneakDamage+=e.Damage;}}
                string kind=e.SneakAttack?"Sneak":e.Critical?"Critical":e.Component;
                if(kind!="Direct") AddText(kind,e.Mob,e.Damage,now);
            }
            else taken[i]+=e.Damage;
        }
        else if(e.Result=="Healing" && e.Amount>0) {healing+=e.Amount;AddText("Healing",e.Recipient,e.Amount,now);}
    }
    private void AddText(string kind,string target,long amount,double now)
    {
        text.RemoveAll(x=>now-x.Start>5);
        int index=text.FindLastIndex(x=>x.Kind==kind&&x.Target==target&&now>=x.Start&&now-x.Start<=.15);
        if(index>=0){var old=text[index];text[index]=old with {Amount=old.Amount+amount,Count=old.Count+1};return;}
        text.Add(new(++serial,now,kind,target,amount,1,slot++%4));
        if(text.Count>16)text.RemoveAt(0);
    }
    public CombatReading Read(double now,float lifetime)
    {
        long outgoing=0,incoming=0;long second=(long)now;
        for(int i=0;i<61;i++) if(seconds[i]>second-60&&seconds[i]<=second){outgoing+=damage[i];incoming+=taken[i];}
        double elapsed=first.HasValue?Math.Clamp(now-first.Value,1,60):1;
        return new(outgoing/elapsed,incoming/elapsed,total,healing,sneakDamage,sneaks,directHits,new(components),text.Where(x=>now>=x.Start&&now-x.Start<lifetime).ToArray());
    }
    public static (float X,float Y,float Alpha) Arc(double age,float lifetime,bool left,float gap,float curvature,float rise)
    {
        float p=Math.Clamp((float)(age/lifetime),0,1);
        return ((left?-1:1)*(gap-Math.Min(curvature,Math.Max(0,gap-24))*p*p),-rise*p,Math.Clamp((1-p)*4,0,1));
    }
}
