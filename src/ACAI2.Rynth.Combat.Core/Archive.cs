using System.Text.Json;
using System.Threading.Channels;
namespace ACAI2.Rynth.Combat.Core;

public sealed record ArchiveWrite(string Id,string Json);
public sealed class CombatArchive : IDisposable
{
    public string Folder {get;}
    public string Notice=>Volatile.Read(ref notice);
    private string notice="Loading local combat records";
    public bool Ready=>Volatile.Read(ref ready)==1;
    private int ready;
    private readonly Channel<ArchiveWrite> queue=Channel.CreateBounded<ArchiveWrite>(new BoundedChannelOptions(4){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
    private RunRecord[] history=[];
    private readonly Dictionary<string,string> enqueued=[];
    public RunRecord[] History=>Volatile.Read(ref history);
    public Task Completion {get;}
    public Appearance LoadedAppearance {get;private set;}=new();
    public bool ProfileReadable {get;private set;}=true;
    public CombatArchive(string root,string server,string character)
    {Folder=Profiles.Folder(root,server,character);Completion=Task.Run(()=>Run(server,character));}
    private async Task Run(string server,string character)
    {
        try
        {
            Directory.CreateDirectory(Folder);using var owner=new FileStream(Path.Combine(Folder,"owner.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            try{LoadedAppearance=Profiles.Load(Path.Combine(Folder,"ui.json"));}catch(Exception ex){ProfileReadable=false;notice="Profile preserved after load error: "+ex.Message;}
            var list=new List<RunRecord>();
            foreach(string file in Directory.EnumerateFiles(Folder,"run-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(50))
            {
                try{if(new FileInfo(file).Length>2*1024*1024)continue;var r=JsonSerializer.Deserialize<RunRecord>(File.ReadAllText(file));if(r is {Schema:1}&&r.Server==server&&r.Character==character&&r.Id.Length==32&&r.Id.All(Uri.IsHexDigit)&&r.Stats.Mobs.Count<=257&&r.Stats.Deaths.Count<=64&&r.Stats.BigHits.Count<=24)list.Add(r);}catch{notice="Some unreadable run records were skipped.";}
            }
            Volatile.Write(ref history,list.OrderByDescending(r=>r.Opened).ToArray());Volatile.Write(ref ready,1);
            if(notice=="Loading local combat records")notice="Compact run records only; no combat CSV or chat transcript.";
            await foreach(var write in queue.Reader.ReadAllAsync())
            {
                try
                {
                    string path=Path.Combine(Folder,"run-"+write.Id+".json"),tmp=path+".tmp";
                    if(!File.Exists(path)||File.ReadAllText(path)!=write.Json){File.WriteAllText(tmp,write.Json);File.Move(tmp,path,true);}
                }
                catch(Exception ex){Volatile.Write(ref notice,"Run SAVE FAILED: "+ex.Message);}
            }
        }
        catch(Exception ex){Volatile.Write(ref notice,"Combat archive unavailable: "+ex.Message);}
        finally{Volatile.Write(ref ready,0);}
    }
    public bool Save(RunRecord run)
    {if(!Ready||run.Id.Length!=32||!run.Id.All(Uri.IsHexDigit))return false;string json=JsonSerializer.Serialize(run);if(enqueued.GetValueOrDefault(run.Id)==json)return true;if(!queue.Writer.TryWrite(new(run.Id,json)))return false;enqueued[run.Id]=json;if(enqueued.Count>64)enqueued.Remove(enqueued.Keys.First());return true;}
    public void Dispose()=>queue.Writer.TryComplete();
}
public sealed record CombatView(Totals Session,RunRecord? Current,RunRecord[] History,FloatEvent[] Floats,double Dps,double Taken,bool RunScope,string Scope,string Notice,int Dropped,bool Preview)
{
    public Totals Stats=>RunScope&&Current!=null?Current.Stats:Session;
    public static CombatView Empty=new(new(),null,[],[],0,0,false,"Session","Loading",0,false);
}
