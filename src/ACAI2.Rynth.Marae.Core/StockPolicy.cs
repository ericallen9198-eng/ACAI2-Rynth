using System.Globalization;
using System.Text;
using System.Text.Json;
namespace ACAI2.Rynth.Marae.Core;

public sealed record Hunter(int Id,string Name,int Zone,double NS,double EW,double Z,string Key="");
public sealed record RoutePoint(string Type,double NS,double EW,double Z,string Text="");
public sealed record RouteSnapshot(string ActiveNavName,int RouteType,int ActiveNavIndex,RoutePoint[] Points)
{
    public static RouteSnapshot Read(string json)
    {
        var route=JsonSerializer.Deserialize<RouteSnapshot>(json)??throw new InvalidDataException("No route snapshot.");
        if(route.Points==null||route.Points.Length is <1 or >12000||route.ActiveNavIndex<0||route.ActiveNavIndex>=route.Points.Length)throw new InvalidDataException("Invalid route snapshot.");
        return route;
    }
    public string ResumeFile()
    {
        if(RouteType is not (1 or 4))throw new InvalidDataException("Only the mission's circular and once hunt routes may be interrupted.");
        var points=RouteType==1?Points.Skip(ActiveNavIndex).Concat(Points.Take(ActiveNavIndex)):Points.Skip(ActiveNavIndex);
        return Serialize(RouteType,points.ToArray());
    }
    public static string Serialize(int kind,IReadOnlyList<RoutePoint> points)
    {
        if(points.Count is <1 or >12000)throw new InvalidDataException("Invalid waypoint count.");
        var s=new StringBuilder().AppendLine("uTank2 NAV 1.2").AppendLine(kind.ToString(CultureInfo.InvariantCulture)).AppendLine(points.Count.ToString(CultureInfo.InvariantCulture));
        foreach(var p in points)
        {
            if(!double.IsFinite(p.NS)||!double.IsFinite(p.EW)||!double.IsFinite(p.Z)||Math.Abs(p.NS)>103||Math.Abs(p.EW)>103)throw new InvalidDataException("Invalid waypoint coordinates.");
            int type=p.Type switch {"Point"=>0,"Checkpoint"=>8,"Chat"=>4,_=>throw new InvalidDataException("Cannot safely restore a route containing "+p.Type)};
            s.AppendLine(type.ToString()).AppendLine(p.EW.ToString("R",CultureInfo.InvariantCulture)).AppendLine(p.NS.ToString("R",CultureInfo.InvariantCulture)).AppendLine(p.Z.ToString("R",CultureInfo.InvariantCulture)).AppendLine("0");
            if(type==4)
            {
                if(p.Text.Length>4096||p.Text.IndexOfAny(['\r','\n'])>=0)throw new InvalidDataException("Invalid chat waypoint.");
                s.AppendLine(p.Text);
            }
        }
        return s.ToString();
    }
}
// Dialogue confirmation is independent of hunt target/count wording. Greetings alone do not count.
public sealed class NpcGate(Hunter hunter,double now)
{
    public Hunter Hunter=>hunter;
    public int Attempts {get;private set;}
    public bool Confirmed {get;private set;}
    public double Deadline {get;private set;}=now;
    public bool Due(double time)=>!Confirmed&&time>=Deadline&&Attempts<3;
    public bool Exhausted(double time)=>!Confirmed&&Attempts>=3&&time>=Deadline;
    public void Issued(double time){Attempts++;Deadline=time+8;}
    public bool Observe(string line)
    {
        if(Attempts==0)return false;
        string prefix=hunter.Name+" tells you, \"";
        if(!line.StartsWith(prefix,StringComparison.Ordinal)||!line.EndsWith('"'))return false;
        string body=line[prefix.Length..^1];
        Confirmed|=NpcDialogue.Parse(body)!=null;
        return Confirmed;
    }
}
public static class MapGeometry
{
    public static (double NS,double EW) Goal(double ns,double ew,double heading,double yards)
    {
        if(!double.IsFinite(ns)||!double.IsFinite(ew)||!double.IsFinite(heading)||!double.IsFinite(yards)||yards<=0||yards>180||heading<0||heading>=360)throw new InvalidDataException("Invalid clue step.");
        double n=ns+Math.Cos(heading*Math.PI/180)*yards/240,e=ew+Math.Sin(heading*Math.PI/180)*yards/240;
        if(Math.Abs(n)>102.85||Math.Abs(e)>102.85)throw new InvalidDataException("Clue step leaves the landscape.");return(n,e);
    }
    public static (double NS,double EW) StandOff(double ns,double ew,double targetNs,double targetEw,double yards)
    {
        double dn=ns-targetNs,de=ew-targetEw,d=Math.Sqrt(dn*dn+de*de)*240;
        return d<=yards?(ns,ew):(targetNs+dn*yards/d,targetEw+de*yards/d);
    }
}
// Observes native recovery; never starts a second corpse trip or relies on buffed maximum HP.
public sealed class RecoveryObserver
{
    private double started,settleUntil;
    private bool active,finished;
    public void Observe(string line,double now)
    {
        if(line.StartsWith("[RynthAi] Your corpse is at ",StringComparison.Ordinal)||line.StartsWith("[RynthAi] Own corpse: travelling",StringComparison.Ordinal))Start(now);
        if(line.StartsWith("[RynthAi] Own corpse: recovered everything",StringComparison.Ordinal)||line.StartsWith("[RynthAi] Own corpse: there was nothing",StringComparison.Ordinal)||
           line=="You have retained all your items. You do not need to recover your corpse!"||line.StartsWith("[RynthAi] Own corpse: travel back ended:",StringComparison.Ordinal))finished=true;
    }
    private void Start(double now){if(!active){active=true;started=now;finished=false;}settleUntil=now+6;}
    public bool Busy(double now,bool dead,bool portaling,bool nativeGoTo)
    {
        if(dead)Start(now);
        if(portaling&&active)settleUntil=now+6;
        if(nativeGoTo)return true;
        if(!active)return false;
        if(!dead&&!portaling&&now>=settleUntil&&(finished||now-started>=930)){active=false;return false;}
        return true;
    }
}
public sealed record PendingPickup(uint Container,uint Item,uint Template,int Amount,double InventoryBefore);
public static class CachePickupPolicy
{
    public static bool CanIssue(bool unresolved,uint item,uint container,uint checkpointItem,uint checkpointContainer,uint openContainer,bool cacheName,bool itemPresent,uint template,bool money,bool nativeMoveAvailable,bool pending)=>(unresolved&&!pending&&item!=0&&container!=0&&item==checkpointItem&&container==checkpointContainer&&container==openContainer&&cacheName&&itemPresent&&(template==1004101||money)&&nativeMoveAvailable);
    public static bool Confirmed(PendingPickup pending,double inventoryNow,bool sourceChanged)=>inventoryNow>pending.InventoryBefore&&sourceChanged;
}

public static class ResumePolicy
{
    public static bool CanStart(bool pending,bool initialized,bool recoveryBusy,bool portaling)=>pending&&initialized&&!recoveryBusy&&!portaling;
}
