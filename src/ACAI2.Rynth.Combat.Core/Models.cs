using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ACAI2.Rynth.Combat.Core;

public enum MeterMode { Normal,Compact,UltraCompact }

public sealed class Appearance
{
    public int Schema {get;set;}=1;
    public MeterMode MeterMode {get;set;}
    public bool DigitsLocked {get;set;}=true;
    public float DigitsX {get;set;}=30;
    public float DigitsY {get;set;}=120;
    public float DigitsSize {get;set;}=36;
    public float PackIdleSeconds {get;set;}=4;
    public bool Visible {get;set;}=true;
    public bool Floating {get;set;}=true;
    public bool Locked {get;set;}
    public bool CustomLettering {get;set;}=true;
    public bool Bold {get;set;}=true;
    public bool Subdued {get;set;}
    public float X {get;set;}=30;
    public float Y {get;set;}=120;
    public float Opacity {get;set;}=.72f;
    public float Scale {get;set;}=1;
    public float FontSize {get;set;}=23;
    public float Outline {get;set;}=1.3f;
    public float RegularScale {get;set;}=.85f;
    public float RegularAlpha {get;set;}=.65f;
    public float CritScale {get;set;}=1.35f;
    public float HealOffset {get;set;}=150;
    public float Gap {get;set;}=120;
    public float Curve {get;set;}=65;
    public float Rise {get;set;}=145;
    public float Lifetime {get;set;}=2.6f;
    public float Overlap {get;set;}=.35f;
    public float Intensity {get;set;}=.85f;
    public float AnchorX {get;set;}=.5f;
    public float AnchorY {get;set;}=.65f;
    public bool AutoThread {get;set;}=true;
    public int BigHitMinimum {get;set;}=100;
    public Dictionary<string,bool> Modules {get;set;}=[]; // Missing = automatic discovery.
    public Dictionary<string,uint> Colors {get;set;}=Defaults();
    public static Dictionary<string,uint> Defaults()=>new(){["KineticReady"]=0xffffd970,["KineticRelease"]=0xff51dafa,["VulnLightning"]=0xffff91d7,["VulnFrost"]=0xffffbf62,["VulnFire"]=0xff509dff,["VulnAcid"]=0xff6ae67a,["VulnNether"]=0xffe28bc4,["VulnBludgeon"]=0xff91b8d6,["VulnSlash"]=0xffd2d4dc,["VulnPierce"]=0xffb0c7e0,["Imperil"]=0xff68a3ff,["Direct"]=0xffece8e3,["Critical"]=0xff51dafa,["Sneak"]=0xffffa0df,["Acid"]=0xff86f9be,["Poison"]=0xff5fd779,["Healing"]=0xffb4f59a,["Taken"]=0xff7774ff,["Evade"]=0xffdfd0bd,["Resist"]=0xffffbb95,["Ward"]=0xffffc774,["Utility"]=0xffeec3dd,["Summons"]=0xffffb77b,["Thorns"]=0xff85cb87,["Drain"]=0xffb797ff,["Kill"]=0xff7ceac1,["Death"]=0xff6565ff,["Fire"]=0xff63a8ff,["Cold"]=0xffffe08a,["Electrical"]=0xff9cf3ff,["Nether"]=0xffe694cb};
    public void Validate()
    {
        if(Schema!=1)throw new InvalidDataException("Unsupported Combat settings schema");
        float Clamp(float v,float lo,float hi,float fallback)=>float.IsFinite(v)?Math.Clamp(v,lo,hi):fallback;
        if(!Enum.IsDefined(MeterMode))MeterMode=MeterMode.Normal;
        DigitsX=Clamp(DigitsX,0,16000,30);DigitsY=Clamp(DigitsY,0,16000,120);DigitsSize=Clamp(DigitsSize,18,90,36);PackIdleSeconds=Clamp(PackIdleSeconds,2,12,4);
        BigHitMinimum=Math.Clamp(BigHitMinimum,1,10000);
        RegularScale=Clamp(RegularScale,.6f,1,.85f);RegularAlpha=Clamp(RegularAlpha,.25f,1,.65f);CritScale=Clamp(CritScale,1.05f,1.8f,1.35f);HealOffset=Clamp(HealOffset,0,300,150);
        X=Clamp(X,0,16000,30);Y=Clamp(Y,0,16000,120);Opacity=Clamp(Opacity,0,1,.72f);Scale=Clamp(Scale,.7f,2,1);
        FontSize=Clamp(FontSize,12,42,23);Outline=Clamp(Outline,0,3,1.3f);Gap=Clamp(Gap,40,300,120);Curve=Clamp(Curve,0,160,65);Rise=Clamp(Rise,30,300,145);Lifetime=Clamp(Lifetime,.7f,5,2.6f);Overlap=Clamp(Overlap,0,1,.35f);Intensity=Clamp(Intensity,0,1,.85f);AnchorX=Clamp(AnchorX,.05f,.95f,.5f);AnchorY=Clamp(AnchorY,.1f,.95f,.65f);
        Modules??=[];Modules=Modules.Where(p=>p.Key.Length<64).Take(32).ToDictionary();
        Colors??=[];var defaults=Defaults();foreach(var p in defaults)Colors.TryAdd(p.Key,p.Value);Colors=Colors.Where(p=>defaults.ContainsKey(p.Key)).ToDictionary();
    }
}
public static class Profiles
{
    public static string Folder(string root,string server,string name)=>Path.Combine(root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(server.ToUpperInvariant()+"\0"+name.ToUpperInvariant()))));
    public static Appearance Load(string path)
    {if(!File.Exists(path))return new();if(new FileInfo(path).Length>32768)throw new InvalidDataException("Profile too large");var a=JsonSerializer.Deserialize<Appearance>(File.ReadAllText(path))??throw new InvalidDataException("Empty profile");a.Validate();return a;}
    public static void Save(string path,object value)
    {Directory.CreateDirectory(Path.GetDirectoryName(path)!);string tmp=path+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(value));File.Move(tmp,path,true);}
}
public sealed record DeathRecord(DateTime Utc,string Killer,int? Blow,int? Health,int? Maximum,int? Overkill,string Message,HitContext? Context=null);
public sealed class TaggedHits
{
    public int Count {get;set;}
    public long Damage {get;set;}
    public int? Low {get;set;}
    public int High {get;set;}
    public double Average=>Damage/(double)Math.Max(1,Count);
    public void Add(int amount){Count++;Damage+=amount;Low=Low.HasValue?Math.Min(Low.Value,amount):amount;High=Math.Max(High,amount);}
}
public sealed class MobStats
{
    public int Seen {get;set;}
    public int Kills {get;set;}
    public long Dealt {get;set;}
    public long PhysicalDamage {get;set;}
    public long UnclassifiedDamage {get;set;}
    public int UnclassifiedHits {get;set;}
    public int UnclassifiedHigh {get;set;}
    public int PhysicalHits {get;set;}
    public int PhysicalHigh {get;set;}
    public int Evades {get;set;}
    public long MagicDamage {get;set;}
    public int MagicHits {get;set;}
    public int MagicHigh {get;set;}
    public int Resists {get;set;}
}
public sealed class Totals
{
    public long Damage {get;set;}
    public long PeriodicTaken {get;set;}
    public int PeriodicTicks {get;set;}
    public int PeriodicHigh {get;set;}
    public long Taken {get;set;}
    public long Healing {get;set;}
    public TaggedHits RecklessOutgoing {get;set;}=new();
    public TaggedHits RecklessIncoming {get;set;}=new();
    public int KineticReady {get;set;}
    public int KineticReleases {get;set;}
    public int KineticExpired {get;set;}
    public int DirectHits {get;set;}
    public int Crits {get;set;}
    public int Sneaks {get;set;}
    public long SneakDamage {get;set;}
    public long PhysicalDamage {get;set;}
    public long UnclassifiedDamage {get;set;}
    public int UnclassifiedHits {get;set;}
    public int UnclassifiedHigh {get;set;}
    public int PhysicalHits {get;set;}
    public int PhysicalHigh {get;set;}
    public int Evades {get;set;}
    public long MagicDamage {get;set;}
    public int MagicHits {get;set;}
    public int MagicHigh {get;set;}
    public int Resists {get;set;}
    public int DeathCount {get;set;}
    public int Blocks {get;set;}
    public int Kills {get;set;}
    public int WardCapacity {get;set;}
    public long WardAbsorbed {get;set;}
    public Dictionary<string,long> Components {get;set;}=[];
    public Dictionary<string,int> Procs {get;set;}=[];
    public Dictionary<string,int> IncomingEffects {get;set;}=[];
    public Dictionary<string,long> HealingSources {get;set;}=[];
    public Dictionary<string,MobStats> Mobs {get;set;}=[];
    public List<DeathRecord> Deaths {get;set;}=[];
    public List<IncomingHit> BigHits {get;set;}=[];
    public int HighestObserved=>Math.Max(Math.Max(PhysicalHigh,MagicHigh),Math.Max(Math.Max(UnclassifiedHigh,PeriodicHigh),Math.Max(BigHits.Count>0?BigHits.Max(h=>h.Damage):0,Deaths.Count>0?Deaths.Max(d=>d.Blow??0):0)));
    public bool Truncated {get;set;}
    public MobStats Mob(string name)
    {name=name.Length>160?name[..160]:name;if(name.Length==0)name="Unattributed";if(!Mobs.ContainsKey(name)&&Mobs.Count>=256){Truncated=true;name="Other types (limit)";}return Mobs.TryGetValue(name,out var m)?m:Mobs[name]=new();}
    public Totals Copy()=>JsonSerializer.Deserialize<Totals>(JsonSerializer.Serialize(this))!;
    public double EvadePercent=>100d*Evades/Math.Max(1,PhysicalHits+Evades);
    public double ResistPercent=>100d*Resists/Math.Max(1,MagicHits+Resists);
    public double RecklessOutgoingPercent=>100d*RecklessOutgoing.Count/Math.Max(1,DirectHits);
    public double RecklessIncomingPercent=>100d*RecklessIncoming.Count/Math.Max(1,PhysicalHits);
    public double SneakPercent=>100d*Sneaks/Math.Max(1,DirectHits);
}
public sealed class RunRecord
{
    public int Schema {get;set;}=1;
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Server {get;set;}="";
    public string Character {get;set;}="";
    public string Name {get;set;}="";
    public string ParserVersion {get;set;}="0.1.0";
    public string Realm {get;set;}="";
    public DateTime Opened {get;set;}
    public DateTime? Entered {get;set;}
    public DateTime? Cleared {get;set;}
    public DateTime? Ended {get;set;}
    public double Observed {get;set;}
    public bool Gaps {get;set;}
    public int? KilledProgress {get;set;}
    public int? MonsterTotal {get;set;}
    public Totals Stats {get;set;}=new();
    public double? CompletionSeconds=>Entered.HasValue&&Cleared.HasValue?Math.Max(0,(Cleared.Value-Entered.Value).TotalSeconds):null;
}
