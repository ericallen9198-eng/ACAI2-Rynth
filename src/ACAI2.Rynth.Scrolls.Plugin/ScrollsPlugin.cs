using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ACAI2.Rynth.Scrolls.Core;
using RynthCore.PluginCore;
namespace ACAI2.Rynth.Scrolls.Plugin;

internal sealed record UiRequest(long Epoch,string Command,string Value="",string[]? Keys=null,float X=0,float Y=0);
internal sealed record ScrollView(long Epoch,string Character,string Status,Checklist[] Rows,Supply[] Supplies,string[] Selected,string School,string Search,bool MissingOnly,bool Running,bool Review,int Completed,int Total,string[] History,float X,float Y,bool Visible,string Tab="Self",bool EssentialsOnly=false,int SkillsAvailable=0)
{public static ScrollView Empty=>new(0,"Waiting for login","Log in to scan learned level VIII spells.",[],[],[],"All","",true,false,false,0,0,[],90,90,true);}
public sealed class ScrollsPlugin:RynthPluginBase
{
    public const string Version="0.1.2";
    public override uint MinimumApiVersion=>92;
    private readonly ConcurrentQueue<UiRequest> requests=new();
    private readonly ScrollPanel panel=new();
    private Catalog catalog=null!;private ScrollProfile? profile;private CraftGuard? guard;private CraftBatch? batch;
    private ScrollView view=ScrollView.Empty;private Checklist[] rows=[];private InventoryItem[] inventory=[];
    private string path="",status="Ready to scan";private uint player;private long epoch;private int reset;
    private volatile bool loggedOut;private bool readable=true,stopRequested,renderFailed;
    private int skillsAvailable;
    private double nextProbe,nextScan,nextTick,sentAt,settleAt,idleDeadline;
    private (uint Cell,float X,float Y,float Z)? anchor;
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    public override int Initialize(){catalog=Catalog.Load();catalog.Validate();Host.Log("[ACAI2 Scrolls] v"+Version+" — /acai2scrolls opens the level VIII scroll maker.");return 0;}
    public override void OnLoginComplete(){loggedOut=false;Interlocked.Exchange(ref reset,1);}
    public override void OnLogout(){loggedOut=true;Interlocked.Exchange(ref reset,1);}
    public override void OnChatBarEnter(string? text,ref int eat)
    {
        var s=(text??"").Trim();if(!s.Equals("/acai2scrolls",StringComparison.OrdinalIgnoreCase)&&!s.Equals("/acai2scrolls stop",StringComparison.OrdinalIgnoreCase))return;
        eat=1;Queue(new(Volatile.Read(ref epoch),s.EndsWith(" stop",StringComparison.OrdinalIgnoreCase)?"stop":"show"));
    }
    private void Queue(UiRequest request){if(requests.Count<32)requests.Enqueue(request);}
    private void Local(string text){Host.Log(text);if(Host.HasWriteToChat)Host.WriteToChat(text,1);}
    private void Save(){if(!readable||profile==null)throw new InvalidOperationException("Profile cannot be saved; no crafting will be issued.");profile.Save(path);}
    private void Reset()
    {
        // Expiring yield is allowed to lapse; a different login must never resume the prior macro.
        Interlocked.Increment(ref epoch);batch=null;guard=null;profile=null;player=0;rows=[];inventory=[];skillsAvailable=0;anchor=null;readable=true;stopRequested=false;
        while(requests.TryDequeue(out _)){}Volatile.Write(ref view,ScrollView.Empty with {Epoch=epoch});
    }
    private void Bind()
    {
        uint id=Host.GetPlayerId();if(id==0||!Host.TryGetWorldName(out string server)||!Host.TryGetObjectName(id,out string name))return;
        if(id==player&&profile?.Server==server&&profile.Character==name)return;
        Reset();player=id;string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(server+"\n"+name)));
        path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ACAI2","Scrolls",key,"profile.json");
        try{profile=ScrollProfile.Load(path,server,name);status=profile.Pending==null?"Ready — scan your spellbook and choose scrolls.":"Interrupted craft needs review. Check the inventory before clearing its checkpoint.";}
        catch(Exception ex){readable=false;profile=new(){Server=server,Character=name};status="Saved profile preserved: "+ex.Message;}
        guard=new(Host);nextScan=0;panel.Reload();
    }
    private InventoryItem[] Inventory()
    {
        var found=new List<InventoryItem>();var seen=new HashSet<uint>();var containers=new Queue<uint>();containers.Enqueue(player);uint[] buf=new uint[2048];
        while(containers.TryDequeue(out uint container))
        {
            int n=Host.GetContainerContents(container,buf);if(n<0||n>=buf.Length)throw new InvalidOperationException("Inventory read incomplete; open your packs and rescan.");
            for(int i=0;i<n;i++)
            {
                uint id=buf[i];if(!seen.Add(id))continue;if(seen.Count>2048)throw new InvalidOperationException("Inventory exceeds scan bound.");
                if(!Host.TryGetObjectWcid(id,out uint template)||!Host.TryGetObjectName(id,out string name))throw new InvalidOperationException("Inventory metadata incomplete; wait and rescan.");
                int count=Host.TryGetObjectIntProperty(id,12,out int stack)&&stack>0?stack:1;found.Add(new(id,template,name,count));
                if(Host.GetNumContainedItems(id)>0)containers.Enqueue(id);
            }
        }
        return found.ToArray();
    }
    private SpellbookSnapshot ReadSpellbook()
    {
        if(!Host.HasReadKnownSpells)throw new InvalidOperationException("Update RynthCore: the learned-spell snapshot API is unavailable.");
        return SpellbookSnapshot.Read(Host.ReadKnownSpells);
    }
    private void Scan()
    {
        if(!Host.HasGetContainerContents||!Host.HasGetObjectWcid)throw new InvalidOperationException("Update RynthCore: inventory APIs are required.");
        var book=ReadSpellbook();var items=Inventory();
        // TryGetObjectSkill explicitly serves an owner-bound cached player-skill
        // snapshot off-thread. A missing skill read is unavailable, not untrained.
        var training=new Dictionary<uint,int>();
        if(Host.HasGetObjectSkill)
            foreach(var skill in SpellRecommendations.Skills)
                if(Host.TryGetObjectSkill(player,skill.Id,out _,out int state)&&state is >=0 and <=3)training[skill.Id]=state;
        var result=book.Checklist(catalog,items,training);skillsAvailable=training.Count;
        inventory=items;rows=result;
    }
    private string Safety()
    {
        if(Host.GetPlayerId()!=player||loggedOut)return "Character changed";
        if(!Host.HasIsPortaling||Host.IsPortaling())return "Portal travel";
        if(!Host.HasGetCurrentCombatMode||Host.GetCurrentCombatMode()!=1)return "Switch to peace mode";
        if(!Host.TryGetPlayerVitals(out uint hp,out _,out _,out _,out _,out _)||hp==0)return "Character is not alive/ready";
        if(!Host.TryGetObjectPosition(player,out uint cell,out float x,out float y,out float z))return "Player position unavailable";
        if(anchor is {} a&&(cell!=a.Cell||Math.Sqrt(Math.Pow(x-a.X,2)+Math.Pow(y-a.Y,2)+Math.Pow(z-a.Z,2))>.5))return "Character moved; stand still while crafting";
        return "";
    }
    private void Start(double time)
    {
        if(!readable||profile?.Pending!=null)throw new InvalidOperationException("Review the previous craft before starting.");
        if(batch!=null)return;anchor=null;string why=Safety();if(why.Length>0)throw new InvalidOperationException(why);
        Scan();var selected=rows.Where(r=>profile!.Selected.Contains(r.Recipe.Key)&&!r.Known&&r.InPack==0).Select(r=>r.Recipe).ToArray();
        if(selected.Length==0)throw new InvalidOperationException("Select missing scrolls not already in your packs.");
        if(Planner.Shopping(selected,inventory).Any(s=>s.Missing>0))throw new InvalidOperationException("Components are short; check the Components tab.");
        if(!Host.HasUseObjectOn||!Host.HasGetBusyState)throw new InvalidOperationException("Native crafting APIs unavailable.");
        if(!Host.TryGetObjectPosition(player,out uint cell,out float x,out float y,out float z))throw new InvalidOperationException("Position unavailable");
        anchor=(cell,x,y,z);guard!.Acquire();batch=new(selected);stopRequested=false;settleAt=time+2;idleDeadline=time+30;status="RynthAi paused; waiting for actions to settle.";
    }
    private void End(string message,bool resume)
    {
        batch=null;anchor=null;stopRequested=false;status=message;
        try{guard?.Release(resume);}catch(Exception ex){status+=" Pause release: "+ex.Message;}
        nextScan=0;
    }
    private void Advance(double time)
    {
        if(batch==null||profile==null)return;
        string why=Safety();if(why.Length>0){End(why+". Batch stopped; RynthAi left stopped.",false);return;}
        if(!guard!.Hold()||guard.Running){End("Crafting hold was lost or RynthAi restarted; batch stopped.",false);return;}
        inventory=Inventory();
        if(profile.Pending is {} pending)
        {
            if(Planner.Acknowledged(pending,inventory)&&Host.GetBusyState()==0&&time>=sentAt+.6)
            {
                batch.Confirm(pending,inventory);profile.Pending=null;Save();settleAt=time+.5;idleDeadline=time+30;
                if(batch.Done){profile.Record(DateTimeOffset.UtcNow.ToString("u")+" Made "+batch.Completed+" scroll(s).");Save();End("Completed — "+batch.Completed+" scroll(s) left in your packs.",true);return;}
                if(stopRequested){End("Stopped after the confirmed craft. Progress is in your inventory.",true);return;}
                status="Confirmed: "+pending.Step.ResultName;
            }
            else if(time-sentAt>20)End("Craft was not confirmed. Inspect inventory before reviewing; RynthAi left stopped.",false);
            return;
        }
        if(stopRequested){End("Stopped between craft steps.",true);return;}
        if(time<settleAt)return;
        if(Host.GetBusyState()!=0||!guard.Idle())
        {if(time>=idleDeadline)End("Outstanding actions did not settle; no further craft issued.",false);return;}
        if(ReadSpellbook().Contains(batch.Current.SpellId)){End("The selected spell is now learned; rescan before continuing.",false);return;}
        var action=batch.Prepare(inventory);if(action.StepIndex==0&&Planner.Count(inventory,batch.Current.ScrollTemplate)>0){End("A selected scroll appeared in your packs; rescan before continuing.",false);return;}profile.Pending=action;Save(); // Durable BEFORE native dispatch.
        sentAt=time;status="Making "+batch.Current.Name+" — step "+(action.StepIndex+1)+"/3";
        if(!Host.UseObjectOn(action.SourceId,action.TargetId))End("Native craft request was not accepted/confirmed. Review inventory before another attempt.",false);
    }
    public override void OnTick()
    {
        if(Interlocked.Exchange(ref reset,0)!=0)Reset();if(loggedOut)return;double time=Now;if(time<nextTick)return;nextTick=time+.2;
        try
        {
            if(time>=nextProbe){nextProbe=time+1;Bind();}if(profile==null)return;
            while(requests.TryDequeue(out var r))
            {
                if(r.Epoch!=epoch)continue;
                switch(r.Command)
                {
                    case "show":profile.Visible=true;renderFailed=false;Save();break;
                    case "hide":profile.Visible=false;Save();break;
                    case "move":profile.X=r.X;profile.Y=r.Y;Save();break;
                    case "school":profile.School=r.Value;Save();break;
                    case "tab":if(r.Value is "Self" or "Others" or "Equipment" or "Components" or "Activity & recipes"){profile.Tab=r.Value;Save();}break;
                    case "essentials":profile.EssentialsOnly=r.Value=="1";Save();break;
                    case "search":profile.Search=r.Value[..Math.Min(96,r.Value.Length)];Save();break;
                    case "missing":profile.MissingOnly=r.Value=="1";Save();break;
                    case "add":if(batch==null){profile.Selected.UnionWith((r.Keys??[]).Where(k=>catalog.Recipes.Any(x=>x.Key==k)));Save();}break;
                    case "choose":if(batch==null&&catalog.Recipes.Any(x=>x.Key==r.Value)){profile.Selected.Add(r.Value);Save();}break;
                    case "unchoose":if(batch==null){profile.Selected.Remove(r.Value);Save();}break;
                    case "selection":if(batch==null){profile.Selected=(r.Keys??[]).Where(k=>catalog.Recipes.Any(x=>x.Key==k)).Take(250).ToHashSet();Save();}break;
                    case "print":
                        Local("[ACAI2 Scrolls] Selected missing-spell components (need / owned / short):");
                        foreach(var supply in Planner.Shopping(rows.Where(r=>profile.Selected.Contains(r.Recipe.Key)&&!r.Known&&r.InPack==0).Select(r=>r.Recipe),inventory))
                            Local($"[ACAI2 Scrolls] {supply.Name}: {supply.Required} / {supply.Owned} / {supply.Missing}");
                        break;
                    case "scan":if(batch==null){Scan();status="Scan complete — "+rows.Count(x=>!x.Known)+" missing, "+rows.Count(x=>!x.Known&&x.InPack>0)+" already in packs.";}break;
                    case "craft":Start(time);break;
                    case "stop":if(batch!=null){stopRequested=true;status="Stopping after the outstanding craft settles.";}break;
                    case "review":if(batch==null&&profile.Pending!=null){profile.Record(DateTimeOffset.UtcNow.ToString("u")+" User reviewed interrupted craft "+profile.Pending.RecipeKey+"; no action replayed.");profile.Pending=null;Save();Scan();status="Review recorded. Rescan supplies and explicitly start a new batch.";}break;
                }
            }
            Advance(time);
            if(batch==null&&time>=nextScan){nextScan=time+3;Scan();}
        }
        catch(Exception ex){if(batch!=null)End(ex.Message+". Batch stopped; check inventory.",false);else status=ex.Message;}
        Publish();
    }
    private void Publish()
    {
        if(profile==null)return;
        var selected=rows.Where(r=>profile.Selected.Contains(r.Recipe.Key)&&!r.Known&&r.InPack==0).Select(r=>r.Recipe);
        Volatile.Write(ref view,new(epoch,profile.Character,status,rows,Planner.Shopping(selected,inventory),profile.Selected.ToArray(),profile.School,profile.Search,profile.MissingOnly,batch!=null,profile.Pending!=null&&batch==null,batch?.Completed??0,batch?.Total??0,profile.Recent.ToArray(),profile.X,profile.Y,profile.Visible,profile.Tab,profile.EssentialsOnly,skillsAvailable));
    }
    public override void OnRender()
    {
        if(renderFailed)return;
        try{panel.Render(Volatile.Read(ref view),catalog?.Unresolved??[],Queue);}catch(Exception ex){renderFailed=true;Host.Log("[ACAI2 Scrolls] Panel error: "+ex.Message);}
    }
}
