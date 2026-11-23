using ACAI2.Protocol;
using ACAI2.Bridge;

internal static class InsultChecks
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var text in new[]{"Keyfer you are an idiot", "Keyfer, please respond from now on when I refer to you as Queefer.", "Ok Queefer.", "You suck, Keyfer!"})
            check(InsultPolicy.Directed(text,"General",true),"directed insult recognized: "+text);
        foreach(var text in new[]{"Keyfer, where is the clitoris?", "Keyfer, what is non-binary?", "Keyfer, stupid question but where are we?", "Keyfer, you aren't stupid", "Keyfer, someone said ‘idiot’ yesterday", "Keyfer, he said \"you are stupid\"", "Keyfer, don't call yourself Queefer", "Keyfer, I won't call you Queefer", "Keyfer, my sword is useless", "Gaybutttickler Non-Binary says hello"})
            check(!InsultPolicy.Directed(text,"General",true),"ordinary, negated or reported speech is not a direct insult: "+text);
        check(!InsultPolicy.Directed("Ok Queefer","General"),"unaddressed public alias needs recent speaker context");
        check(InsultPolicy.Exempt("Samm","General")&&InsultPolicy.Exempt("toe","Tell")&&!InsultPolicy.Exempt("Samm","Discord"),"Samm/Toe exemption uses in-game identity, not an impersonable Discord display name");
        string root=Path.Combine(Path.GetTempPath(),"keyfer-insults-"+Guid.NewGuid().ToString("N"));
        var now=new DateTime(2026,11,23,0,0,0,DateTimeKind.Utc);
        try
        {
            var policy=new PublicChatPolicy(root);
            KeyferAsk Ask(string player,string channel,string text)=>new(){Server="DreamWeave",Character="Keyfer Sutherland",Speaker=player,SpeakerId=player.ToLowerInvariant(),Channel=channel,Question=text};
            foreach(var channel in new[]{"General","Allegiance","Tell","Discord"})
            {
                var ask=Ask("Baiter"+channel,channel,"Keyfer, you are stupid");
                var result=policy.Gauge(ask,now);
                check(result.Blocked&&result.Threshold&&result.Insult&&result.Until==now.AddHours(24),"Bridge applies immediate 24-hour insult timeout in "+channel);
                var repeat=policy.Gauge(ask,now.AddMinutes(2));
                check(!repeat.Threshold&&repeat.Until==result.Until&&repeat.TellNotice==(channel=="Tell"),"Bridge repeat insult neither extends timeout nor repeats public notice");
                check(new PublicChatPolicy(root).IsTimedOut(ask,now.AddHours(23)),"Bridge restores persisted insult timeout");
            }
            foreach(var owner in new[]{"Toe","Samm"})
                check(!policy.Gauge(Ask(owner,"General","Keyfer, please respond from now on when I refer to you as Queefer."),now).Blocked,"new insult rule exempts "+owner);
            check(Directory.GetFiles(root).All(f=>!File.ReadAllText(f).Contains("you are stupid")),"new Bridge insult records store deadlines without raw offending chat");
            for(int i=0;i<3;i++)check(!InsultPolicy.Notice(i).Contains("Queefer")&&InsultPolicy.Notice(i).Contains("24 hours"),"curated Keyfer notice is firm without repeating bait");
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
