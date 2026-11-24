namespace ACAI2.Rynth.MapHunt.Core;
public static class MapHuntWorkflow
{
    public static string Folder(string configured,string? currentMeta,string? currentNav)
    {
        if(!string.IsNullOrWhiteSpace(configured))return Path.GetFullPath(configured);
        foreach(string? current in new[]{currentMeta,currentNav})
        {
            if(string.IsNullOrWhiteSpace(current)||!Path.IsPathFullyQualified(current))continue;
            string? parent=Path.GetDirectoryName(current);
            if(parent!=null&&(Path.GetFileName(parent).Equals("MetaFiles",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(parent).Equals("NavProfiles",StringComparison.OrdinalIgnoreCase)))return Path.GetDirectoryName(parent)!;
        }
        return @"C:\Games\RynthSuite\RynthAi";
    }
    public static void Install(string folder,string content)
    {
        if(!Directory.Exists(folder))throw new DirectoryNotFoundException("RynthAi folder not found. Set its actual location under Advanced: "+folder);
        string target=Path.Combine(folder,"MetaFiles","MapHunt App.af");
        if(File.Exists(target)&&File.ReadAllText(target)==content)return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if(File.Exists(target))File.Copy(target,target+".previous",true);
        string temp=target+".tmp";File.WriteAllText(temp,content);File.Move(temp,target,true);
    }
}
