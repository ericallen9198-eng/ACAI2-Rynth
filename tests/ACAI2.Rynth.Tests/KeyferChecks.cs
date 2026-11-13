using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ACAI2.Protocol;
using ACAI2.Rynth.Core;
using ACAI2.Rynth.Core.Keyfer;

internal static class KeyferChecks
{
    internal static async Task Run(Action<bool,string> check)
    {
        var who=new GameIdentity("DreamWeave","Keyfer Sutherland",0x50001234);
        check(!new KeyferSettings().Matches(who),"Keyfer defaults off until local configuration enables its exact identity");
        bool bad=false;try{new KeyferSettings{bridgeUrl="https://example.com"}.Validate();}catch(InvalidDataException){bad=true;}
        check(bad,"Keyfer requests cannot be configured to forward private tells to a remote HTTP endpoint");
        check(new KeyferSettings().treasurePrizeCommand=="","prize transfers default disabled independently of treasure tracking");
        var options=new KeyferSettings{keyferEnabled=true};
        check(options.Matches(who)&&!options.Matches(who with{Character="Samm"})&&!options.Matches(who with{Server="Other"}),"Keyfer activation requires the configured server and character");
        foreach(var command in new[]{"/a Hi","/cg Hi","/t Lawful Waffle, Hello","/top ffa","/top 2v2","/e laughs."})
            check(KeyferSession.AllowedCommand(command,options),"guarded dispatcher accepts supported fixed command: "+command);
        foreach(var command in new[]{"/attack","/cast 1","/give Samm","/ub use chest","/rc quit","/a Hello\n/quit","/b t p 250000 Samm","/top ffa extra","/t Samm, /quit\r\n"})
            check(!KeyferSession.AllowedCommand(command,options),"guarded dispatcher rejects unsupported action: "+command.Replace('\n',' '));
        options.treasurePrizeCommand="/b t p 250000 {winner}";
        check(KeyferSession.AllowedCommand("/b t p 250000 Lawful Waffle",options)&&!KeyferSession.AllowedCommand("/b t p 500000 Lawful Waffle",options),"explicit prize configuration fixes both amount and command shape");
        var pasted=GameChat.Parse("[Allegiance] Samm says, \"Survey-Archivist tells you, \"10 surveys.\"\"");
        check(pasted?.Channel=="Allegiance","pasted NPC tell inside allegiance does not change the outer chat channel");
        check(!GameChat.IsFollowUp("we should continue tomorrow")&&GameChat.IsFollowUp("continue"),"continuation remains deliberate and contextual rather than matching any sentence containing continue");
        check(GameChat.ReplyCommands("Tell","Samm, /quit","test").Count()==0&&GameChat.ReplyCommands("Allegiance","Samm","/quit\n/attack").All(x=>x.StartsWith("/a ")&&!x.Contains('\n')),"AI output remains speech text and unsafe tell recipients are rejected");

        string root=Path.Combine(Path.GetTempPath(),"keyfer-adapter-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var fake=new FakeBridge();DateTime clock=DateTime.UtcNow;
        KeyferSettings Settings()=>new(){keyferEnabled=true,keyferWorldEvents=false,treasureEnabled=false,DataRoot=root,HandlerFactory=()=>new FakeHandler(fake),Clock=()=>clock};
        using var lease=new SessionLease(who,DateTime.UtcNow);
        KeyferSession? client=null;Task shutdown=Task.CompletedTask;
        try
        {
            client=new(Settings(),lease);await Until(()=>client.Ready);
            check(fake.Calls.Take(3).Select(x=>x.Path).SequenceEqual(new[]{"/v1/client/register","/v1/client/heartbeat","/v1/client/perception"}),"Keyfer registers and heartbeats with protocol-2 identity before any conversational request");
            bool duplicate=false;try{using var second=new KeyferSession(Settings(),lease);}catch(IOException){duplicate=true;}
            check(duplicate,"two Rynth sessions cannot simultaneously own the same Keyfer data and responder");
            var sent=new List<string>();var local=new List<string>();
            client.Capture("Samm tells you, \"PRIVATE HELLO\"",3,DateTime.UtcNow);
            check(fake.Calls.All(x=>x.Path!="/v1/keyfer/ask"),"chat callback queues work without doing HTTP or sending game commands");
            client.Pump(sent.Add,local.Add);await Until(()=>fake.Calls.Any(x=>x.Path=="/v1/keyfer/ask"));
            await Until(()=>{client.Pump(sent.Add,local.Add);return sent.Count>0;});
            check(sent.SequenceEqual(new[]{"/t Samm, Hello Samm."}),"private ask returns a paced tell to the original speaker through the pump");
            check(!fake.Calls.Where(x=>x.Path.EndsWith("/perception")||x.Path.EndsWith("/chat")).Any(x=>x.Json.Contains("PRIVATE HELLO")),"private text never enters public observation or perception routes");
            using(var ask=JsonDocument.Parse(fake.Calls.Single(x=>x.Path=="/v1/keyfer/ask").Json))
                check(ask.RootElement.GetProperty("InstanceId").GetString()==lease.InstanceId&&ask.RootElement.GetProperty("Character").GetString()==who.Character&&ask.RootElement.GetProperty("Channel").GetString()=="Tell","ask preserves session identity and private channel");
            int asks=fake.Calls.Count(x=>x.Path.EndsWith("/ask"));
            client.Capture("[General] Other says, \"Nice weather.\"",1,DateTime.UtcNow);client.Pump(sent.Add,local.Add);
            await Until(()=>fake.Calls.Any(x=>x.Path.EndsWith("/chat")));
            check(fake.Calls.Count(x=>x.Path.EndsWith("/ask"))==asks,"unaddressed General speech is observation only, without an AI ask");
            fake.Answer=new(){Ok=true,Answer="Detailed answer.",ReplyChannel="Tell",GeneralNotice="Let's take this to tells.",NoticeChannel="General"};
            clock=clock.AddSeconds(3);client.Capture("[General] Samm says, \"Keyfer explain that?\"",1,DateTime.UtcNow);client.Pump(sent.Add,local.Add);
            await Until(()=>fake.Calls.Count(x=>x.Path.EndsWith("/ask"))==asks+1);await Until(()=>{client.Pump(sent.Add,local.Add);return sent.LastOrDefault()=="/cg Let's take this to tells.";});
            check(sent.Last()=="/cg Let's take this to tells.","public-to-private routing emits only the short notice on General first");
            clock=clock.AddSeconds(2);client.Pump(sent.Add,local.Add);
            check(sent.Last()=="/t Samm, Detailed answer."&&sent.All(x=>x!="/cg Detailed answer."),"actual detailed answer is delivered privately, preserving the Bridge's route decision");
            fake.Answer=new(){Ok=true,ArenaMatchId="test-arena",ArenaChannel="Allegiance",ArenaUpdates=["For fun only!","ROUND 1: both fighters act.","SIM RESULT: Toe wins."]};
            clock=clock.AddSeconds(3);client.Capture("[Allegiance] Samm says, \"!arena Toe vs Samm\"",1,DateTime.UtcNow);client.Pump(sent.Add,local.Add);
            await Until(()=>fake.Calls.Count(x=>x.Path.EndsWith("/ask"))==asks+2);await Until(()=>{client.Pump(sent.Add,local.Add);return sent.LastOrDefault()=="/a For fun only!";});
            check(sent.Last()=="/a For fun only!","arena requests bypass name-addressing and retain the disclaimer as the opening line");
            clock=clock.AddSeconds(2);int before=sent.Count;client.Pump(sent.Add,local.Add);
            check(sent.Count==before,"arena narration waits four seconds between updates instead of flooding chat");
            clock=clock.AddSeconds(2);client.Pump(sent.Add,local.Add);clock=clock.AddSeconds(4);client.Pump(sent.Add,local.Add);
            await Until(()=>fake.Calls.Any(x=>x.Path.EndsWith("/arena/complete")));
            check(sent.Last()=="/a SIM RESULT: Toe wins."&&fake.Calls.Count(x=>x.Path.EndsWith("/arena/complete"))==1,"Allegiance arena completes only after its final line reaches the native dispatcher");
            check(client.Control("quiet on").Contains("keep listening")&&client.Control("emotes off").Contains("OFF"),"quiet and emote controls persist local Keyfer state without native automation");
            fake.BlockAsk=true;fake.BlockStarted=new(TaskCreationOptions.RunContinuationsAsynchronously);
            clock=clock.AddSeconds(3);client.Capture("Samm tells you, \"slow response\"",3,DateTime.UtcNow);client.Pump(sent.Add,local.Add);
            await fake.BlockStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));before=sent.Count;lease.Dispose();client.Pump(sent.Add,local.Add);
            await Until(()=>fake.CancelledAsk);
            check(sent.Count==before&&!client.Ready,"logout cancels in-flight asks and prevents stale native replies");
            client.Dispose();await client.Shutdown;client=null;
            using var nextLease=new SessionLease(who,DateTime.UtcNow);
            fake.BlockAsk=false;KeyferSession? reloaded=null;
            await Until(()=>{try{reloaded=new(Settings(),nextLease);return true;}catch(IOException){return false;}});
            using(reloaded){await Until(()=>reloaded!.Ready);check(reloaded!.Control("status").Contains("quiet ON"),"new login restores quiet state from the isolated Rynth state folder");
                for(int i=0;i<1000;i++)reloaded.Capture("Samm tells you, \"bounded\"",3,DateTime.UtcNow);
                check(reloaded.Status.EndsWith("queued 128",StringComparison.Ordinal),"callback buffering remains bounded while the pump is not draining it");
            }
            shutdown=reloaded!.Shutdown;await shutdown;
            check(Directory.GetFiles(root,"*.jsonl",SearchOption.AllDirectories).All(p=>!File.ReadAllText(p).Contains("PRIVATE HELLO")),"local adapter logs do not retain incoming private conversation text");
        }
        finally{client?.Dispose();if(client!=null)await client.Shutdown;await shutdown;if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private static async Task Until(Func<bool> condition)
    {for(int i=0;i<300;i++){if(condition())return;await Task.Delay(10);}throw new Exception("Timed out waiting for controlled Keyfer worker.");}
    private sealed class FakeBridge
    {
        public ConcurrentQueue<(string Path,string Json)> Calls=new();
        public KeyferAnswer Answer=new(){Ok=true,Answer="Hello Samm."};
        public bool BlockAsk,CancelledAsk;
        public TaskCompletionSource BlockStarted=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class FakeHandler(FakeBridge bridge) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            string path=request.RequestUri!.AbsolutePath;string body=await request.Content!.ReadAsStringAsync(ct);bridge.Calls.Enqueue((path,body));
            if(path.EndsWith("/ask")&&bridge.BlockAsk)
            {
                bridge.BlockStarted.TrySetResult();
                try{await Task.Delay(Timeout.Infinite,ct);}catch(OperationCanceledException){bridge.CancelledAsk=true;throw;}
            }
            string json=path.EndsWith("/ask")?JsonSerializer.Serialize(bridge.Answer):path.EndsWith("/leaderboards/poll")?"null":"{\"Ok\":true}";
            return new(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
        }
    }
}
