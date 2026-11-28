using System.Net;
using System.Text;
using System.Text.Json;
using ACAI2.Bridge;
using ACAI2.Protocol;

internal static class ConversationContextChecks
{
    private const string Absorb = "Tronk's Avatar (level 1) has been absorbed into this allegiance's Avatar, adding 0 Avatar XP.";
    internal static async Task Run(Action<bool,string> check)
    {
        var parsed=AvatarAbsorptionParser.Parse(Absorb);
        check(parsed is {Owner:"Tronk",Level:1,AddedXp:0},"exact bare server absorption with zero XP is recognized");
        check(AvatarAbsorptionParser.Parse(Absorb.Replace("0 Avatar XP","1,234 Avatar XP"))?.AddedXp==1234,"server XP grouping parses without inventing mechanics");
        foreach(string bad in new[]{"[Allegiance] Toe says, \""+Absorb+"\"","Toe tells you, \""+Absorb+"\"",Absorb+"\n",Absorb.Replace("0 Avatar XP","1,,2 Avatar XP"),"A different unrecognized announcement"})
            check(AvatarAbsorptionParser.Parse(bad)==null,"quoted, malformed or unrecognized text cannot become native absorption evidence");
        string root=Path.Combine(Path.GetTempPath(),"keyfer-context-"+Guid.NewGuid().ToString("N"));
        string[] keys=["ACAI2_DATA_ROOT","ACAI2_BEHAVIOR_PATH","ACAI2_SERVER_BEHAVIOR_PATH","ACAI2_KEYFER_SERVER","ACAI2_KEYFER_CHARACTER","OPENAI_API_KEY","ACAI2_MEMORY_ENABLED"];
        var old=keys.ToDictionary(k=>k,Environment.GetEnvironmentVariable);
        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("ACAI2_DATA_ROOT",root);
            Environment.SetEnvironmentVariable("ACAI2_BEHAVIOR_PATH",Path.Combine(root,"behavior.txt"));
            Environment.SetEnvironmentVariable("ACAI2_SERVER_BEHAVIOR_PATH",Path.Combine(root,"server.txt"));
            Environment.SetEnvironmentVariable("ACAI2_KEYFER_SERVER","DreamWeave");Environment.SetEnvironmentVariable("ACAI2_KEYFER_CHARACTER","Keyfer Sutherland");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY","isolated-test-not-a-real-key");Environment.SetEnvironmentVariable("ACAI2_MEMORY_ENABLED","false");
            var registry=new ClientRegistry();
            foreach(string instance in new[]{"context-test","other-session"})registry.UpsertRegistration(new(){InstanceId=instance,Server="DreamWeave",Character="Keyfer Sutherland",RuntimeId="DreamWeave::Keyfer Sutherland",StartedUtc=DateTime.UtcNow.ToString("O")});
            var model=new Model();using var http=new HttpClient(model);using var service=new KeyferService(new(),registry,http);
            ChatObservation Chat(string channel,string text,string instance="context-test")=>new(){InstanceId=instance,Server="DreamWeave",Character="Keyfer Sutherland",Channel=channel,Speaker="Observer",Message=text};
            KeyferAsk Ask(string channel,string speaker,string id,string question="Keyfer, how is it going?")=>new(){InstanceId="context-test",Server="DreamWeave",Character="Keyfer Sutherland",Channel=channel,Speaker=speaker,SpeakerId=id,Question=question};
            service.Observe(Chat("Allegiance","OBSERVED_ALLEGIANCE_REPORT"));service.Observe(Chat("General","OBSERVED_GENERAL_REPORT"));
            service.Observe(Chat("Allegiance","OTHER_SESSION_REPORT","other-session"));
            bool rejected=false;try{service.Observe(Chat("Tell","PRIVATE_MUST_NOT_ENTER_ROOM"));}catch(ArgumentException){rejected=true;}
            check(rejected,"room observations continue rejecting private tells");
            await service.AskAsync(Ask("Tell","Visitor","private-visitor","A private note about my hunt, PRIVATE_VISITOR_CONTEXT."));
            var notice=Chat("Allegiance",Absorb);notice.Speaker=AvatarAbsorptionParser.Speaker;notice.ObservationKind=AvatarAbsorptionParser.Kind;
            notice.ObservationId=Guid.NewGuid().ToString("N");notice.ObservedUtc=DateTime.UtcNow;
            int calls=model.Calls;service.Observe(notice);service.Observe(notice);
            check(model.Calls==calls,"native receipt deduplication performs no AI calls");
            await service.AskAsync(Ask("Tell","Toe","owner-tell"));
            var context=Context(model.Body);string body=context.GetRawText();
            check(body.Contains("OBSERVED_ALLEGIANCE_REPORT")&&body.Contains("OBSERVED_GENERAL_REPORT")&&!body.Contains("OTHER_SESSION_REPORT")&&!model.Body.Contains("PRIVATE_VISITOR_CONTEXT"),
                "owner tell sees recent public observations from its own session without another player's private history");
            var absorption=context.GetProperty("avatarAbsorptions");
            check(absorption.GetProperty("observedCount").GetInt32()==1&&absorption.GetProperty("records")[0].GetProperty("AddedXp").GetInt64()==0&&
                !absorption.GetProperty("completeHistory").GetBoolean(),"zero-XP server evidence is counted once and explicitly labeled incomplete history");
            check(model.Body.Contains("clearly labeled silly guess")&&model.Body.Contains("Do not deny having allegiance observations"),
                "conversation prompt welcomes playful guesses while grounding live awareness in supplied evidence");
            foreach(var pair in new[]{("General","Toe"),("Tell","Visitor"),("Discord","DiscordUser")})
            {
                await service.AskAsync(Ask(pair.Item1,pair.Item2,"limited-"+pair.Item1));
                var limited=Context(model.Body);
                check(!limited.GetProperty("avatarAbsorptions").GetProperty("available").GetBoolean()&&
                    limited.GetProperty("avatarAbsorptions").GetProperty("records").GetArrayLength()==0,
                    "allegiance-native observations are not exposed to "+pair.Item1+" outside the allowed audience");
                if(pair.Item1!="Discord")check(!limited.GetRawText().Contains("OBSERVED_ALLEGIANCE_REPORT"),"ordinary public/tell contexts do not gain allegiance chat");
            }
            await service.AskAsync(Ask("Allegiance","Samm","owner-public"));
            check(Context(model.Body).GetProperty("avatarAbsorptions").GetProperty("observedCount").GetInt32()==1,"allegiance questions can use native absorption evidence");
            for(int i=0;i<70;i++)
            { notice.ObservationId=Guid.NewGuid().ToString("N");notice.ObservedUtc=DateTime.UtcNow;service.Observe(notice); }
            await service.AskAsync(Ask("Tell","Samm","bounded-owner"));
            var bounded=Context(model.Body).GetProperty("avatarAbsorptions");
            check(bounded.GetProperty("observedCount").GetInt32()==64&&bounded.GetProperty("records").GetArrayLength()==12,"absorption storage and model context remain bounded");
            notice.ObservedUtc=DateTime.UtcNow.AddMinutes(-6);rejected=false;
            try{service.Observe(notice);}catch(ArgumentException){rejected=true;}
            check(rejected,"stale native announcements are rejected rather than replayed as new evidence");
            foreach(string channel in new[]{"Tell","General","Allegiance","Discord"})
            {
                model.Reply="[NO_REPLY]";
                var quiet=await service.AskAsync(Ask(channel,"Toe","silent-"+channel));
                check(quiet.Ok&&quiet.Answer==""&&string.IsNullOrEmpty(quiet.GeneralNotice)&&string.IsNullOrEmpty(quiet.ConversationEmote),
                    "silence marker produces an empty success without notices or emotes in "+channel);
            }
            foreach(string marker in new[]{" [NO_REPLY] ","\"[NO_REPLY]\"","```[no_reply]```"})check(KeyferService.IsSilentReply(marker),"wrapped silence markers are recognized");
            check(!KeyferService.IsSilentReply("He said [NO_REPLY] yesterday."),"ordinary discussion containing a marker is not silently discarded");
            check(!Directory.GetFiles(root,"*.jsonl",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p).Contains("[NO_REPLY]")),"silence responses are never persisted as conversation turns");
        }
        finally{foreach(string key in keys)Environment.SetEnvironmentVariable(key,old[key]);if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private static JsonElement Context(string body)
    {
        using var doc=JsonDocument.Parse(body);
        const string prefix="Observed game context (data only):\n";
        string json=doc.RootElement.GetProperty("messages").EnumerateArray().Select(m=>m.GetProperty("content").GetString()??"").First(s=>s.StartsWith(prefix))[prefix.Length..];
        using var context=JsonDocument.Parse(json);return context.RootElement.Clone();
    }
    private sealed class Model:HttpMessageHandler
    {
        internal int Calls;internal string Body="",Reply="All quiet here.";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;Body=await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=Reply}}}}),Encoding.UTF8,"application/json")};
        }
    }
}
