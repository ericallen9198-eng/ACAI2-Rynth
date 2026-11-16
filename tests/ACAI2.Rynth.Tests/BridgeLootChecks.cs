using System.Net;
using System.Text;
using System.Text.Json;
using ACAI2.Bridge;
using ACAI2.Protocol;
using ACAI2.Rynth.Core.Keyfer;

internal static class BridgeLootChecks
{
    internal static async Task Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"keyfer-bridge-loot-"+Guid.NewGuid().ToString("N"));
        string[] keys=["ACAI2_DATA_ROOT","ACAI2_BEHAVIOR_PATH","ACAI2_SERVER_BEHAVIOR_PATH","ACAI2_KEYFER_SERVER","ACAI2_KEYFER_CHARACTER","OPENAI_API_KEY","ACAI2_MEMORY_ENABLED"];
        var old=keys.ToDictionary(k=>k,Environment.GetEnvironmentVariable);
        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("ACAI2_DATA_ROOT",root);Environment.SetEnvironmentVariable("ACAI2_BEHAVIOR_PATH",Path.Combine(root,"behavior.txt"));Environment.SetEnvironmentVariable("ACAI2_SERVER_BEHAVIOR_PATH",Path.Combine(root,"server.txt"));
            Environment.SetEnvironmentVariable("ACAI2_KEYFER_SERVER","DreamWeave");Environment.SetEnvironmentVariable("ACAI2_KEYFER_CHARACTER","Keyfer Sutherland");Environment.SetEnvironmentVariable("OPENAI_API_KEY","isolated-test-not-a-real-key");Environment.SetEnvironmentVariable("ACAI2_MEMORY_ENABLED","false");
            var options=new KeyferOptions();var registry=new ClientRegistry();
            registry.UpsertRegistration(new(){InstanceId="loot-test",Server="DreamWeave",Character="Keyfer Sutherland",RuntimeId="DreamWeave::Keyfer Sutherland",StartedUtc=DateTime.UtcNow.ToString("O")});
            var fake=new Model();using var http=new HttpClient(fake);using var service=new KeyferService(options,registry,http,new NoWiki());
            var now=DateTime.UtcNow;
            var ledger=new OwnLootLedger("DreamWeave","Keyfer Sutherland","loot-test",root,now);
            ledger.Poll("{\"chest\":{\"name\":\"Legendary Weapon Chest\",\"running\":true,\"looted\":154}}",now);
            ledger.Observe("[RynthAi] Looted [Keep] Dolabra (rule \"(MOD25) Melee / Attack >=25%\"): +16%a, +16%md, Legendary Heart Thirst",now);
            var reply=await service.AskAsync(new(){InstanceId="loot-test",Server="DreamWeave",Character="Keyfer Sutherland",Speaker="Toe",SpeakerId="toe-public-test",Channel="Allegiance",Question="Keyfer, have we kept anything from my pulling?",LootObservation=ledger.Snapshot(now)});
            check(reply.Ok&&fake.Calls==1&&fake.Body.Contains("ownChestLoot")&&fake.Body.Contains("Dolabra")&&fake.Body.Contains("ReportedCompletedCycles"),"real Bridge service supplies typed own chest findings to the fake model even without a nearby-world event");
            check(fake.Body.Contains("No-match")||fake.Body.Contains("nothing matched"),"model context explicitly distinguishes profile rejection from empty chests");
            check(reply.ReplyChannel!="Tell"&&string.IsNullOrEmpty(reply.GeneralNotice),"Bridge itself preserves Toe's asking channel without a public-to-private handoff");
            check(!Directory.GetFiles(root,"*",SearchOption.AllDirectories).Any(f=>Path.GetFileName(f)=="notable-loot.json"),"Bridge context is not duplicated into a separate per-poll loot history file");
        }
        finally
        {
            foreach(var key in keys)Environment.SetEnvironmentVariable(key,old[key]);
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
    private sealed class Model:HttpMessageHandler
    {
        internal int Calls;internal string Body="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;Body=await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK){Content=new StringContent("{\"choices\":[{\"message\":{\"content\":\"I kept a Dolabra.\"}}]}",Encoding.UTF8,"application/json")};
        }
    }
    private sealed class NoWiki:IDreamWeaveWiki
    {
        public Task<WikiResult> SearchAsync(string query,CancellationToken ct)=>throw new Exception("Own-loot conversation must not perform a wiki lookup.");
    }
}
