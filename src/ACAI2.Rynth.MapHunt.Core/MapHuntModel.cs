using System.Globalization;
using System.Text.Json;
namespace ACAI2.Rynth.MapHunt.Core;

public interface IMapHuntBot
{
    string Evaluate(string expression);
    string? Setting(string name);
    bool Running {get;}
    void Command(string command);
    bool GoTo(double north,double east);
}
public sealed record MapHuntPreferences(bool Mesh=false,int Close=30,int Far=130)
{
    public MapHuntPreferences Validated()=>this with {Close=Math.Clamp(Close,10,60),Far=Math.Clamp(Far,60,180)};
}
public sealed record MapHuntEvent(string Anchor,double Deadline,double Doubloons,string State);
public sealed class MapHuntProfile
{
    public int Schema {get;set;}=1;
    public string Server {get;set;}="";
    public string Character {get;set;}="";
    public MapHuntPreferences Preferences {get;set;}=new();
    public bool Active {get;set;}
    public MapHuntEvent? Event {get;set;}
    public bool Visible {get;set;}=true;
    public float X {get;set;}=260;
    public float Y {get;set;}=120;
    public string RynthAiFolder {get;set;}="";
    public void Validate(string server,string character)
    {
        if(Schema!=1||Server!=server||Character!=character)throw new InvalidDataException("MapHunt profile identity/schema mismatch");
        Preferences=(Preferences??new()).Validated();
        if(RynthAiFolder==null||RynthAiFolder.Length>1024)throw new InvalidDataException("Invalid RynthAi folder");
        if(!float.IsFinite(X)||X<0)X=260;if(!float.IsFinite(Y)||Y<0)Y=120;
        if(Event!=null&&(!MapHuntGeometry.TryCoordinates(Event.Anchor,out _,out _)||!double.IsFinite(Event.Deadline)||!double.IsFinite(Event.Doubloons)||Event.Doubloons<0||Event.State.Length>64))throw new InvalidDataException("Invalid encounter checkpoint");
    }
    public static MapHuntProfile Load(string path,string server,string character)
    {
        if(!File.Exists(path))return new(){Server=server,Character=character};
        if(new FileInfo(path).Length>16384)throw new InvalidDataException("MapHunt profile exceeds its size limit");
        var p=JsonSerializer.Deserialize<MapHuntProfile>(File.ReadAllText(path))??throw new InvalidDataException("Empty MapHunt profile");p.Validate(server,character);return p;
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);
    }
}
public static class MapHuntGeometry
{
    public static string Number(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
    public static bool TryCoordinates(string? text,out double ns,out double ew)
    {
        ns=ew=0;var parts=(text??"").Split('|');
        return parts.Length==3&&double.TryParse(parts[0],NumberStyles.Float,CultureInfo.InvariantCulture,out ns)&&double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out ew)&&double.TryParse(parts[2],NumberStyles.Float,CultureInfo.InvariantCulture,out double z)&&double.IsFinite(ns)&&double.IsFinite(ew)&&double.IsFinite(z)&&Math.Abs(ns)<=103&&Math.Abs(ew)<=103;
    }
    public static (double NS,double EW)? Approach(string target,string here)
    {
        if(!TryCoordinates(target,out double ns,out double ew)||!TryCoordinates(here,out double pn,out double pe))return null;
        double dn=pn-ns,de=pe-ew,yards=Math.Sqrt(dn*dn+de*de)*240;
        return yards>3?(ns+dn*2.5/yards,ew+de*2.5/yards):null;
    }
}
public sealed record MapHuntView(string Phase,string Detail,string State,bool Running,bool Review,MapHuntPreferences Preferences)
{
    public static MapHuntView Idle {get;}=new("Ready","Choose your route and press Start / Resume.","",false,false,new());
}
