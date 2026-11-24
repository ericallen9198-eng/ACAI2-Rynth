using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ACAI2.Rynth.MapHunt.Core;
using RynthCore.PluginCore;
namespace ACAI2.Rynth.MapHunt.Plugin;

public sealed class MapHuntPlugin:RynthPluginBase
{
    public const string Version="0.1.0";
    public override uint MinimumApiVersion=>92;
    private sealed record Request(long Epoch,string Command,MapHuntPreferences? Preferences=null,bool Visible=true,float X=0,float Y=0,string Folder="");
    private readonly ConcurrentQueue<Request> requests=new();
    private readonly MapHuntPanel panel=new();
    private MapHuntProfile? profile;
    private NativeMapHuntBot? bot;
    private MapHuntController? controller;
    private MapHuntView view=MapHuntView.Idle;
    private string character="Waiting for login",notice="Log into DreamWeave. RynthAi and mission API 6 support are required.",path="";
    private long epoch;
    private uint player;
    private int lifecycle;
    private volatile bool waiting;
    private bool ready,profileReadable=true,renderFailed,deferredStop;
    private double nextProbe,nextStep;
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    private string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ACAI2-MapHunt");
    public override int Initialize(){Host.Log("[ACAI2 MapHunt] v"+Version+". /acai2maphunt opens the standalone panel. Stop the old MapHunt Lua script before starting this app.");return 0;}
    public override void OnLoginComplete(){waiting=false;Interlocked.Exchange(ref lifecycle,1);}
    public override void OnLogout(){waiting=true;Interlocked.Exchange(ref lifecycle,1);}
    public override void OnChatBarEnter(string? text,ref int eat)
    {
        string s=(text??"").Trim();const string prefix="/acai2maphunt";
        if(!s.Equals(prefix,StringComparison.OrdinalIgnoreCase)&&!s.StartsWith(prefix+" ",StringComparison.OrdinalIgnoreCase))return;
        eat=1;Enqueue(s.Length>prefix.Length?s[(prefix.Length+1)..].Trim().ToLowerInvariant():"show");
    }
    private void Enqueue(string command)
    {if(requests.Count<32)requests.Enqueue(new(Volatile.Read(ref epoch),command));}
    private void Reset()
    {
        // A Stop click arriving at logout still clears only that old character's intent, without native calls.
        while(requests.TryDequeue(out var r))if(r.Epoch==epoch&&r.Command=="stop"&&profile!=null&&profileReadable){profile.Active=false;profile.Event=null;try{Save();}catch{}}
        Interlocked.Increment(ref epoch);controller=null;bot=null;profile=null;player=0;ready=false;deferredStop=false;path="";nextStep=0;nextProbe=0;renderFailed=false;
        Volatile.Write(ref view,MapHuntView.Idle);character="Waiting for login";
    }
    private void Save(){if(profileReadable&&profile!=null&&path.Length>0)profile.Save(path);}
    private void PrepareMeta()
    {
        if(bot==null||profile==null)throw new InvalidOperationException("No character is bound.");
        if(profile.Preferences.Mesh&&Host.GetPluginInterface("RynthNav","RynthNav.Path",2)==IntPtr.Zero)throw new InvalidOperationException("Mesh clue travel needs RynthNav. Enable it, or turn Mesh clue travel off.");
        string folder=MapHuntWorkflow.Folder(profile.RynthAiFolder,bot.Setting("CurrentMetaPath"),bot.Setting("CurrentNavPath"));
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MapHunt.App.af")??throw new InvalidDataException("Bundled MapHunt workflow is missing.");
        using var reader=new StreamReader(stream);MapHuntWorkflow.Install(folder,reader.ReadToEnd());
        if(profile.RynthAiFolder!=folder){profile.RynthAiFolder=folder;Save();panel.Load(profile);}
    }
    public override void OnTick()
    {
        if(Interlocked.Exchange(ref lifecycle,0)!=0)Reset();
        if(waiting)return;
        double time=Now;
        if(time>=nextProbe)
        {
            nextProbe=time+1;uint id=Host.GetPlayerId();
            if(id==0||!Host.TryGetWorldName(out string world)||!Host.TryGetObjectName(id,out string name))return;
            if(!world.Equals("DreamWeave",StringComparison.OrdinalIgnoreCase)){notice="This app handles DreamWeave treasure maps. Log into DreamWeave to start.";return;}
            if(player!=id||profile?.Server!=world||profile?.Character!=name)
            {
                Reset();player=id;character=name;
                string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(world+"\n"+name)));
                path=Path.Combine(Root,key,"maphunt.json");profileReadable=true;
                try{profile=MapHuntProfile.Load(path,world,name);}
                catch(Exception ex){profileReadable=false;profile=new(){Server=world,Character=name};notice="Saved settings could not be read; existing file preserved. "+ex.Message;}
                panel.Load(profile);bot=new(Host);controller=new(bot,profile,PrepareMeta,Save);
            }
        }
        if(controller!=null&&profileReadable&&!ready&&time>=nextStep&&Host.GetPlayerId()==player&&Host.HasIsPortaling&&!Host.IsPortaling())
        {
            try{nextStep=time+.5;controller.ImportPreferences();ready=true;notice="";if(profile?.Active==true)controller.Begin(time,automatic:true);}
            catch(Exception ex){controller.Fault(ex.Message);Volatile.Write(ref view,controller.View);notice=ex.Message;}
        }
        while(requests.TryDequeue(out var request))
        {
            if(request.Command is "show" or "hide"){panel.Visible=request.Command=="show";if(panel.Visible)renderFailed=false;if(profile!=null){profile.Visible=panel.Visible;try{Save();}catch{}}continue;}
            if(request.Epoch!=epoch||controller==null||profile==null)continue;
            try
            {
                if(!profileReadable)throw new InvalidOperationException("Saved profile is unreadable; preserve it for review before starting.");
                bool safe=Host.GetPlayerId()==player&&Host.HasIsPortaling&&!Host.IsPortaling();
                if(!safe&&request.Command is "start" or "review" or "preferences"){notice="Wait for login or portal travel to finish, then try again.";continue;}
                if(!safe&&request.Command=="stop"){profile.Active=false;profile.Event=null;deferredStop=true;Save();continue;}
                switch(request.Command)
                {
                    case "start":controller.Begin(time);break;
                    case "stop":controller.Stop();break;
                    case "review":controller.Review(time);break;
                    case "preferences":controller.Preferences(request.Preferences!);break;
                    case "appearance":profile.Visible=request.Visible;profile.X=request.X;profile.Y=request.Y;profile.RynthAiFolder=request.Folder;Save();break;
                    case "status":Host.WriteToChat("[ACAI2 MapHunt] "+controller.View.Phase+": "+controller.View.Detail,1);break;
                }
            }
            catch(Exception ex){controller.Fault(ex.Message);notice=ex.Message;}
        }
        if(controller==null||!profileReadable||!ready||time<nextStep)return;
        nextStep=time+.5;
        // Invalid native identity/portal frames must not run an old character's controller.
        if(Host.GetPlayerId()!=player||!Host.HasIsPortaling||Host.IsPortaling())return;
        try
        {
            if(deferredStop){controller.Stop();deferredStop=false;}
            controller.Tick(time);Volatile.Write(ref view,controller.View);
        }
        catch(Exception ex){controller.Fault(ex.Message);Volatile.Write(ref view,controller.View);notice=ex.Message;}
    }
    public override void OnRender()
    {
        if(renderFailed)return;
        try
        {
            var v=Volatile.Read(ref view);long bound=Volatile.Read(ref epoch);
            panel.Draw(character,v,notice,cmd=>{if(requests.Count<32)requests.Enqueue(new(bound,cmd));},p=>{if(requests.Count<32)requests.Enqueue(new(bound,"preferences",p));},(visible,x,y,folder)=>{if(requests.Count<32)requests.Enqueue(new(bound,"appearance",Visible:visible,X:x,Y:y,Folder:folder));});
        }
        catch(Exception ex){renderFailed=true;Host.Log("[ACAI2 MapHunt] Panel hidden after rendering error: "+ex.Message);}
    }
    public override void Shutdown()
    {
        // Unloading while online clears owned movement; a completed logout never invokes another session's API.
        if(!waiting&&player!=0&&Host.GetPlayerId()==player){try{controller?.Stop();}catch{}}
        Reset();
    }
}
