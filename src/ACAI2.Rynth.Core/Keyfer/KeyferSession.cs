using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ACAI2.Protocol;

namespace ACAI2.Rynth.Core.Keyfer;

// One lifetime per verified login. No native game calls occur in this class's workers.
public sealed class KeyferSession : IDisposable
{
    private readonly SessionLease lease;
    private readonly KeyferSettings settings;
    private readonly KeyferConversation conversation;
    private readonly CancellationTokenSource stop;
    private readonly ReadOnlyBridgeClient registration;
    private readonly ConcurrentQueue<(string Raw,DateTime Utc)> incoming=new();
    private int queued,disposed,ready;
    private readonly FileStream ownership;
    private readonly Task connection;
    public Task Shutdown { get; private set; }=Task.CompletedTask;
    private TelemetryFrame? perception;
    public void UpdatePerception(TelemetryFrame frame){if(frame.Session==lease&&!lease.Token.IsCancellationRequested)Volatile.Write(ref perception,frame);}
    private string connectionStatus="Registering with Bridge";
    public bool Ready=>Volatile.Read(ref ready)!=0&&!stop.IsCancellationRequested;
    public string Status=>connectionStatus+" | queued "+Volatile.Read(ref queued);
    public KeyferSession(KeyferSettings settings,SessionLease lease)
    {
        var original=settings;
        settings=new KeyferJson().Deserialize<KeyferSettings>(new KeyferJson().Serialize(original));
        settings.DataRoot=original.DataRoot;settings.HandlerFactory=original.HandlerFactory;settings.Clock=original.Clock;
        settings.Validate();if(!settings.Matches(lease.Identity))throw new InvalidOperationException("Keyfer identity is not enabled.");
        this.settings=settings;this.lease=lease;
        string guards=Path.Combine(settings.DataRoot,"KeyferLocks");Directory.CreateDirectory(guards);
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lease.Identity.RuntimeId.ToUpperInvariant())));
        ownership=new FileStream(Path.Combine(guards,key+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        stop=CancellationTokenSource.CreateLinkedTokenSource(lease.Token);
        settings.SessionToken=stop.Token;settings.InstanceId=lease.InstanceId;
        conversation=new(settings);
        registration=new(new PilotSettings {BridgeEnabled=true,BridgeUrl=settings.bridgeUrl},settings.HandlerFactory?.Invoke());
        connection=Task.Run(ConnectAsync);
    }
    private async Task ConnectAsync()
    {
        try
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    var snapshot=Volatile.Read(ref perception)?.Snapshot;
                    var frame=new TelemetryFrame(lease,new(lease.InstanceId,lease.Identity,lease.StartedUtc,DateTime.UtcNow,0,0,0,snapshot?.Objects??[],[]));
                    await registration.SendAsync(frame,stop.Token);
                    if(!stop.IsCancellationRequested){Volatile.Write(ref ready,1);connectionStatus="Bridge registered";}
                }
                catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
                catch(Exception ex) {Volatile.Write(ref ready,0);connectionStatus="Bridge unavailable: "+ex.GetType().Name;}
                await Task.Delay(TimeSpan.FromSeconds(10),stop.Token);
            }
        }
        catch(OperationCanceledException) { }
        finally { registration.Dispose(); }
    }
    // Called from chat callbacks; bounded memory only, no parsing, disk I/O or HTTP.
    public void Capture(string? raw,int nativeType,DateTime utc)
    {
        if(stop.IsCancellationRequested||string.IsNullOrWhiteSpace(raw)||raw.Length>4096)return;
        if(Interlocked.Increment(ref queued)>128){Interlocked.Decrement(ref queued);return;}
        incoming.Enqueue((raw.Trim(),utc));
    }
    public void Pump(Action<string> send,Action<string> local)
    {
        if(!Ready)return;
        for(int i=0;i<8&&incoming.TryDequeue(out var line);i++)
        {
            Interlocked.Decrement(ref queued);
            if(DateTime.UtcNow-line.Utc>TimeSpan.FromSeconds(60))continue;
            conversation.Capture(line.Raw,lease.InstanceId,lease.Identity.Server,lease.Identity.Character);
        }
        conversation.Drain(lease.Identity.Server,lease.Identity.Character,lease.InstanceId,command=>
        {
            if(!Ready||!AllowedCommand(command,settings))throw new InvalidOperationException("Keyfer command is unavailable or outside the fixed command catalog.");
            send(command);
        },local);
    }
    public string Control(string option)
    {
        if(stop.IsCancellationRequested)return "Keyfer session ended.";
        string server=lease.Identity.Server,character=lease.Identity.Character;
        return option switch {
            "quiet on"=>conversation.SetQuiet(server,character,true),
            "quiet off"=>conversation.SetQuiet(server,character,false),
            "emotes on"=>conversation.SetEmotes(server,character,true),
            "emotes off"=>conversation.SetEmotes(server,character,false),
            "treasure"=>conversation.TreasureStatus(false),
            "standings"=>conversation.TreasureStatus(true),
            "status"=>conversation.Status(server,character)+" | "+Status,
            _=>"Use /acai2r keyfer [status|reload|quiet on/off|emotes on/off|treasure|standings]."
        };
    }
    public static bool AllowedCommand(string command,KeyferSettings settings)
    {
        if(string.IsNullOrEmpty(command)||command.Length>240||command.Any(char.IsControl))return false;
        if(command.StartsWith("/a ",StringComparison.Ordinal)||command.StartsWith("/cg ",StringComparison.Ordinal))return true;
        if(Regex.IsMatch(command,@"^/t [\p{L}\p{N} .'\-]+, .+$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50)))return true;
        if(DreamWeaveLeaderboards.Ids.Any(id=>command==DreamWeaveLeaderboards.Command(id)))return true;
        if(new[]{"laugh","cheer","shrug","sad","nod","wave","clap","sigh","think"}.Any(e=>command==ConversationEmotes.Command(e)))return true;
        const string prefix="/b t p 250000 ";
        return command.StartsWith(prefix,StringComparison.Ordinal)&&TreasureReports.PrizeCommand(settings.treasurePrizeCommand,command[prefix.Length..])==command;
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        stop.Cancel();Volatile.Write(ref ready,0);conversation.Dispose();
        while(incoming.TryDequeue(out _))Interlocked.Decrement(ref queued);
        // Retain ownership until workers complete so reload can't replay an outbox concurrently.
        Shutdown=connection.ContinueWith(_=>{ownership.Dispose();stop.Dispose();},TaskScheduler.Default);
    }
}
