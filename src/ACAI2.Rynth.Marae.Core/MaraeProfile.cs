using System.Text.Json;
namespace ACAI2.Rynth.Marae.Core;
public sealed record AreaOptions(int Level,bool Enabled=true,bool Maps=false,bool HuntOnly=false);
public sealed record MaraeOptions(AreaOptions[] Areas,bool Mesh=false,int Close=30,int Far=130,bool CampLimit=false,bool Fallback=true)
{
    public static MaraeOptions Default=>new([new(180),new(225),new(275)]);
    public MaraeOptions Validate()
    {
        if(Areas.Length!=3||!Areas.Select(a=>a.Level).SequenceEqual(new[]{180,225,275}))throw new InvalidDataException("Invalid area preferences.");
        return this with {Close=Math.Clamp(Close,10,60),Far=Math.Clamp(Far,60,180)};
    }
}
public sealed class MaraeProfile
{
    public string Server {get;set;}="";
    public string Character {get;set;}="";
    public MaraeOptions Options {get;set;}=MaraeOptions.Default;
    public Dictionary<string,double> Progress {get;set;}=new();
    public Dictionary<string,Contract> Contracts {get;set;}=new();
    public PendingPickup? PendingMove {get;set;}
    public bool Imported {get;set;}
    public bool Active {get;set;}
    public bool Visible {get;set;}=true;
    public float X {get;set;}=240;
    public float Y {get;set;}=120;
    public string Folder {get;set;}=@"C:\Games\RynthSuite\RynthAi";
    public static MaraeProfile Load(string path,string server,string character)
    {
        if(!File.Exists(path))return new(){Server=server,Character=character};
        if(new FileInfo(path).Length>131072)throw new InvalidDataException("Saved Marae profile exceeds its size bound.");
        var p=JsonSerializer.Deserialize<MaraeProfile>(File.ReadAllText(path))??throw new InvalidDataException("Empty Marae profile.");
        if(p.Server!=server||p.Character!=character||p.Progress.Count>100||p.Contracts.Count>27)throw new InvalidDataException("Saved Marae identity or bounds mismatch.");
        p.Options=p.Options.Validate();return p;
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);
    }
}
public sealed record Contract(string Status,string Target="");
public static class NpcDialogue
{
    public static string? CompletionTarget(string line)
    {
        const string prefix="You have killed ",suffix="! Your task is complete!";
        if(!line.StartsWith(prefix,StringComparison.Ordinal)||!line.EndsWith(suffix,StringComparison.Ordinal)||line.Length<=prefix.Length+suffix.Length)return null;
        string middle=line[prefix.Length..^suffix.Length];int space=middle.IndexOf(' ');
        return space>0&&int.TryParse(middle[..space],out int count)&&count>=0?middle[(space+1)..]:null;
    }
    public static Contract? Parse(string body)
    {
        if(body.StartsWith("Slay ")&&body.Contains("Return when the work is done."))
        {int start=body.IndexOf(' ',5);return new("in progress",start<0?"":body[(start+1)..].Split('.')[0]);}
        if(body.StartsWith("You have defeated ")&&body.Contains("The hunt is not yet finished"))
        {int of=body.IndexOf(" of ",StringComparison.Ordinal),start=of<0?-1:body.IndexOf(' ',of+4);return new("in progress",start<0?"":body[(start+1)..].Split('.')[0]);}
        if(body.StartsWith("Your hunt for today is done.")||(body.StartsWith("Return in ")&&body.Contains("for another hunt")))return new("cooldown");
        if(body.StartsWith("You have served the Hea well."))return new("rewarded");
        if(body.StartsWith("The hunt is complete"))return new("completed");
        return null;
    }
    public static bool SameTarget(string killed,string target)=>target.Length>0&&(killed.Equals(target,StringComparison.OrdinalIgnoreCase)||killed.Equals(target+"s",StringComparison.OrdinalIgnoreCase)||killed.Equals(target+"es",StringComparison.OrdinalIgnoreCase)||(target.EndsWith('y')&&killed.Equals(target[..^1]+"ies",StringComparison.OrdinalIgnoreCase)));
}
