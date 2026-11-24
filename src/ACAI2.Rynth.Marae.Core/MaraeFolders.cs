using System.Text.Json;
using RynthCore.Install;
namespace ACAI2.Rynth.Marae.Core;

public static class MaraeFolders
{
    public static string OfficialRoot=>RynthInstallPaths.RynthAiDir;
    public static bool IsDataRoot(string? folder)=>!string.IsNullOrWhiteSpace(folder)&&Path.IsPathFullyQualified(folder)&&Directory.Exists(folder)&&
        new[]{"MetaFiles","NavProfiles","SettingsProfiles","LootProfiles"}.Any(name=>Directory.Exists(Path.Combine(folder,name)));
    public static string InstallerHint(string assemblyFolder)
    {
        if(string.IsNullOrWhiteSpace(assemblyFolder)||!Path.IsPathFullyQualified(assemblyFolder))return "";
        try
        {
            string file=Path.Combine(assemblyFolder,"installation.json");if(!File.Exists(file)||new FileInfo(file).Length>8192)return "";
            using var doc=JsonDocument.Parse(File.ReadAllText(file));string? path=doc.RootElement.GetProperty("RynthAiFolder").GetString();
            return IsDataRoot(path)?Path.GetFullPath(path!):"";
        }
        catch(IOException){return "";}catch(UnauthorizedAccessException){return "";}catch(JsonException){return "";}catch(KeyNotFoundException){return "";}catch(InvalidOperationException){return "";}catch(ArgumentException){return "";}
    }
    public static string Resolve(string configured,string installed,string? currentMeta,string? currentNav,string official)
    {
        // Rynth's actual loaded profile paths take precedence over stale installer/preferences.
        foreach(string? active in new[]{currentMeta,currentNav})
        {
            if(string.IsNullOrWhiteSpace(active)||!Path.IsPathFullyQualified(active))continue;
            string? parent=Path.GetDirectoryName(active);
            for(int depth=0;parent!=null&&depth<10;depth++,parent=Path.GetDirectoryName(parent))
                if(Path.GetFileName(parent).Equals("MetaFiles",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(parent).Equals("NavProfiles",StringComparison.OrdinalIgnoreCase))
                {string? root=Path.GetDirectoryName(parent);if(IsDataRoot(root))return Path.GetFullPath(root!);break;}
        }
        foreach(string path in new[]{configured,installed,official})if(IsDataRoot(path))return Path.GetFullPath(path);
        return "";
    }
}
