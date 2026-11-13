using System.Collections.Concurrent;
using System.Text.Json;
using ACAI2.Rynth.Threads.Core;
using RynthCore.PluginCore;
using RynthCore.PluginSdk;
namespace ACAI2.Rynth.Threads.Plugin;

public sealed class ThreadsPlugin : RynthPluginBase
{
    public const string Version="0.1.2";
    public override uint MinimumApiVersion=>92;
    private readonly ConcurrentQueue<string> commands=new();
    private readonly ConcurrentQueue<(string Text,int Type)> prepChat=new();
    private readonly PrepMachine prep=new();
    private readonly ConcurrentQueue<PrepPlan> starts=new();
    private readonly ThreadPanel panel=new();
    private ThreadWorker? journal;
    private ThreadIdentity? identity;
    private string folder="",notice="Waiting for DreamWeave login",previousCheckpoint="";
    private DateTime probe,saveUi;
    private bool prepReadable=true;
    private double nextStep,pauseDeadline,settleAfter;
    private PrepPlan? waitingPlan;
    private bool held,resumeAfterStop;
    private BotPauseLease? botLease;
    private BotPauseLease Lease=>botLease??=new(ReadMacro,SetMacro,ReleaseHold);
    private int lifecycle,queued;
    private uint player,pendingIdentify;
    private readonly Dictionary<uint,long> identifyAfter=new();
    private bool waiting,renderFailed;
    private Task closing=Task.CompletedTask;
    private (uint Cell,float X,float Y,float Z)? anchor;
    private string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ACAI2-Threads");
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    public override int Initialize(){Host.Log("[ACAI2 Threads] v0.1.2. /acai2threads opens the standalone panel. Gem preparation starts only from the panel.");return 0;}
    public override void OnLoginComplete(){waiting=false;Interlocked.Exchange(ref lifecycle,1);}
    public override void OnLogout(){waiting=true;journal?.Dispose();Interlocked.Exchange(ref lifecycle,1);}
    public override void OnChatWindowText(string? text,int type,ref int eat)
    {
        if(waiting||Volatile.Read(ref lifecycle)!=0||text==null||text.Length>4096||!ThreadText.Native(text,type))return;
        // Filter before queuing: ordinary combat chatter is never retained.
        bool relevant=text.StartsWith("The fragment ",StringComparison.Ordinal)||text.StartsWith("Pressed.",StringComparison.Ordinal)||text.StartsWith("Components selected:",StringComparison.Ordinal)||text.StartsWith("The gem carries:",StringComparison.Ordinal)||text.StartsWith("Level ",StringComparison.Ordinal)||text.StartsWith("Remaining slots",StringComparison.Ordinal)||text.StartsWith("[REALM]",StringComparison.Ordinal)||text.StartsWith("The Thread Gem ",StringComparison.Ordinal)||text.StartsWith("Your Thread Gem ",StringComparison.Ordinal)||text.StartsWith("Monsters killed ",StringComparison.Ordinal)||text.Contains(" is cleared.",StringComparison.Ordinal)||text.Contains(" - ",StringComparison.Ordinal)||text is "You are dead!" or "You have died!";
        if(!relevant)return;
        uint bound=prep.Running&&prep.Data.State=="Awaiting press"?prep.Data.Fragment:0;
        journal?.Submit(new("chat",text,DateTime.UtcNow,unchecked((int)bound),"",type));
        if(Interlocked.Increment(ref queued)<=128)prepChat.Enqueue((text,type));else Interlocked.Decrement(ref queued);
    }
    public override void OnChatBarEnter(string? text,ref int eat)
    {
        string cmd=(text??"").Trim().ToLowerInvariant();
        if(cmd!="/acai2threads"&&!cmd.StartsWith("/acai2threads ",StringComparison.Ordinal))return;
        eat=1;if(commands.Count<16)commands.Enqueue(cmd.Length>13?cmd[13..].Trim():"show");
    }
    public override void OnTick()
    {
        if(Interlocked.Exchange(ref lifecycle,0)!=0){End();probe=DateTime.MinValue;}
        if(!waiting&&DateTime.UtcNow>=probe)
        {
            probe=DateTime.UtcNow.AddSeconds(1);
            uint id=Host.GetPlayerId();
            if(id==0||!Host.TryGetWorldName(out string world)||!world.Equals("DreamWeave",StringComparison.OrdinalIgnoreCase)||!Host.TryGetObjectName(id,out string name)){End();return;}
            var who=new ThreadIdentity(world,name);
            if(identity!=who||player!=id)
            {
                End();if(!closing.IsCompleted)return;
                identity=who;player=id;folder=who.Folder(Root);journal=new(who,Root);renderFailed=false;prepReadable=true;
                try{Directory.CreateDirectory(folder);string path=Path.Combine(folder,"prep.json");if(File.Exists(path)){
                    var checkpoint=JsonSerializer.Deserialize<PrepCheckpoint>(File.ReadAllText(path))??throw new InvalidDataException("Empty prep state");
                    if(checkpoint.Schema!=1||checkpoint.Server!=world||checkpoint.Character!=name)throw new InvalidDataException("Prep state schema/identity mismatch");
                    prep.Restore(checkpoint);}
                    panel.Load(Path.Combine(folder,"ui.json"));}
                catch(Exception ex){prepReadable=false;notice="Local state unavailable; prep disabled, existing file preserved: "+ex.GetType().Name;}
            }
        }
        while(commands.TryDequeue(out string? cmd))
        {
            if(cmd is "show" or "" or "setup"){panel.Visible=true;renderFailed=false;}
            else if(cmd=="hide")panel.Visible=false;
            else if(cmd=="stop"){resumeAfterStop=waitingPlan!=null||prep.Data.State is "Supply check" or "Loading" or "Idle" or "Completed";waitingPlan=null;prep.Stop();settleAfter=Now+2;notice=resumeAfterStop?"Stopped; waiting for the client to settle before releasing RynthAi.":"Stopped during an outstanding request; RynthAi stays paused for review.";SavePrep();Local(notice);}
            else if(cmd=="status")Local("v"+Version+" | prep "+prep.Data.State+" "+prep.Data.Notice+" | "+notice);
            else if(cmd=="export")journal?.Submit(new("command","export",DateTime.UtcNow));
        }
        if(starts.TryDequeue(out var requested))StartPrep(requested);
        while(prepChat.TryDequeue(out var line)){Interlocked.Decrement(ref queued);prep.Observe(line.Text,line.Type);}
        if(identity==null)return;
        if(Now>=nextStep)
        {
            nextStep=Now+.2;
            try
            {
                if(Lease.Active)held=HoldBot();
                if(waitingPlan!=null)AdvanceStart();
                if(prep.Running)
                {
                    var world=World();
                    if(world.Safe&&!world.Busy&&pendingIdentify!=0&&DateTimeOffset.UtcNow.ToUnixTimeSeconds()>identifyAfter[pendingIdentify])
                    {uint pending=pendingIdentify;pendingIdentify=0;if(!Host.RequestId(pending))prep.Refused("Rynth refused identification");}
                    var action=prep.Step(world,Now);SavePrep();
                    if(action!=null)Dispatch(action);
                    if(!prep.Running){settleAfter=Now+2;Local("Gem maker: "+prep.Data.State+" — "+prep.Data.Notice);}
                }
                else if(Host.HasGetSelectedItemId)
                {
                    uint selected=Host.GetSelectedItemId();
                    if(selected!=0&&Host.TryGetObjectName(selected,out string itemName)&&ThreadFragmentInspection.FragmentType(itemName).Length>0)
                    {string desc=Description(selected);journal?.Submit(new("fragment",desc,DateTime.UtcNow,unchecked((int)selected),itemName));}
                }
            }
            catch(Exception ex){waitingPlan=null;prep.Refused(ex.GetType().Name+": "+ex.Message);notice=ex.Message;Local("Gem maker: "+notice);try{SavePrep();}catch{};if(Lease.Active)Lease.Release(false);held=false;}
            if(Lease.Active&&waitingPlan==null&&!prep.Running&&Now>=settleAfter)FinishPause();
        }
        if(panel.Dirty&&DateTime.UtcNow>=saveUi)
        {saveUi=DateTime.UtcNow.AddSeconds(2);try{panel.Save(Path.Combine(folder,"ui.json"));panel.Dirty=false;}catch(Exception ex){notice="UI save failed: "+ex.GetType().Name;}}
    }
    private string Description(uint id)=>Host.TryGetObjectStringProperty(id,16,out string desc)?desc:"";
    private PrepItem[] Inventory()
    {
        var seen=new HashSet<uint>();var found=new List<PrepItem>();var containers=new Queue<uint>();containers.Enqueue(player);
        var buf=new uint[2048];
        while(containers.TryDequeue(out uint container)&&seen.Count<2048)
        {
            int n=Host.GetContainerContents(container,buf);if(n<0)throw new InvalidOperationException("Inventory snapshot unavailable");
            for(int i=0;i<n&&i<buf.Length;i++)
            {
                uint id=buf[i];if(!seen.Add(id))continue;if(!Host.TryGetObjectName(id,out string name))continue;
                int count=Host.TryGetObjectIntProperty(id,12,out int stack)&&stack>0?stack:1;
                bool identified=Host.HasAppraisalData(id);
                if(identifyAfter.TryGetValue(id,out long prior))
                {
                    identified=Host.TryGetObjectProperties(id,out var properties)&&properties is {HasIdData:true}&&properties.LastIdTime>prior;
                    if(identified)identifyAfter.Remove(id);
                }
                found.Add(new(id,name,count,Description(id),identified));
                if(Host.GetNumContainedItems(id)>0)containers.Enqueue(id);
            }
        }
        if(seen.Count>=2048)throw new InvalidOperationException("Inventory snapshot exceeded its bound");
        return found.ToArray();
    }
    private PrepWorld World(bool skipBot=false)
    {
        if(player==0||!Host.HasUseObjectOn||!Host.HasRequestId||!Host.HasGetContainerContents||!Host.HasGetObjectPosition||!Host.HasGetBusyState||!Host.HasGetPlayerVitals||!Host.HasGetObjectIntProperty||!Host.HasGetObjectProperties||!Host.HasGetPluginInterface||!Host.HasVendorTrade)
            throw new InvalidOperationException("Required stock Rynth APIs are unavailable");
        if(!Host.TryGetObjectPosition(player,out uint cell,out float px,out float py,out float pz))throw new InvalidOperationException("Player position unavailable");
        bool moved=anchor.HasValue&&(anchor.Value.Cell!=cell||Math.Abs(anchor.Value.X-px)>.5||Math.Abs(anchor.Value.Y-py)>.5||Math.Abs(anchor.Value.Z-pz)>.5);
        int mode=Host.HasGetCurrentCombatMode?Host.GetCurrentCombatMode():0;
        bool peaceful=mode==1;
        bool alive=Host.TryGetPlayerVitals(out uint hp,out _,out _,out _,out _,out _)&&hp>0;
        string blocker=journal?.Ready!=true?"Local journal is not ready":waiting||identity==null||Host.GetPlayerId()!=player?"Login identity changed":!alive?"Player vitals unavailable or health is zero":!peaceful?"Current combat mode is "+mode+"; switch to peace mode":moved?"Player moved away from the batch start":!Host.HasIsPortaling?"Portal-state API is unavailable":Host.IsPortaling()?"Player is portaling":skipBot?"":BotBlocker();
        bool safe=blocker.Length==0;
        var live=Host.GetLiveObjectIds();
        StationMatch Nearby(string name)
        {
            var candidates=new List<StationCandidate>();
            foreach(uint id in live)
            {
                if(!Host.TryGetObjectName(id,out string n)||n!=name)continue;
                StationPosition? pos=Host.TryGetObjectPosition(id,out uint c,out float x,out float y,out float z)?new(c,x,y,z):null;
                double? radius=Host.HasGetObjectDoubleProperty&&Host.TryGetObjectDoubleProperty(id,54,out double r)?r:null;
                candidates.Add(new(id,n,pos,radius));
            }
            return StationLocator.Find(name,new(cell,px,py,pz),candidates);
        }
        var press=Nearby("Fragment Press");
        var fragmentVendor=Nearby("Fragment-Clerk Maren Idswold");
        var reagentVendor=Nearby("Reagent-Steward Aya Torimaru");
        uint vendor=0;PrepStock[] stock=[];
        if(Host.TryGetVendorInfo(out var info))
        {
            vendor=info.VendorId;
            if(!info.UsesAltCurrency&&info.TradingAvailable&&!info.TradeInFlight)stock=Host.GetVendorItems().Select(i=>new PrepStock(i.ObjectId,i.Name,i.Amount,i.UnitPrice)).ToArray();
        }
        return new(player,safe,Host.GetBusyState()!=0,Inventory(),press.Id,fragmentVendor.Id,reagentVendor.Id,vendor,stock,blocker,press.Problem,fragmentVendor.Problem,reagentVendor.Problem);
    }
    private unsafe IntPtr BotInterface(string name,int pointers)
    {
        IntPtr ptr=Host.GetPluginInterface("RynthAi",name,1);
        if(ptr==IntPtr.Zero)throw new InvalidOperationException("RynthAi interface "+name+" v1 is unavailable; enable/update RynthAi.");
        uint* header=(uint*)ptr;
        if(header[0]<1||header[1]<8+pointers*IntPtr.Size)throw new InvalidOperationException("RynthAi interface "+name+" has an unsupported layout.");
        return ptr;
    }
    private unsafe bool ReadMacro()
    {
        var fn=*(delegate* unmanaged[Cdecl]<int>*)((byte*)BotInterface("RynthAi.Script",5)+8+4*IntPtr.Size);
        if(fn==null)throw new InvalidOperationException("RynthAi macro-state function is unavailable.");
        return fn()!=0;
    }
    private unsafe void SetMacro(bool running)
    {
        var fn=*(delegate* unmanaged[Cdecl]<int,void>*)((byte*)BotInterface("RynthAi.Script",6)+8+5*IntPtr.Size);
        if(fn==null)throw new InvalidOperationException("RynthAi macro-control function is unavailable.");
        fn(running?1:0);
    }
    private unsafe bool HoldBot()
    {
        IntPtr ptr=Host.GetPluginInterface("RynthAi","RynthAi.Yield",1);
        if(ptr==IntPtr.Zero)return false;
        uint* header=(uint*)ptr;if(header[0]<1||header[1]<8+2*IntPtr.Size)return false;
        var fn=*(delegate* unmanaged[Cdecl]<uint,int>*)((byte*)ptr+8);
        return fn!=null&&fn(3000)==1;
    }
    private unsafe void ReleaseHold()
    {
        if(waiting)return; // An expiring hold is safe across logout; never resume another login.
        IntPtr ptr=Host.GetPluginInterface("RynthAi","RynthAi.Yield",1);if(ptr==IntPtr.Zero)return;
        uint* header=(uint*)ptr;if(header[0]<1||header[1]<8+2*IntPtr.Size)return;
        var fn=*(delegate* unmanaged[Cdecl]<int>*)((byte*)ptr+8+IntPtr.Size);if(fn!=null)fn();
    }
    private unsafe string BotBlocker()
    {
        if(ReadMacro())return "RynthAi macro is running; it was restarted while this batch owned the pause.";
        var fn=*(delegate* unmanaged[Cdecl]<uint*,int>*)((byte*)BotInterface("RynthAi.Activity",1)+8);
        uint flags=0;if(fn==null||fn(&flags)!=1)return "RynthAi activity is not ready.";
        // AutoCram reports its enabled setting, even with the macro stopped.
        // Ignore that setting only while an acknowledged expiring Yield prevents item moves.
        uint blocked=flags&~(64u|(held?256u:0u));
        if(blocked==0)return "";
        string[] names=["MacroRunning","Buffing","Combat","Looting","Salvaging","Navigating","ServerAction","Crafting","AutoCram","PacedAction"];
        return "RynthAi is not idle: "+string.Join(", ",Enumerable.Range(0,names.Length).Where(i=>(blocked&(1u<<i))!=0).Select(i=>names[i]))+((blocked&256)!=0?". Yield is unavailable; disable AutoCram for this batch.":".");
    }
    private void AdvanceStart()
    {
        var world=World();
        if(Now>pauseDeadline){waitingPlan=null;resumeAfterStop=true;settleAfter=Now+2;notice="Start refused: "+(world.Problem.Length>0?world.Problem:"client remained busy")+". No gem action issued.";Local(notice);return;}
        if(!world.Safe||world.Busy){notice="Waiting for RynthAi/client to settle: "+(world.Problem.Length>0?world.Problem:"client busy");return;}
        var plan=waitingPlan!;waitingPlan=null;
        if(prep.Start(plan,world,identity!.Server,identity.Character,DateTime.UtcNow,Now))
        {pendingIdentify=0;identifyAfter.Clear();SavePrep();notice="Batch started; RynthAi is held until preparation finishes.";Local(notice);}
    }
    private void FinishPause()
    {
        try
        {
            if(Host.GetBusyState()!=0)return;
            bool canResume=prep.Data.State!="Needs review"&&(prep.Data.State=="Completed"||resumeAfterStop)&&World(skipBot:true).Safe;
            Lease.Release(canResume);held=false;
            if(!canResume)Local("RynthAi pause released without resuming the macro; review the stopped/uncertain batch first.");
            resumeAfterStop=false;
        }
        catch(Exception ex){Lease.Release(false);held=false;Local("RynthAi left paused: "+ex.Message);}
    }
    private void Dispatch(PrepAction a)
    {
        if(a.Kind=="Identify")
        {
            long prior=Host.TryGetObjectProperties(a.Source,out var properties)&&properties!=null?properties.LastIdTime:0;
            identifyAfter[a.Source]=prior;
            // The API clock has whole-second resolution: issue in a later second
            // so only a genuinely newer appraisal can pass the result gate.
            if(DateTimeOffset.UtcNow.ToUnixTimeSeconds()<=prior){pendingIdentify=a.Source;return;}
        }
        bool ok=a.Kind switch {
            "Identify"=>Host.RequestId(a.Source),
            "Open vendor"=>Host.UseObject(a.Source),
            "Buy"=>Host.VendorBuy(a.Target,[new VendorTradeEntryNative(a.Source,a.Amount)])!=0,
            "Dose"=>Host.UseObjectOn(a.Source,a.Target),
            "Press"=>Host.UseObjectOn(a.Source,a.Target),
            _=>false};
        if(!ok){prep.Refused("Rynth refused the "+a.Kind+" request");SavePrep();}
        if(a.Kind=="Press")journal?.Submit(new("prep-recipe",string.Join(", ",prep.Data.Plan.Doses()),DateTime.UtcNow,unchecked((int)a.Source),StartingLevel:prep.Data.Plan.Level));
    }
    private void SavePrep()
    {
        if(folder.Length==0||!prepReadable||prep.Data.Server.Length==0)return;string json=JsonSerializer.Serialize(prep.Data);if(json==previousCheckpoint)return;
        string path=Path.Combine(folder,"prep.json"),temp=path+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){var bytes=System.Text.Encoding.UTF8.GetBytes(json);stream.Write(bytes);stream.Flush(true);}
        File.Move(temp,path,true);previousCheckpoint=json;
    }
    private void StartPrep(PrepPlan plan)
    {
        try
        {
            if(prep.Running||waitingPlan!=null||Lease.Active){Local("A batch is already active or settling.");return;}
            plan.Validate();
            if(!prepReadable)throw new InvalidOperationException("Existing local state could not be read; preserve it for review.");
            anchor=null;var world=World(skipBot:true);
            if(!world.Safe)throw new InvalidOperationException(world.Problem);
            if(world.PressId==0)throw new InvalidOperationException(world.PressProblem.Length>0?world.PressProblem:"Fragment Press could not be resolved.");
            Host.TryGetObjectPosition(player,out uint c,out float x,out float y,out float z);anchor=(c,x,y,z);
            Lease.Acquire();resumeAfterStop=false;held=HoldBot();waitingPlan=plan;pauseDeadline=Now+15;
            notice="Start received. RynthAi paused; waiting for outstanding actions to settle.";Local(notice);
        }
        catch(Exception ex){waitingPlan=null;notice="Start refused: "+ex.Message;Local(notice);if(Lease.Active){resumeAfterStop=true;settleAfter=Now+2;}}
    }
    public override void OnRender()
    {
        if(identity==null||renderFailed)return;
        try{panel.Draw(identity.Character,journal?.View??ThreadView.Empty,prep,notice,p=>{if(starts.IsEmpty)starts.Enqueue(JsonSerializer.Deserialize<PrepPlan>(JsonSerializer.Serialize(p))!);},()=>commands.Enqueue("stop"),s=>journal?.Submit(new("command",s,DateTime.UtcNow)));}
        catch(Exception ex){renderFailed=true;Local("Thread panel hidden after error: "+ex.GetType().Name+" "+ex.Message);}
    }
    private void End()
    {
        waitingPlan=null;resumeAfterStop=false;if(botLease?.Active==true){try{botLease.Release(false);}catch{}}held=false;
        prep.Stop("Login ended");try{SavePrep();}catch{};
        if(journal!=null){journal.Dispose();closing=journal.Completion;}journal=null;identity=null;player=0;pendingIdentify=0;identifyAfter.Clear();folder="";anchor=null;previousCheckpoint="";prep.Restore(new());
        while(starts.TryDequeue(out _)){}while(commands.TryDequeue(out _)){}
        while(prepChat.TryDequeue(out _))Interlocked.Decrement(ref queued);
    }
    private void Local(string text){Host.Log("[ACAI2 Threads] "+text);if(Host.HasWriteToChat)Host.WriteToChat("[ACAI2 Threads] "+text,1);}
    public override void Shutdown()=>End();
}
