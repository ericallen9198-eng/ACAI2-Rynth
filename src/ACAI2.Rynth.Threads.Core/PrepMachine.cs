using System.Text.RegularExpressions;
namespace ACAI2.Rynth.Threads.Core;

public sealed record PrepItem(uint Id,string Name,int Count,string Description,bool Identified);
public sealed record PrepStock(uint Id,string Name,int Amount,int UnitPrice);
public sealed record PrepWorld(uint PlayerId,bool Safe,bool Busy,PrepItem[] Inventory,uint PressId,uint FragmentVendor,uint ReagentVendor,uint OpenVendor,PrepStock[] Stock,string Problem="");
public sealed record PrepAction(string Kind,uint Source=0,uint Target=0,int Amount=0,string Detail="");
public sealed class PrepPlan
{
    public int Target {get;set;}=1;
    public int Level {get;set;}=325;
    public string BlankName=>"Raw Fragment (Level "+Level+")";
    public bool TopUp {get;set;}
    public bool BuyMissing {get;set;}
    public int Budget {get;set;}=250000;
    public string Scarab {get;set;}="Lead Scarab";
    public string Herb {get;set;}="Hyssop";
    public string Powder {get;set;}="Powdered Amber";
    public string Reagent {get;set;}="Brimstone";
    public string Talisman {get;set;}="";
    public string Taper {get;set;}="Brown Taper";
    public int TaperCount {get;set;}=4;
    public string[] Doses()=>new[]{Scarab,Herb,Powder,Reagent,Talisman}.Where(s=>s.Length>0).Concat(Enumerable.Repeat(Taper,TaperCount)).ToArray();
    public void Validate()
    {
        if(Level is not (300 or 325 or 350 or 375))throw new InvalidDataException("Choose blank level 300, 325, 350 or 375.");
        if(Target is <1 or >50||Budget is <0 or >10000000||TaperCount is <0 or >4)throw new InvalidDataException("Batch 1..50, tapers 0..4, budget 0..10,000,000.");
        var doses=Doses();
        if(doses.Length is <1 or >9||new[]{Scarab,Herb,Powder,Reagent,Talisman,Taper}.Any(n=>n.Length>100||n.Any(char.IsControl)||n.Contains(',')||n.Contains('`'))||Scarab.Length==0||TaperCount>0&&Taper.Length==0)
            throw new InvalidDataException("Use 1..9 exact component doses and a scarab; component names must be plain item names.");
    }
}
public sealed class PrepCheckpoint
{
    public int Schema {get;set;}=1;
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Server {get;set;}="";
    public string Character {get;set;}="";
    public PrepPlan Plan {get;set;}=new();
    public string State {get;set;}="Idle";
    public string Notice {get;set;}="";
    public int Needed {get;set;}
    public int Done {get;set;}
    public int Dose {get;set;}
    public uint Fragment {get;set;}
    public int ReservedSpend {get;set;}
    public List<uint> Finished {get;set;}=[];
    public DateTime StartedUtc {get;set;}
}
// Deterministic controller. It returns one request at a time; only the native pump
// may dispatch it, after a durable checkpoint. No Lua, meta interpreter or Rynth fork.
public sealed class PrepMachine
{
    public PrepCheckpoint Data {get;private set;}=new();
    public bool Running=>Data.State is not ("Idle" or "Completed" or "Stopped" or "Needs review");
    private readonly HashSet<uint> initial=[];
    private readonly HashSet<uint> usedFragments=[];
    private double deadline,next;
    private int beforeCount;
    private string ingredient="",buyName="";
    private bool loadAck,pressAck;
    private HashSet<uint> beforeGems=[];
    private uint newGem;
    public bool Start(PrepPlan plan,PrepWorld world,string server,string character,DateTime utc,double now)
    {
        if(Running)return false;plan.Validate();
        if(!world.Safe||world.Busy||world.PressId==0)throw new InvalidOperationException(world.Problem.Length>0?world.Problem:"Pause RynthAi, stop other crafting, enter peace mode and stand beside one Fragment Press.");
        plan=System.Text.Json.JsonSerializer.Deserialize<PrepPlan>(System.Text.Json.JsonSerializer.Serialize(plan))!;
        initial.Clear();usedFragments.Clear();foreach(var i in world.Inventory)if(i.Name=="Thread Gem")initial.Add(i.Id);
        int have=world.Inventory.Count(i=>i.Name=="Thread Gem"&&Usable(i));
        if(plan.TopUp&&world.Inventory.Any(i=>i.Name=="Thread Gem"&&!i.Identified))throw new InvalidOperationException("Examine existing Thread Gems before top-up; unknown gems are not treated as usable or empty.");
        Data=new(){Plan=plan,Server=server,Character=character,StartedUtc=utc,Needed=plan.TopUp?Math.Max(0,plan.Target-have):plan.Target,State="Supply check"};
        next=now;deadline=now+30;newGem=0;
        if(Data.Needed==0){Data.State="Completed";Data.Notice="Already at the requested usable-gem stock; no purchases or crafting.";}
        return true;
    }
    public void Stop(string reason="Stopped by user") {if(Running){Data.State="Stopped";Data.Notice=reason+". An issued action may still finish; partial fragments require review. Nothing will auto-resume.";}}
    public void Restore(PrepCheckpoint previous)
    {Data=previous;if(Running){Data.State="Needs review";Data.Notice="Interrupted batch retained for review. No automatic redose, repress or recovery by historical item IDs.";}}
    public void Observe(string raw,int type=0)
    {
        if(!Running||!ThreadText.Native(raw,type))return;
        string line=raw.Trim();
        if(Data.State=="Awaiting dose"&&line.StartsWith("The fragment takes the "+ingredient+".",StringComparison.Ordinal))loadAck=true;
        if(Data.State=="Awaiting press"&&line=="Pressed. It holds.")pressAck=true;
    }
    public void Refused(string message){if(Running){Data.State="Needs review";Data.Notice=message+"; no automatic retry of a possibly issued action.";}}
    public PrepAction? Step(PrepWorld w,double now)
    {
        if(!Running)return null;
        if(!w.Safe){Stop(w.Problem.Length>0?w.Problem:"Combat, movement, portal, death, other macro activity or identity changed");return null;}
        if(Data.Finished.Any(id=>!w.Inventory.Any(i=>i.Id==id&&i.Name=="Thread Gem"))){Refused("A completed batch gem left inventory");return null;}
        if(now<next)return null;
        if(now>deadline){Refused("Timed out in "+Data.State);return null;}
        if(Data.State=="Awaiting purchase")
        {
            if(Count(w,buyName)>=beforeCount+DataAmount){Advance("Supply check",now);return null;}
            return null;
        }
        if(Data.State=="Awaiting vendor")
        {if(w.OpenVendor==waitingVendor)Advance("Supply check",now);return null;}
        if(Data.State=="Awaiting dose")
        {
            int count=Count(w,ingredient);
            if(count<beforeCount-1){Refused("Component count changed by more than one");return null;}
            if(loadAck&&count==beforeCount-1){Data.Dose++;Advance("Loading",now,1);}
            return null;
        }
        if(Data.State=="Awaiting press")
        {
            var fresh=w.Inventory.Where(i=>i.Name=="Thread Gem"&&!beforeGems.Contains(i.Id)).ToArray();
            if(fresh.Length>1){Refused("Multiple new gems appeared; result attribution ambiguous");return null;}
            if(pressAck&&fresh.Length==1&&!w.Inventory.Any(i=>i.Id==Data.Fragment&&i.Name.StartsWith("Raw Fragment",StringComparison.Ordinal)))
            {newGem=fresh[0].Id;Advance("Inspecting result",now);return new("Identify",newGem);}
            return null;
        }
        if(Data.State=="Inspecting result")
        {
            var gem=w.Inventory.SingleOrDefault(i=>i.Id==newGem);
            if(gem is not {Identified:true})return null;
            if(gem.Description.Contains("Unpressed.",StringComparison.Ordinal)||!gem.Description.Contains("Unbound.",StringComparison.Ordinal)||!gem.Description.Contains("Modifiers:",StringComparison.Ordinal)||!Regex.IsMatch(gem.Description,@"(?m)^Entries: [1-9]\d* of \d+\s*$")){Refused("New gem result description is unreadable or lacks remaining entries");return null;}
            Data.Finished.Add(gem.Id);Data.Done++;Data.Fragment=0;Data.Dose=0;
            Data.Notice=Regex.IsMatch(gem.Description,@"(?m)^Stalwart\s*:")?"Stalwart result recorded and kept for review; this version does not destroy gems.":"New finished gem confirmed and recorded.";
            if(Data.Done>=Data.Needed){Data.State="Completed";return null;}
            Advance("Supply check",now,1);return null;
        }
        if(Data.State=="Inspecting fragment")
        {
            var fragment=w.Inventory.SingleOrDefault(i=>i.Id==Data.Fragment);
            if(fragment is not {Identified:true})return null;
            if(!Blank(fragment,Data.Plan.Level)){Refused("Fragment is not a verified untouched level-"+Data.Plan.Level+" blank");return null;}
            Advance("Loading",now,1);return null;
        }
        if(w.Busy)return null;
        if(Data.State=="Supply check")
        {
            var need=Data.Plan.Doses().GroupBy(x=>x).ToDictionary(x=>x.Key,x=>x.Count()*(Data.Needed-Data.Done));
            need[Data.Plan.BlankName]=Data.Needed-Data.Done;
            foreach(var missing in need.Where(p=>Count(w,p.Key)<p.Value))
            {
                if(!Data.Plan.BuyMissing){Refused("Missing "+missing.Key+": need "+(missing.Value-Count(w,missing.Key)));return null;}
                uint vendor=missing.Key.StartsWith("Raw Fragment",StringComparison.Ordinal)?w.FragmentVendor:w.ReagentVendor;
                if(vendor==0){Refused("Stand beside the required named vendor");return null;}
                if(w.OpenVendor!=vendor){waitingVendor=vendor;Advance("Awaiting vendor",now);return new("Open vendor",vendor);}
                var rows=w.Stock.Where(s=>s.Name==missing.Key).ToArray();
                if(rows.Length!=1){Refused("Vendor stock has no unique exact match for "+missing.Key);return null;}
                int amount=missing.Value-Count(w,missing.Key);var stock=rows[0];
                long cost=(long)stock.UnitPrice*amount;
                if(stock.UnitPrice<=0||stock.Amount>=0&&stock.Amount<amount||cost>Data.Plan.Budget-Data.ReservedSpend){Refused("Price/stock/budget check failed for "+missing.Key);return null;}
                Data.ReservedSpend+=(int)cost;buyName=missing.Key;beforeCount=Count(w,buyName);DataAmount=amount;Advance("Awaiting purchase",now);
                return new("Buy",stock.Id,vendor,amount,buyName);
            }
            var blanks=w.Inventory.Where(i=>i.Name==Data.Plan.BlankName&&!usedFragments.Contains(i.Id)).ToArray();
            if(blanks.Length==0){Refused("No unused level-"+Data.Plan.Level+" blank remains");return null;}
            var selected=blanks.FirstOrDefault(i=>Blank(i,Data.Plan.Level))??blanks.FirstOrDefault(i=>!i.Identified);
            if(selected==null){Refused("Only altered fragments remain; no redosing");return null;}
            Data.Fragment=selected.Id;usedFragments.Add(selected.Id);Advance("Inspecting fragment",now);return new("Identify",selected.Id);
        }
        if(Data.State=="Loading")
        {
            if(Data.Dose>=Data.Plan.Doses().Length)
            {
                if(w.PressId==0){Refused("Fragment Press missing");return null;}
                beforeGems=w.Inventory.Where(i=>i.Name=="Thread Gem").Select(i=>i.Id).ToHashSet();pressAck=false;
                Advance("Awaiting press",now);return new("Press",Data.Fragment,w.PressId);
            }
            ingredient=Data.Plan.Doses()[Data.Dose];
            var stack=w.Inventory.FirstOrDefault(i=>i.Name==ingredient&&i.Count>0);
            if(stack==null||!w.Inventory.Any(i=>i.Id==Data.Fragment)){Refused("Fragment or component disappeared");return null;}
            beforeCount=Count(w,ingredient);loadAck=false;Advance("Awaiting dose",now);
            // Raw Fragment is the source; component stack is the target, as in the game instructions.
            return new("Dose",Data.Fragment,stack.Id,1,ingredient);
        }
        return null;
    }
    private int DataAmount;
    private uint waitingVendor;
    private void Advance(string state,double now,double settle=0){Data.State=state;deadline=now+30;next=now+settle;}
    private static int Count(PrepWorld w,string name)=>w.Inventory.Where(i=>i.Name==name).Sum(i=>i.Count);
    public static bool Blank(PrepItem i,int level=325)=>i.Identified&&i.Name=="Raw Fragment (Level "+level+")"&&Regex.IsMatch(i.Description,@"(?m)^Level: "+level+@"\s*$")&&i.Description.Contains("Modifiers: none",StringComparison.Ordinal)&&i.Description.Contains("Pressed: nothing",StringComparison.Ordinal)&&i.Description.Contains("Unpressed.",StringComparison.Ordinal);
    private static bool Usable(PrepItem i)=>i.Identified&&!Regex.IsMatch(i.Description,@"(?m)^Stalwart\s*:")&&Regex.IsMatch(i.Description,@"(?m)^Entries: [1-9]\d* of \d+\s*$");
}
