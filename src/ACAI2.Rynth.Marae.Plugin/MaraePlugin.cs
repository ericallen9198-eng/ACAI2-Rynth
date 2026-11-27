using ACAI2.Rynth.Ui;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACAI2.Rynth.Marae.Core;
using RynthCore.PluginCore;
namespace ACAI2.Rynth.Marae.Plugin;

public sealed class MaraePlugin:RynthPluginBase
{
    public const string Version="1.18.0";
    internal static readonly ToolbarWindow Toolbar=new("marae","ACAI2 Marae","\uE31A",460,420);
    private readonly ToolbarVisibility toolbar=new(Toolbar);
    public override uint MinimumApiVersion=>92;
    private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    private static readonly Hunter[] Hunters=JsonSerializer.Deserialize<Hunter[]>(Resource("Marae.hunters.json"))!;
    private static readonly string[] ProgressKeys=JsonSerializer.Deserialize<string[]>(Resource("Marae.keys.json"))!.Concat(new[]{"mm_deadline_180","mm_deadline_225","mm_deadline_275","mm_hunt_cursor"}).ToArray();
    private readonly ConcurrentQueue<(long Epoch,PanelRequest Request)> requests=new();
    private readonly ConcurrentQueue<(long Epoch,string Text)> chat=new();
    private readonly MaraePanel panel=new();
    private readonly List<string> mismatch=new();
    private readonly Dictionary<string,Queue<string>> recent=new();
    private MaraeView view=new("Waiting for login","Waiting","Enable RynthAi and log into DreamWeave.",false,false,MaraeOptions.Default,@"C:\Games\RynthSuite\RynthAi",true,240,120);
    private NativeMaraeBot? bot;
    private MaraeProfile? profile;
    private RecoveryObserver recovery=new();
    private uint player;private long epoch;private int lifecycle;private volatile bool loggedOut;
    private double nextTick,nextCheckpoint,deadline,nextProbe;private string profilePath="",fileKey="",startup="",phase="Ready",detail="",state="",walkPath="",resumePath="";
    private long walkGeneration=-1,restoreGeneration=-1,npcGeneration=-1,pickupGeneration=-1,logGeneration=-1;
    private CacheWatcher cacheWatcher=new();
    private readonly Dictionary<uint,double> nearCaches=new();
    private double nextCacheScan,idleSince;
    private string watchResume="";
    private bool watchOpened,watchDenied;
    private double watchNav,watchLoot;
    private NpcGate? npc;private string npcRoute="";private bool busy,initialized,readable=true,renderFailed,deferredStop,autoResumePending;
    private string savedRoute="",temporarySignature="";private double? oldMin;private bool? oldNearest;private string temporaryRoute="";
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    private static string Resource(string name){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)??throw new InvalidDataException(name);using var r=new StreamReader(s);return r.ReadToEnd();}
    public override int Initialize(){Host.Log("[ACAI2 Marae] v"+Version+" — /acai2marae opens the app. Uses the normal RynthAi DLL.");return 0;}
    public override void OnLoginComplete(){loggedOut=false;Interlocked.Exchange(ref lifecycle,1);}
    public override void OnLogout(){loggedOut=true;Interlocked.Exchange(ref lifecycle,1);}
    public override void OnChatBarEnter(string? text,ref int eat)
    {
        var s=(text??"").Trim();if(s is not ("/acai2marae" or "/marae"))return;eat=1;Enqueue(new("show"));
    }
    private void EnqueueAt(long bound,PanelRequest r){if(requests.Count<32)requests.Enqueue((bound,r));}
    private void Enqueue(PanelRequest r){if(requests.Count<32)requests.Enqueue((Volatile.Read(ref epoch),r));}
    public override void OnChatWindowText(string? text,int type,ref int eat)
    {
        if(text==null||text.Length>1024||chat.Count>=128)return;
        // Only bounded mission/death messages; ordinary player chat and tells are never stored.
        if(CacheWatcher.OwnershipRefusal(text)||text.StartsWith("Hea ",StringComparison.Ordinal)&&text.Contains(" tells you, \"",StringComparison.Ordinal)||text.StartsWith("You have killed ",StringComparison.Ordinal)||text.StartsWith("[RynthAi] Own corpse:",StringComparison.Ordinal)||text.StartsWith("[RynthAi] Your corpse is at ",StringComparison.Ordinal)||text=="You have retained all your items. You do not need to recover your corpse!")chat.Enqueue((Volatile.Read(ref epoch),text));
    }
    private void Reset()
    {
        toolbar.Reset();Interlocked.Increment(ref epoch);bot=null;profile=null;player=0;initialized=false;readable=true;startup="";npc=null;recovery=new();busy=false;
        cacheWatcher=new();nearCaches.Clear();nextCacheScan=idleSince=0;watchResume="";watchOpened=watchDenied=false;
        nextTick=nextProbe=nextCheckpoint=0;deferredStop=autoResumePending=false;walkGeneration=restoreGeneration=npcGeneration=pickupGeneration=logGeneration=-1;
        savedRoute=temporaryRoute=temporarySignature="";oldMin=null;oldNearest=null;recent.Clear();mismatch.Clear();panel.Reset();renderFailed=false;
        while(chat.TryDequeue(out _)){}while(requests.TryDequeue(out _)){}
    }
    private string Eval(string code)=>bot!.Evaluate(code);
    private double Num(string code)=>double.TryParse(Eval(code),NumberStyles.Float,Inv,out var n)&&double.IsFinite(n)?n:0;
    private double Var(string k)=>Num("getvar["+k+"]");
    private double PVar(string k)=>Num("getpvar["+k+"]");
    private void Set(string k,double n)=>Eval("setvar["+k+","+n.ToString("R",Inv)+"]");
    private void PSet(string k,double n)=>Eval("setpvar["+k+","+n.ToString("R",Inv)+"]");
    private void Setting(string k,double n)=>Eval("rasetsetting["+k+","+n.ToString("R",Inv)+"]");
    private bool Owns=>Eval("vtgetmeta[]").Replace('\\','/').Split('/').Last().Replace(".af","",StringComparison.OrdinalIgnoreCase).Replace(".met","",StringComparison.OrdinalIgnoreCase).Equals("Marae Lassel - Embedded",StringComparison.OrdinalIgnoreCase);
    private void Save(){if(readable&&profile!=null)profile.Save(profilePath);}
    private void Publish()
    {
        if(profile==null)return;
        Volatile.Write(ref view,new(profile.Character,phase,detail,Owns&&bot!.Running||startup.Length>0,PVar("mm_pickup_unresolved")==1||profile.PendingMove!=null,profile.Options,profile.Folder,profile.Visible,profile.X,profile.Y,epoch));
    }
    public override void OnTick()
    {
        if(Interlocked.Exchange(ref lifecycle,0)!=0)Reset();if(loggedOut)return;
        double time=Now;if(time<nextTick)return;nextTick=time+.25;
        try
        {
            if(time>=nextProbe)
            {
                nextProbe=time+1;uint id=Host.GetPlayerId();
                if(id==0||!Host.TryGetWorldName(out string world)||!Host.TryGetObjectName(id,out string name)){if(player!=0)Reset();return;}
                if(!world.Equals("DreamWeave",StringComparison.OrdinalIgnoreCase)){if(player!=0)Reset();return;}
                if(player!=id||profile?.Character!=name||profile.Server!=world)
                {
                    Reset();player=id;fileKey=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(world+"\n"+name)))[..16];
                    profilePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ACAI2-Marae",fileKey,"profile.json");
                    try{profile=MaraeProfile.Load(profilePath,world,name);}catch(Exception ex){readable=false;profile=new(){Server=world,Character=name};phase="Saved data needs review";detail=ex.Message;}
                    bot=new(Host);
                }
            }
            if(profile==null||bot==null||Host.GetPlayerId()!=player)return;
            if(Host.HasSetPanelOpen)toolbar.Sync((key,mode)=>Host.SetPanelOpen(key,(RynthCore.PluginSdk.RynthPanelOpen)mode),open=>{if(readable&&profile.Visible!=open){profile.Visible=open;Save();}});
            if(Host.GetPluginInterface("RynthAi","RynthAi.Script",1)==IntPtr.Zero)return;
            Set("mm_stock_ready",1);Set("mm_stock_heartbeat",DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            bool portal=!Host.HasIsPortaling||Host.IsPortaling(),dead=Num("getcharvital_current[1]")<=0;
            string navJson=Host.GetPluginExportJson("RynthNav","RynthNavGetStatusJson")??"{}";
            bool nativeGo=false;try{using var d=JsonDocument.Parse(navJson);nativeGo=d.RootElement.TryGetProperty("goto",out var g)&&(g.ValueKind==JsonValueKind.True||g.ValueKind==JsonValueKind.Number&&g.GetInt32()!=0);}catch(JsonException){}
            while(chat.TryDequeue(out var line))if(line.Epoch==epoch)
            {
                recovery.Observe(line.Text,time);
                if(cacheWatcher.Active!=0&&CacheWatcher.OwnershipRefusal(line.Text)&&Owns&&Var("mm_watch_active")==1&&Var("tm_cache_probe_id")==cacheWatcher.Active&&Host.GetGroundContainerId()!=cacheWatcher.Active)watchDenied=true;
                if(Owns&&bot.Running&&Var("mm_stock_schema")==1){ObserveNpc(line.Text);npc?.Observe(line.Text);}
            }
            busy=recovery.Busy(time,dead,portal,nativeGo);Set("mm_stock_recovery_busy",busy?1:0);
            if(!readable){Publish();return;}
            if(!initialized&&!portal)
            {
                initialized=true;ResolveFolder();ImportPreferences();autoResumePending=profile.Active;
            }
            while(requests.TryDequeue(out var queued))
            {
                if(queued.Epoch!=epoch)continue;var r=queued.Request;
                if(r.Command=="show"){toolbar.Request(true);profile.Visible=true;renderFailed=false;Save();continue;}
                if(r.Command=="appearance"){profile.Visible=r.Visible;profile.X=r.X;profile.Y=r.Y;Save();continue;}
                if(r.Command=="options"&&r.Options!=null){profile.Options=r.Options.Validate();ApplyPreferences(false);Save();continue;}
                if(r.Command=="folder"&&!bot.Running){profile.Folder=r.Folder??profile.Folder;Save();continue;}
                if(r.Command=="stop"){autoResumePending=false;if(Owns)Checkpoint();profile.Active=false;Save();startup="";if(!portal&&Owns){Cleanup();bot.Command("/ra stop");}else if(portal)deferredStop=true;phase="Stopped";detail="Progress saved. Start / Resume continues the mission.";continue;}
                if(portal||busy){phase="Waiting for travel / recovery";detail="Try Start / Resume once native travel or recovery finishes.";continue;}
                if(r.Command=="review"){PSet("mm_pickup_unresolved",0);PSet("mm_stock_pickup_issued",0);profile.PendingMove=null;Save();Begin(false,false,time);}
                else if(r.Command is "start" or "new")Begin(r.Command=="new",false,time);
            }
            if(ResumePolicy.CanStart(autoResumePending,initialized,busy,portal)){autoResumePending=false;Begin(false,true,time);}
            if(autoResumePending&&busy){phase="Waiting for native recovery";detail="Saved run will resume after native travel or corpse recovery settles.";Publish();return;}
            if(portal){Publish();return;}
            if(deferredStop){deferredStop=false;if(Owns){Cleanup();bot.Command("/ra stop");}}
            if(startup.Length>0)
            {
                if(time>deadline){startup="";profile.Active=false;Save();phase="Startup not confirmed";detail="Check the RynthAi folder under Advanced and press Start / Resume.";Publish();return;}
                if(startup=="stop"&&!bot.Running){bot.Command("/ra meta load Marae Lassel - Embedded.af");startup="load";deadline=time+15;}
                else if(startup=="load"&&Owns&&!bot.Running){ApplyPreferences(true);Eval("vtsetsetting[EnableMeta,1]");Eval("vtsetmetastate[Default]");bot.Command("/ra start");startup="arm";deadline=time+15;}
                else if(startup=="arm"&&Owns&&bot.Running&&Var("mm_stock_schema")==1)startup="";
            }
            if(!Owns){Publish();return;}
            state=Eval("vtgetmetastate[]");
            if(Var("mm_stock_schema")!=1){phase="Ready";detail="Press Start / Resume to load the stock-compatible mission.";Publish();return;}
            long restore=(long)Var("mm_stock_restore_generation");
            if(restore!=restoreGeneration){restoreGeneration=restore;if(!busy)RestoreRoute();else DropRoute();}
            if(!bot.Running||state=="HOLD")
            {
                if(npc!=null){ReleaseNpc(false);}
                if(profile.Active&&startup.Length==0){Checkpoint();Cleanup();profile.Active=false;Save();}
                if(state=="HOLD"&&bot.Running)bot.Command("/ra stop");
                phase=state=="HOLD"?"Run paused":"Stopped";detail=Eval("getvar[ml_status]");Publish();return;
            }
            if(busy){npc=null;DropRoute();phase="Native corpse recovery / travel";detail="RynthAi owns recovery. The mission will continue afterward.";Publish();return;}
            if(HandleCacheWatcher(time)){Publish();return;}
            HandleNpc(time);
            long walk=(long)Var("mm_stock_walk_generation");
            string moveState=state.StartsWith("WATCH_",StringComparison.Ordinal)?state[6..]:state;
            if(walk!=walkGeneration&&moveState is "MAP_MESH_WAIT" or "CACHE_RETURN_WAIT" or "CACHE_APPROACH_WAIT" or "DIGSITE_RETURN_WAIT")
            {walkGeneration=walk;try{Move(moveState);}catch(Exception ex){Set("tm_walk_ack",-1);Eval("setvar[tm_mesh_error,`Complete mesh path unavailable`]");Host.Log("[ACAI2 Marae] "+ex.Message);}}
            SettlePickup();
            long pickup=(long)Var("mm_stock_pickup_generation");
            if((state=="CACHE_CONFIRM"||state=="WATCH_CACHE_CONFIRM")&&Var("tm_cache_sent")==-1&&pickup!=pickupGeneration){pickupGeneration=pickup;Pickup();}
            CaptureMismatch();
            if(time>=nextCheckpoint){nextCheckpoint=time+5;Checkpoint();}
            phase="Area "+Var("ml_zone").ToString(Inv)+" · "+(Var("ml_hunt_only")==1?"Hunt only":"Tasks")+(Var("ml_use_maps")==1?" + maps":"");
            detail=npc==null?state+"\n"+Eval("getvar[ml_status]"):"Talking to "+npc.Hunter.Name+" (attempt "+npc.Attempts+"/3)";
            if(cacheWatcher.Active!=0){phase="Nearby cache check";detail=state+" · cache "+cacheWatcher.Active.ToString("X8",Inv);}
            Publish();
        }
        catch(Exception ex){phase="Marae needs attention";detail=ex.Message;Host.Log("[ACAI2 Marae] "+ex.Message);if(profile!=null)Volatile.Write(ref view,new(profile.Character,phase,detail,false,false,profile.Options,profile.Folder,profile.Visible,profile.X,profile.Y,epoch));nextTick=time+2;}
    }
    private bool HandleCacheWatcher(double time)
    {
        // Observe local caches without requiring an event-complete signal; dispatch is gated below.
        if(time>=nextCacheScan)
        {
            nextCacheScan=time+1;nearCaches.Clear();var visible=new HashSet<uint>();
            foreach(uint id in Host.GetLiveObjectIds())
                if(Host.TryGetObjectName(id,out string name)&&name=="Unearthed Cache")
                {visible.Add(id);double distance=DistanceTo(id);if(distance<=CacheWatcher.Radius)nearCaches[id]=distance;}
            cacheWatcher.Observe(nearCaches,visible,time);
        }
        if(cacheWatcher.Active==0&&state.StartsWith("CACHE_",StringComparison.Ordinal)&&Var("tm_cache_pickups")>0)cacheWatcher.Handled(checked((uint)Var("tm_cache_id")));
        if(cacheWatcher.Active!=0)
        {
            if(!state.StartsWith("WATCH_",StringComparison.Ordinal)){FinishWatch(time,false);return false;}
            SettlePickup();
            watchOpened|=Host.GetGroundContainerId()==cacheWatcher.Active;
            bool noPending=Var("tm_cache_pending")==0&&profile!.PendingMove==null&&PVar("mm_pickup_unresolved")!=1;
            if(state=="WATCH_RETURN"||noPending&&(watchDenied||time>=cacheWatcher.Deadline||!profile!.Options.WatchCaches))
            {FinishWatch(time,true);return true;}
            return false;
        }
        if(state.StartsWith("WATCH_",StringComparison.Ordinal))
        {
            // Hot reload has no route lease. Restart from the area dispatch, never replay an item move.
            if(PVar("mm_pickup_unresolved")==1||profile!.PendingMove!=null)Eval("vtsetmetastate[INVENTORY_REVIEW]");
            else Eval("vtsetmetastate[DISPATCH]");
            return true;
        }
        if(CacheWatcher.HuntState(state)&&savedRoute.Length>0)RestoreRoute();
        bool idle=Num("ratargetcount[]")==0&&Num("getbusystate[]")==0;
        if(!idle)idleSince=time;
        bool allowed=profile!.Active&&startup.Length==0&&CacheWatcher.MayStart(state,profile.Options.WatchCaches,bot!.Running,busy,false,false,PVar("mm_pickup_unresolved")==1||profile.PendingMove!=null,idle&&time-idleSince>=1);
        uint candidate=cacheWatcher.Start(nearCaches,time,allowed);
        if(candidate==0)return false;
        try
        {
            watchResume=state;watchNav=Num("ragetsetting[EnableNavigation]");watchLoot=Num("ragetsetting[EnableLooting]");
            SaveRoute();temporaryRoute=CurrentRoute;temporarySignature=RouteSignature();
            watchOpened=watchDenied=false;Setting("EnableNavigation",0);
            Set("mm_watch_active",1);Set("mm_watch_terminal",0);Set("mm_watch_id",candidate);Set("mm_watch_deadline",DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60);
            Set("tm_doubloon_before",Num("getinventorycountbytemplatetype[1004101]"));
            Eval("setvar[tm_cache_anchor,getplayercoordinates[]]");Set("tm_cache_deadline",DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60);
            Eval("vtsetmetastate[WATCH_CACHE_PREP]");state="WATCH_CACHE_PREP";
            Host.Log("[ACAI2 Marae] Checking nearby cache "+candidate.ToString("X8",Inv)+"; hunt position saved.");
        }
        catch(InvalidDataException ex){FinishWatch(time,false);Host.Log("[ACAI2 Marae] Nearby cache deferred: "+ex.Message);return false;}
        catch{FinishWatch(time,false);throw;}
        return true;
    }
    private void FinishWatch(double time,bool resume)
    {
        uint id=cacheWatcher.Active;bool unresolved=PVar("mm_pickup_unresolved")==1||profile!.PendingMove!=null;
        bool ownsRoute=temporarySignature.Length>0&&CurrentRoute==temporaryRoute&&RouteSignature()==temporarySignature;
        cacheWatcher.Finish(time,watchOpened&&Var("mm_watch_terminal")==1,watchDenied);Set("mm_watch_active",0);
        Eval("if[getpvar[pt_cache_pickup_held]==1,rasetsetting[`LootChestsMacroOff`,getpvar[pt_cache_old_pickup]]]");PSet("pt_cache_pickup_held",0);
        if(Num("ragetsetting[EnableLooting]")==0)Setting("EnableLooting",watchLoot);
        if(!busy)RestoreRoute();else DropRoute();
        if(resume)
        {
            if(unresolved)Eval("vtsetmetastate[INVENTORY_REVIEW]");
            else if(CacheWatcher.HuntState(watchResume))
            {
                Set("tm_cache_pending",0);Set("tm_cache_id",0);Set("tm_cache_probe_id",0);Set("tm_cache_anchor",0);Set("tm_cache_deadline",0);
                Eval("vtsetmetastate["+watchResume+"]");if(ownsRoute)Setting("EnableNavigation",watchNav);
            }
        }
        watchResume="";idleSince=time;
        Host.Log("[ACAI2 Marae] Nearby cache "+id.ToString("X8",Inv)+(watchDenied?" refused ownership; skipped.":watchOpened?" opened; check ended.":" did not open; bounded retry or skip.") );
    }
    private void ResolveFolder()
    {
        string assemblyFolder=Path.GetDirectoryName(typeof(MaraePlugin).Assembly.Location)??"";
        string installed=MaraeFolders.InstallerHint(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ACAI2","Marae"));
        if(installed.Length==0)installed=MaraeFolders.InstallerHint(assemblyFolder);
        string folder=MaraeFolders.Resolve(profile!.Folder,installed,bot!.Setting("CurrentMetaPath"),bot.Setting("CurrentNavPath"),MaraeFolders.OfficialRoot);
        if(folder.Length>0&&profile.Folder!=folder){profile.Folder=folder;Save();}
    }
    private void ImportPreferences()
    {
        if(profile!.Imported)return;
        double Read(string key,double fallback)=>Num("testpvar["+key+"]")==1?PVar(key):fallback;
        var p=profile.Options;
        profile.Options=p with {Areas=p.Areas.Select(a=>new AreaOptions(a.Level,Read("ml_area_"+a.Level,a.Enabled?1:0)==1,Read("ml_maps_"+a.Level,a.Maps?1:0)==1,Read("ml_hunt_only_"+a.Level,a.HuntOnly?1:0)==1)).ToArray(),Mesh=Read("ml_mesh_maps",p.Mesh?1:0)==1,Close=(int)Read("mm_mesh_close",30),Far=(int)Read("mm_mesh_far",130),CampLimit=Read("mm_camp_limit",p.CampLimit?1:0)==1,Fallback=Read("mm_fallback_180",1)==1};
        foreach(string key in ProgressKeys)profile.Progress[key]=PVar(key);
        profile.Options=profile.Options.Validate();profile.Imported=true;Save();
    }
    private void ApplyPreferences(bool progress)
    {
        var p=profile!.Options.Validate();PSet("ml_run_mode",1);PSet("ml_mesh_maps",p.Mesh?1:0);PSet("mm_mesh_close",p.Close);PSet("mm_mesh_far",p.Far);PSet("mm_camp_limit",p.CampLimit?1:0);PSet("mm_fallback_180",p.Fallback?1:0);
        foreach(var a in p.Areas){PSet("ml_area_"+a.Level,a.Enabled?1:0);PSet("ml_maps_"+a.Level,a.Maps?1:0);PSet("ml_hunt_only_"+a.Level,a.HuntOnly?1:0);}
        if(progress)foreach(string key in ProgressKeys)PSet(key,profile.Progress.GetValueOrDefault(key));
    }
    private void Begin(bool fresh,bool automatic,double time)
    {
        if(startup.Length>0)return;
        autoResumePending=false;
        if(!profile!.Options.Areas.Any(a=>a.Enabled)){phase="Choose an area";detail="Enable at least one area before starting.";return;}
        if(PVar("mm_pickup_unresolved")==1||profile.PendingMove!=null){phase="Review cache pickup";detail="A previous item move was not confirmed. Check the inventory before using the review button.";return;}
        if(automatic&&bot!.Running&&!Owns){phase="Resume available";detail="Another macro is running. Press Start / Resume when ready.";return;}
        if(profile.Options.Mesh&&Host.GetPluginInterface("RynthNav","RynthNav.Path",2)==IntPtr.Zero)throw new InvalidOperationException("Enable RynthNav, or turn Mesh map travel off.");
        ResolveFolder();
        if(!MaraeFolders.IsDataRoot(profile.Folder))throw new DirectoryNotFoundException("RynthAi data folder not found. Run the installer and choose the folder containing MetaFiles or NavProfiles, or select Advanced here.");
        string target=Path.Combine(profile.Folder,"MetaFiles","Marae Lassel - Embedded.af"),content=Resource("Marae.workflow.af");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if(!File.Exists(target)||File.ReadAllText(target)!=content){if(File.Exists(target))File.Copy(target,target+".before-stock-app",true);File.WriteAllText(target+".tmp",content);File.Move(target+".tmp",target,true);}
        string nav=Path.Combine(profile.Folder,"NavProfiles","ACAI2-Marae");Directory.CreateDirectory(nav);walkPath=Path.Combine(nav,fileKey+"_walk.nav");resumePath=Path.Combine(nav,fileKey+"_resume.nav");
        if(Owns){Checkpoint();Cleanup();}
        if(fresh){profile.Progress.Clear();profile.Contracts.Clear();}
        profile.Active=true;Save();
        // The old Lua controller must not race this app. Fixed local commands only.
        bot!.Command("/lua stop MaraeLassel");bot.Command("/lua autostart MaraeLassel none");
        bot.Command("/ra stop");startup="stop";deadline=time+15;phase="Starting";detail="Loading the embedded mission with saved character preferences.";
        Set("mm_stock_schema",0);npc=null;walkGeneration=restoreGeneration=npcGeneration=pickupGeneration=-1;
    }
    private void Checkpoint()
    {
        if(profile==null||!Owns||Var("mm_stock_schema")!=1)return;
        foreach(string key in ProgressKeys)profile.Progress[key]=PVar(key);Save();
    }
    private void Cleanup()
    {
        if(!Owns)return;
        if(cacheWatcher.Active!=0)FinishWatch(Now,false);
        ReleaseNpc(false);if(busy)DropRoute();else RestoreRoute();
        Eval("if[getpvar[pt_cache_pickup_held]==1,rasetsetting[`LootChestsMacroOff`,getpvar[pt_cache_old_pickup]]]");PSet("pt_cache_pickup_held",0);
    }
    private string CurrentRoute=>bot!.Setting("CurrentNavPath")??"";
    private void HandleNpc(double time)
    {
        long gen=(long)Var("mm_stock_npc_generation");
        if(gen!=npcGeneration)
        {
            npcGeneration=gen;int id=(int)Var("mm_stock_npc_request");var h=Hunters.FirstOrDefault(h=>h.Id==id);
            if(h!=null&&h.Zone==(int)Var("ml_zone")&&(state.EndsWith("NPC_TALK_WAIT",StringComparison.Ordinal)||state.EndsWith("TURNIN_WAIT",StringComparison.Ordinal)))
            {npc=new(h,time);npcRoute=CurrentRoute;}
        }
        if(npc==null)return;
        if(!(state.EndsWith("NPC_TALK_WAIT",StringComparison.Ordinal)||state.EndsWith("TURNIN_WAIT",StringComparison.Ordinal))||CurrentRoute!=npcRoute){ReleaseNpc(false);return;}
        if(npc.Confirmed){ReleaseNpc(true);return;}
        if(npc.Exhausted(time)){try{LogMismatch(npc.Hunter,"No recognized reply after 3 attempts; continuing other tasks.");}finally{ReleaseNpc(true);}return;}
        if(!npc.Due(time))return;
        var candidates=new List<uint>(2);
        foreach(uint id in Host.GetLiveObjectIds())
            if(Host.TryGetObjectName(id,out string name)&&name==npc.Hunter.Name&&Host.TryGetObjectIntProperty(id,1,out int itemType)&&(itemType&16)!=0&&DistanceTo(id)<=12)candidates.Add(id);
        // Count a failed local lookup as a bounded attempt; never spin here forever.
        npc.Issued(time);
        if(candidates.Count==1)Eval("actiontryuseitem[wobjectfindbyid["+candidates[0].ToString(Inv)+"]]");
    }
    private double DistanceTo(uint id)
    {
        if(!Host.TryGetObjectPosition(id,out uint cell,out float x,out float y,out _)||!Host.TryGetPlayerPose(out uint pc,out float px,out float py,out _,out _,out _,out _,out _))return double.PositiveInfinity;
        if((cell>>16)!=(pc>>16)&&((cell&65535)>=0x100||(pc&65535)>=0x100))return double.PositiveInfinity;
        double dx=((cell>>24)-(double)(pc>>24))*192+x-px,dy=(((cell>>16)&255)-(double)((pc>>16)&255))*192+y-py;
        return Math.Sqrt(dx*dx+dy*dy);
    }
    private void ReleaseNpc(bool continueRoute)
    {
        if(npc==null&&Var("mm_stock_npc_held")!=1)return;
        bool same=npc!=null&&CurrentRoute==npcRoute;
        if(Var("mm_stock_npc_held")==1)
        {
            if(Num("ragetsetting[FollowNavMin]")==.5)Setting("FollowNavMin",Var("mm_stock_npc_old_min"));Set("mm_stock_npc_held",0);
        }
        npc=null;if(continueRoute&&same&&bot!.Running&&!busy)Setting("EnableNavigation",1);
    }
    private void ObserveNpc(string line)
    {
        if(profile==null)return;
        int zone=(int)Var("ml_zone");
        if(NpcDialogue.CompletionTarget(line) is string target)
        {
            foreach(var h in Hunters.Where(h=>h.Zone==zone))if(profile.Contracts.TryGetValue(zone+"/"+h.Key,out var c)&&NpcDialogue.SameTarget(target,c.Target)){PSet("mm_"+zone+"_done_"+h.Key,1);profile.Contracts[zone+"/"+h.Key]=c with {Status="completed"};}Save();return;
        }
        var hunter=Hunters.FirstOrDefault(h=>h.Zone==zone&&line.StartsWith(h.Name+" tells you, \"",StringComparison.Ordinal));if(hunter==null||!line.EndsWith('"'))return;
        string key=zone+"/"+hunter.Key;var q=recent.GetValueOrDefault(key);if(q==null)recent[key]=q=new();if(!q.Contains(line)){if(q.Count==4)q.Dequeue();q.Enqueue(line.Length>512?line[..512]:line);}
        string body=line[(hunter.Name.Length+13)..^1];var contract=NpcDialogue.Parse(body);if(contract==null)return;
        if(contract.Target.Length==0&&profile.Contracts.TryGetValue(key,out var old))contract=contract with {Target=old.Target};
        profile.Contracts[key]=contract;Set("pt_pickup_"+hunter.Key,1);
        if(contract.Status is "cooldown" or "rewarded"){Set("pt_unavailable_"+hunter.Key,1);PSet("mm_"+zone+"_done_"+hunter.Key,1);}
        else if(contract.Status=="completed")PSet("mm_"+zone+"_done_"+hunter.Key,1);
        else{Set("pt_unavailable_"+hunter.Key,0);PSet("mm_"+zone+"_done_"+hunter.Key,0);}
        Save();
    }
    private void LogMismatch(Hunter hunter,string reason)
    {
        string key=hunter.Zone+"/"+hunter.Key;
        string row=DateTimeOffset.UtcNow.ToString("O",Inv)+" "+hunter.Name+": "+reason+"\n"+string.Join("\n",recent.GetValueOrDefault(key)??new Queue<string>());
        if(mismatch.Count==16)mismatch.RemoveAt(0);mismatch.Add(row);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(profilePath)!,"npc-mismatches.json"),JsonSerializer.Serialize(mismatch));
    }
    private void CaptureMismatch()
    {
        long generation=(long)Var("mm_npc_log_generation");if(generation==0||generation==logGeneration)return;logGeneration=generation;
        int zone=(int)Var("mm_npc_log_zone");string missing=Eval("getvar[mm_npc_log_missing]");
        foreach(var h in Hunters.Where(h=>h.Zone==zone))if(missing.Split(new[]{',',' ',';','|'},StringSplitOptions.RemoveEmptyEntries).Contains(h.Key))LogMismatch(h,"Mission could not confirm this NPC; partial-task fallback remains available.");
    }
    private void SaveRoute()
    {
        if(savedRoute.Length>0)return;
        var route=RouteSnapshot.Read(Host.GetPluginExportJson("RynthAi","RynthPluginGetNavJson")??"{}");
        savedRoute=route.ResumeFile();File.WriteAllText(resumePath,savedRoute);
    }
    private void HoldSettings()
    {
        oldMin??=Num("ragetsetting[FollowNavMin]");oldNearest??=Num("ragetsetting[NavResumeNearest]")==1;
        Setting("FollowNavMin",.5);Setting("NavResumeNearest",0);
    }
    private void RestoreSettings()
    {
        if(oldMin.HasValue&&Num("ragetsetting[FollowNavMin]")==.5)Setting("FollowNavMin",oldMin.Value);
        if(oldNearest.HasValue&&Num("ragetsetting[NavResumeNearest]")==0)Setting("NavResumeNearest",oldNearest.Value?1:0);
        oldMin=null;oldNearest=null;
    }
    private void DropRoute(){savedRoute=temporaryRoute=temporarySignature="";RestoreSettings();}
    private void RestoreRoute()
    {
        if(savedRoute.Length==0){RestoreSettings();return;}
        // Restore only our own temporary route. A manually loaded route takes precedence.
        if(temporarySignature.Length>0&&CurrentRoute==temporaryRoute&&RouteSignature()==temporarySignature)
        {
            Setting("NavResumeNearest",0);bot!.Command("/ra nav load ACAI2-Marae\\"+fileKey+"_resume.nav");
        }
        savedRoute=temporaryRoute=temporarySignature="";RestoreSettings();
    }
    private string RouteSignature()
    {
        var r=JsonSerializer.Deserialize<RouteSnapshot>(Host.GetPluginExportJson("RynthAi","RynthPluginGetNavJson")??"{}")??throw new InvalidDataException("Missing route ownership snapshot.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r.Points))));
    }
    private void Move(string phase)
    {
        (double ns,double ew,double z)=Coordinates(Eval("getplayercoordinates[]"));double n,e;
        if(phase=="MAP_MESH_WAIT")
        {
            (n,e)=MapGeometry.Goal(ns,ew,Var("tm_mesh_heading"),Var("tm_mesh_step"));
            var points=NativeMesh.Find(Host,n,e);SaveRoute();HoldSettings();
            File.WriteAllText(walkPath,RouteSnapshot.Serialize(4,points));bot!.Command("/ra nav load ACAI2-Marae\\"+fileKey+"_walk.nav");temporaryRoute=CurrentRoute;temporarySignature=RouteSignature();
            var end=points[^1];Eval("setvar[tm_walk_target,`"+end.NS.ToString("R",Inv)+"|"+end.EW.ToString("R",Inv)+"|"+end.Z.ToString("R",Inv)+"`]");
            Setting("EnableNavigation",1);Set("tm_walk_ack",1);return;
        }
        (n,e,_)=Coordinates(Eval("getvar[tm_walk_target]"));
        if(phase=="CACHE_APPROACH_WAIT")
        {
            if(Math.Sqrt(Math.Pow(ns-n,2)+Math.Pow(ew-e,2))*240<=3){Set("tm_walk_ack",1);return;}
            (n,e)=MapGeometry.StandOff(ns,ew,n,e,2.5);
        }
        SaveRoute();HoldSettings();
        if(!bot!.GoTo(n,e)){Set("tm_walk_ack",-1);return;}
        temporaryRoute=CurrentRoute;temporarySignature=RouteSignature();Setting("EnableNavigation",1);Set("tm_walk_ack",1);
    }
    private static (double N,double E,double Z) Coordinates(string text)
    {
        var p=text.Split('|');if(p.Length!=3||!double.TryParse(p[0],NumberStyles.Float,Inv,out double n)||!double.TryParse(p[1],NumberStyles.Float,Inv,out double e)||!double.TryParse(p[2],NumberStyles.Float,Inv,out double z)||!double.IsFinite(n)||!double.IsFinite(e)||!double.IsFinite(z)||Math.Abs(n)>103||Math.Abs(e)>103)throw new InvalidDataException("Missing navigation coordinates.");return(n,e,z);
    }
    private void SettlePickup()
    {
        var p=profile!.PendingMove;if(p==null)return;
        double total=Num("getinventorycountbytemplatetype["+p.Template.ToString(Inv)+"]");
        bool sourceChanged=Host.TryGetObjectIntProperty(p.Item,12,out int amount)&&amount<p.Amount;
        sourceChanged|=Host.TryGetObjectInstanceIdProperty(p.Item,2,out uint owner)&&owner!=0&&owner!=p.Container;
        if(Host.GetGroundContainerId()==p.Container)
        {
            var ids=new uint[256];int count=Host.GetContainerContents(p.Container,ids);
            sourceChanged|=count>=0&&count<=ids.Length&&!ids.Take(count).Contains(p.Item);
        }
        if(!CachePickupPolicy.Confirmed(p,total,sourceChanged))return;
        profile.PendingMove=null;Save();PSet("mm_stock_pickup_issued",0);
        if((state=="CACHE_CONFIRM"||state=="WATCH_CACHE_CONFIRM")&&(uint)Var("tm_cache_pending")==p.Item)Set("tm_cache_sent",1);
    }
    private void Pickup()
    {
        uint item=checked((uint)Var("tm_cache_pending")),container=checked((uint)Var("tm_cache_id"));
        if(profile!.PendingMove!=null)return; // independent durable acknowledgment required before any later move
        var contents=new uint[256];int count=Host.GetContainerContents(container,contents);
        Host.TryGetObjectWcid(item,out uint wcid);
        // Use the same currency classification as the AF and original working cache helper.
        // Trade Note (250,000): WCID 20630, ObjectClass Money=15, ItemType=262144.
        // ItemType bit 128 is not the Money object class and rejects these notes.
        int objectClass=(int)Num("wobjectgetobjectclass[wobjectfindbyid["+item.ToString(Inv)+"]]");
        bool money=objectClass==15;
        bool allowed=CachePickupPolicy.CanIssue(PVar("mm_pickup_unresolved")==1,item,container,checked((uint)PVar("mm_pickup_item")),checked((uint)PVar("mm_pickup_container")),Host.GetGroundContainerId(),Host.TryGetObjectName(container,out var name)&&name=="Unearthed Cache",count>0&&count<=contents.Length&&contents.Take(count).Contains(item),wcid,money,Host.HasMoveItemExternal,profile.PendingMove!=null);
        if(!allowed){Set("tm_cache_sent",0);return;}
        profile.PendingMove=new(container,item,checked((uint)Var("tm_cache_pending_template")),(int)Var("tm_cache_pending_amount"),Var("tm_cache_before_count"));Save();PSet("mm_stock_pickup_issued",1);
        // Checkpoint precedes dispatch. Exceptions deliberately leave sent=-1 and the durable review marker intact.
        bool accepted=Host.MoveItemExternal(item,player,0);
        if(!accepted){profile.PendingMove=null;Save();PSet("mm_stock_pickup_issued",0);}
        Set("tm_cache_sent",accepted?1:0);
    }
    internal int RenderEmbedded(string? surface,float width,float height)
    {
        if(profile==null||loggedOut||renderFailed)return 0;
        var snapshot=Volatile.Read(ref view);return Toolbar.Draw(surface,width,height,()=>panel.Draw(snapshot,r=>EnqueueAt(snapshot.Epoch,r),hosted:true));
    }
    public override void OnRender()
    {
        if(renderFailed||Host.HasSetPanelOpen)return;
        try{var snapshot=Volatile.Read(ref view);panel.Draw(snapshot,r=>EnqueueAt(snapshot.Epoch,r));}catch(Exception ex){renderFailed=true;Host.Log("[ACAI2 Marae] Panel hidden after render error: "+ex.Message);}
    }
    public override void Shutdown()
    {
        if(!loggedOut&&player!=0&&Host.GetPlayerId()==player&&bot!=null){try{if(Owns){Checkpoint();Cleanup();Set("mm_stock_ready",0);}}catch(Exception ex){Host.Log("[ACAI2 Marae] Cleanup: "+ex.Message);}}
        Reset();
    }
}
