using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
namespace ACAI2.Rynth.Threads.Core;

public sealed record ThreadIdentity(string Server,string Character)
{
    public string Folder(string root)=>Path.Combine(root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Server+"\n"+Character))));
    public void Validate(){if(string.IsNullOrWhiteSpace(Server)||string.IsNullOrWhiteSpace(Character)||Server.Length>128||Character.Length>128||Server.Any(char.IsControl)||Character.Any(char.IsControl))throw new InvalidDataException("A known server and character are required.");}
}
public sealed record ThreadInput(string Kind,string Text,DateTime Utc,int ObjectId=0,string Name="",int NativeType=0,int StartingLevel=0);
public sealed record ThreadView(string Identity,string[] Current,string[] History,string[] Research,string[] Goals,string Notice,int Runs,int Presses,bool Capturing)
{
    public static ThreadView Empty=new("Loading",[],[],[],[],"Loading journal...",0,0,false);
}
public sealed class ThreadWorker : IDisposable
{
    private readonly ThreadIdentity identity;
    private readonly string folder;
    private readonly CancellationTokenSource stop=new();
    private readonly Channel<ThreadInput> inputs=Channel.CreateBounded<ThreadInput>(new BoundedChannelOptions(128){FullMode=BoundedChannelFullMode.Wait,SingleReader=true});
    private readonly Task work;
    private ThreadView view=ThreadView.Empty;
    private int dropped,ready;
    public bool Ready=>Volatile.Read(ref ready)==1&&!stop.IsCancellationRequested;
    public ThreadView View=>Volatile.Read(ref view);
    public Task Completion=>work;
    public ThreadWorker(ThreadIdentity identity,string root)
    {identity.Validate();this.identity=identity;folder=identity.Folder(root);work=Task.Run(Run);}
    public bool Submit(ThreadInput input)
    {
        if(stop.IsCancellationRequested||input.Text.Length>4096)return false;
        if(inputs.Writer.TryWrite(input))return true;
        Interlocked.Increment(ref dropped);return false;
    }
    private async Task Run()
    {
        ThreadGemStore? store=null;ThreadGemCapture? capture=null;ThreadRuns? runs=null;FileStream? owner=null;
        string notice="Local journal only; no combat or automation.",goal="lum";DateTime saved=DateTime.MinValue;
        bool dirty=false;string selectedRun="",selectedRoll="";
        var prepRecipes=new Dictionary<int,(string Recipe,int Level)>();
        try
        {
            Directory.CreateDirectory(folder);owner=new(Path.Combine(folder,"owner.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            store=new(folder,identity.Server,identity.Character,x=>JsonSerializer.Serialize(x), (text,type)=>JsonSerializer.Deserialize(text,type)!);
            capture=new(store.Load());runs=new(capture.Data);runs.Resume();dirty=true;Volatile.Write(ref ready,1);
            if(store.LoadWarnings.Count>0)notice=string.Join("; ",store.LoadWarnings.Take(4));
            while(!stop.IsCancellationRequested)
            {
                using var wait=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);wait.CancelAfter(1000);
                try{await inputs.Reader.WaitToReadAsync(wait.Token);}catch(OperationCanceledException) when(!stop.IsCancellationRequested){}
                int processed=0;
                while(processed++<64&&inputs.Reader.TryRead(out var input))
                {
                    var data=capture.Data;
                    if(input.Kind=="chat"&&ThreadText.Native(input.Text,input.NativeType))
                    {
                        if(input.Text.TrimStart().StartsWith("The Thread Gem opens a way into ",StringComparison.Ordinal))runs.Pause(input.Utc);
                        dirty|=capture.Observe(input.Text,input.Utc,input.ObjectId,input.Name);
                        dirty|=runs.Observe(input.Text,input.Utc);
                        if(input.Text.Trim()=="Pressed. It holds."&&input.ObjectId!=0&&prepRecipes.Remove(input.ObjectId,out var recipe))
                        {
                            var roll=capture.Data.Current();
                            roll.Components=capture.ParseRecipe(recipe.Recipe);roll.PrepRecipeConfirmed=true;roll.RecipeSource="Native prep doses: server and inventory confirmed";
                            roll.FragmentLevel=recipe.Level;roll.FragmentType="Raw Fragment";roll.FragmentObjectId=input.ObjectId;roll.FragmentLevelBasis="Item data matched native prep press";
                            roll.RecipeControl=ThreadGemCapture.Control(roll.Components,false);
                            roll.Forecast=ThreadGemPrediction.Build(capture.Data.Rolls,roll,input.Utc);roll.Forecast.BeforeOutcome=true;
                        }
                    }
                    else if(input.Kind=="prep-recipe"){if(input.StartingLevel is 300 or 325 or 350 or 375)prepRecipes[input.ObjectId]=(input.Text,input.StartingLevel);}
                    else if(input.Kind=="fragment")
                    {
                        var level=ThreadFragmentInspection.Read(input.Name,[input.Text],null);
                        if(level!=null)dirty|=capture.ObserveExaminedFragment(input.ObjectId,level,input.Utc);
                    }
                    else if(input.Kind=="command")
                    {
                        string text=input.Text;
                        if(text=="entered")dirty|=runs.Enter(input.Utc);
                        else if(text=="outside"){runs.Pause(input.Utc);dirty=true;}
                        else if(text.StartsWith("run ",StringComparison.Ordinal))selectedRun=text[4..];
                        else if(text.StartsWith("press ",StringComparison.Ordinal))selectedRoll=text[6..];
                        else if(text.StartsWith("goal ",StringComparison.Ordinal))goal=text[5..];
                        else if(text=="link")
                        {
                            var run=runs.Current;var roll=data.Rolls.FirstOrDefault(x=>x.Id==selectedRoll)??data.LastComplete();
                            if(run!=null&&roll!=null){run.RollId=roll.Id;run.LinkBasis="User linked press to run; gem identity not verified";dirty=true;notice="Linked selected/latest complete press to current run.";}
                        }
                        else if(text.StartsWith("rate ",StringComparison.Ordinal))
                        {
                            var run=data.Runs.FirstOrDefault(x=>x.Id==selectedRun)??runs.Current;
                            var roll=data.Rolls.FirstOrDefault(x=>x.Id==run?.RollId);
                            string rating=text[5..];
                            if(roll!=null&&rating is "easy" or "tough" or "nightmare"){roll.Rating=rating;dirty=true;}
                            else notice="Link a press to the selected/current run before rating its recipe.";
                        }
                        else if(text=="export")
                        {
                            string path=Path.Combine(folder,"export-"+input.Utc.ToString("yyyyMMdd-HHmmss-fff")+".json");
                            File.WriteAllText(path,JsonSerializer.Serialize(new {Schema=1,Server=identity.Server,Character=identity.Character,Rolls=data.Rolls,Runs=data.Runs}));notice="Export saved: "+path;
                        }
                    }
                }
                if(Volatile.Read(ref dropped)>0){if(runs.Current!=null)runs.Current.ObservationGap=true;notice="Input queue overflow: "+Volatile.Read(ref dropped)+" dropped; capture may be incomplete.";}
                dirty|=runs.Tick(DateTime.UtcNow);dirty|=capture.Tick(DateTime.UtcNow);
                if(dirty&&DateTime.UtcNow-saved>=TimeSpan.FromSeconds(5))
                {
                    try{Save(store,capture.Data);dirty=false;saved=DateTime.UtcNow;}
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){notice="SAVE FAILED: "+ex.GetType().Name+". Journal remains in memory; retrying.";saved=DateTime.UtcNow;}
                }
                Publish(capture,runs,selectedRun,selectedRoll,goal,notice);
            }
        }
        catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        catch(Exception ex){Volatile.Write(ref view,new(identity.Character,[],[],[],[],"Thread journal unavailable: "+ex.GetType().Name+" "+ex.Message,0,0,false));}
        finally
        {
            Volatile.Write(ref ready,0);
            if(store!=null&&capture!=null&&runs!=null)
            {try{runs.Pause(DateTime.UtcNow,true);Save(store,capture.Data);}catch(Exception ex){Volatile.Write(ref view,View with{Notice="Final SAVE FAILED: "+ex.GetType().Name});}}
            owner?.Dispose();
        }
    }
    private static void Save(ThreadGemStore store,ThreadGemJournal data)
    {
        // Compact per-record files, overwritten only when changed, never an all-chat log.
        foreach(var roll in data.Rolls)store.SaveRoll(roll);
        foreach(var run in data.Runs)store.SaveRun(run);
        store.Save(data);
    }
    private void Publish(ThreadGemCapture capture,ThreadRuns runs,string selectedRun,string selectedRoll,string goal,string notice)
    {
        var data=capture.Data;var run=data.Runs.FirstOrDefault(x=>x.Id==selectedRun)??runs.Current;
        var current=run==null?new[]{"Waiting for a native Thread opening message.","Open the gem normally; this plugin sends no game commands."}:new[]{
            run.Dungeon+" | "+run.Status,
            "Observed "+TimeSpan.FromSeconds(run.ObservedSeconds).ToString(@"hh\:mm\:ss")+(run.ObservationGap?" | gaps present":""),
            "Kills "+(run.MonstersKilled?.ToString()??"?")+" / "+(run.MonstersTotal?.ToString()??"?")+" | boss "+(run.BossRemaining.HasValue?(run.BossRemaining.Value?"remaining":"defeated"):"unknown"),
            "Returns "+(run.RemainingReturns?.ToString()??"?")+" | deaths "+run.Deaths,
            "Realm "+(run.RealmInstance.Length>0?run.RealmInstance:"unconfirmed"),
            "Clear "+(run.ClearedUtc?.ToLocalTime().ToString("g")??"not observed")+" | end "+(run.EndedUtc?.ToLocalTime().ToString("g")??"not observed"),
            "Press link: "+run.LinkBasis};
        var history=data.Runs.Where(x=>x.Status!="Superseded by known instance").OrderByDescending(x=>x.OpenedUtc).Take(100)
            .Select(x=>x.Id+"|"+x.Dungeon+" | "+x.Status+" | "+x.OpenedUtc.ToLocalTime().ToString("g")).ToArray();
        var roll=data.Rolls.FirstOrDefault(x=>x.Id==selectedRoll)??data.Current()??data.LastComplete();
        var research=new List<string>();
        research.AddRange(ThreadGemReports.Roll(roll));
        if(roll!=null)
        {
            research.AddRange(ThreadGemReports.Forecast(roll.Forecast));
            foreach(string name in roll.Components.Select(x=>x.Name).Distinct().Take(8))research.AddRange(ThreadGemReports.Component(data,name).Take(12));
        }
        Volatile.Write(ref view,new(identity.Character,current,history,research.ToArray(),ThreadGemRecommendations.Recommend(data,goal),notice,data.Runs.Count(x=>x.Status!="Superseded by known instance"),data.Rolls.Count,roll?.Status=="Capturing"));
    }
    public void Dispose(){stop.Cancel();inputs.Writer.TryComplete();}
}
