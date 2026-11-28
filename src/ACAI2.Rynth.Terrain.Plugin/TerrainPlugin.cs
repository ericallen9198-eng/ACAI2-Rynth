using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RynthCore.PluginCore;

namespace ACAI2.Rynth.Terrain.Plugin;

// Planner only: never starts a macro, owns a movement key, recalls or fights.
// Native calls run exclusively on OnTick. Logout callbacks only set a flag.
public sealed class TerrainPlugin:RynthPluginBase
{
    public const string Version="0.4.0";
    public override uint MinimumApiVersion=>92;
    private volatile bool loggedOut;
    private int epoch,seenEpoch=-1;
    private uint player;
    private string identity="",fileKey="";
    private long handled=-1;
    private double nextTick;
    private Pending? pending;
    private Ground[]? walking;
    private long walkingGeneration;
    private string walkingPath="";
    private int blockedTicks;
    private readonly int instance=RandomNumberGenerator.GetInt32(1,int.MaxValue);
    private sealed record Pending(long Generation,string State,string Path,Ground Start,Ground[] Points,double Until);
    private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    private static readonly Ground[] Portals=ReadPortals();
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    public override int Initialize(){Log("[TerrainMesh] v"+Version+" loaded; plans for TerrainSurvey using stock RynthNav.");return 0;}
    public override void OnLogout(){loggedOut=true;Interlocked.Increment(ref epoch);}
    public override void OnLoginComplete(){Interlocked.Increment(ref epoch);loggedOut=false;}
    private static Ground[] ReadPortals()
    {
        using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Terrain.portals.json")!;
        using var doc=JsonDocument.Parse(s);
        return doc.RootElement.GetProperty("links").EnumerateArray().Select(l=>l.GetProperty("nearby_position"))
            .Select(p=>new Ground(p.GetProperty("east").GetDouble()*240+24468,p.GetProperty("north").GetDouble()*240+24468,p.GetProperty("z").GetDouble())).Distinct().ToArray();
    }
    public override void OnTick()
    {
        if(loggedOut||!IsAttached)return;
        int boundEpoch=Volatile.Read(ref epoch);
        if(seenEpoch!=boundEpoch){seenEpoch=boundEpoch;player=0;pending=null;walking=null;handled=-1;}
        double now=Now;if(now<nextTick)return;nextTick=now+.25;
        NativeBot? bot=null;long generation=0;
        try
        {
            uint id=Host.GetPlayerId();
            if(id==0||!Host.TryGetWorldName(out var server)||!Host.TryGetObjectName(id,out var name))return;
            string live=server+"\n"+name;
            if(player!=id||identity!=live)
            {
                player=id;identity=live;handled=-1;pending=null;walking=null;blockedTicks=0;
                fileKey=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(live)))[..16];
            }
            bot=new(Host);
            string meta=bot.Eval("vtgetmeta[]").Replace('\\','/').Split('/').Last();
            if(!meta.Equals("Terrain Access Survey",StringComparison.OrdinalIgnoreCase)&&!meta.Equals("Terrain Access Survey.af",StringComparison.OrdinalIgnoreCase))
            {pending=null;walking=null;return;}
            Set(bot,"te_mesh_schema",1);Set(bot,"te_mesh_heartbeat",DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Set(bot,"te_mesh_instance",instance);
            string state=bot.Eval("vtgetmetastate[]");
            generation=(long)Num(bot,"getvar[te_mesh_request]");
            bool fresh=Num(bot,"getvar[te_logger_active]")==1&&Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds()-Num(bot,"getvar[te_logger_heartbeat]"))<=5;
            bool waiting=state.StartsWith("WAIT_",StringComparison.Ordinal)||state.StartsWith("RETURN_WAIT_",StringComparison.Ordinal);
            bool ready=fresh&&waiting&&bot.Running&&Host.HasIsPortaling&&!Host.IsPortaling()&&Num(bot,"getcharvital_current[1]")>0&&Num(bot,"getvar[te_recovery_busy]")==0;
            if(!ready){pending=null;walking=null;return;}
            if(generation<=0){handled=-1;pending=null;walking=null;return;}
            var path=new NativePath(Host);
            var pose=Pose();
            if(pending is { } p)
            {
                if(p.Generation!=generation||p.State!=state){pending=null;return;}
                if(pose.Flat(p.Start)>3||Math.Abs(pose.Z-p.Start.Z)>4){Fail(bot,generation,-2);pending=null;return;}
                if(bot.Setting("CurrentNavPath").Equals(p.Path,StringComparison.OrdinalIgnoreCase))
                {
                    walking=p.Points;walkingGeneration=generation;walkingPath=p.Path;blockedTicks=0;
                    Set(bot,"te_mesh_status",1);Set(bot,"te_mesh_points",p.Points.Length);Set(bot,"te_mesh_ack",generation);pending=null;
                }
                else if(now>p.Until){Fail(bot,generation,-3);pending=null;}
                return;
            }
            if(generation>0&&generation!=handled&&Num(bot,"getvar[te_can_travel]")==1)
            {
                handled=generation;walking=null;Set(bot,"te_mesh_blocked",0);
                bot.Eval("setvar[te_mesh_error,``]");
                if(Num(bot,"ragetsetting[EnableNavigation]")==1)throw new InvalidOperationException("Survey nav must be disabled before planning");
                Ground goal=new(Num(bot,"getvar[te_mesh_east]")*240+24468,Num(bot,"getvar[te_mesh_north]")*240+24468,Num(bot,"getvar[te_mesh_height]"));
                int variant=checked((int)Num(bot,"getvar[te_mesh_variant]"));
                var points=MeshPlan.Build(pose,goal,variant,path.Find,path.Clear,Portals);
                string folder=NavFolder(bot);Directory.CreateDirectory(folder);
                string filename="Terrain_"+fileKey+"_"+(generation%2)+".nav";
                string destination=Path.Combine(folder,filename),temporary=destination+".tmp";
                File.WriteAllText(temporary,MeshPlan.Serialize(points),new UTF8Encoding(false));File.Move(temporary,destination,true);
                if(loggedOut||Volatile.Read(ref epoch)!=boundEpoch||Host.GetPlayerId()!=id||!bot.Running
                    ||bot.Eval("vtgetmetastate[]")!=state||Num(bot,"getvar[te_mesh_request]")!=generation
                    ||Num(bot,"getvar[te_can_travel]")!=1)return;
                bot.Eval("setvar[te_mesh_name,`"+Path.GetFileNameWithoutExtension(filename)+"`]");
                bot.Load("ACAI2-Terrain\\"+filename);
                pending=new(generation,state,destination,pose,points,now+5);
                Log($"[TerrainMesh] planned request {generation}: variant {variant}, {points.Length} checked corners.");
                return;
            }
            if(walking!=null&&walkingGeneration==generation&&Num(bot,"getvar[te_mesh_walking]")==1&&Num(bot,"getvar[te_can_travel]")==1)
            {
                if(!bot.Setting("CurrentNavPath").Equals(walkingPath,StringComparison.OrdinalIgnoreCase))
                {Set(bot,"te_mesh_blocked",generation);walking=null;return;}
                using var d=JsonDocument.Parse(bot.Eval("ranav[]"));
                int index=d.RootElement.GetProperty("index").GetInt32();
                bool blocked=index<0||index>=walking.Length||!path.Clear(pose,walking[Math.Clamp(index,0,walking.Length-1)]);
                blockedTicks=blocked?blockedTicks+1:0;
                if(blockedTicks>=2)
                {
                    bot.Eval("vtsetsetting[EnableNavigation,0]");Set(bot,"te_mesh_blocked",generation);walking=null;
                    Log($"[TerrainMesh] request {generation}: live raycast blocked; movement held for replanning.");
                }
            }
        }
        catch(Exception ex)
        {
            if(bot!=null&&generation>0)try
            {
                if(walking!=null&&bot.Setting("CurrentNavPath").Equals(walkingPath,StringComparison.OrdinalIgnoreCase))
                {bot.Eval("vtsetsetting[EnableNavigation,0]");Set(bot,"te_mesh_blocked",generation);}
                string detail=new string(ex.Message.Where(c=>char.IsAsciiLetterOrDigit(c)||c is ' ' or '.' or ',' or '(' or ')' or '/' or ':' or '-').Take(240).ToArray());
                bot.Eval("setvar[te_mesh_error,`"+detail+"`]");handled=generation;Fail(bot,generation,-1);
            }catch{}
            pending=null;walking=null;Log("[TerrainMesh] "+ex.Message);nextTick=now+1;
        }
    }
    private Ground Pose()
    {
        if(!Host.TryGetPlayerPose(out uint cell,out float x,out float y,out float z,out _,out _,out _,out _)||(cell&0xffff)>=0x100)
            throw new InvalidOperationException("An outdoor pose is required for survey mesh travel");
        return new((cell>>24)*192d+x,((cell>>16)&255)*192d+y,z);
    }
    private static double Num(NativeBot bot,string expr)
    {
        if(!double.TryParse(bot.Eval(expr),NumberStyles.Float,Inv,out double n)||!double.IsFinite(n))
            throw new InvalidDataException("Invalid native numeric response");
        return n;
    }
    private static void Set(NativeBot bot,string key,double n)=>bot.Eval("setvar["+key+","+n.ToString("R",Inv)+"]");
    private static void Fail(NativeBot bot,long generation,int status)
    {
        Set(bot,"te_mesh_status",status);Set(bot,"te_mesh_ack",generation);
    }
    private static string NavFolder(NativeBot bot)
    {
        // The active AF supplies the actual data root, including non-C installations.
        string path=bot.Setting("CurrentMetaPath");
        string? parent=Path.GetDirectoryName(path);
        if(parent==null||!Path.GetFileName(parent).Equals("MetaFiles",StringComparison.OrdinalIgnoreCase)
            ||!File.Exists(path)||!Path.GetFileName(path).Equals("Terrain Access Survey.af",StringComparison.OrdinalIgnoreCase))
            throw new DirectoryNotFoundException("Cannot discover active survey MetaFiles folder");
        return Path.Combine(Path.GetDirectoryName(parent)!,"NavProfiles","ACAI2-Terrain");
    }
}
