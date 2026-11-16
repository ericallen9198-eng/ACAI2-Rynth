using System.Text.Json;
using ACAI2.Protocol;
using ACAI2.Rynth.Core.Keyfer;
using ACAI2.Bridge;

internal static class OwnLootChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var utc=new DateTime(2026,11,16,0,0,0,DateTimeKind.Utc);
        string root=Path.Combine(Path.GetTempPath(),"keyfer-loot-"+Guid.NewGuid().ToString("N"));
        try
        {
            var l=new OwnLootLedger("DreamWeave","Keyfer Sutherland","test-instance",root,utc);
            const string dolabra="[RynthAi] Looted [Keep] Dolabra (rule \"(MOD25) Melee - all skills and styles / Attack >=25%\"): Oak, Slash Axe, 3-58, 0.95v, +16%a, +16%md, Legendary Heart Thirst, Legendary Weapon Tinkering Expertise, Major Armor, Aura of Cragstone's Will, Aura of Infected Caress, Light Weapons 430, Diff 221, Craft 10";
            const string dagger="[RynthAi] Looted [Keep] Frost Khanjar (rule \"(MOD25) Melee - all skills and styles / Attack >=25%\"): Silver, Cold Dagger, 24-54, 0.56v, +16%a, +15%md, Legendary Heart Thirst, Major Flame Ward, Aura of Atlan's Alacrity, Light Weapons 420, Diff 177, Craft 9";
            l.Poll("{\"chest\":{\"name\":\"Legendary Weapon Chest\",\"running\":true,\"looted\":153}}",utc);
            check(l.Snapshot(utc).Running&&l.Snapshot(utc).ReportedCompletedCycles==153&&l.Snapshot(utc).ObservedCompletedCycles==0,"own RynthAi chest snapshot reports current activity without claiming pre-attachment cycles were observed");
            string[] lines=["[RynthAi] Loot rule: Dolabra -> [Keep] (MOD25) Melee", "[RynthAi] Dolabra: [Keep] (MOD25) Melee", "[RynthAi] Looted [Salvage] Acid Compound Crossbow", "A mule cannot salvage."];
            foreach(string line in lines)l.Observe(line,utc);
            check(l.KeepCount==0,"rule previews, salvage pickups and mule salvage errors never become confirmed Keep findings");
            l.NativeReceipt(10,"Dolabra","Legendary Weapon Chest",utc);l.Observe(dolabra,utc);
            var item=l.Snapshot(utc).RecentKeeps.Single();
            check(l.KeepCount==1&&item.Name=="Dolabra"&&item.Attack==16&&item.MeleeDefense==16&&item.ProfileAttackEstimate==25&&item.ChestConfirmed,"supplied Dolabra line counts once and separates printed +16% attack from +25% with Legendary Heart Thirst");
            l.Observe(dagger,utc.AddSeconds(1));l.NativeReceipt(11,"Frost Khanjar","Legendary Weapon Chest",utc.AddSeconds(1));
            check(l.KeepCount==2&&l.Snapshot(utc.AddSeconds(1)).RecentKeeps.Last().ChestConfirmed,"late native ownership evidence enriches a Keep without double-counting it");
            l.Observe("[RynthAi] Chest looter: looted Legendary Weapon Chest (154 so far).",utc);
            l.Poll("{\"chest\":{\"name\":\"Legendary Weapon Chest\",\"running\":true,\"looted\":154}}",utc);
            l.Observe("[RynthAi] Nothing in Legendary Weapon Chest matches the loot profile.",utc);
            l.Observe("[RynthAi] Chest looter: looted Legendary Weapon Chest (155 so far).",utc);
            var view=l.Snapshot(utc);
            check(view.ReportedCompletedCycles==155&&view.ObservedCompletedCycles==2&&view.ObservedNoMatchCycles==1&&view.ConfirmedKeepPickups==2,"cycle counts, no-match cycles and actual kept items remain separate; status polling does not count a cycle twice");
            check(!l.Observe("[Allegiance] Toe says, \""+dolabra+"\"",utc)&&!l.Observe("Samm tells you, \"PRIVATE SECRET\"",utc)&&l.KeepCount==2,"player quotes and private tells cannot become own-loot evidence");
            l.Observe(dolabra,utc.AddSeconds(2));
            check(l.KeepCount==3&&!l.Snapshot(utc.AddSeconds(2)).RecentKeeps.Last().ChestConfirmed,"a genuine repeated Keep is retained, but an unmatched duplicate-name item never gets an invented chest source");
            for(int i=0;i<100;i++)l.Observe(dolabra,utc.AddSeconds(3+i));
            check(l.Save()&&l.Snapshot(utc.AddSeconds(110)).RecentKeeps.Length==12&&l.Snapshot(utc.AddSeconds(110)).NotableKeeps.Length==24,"in-memory findings are bounded to 12 recent and 24 notable records");
            string path=Directory.GetFiles(root,"notable-loot.json",SearchOption.AllDirectories).Single();string saved=File.ReadAllText(path);
            check(new FileInfo(path).Length<=49152&&!saved.Contains("Acid Compound Crossbow")&&!saved.Contains("cannot salvage")&&!saved.Contains("PRIVATE SECRET")&&!saved.Contains("[RynthAi]"),"the capped notable file stores structured findings rather than chat, previews, salvage junk or private messages");
            var next=new OwnLootLedger("DreamWeave","Keyfer Sutherland","next-instance",root,utc.AddMinutes(5));
            check(next.KeepCount==0&&next.Snapshot(utc.AddMinutes(5)).NotableKeeps.Length==24&&!next.Snapshot(utc.AddMinutes(5)).Running,"login restores dated notable memories while current activity and session counters reset");
            var ask=new KeyferAsk{Server="DreamWeave",Character="Keyfer Sutherland",InstanceId="test-instance",Channel="Allegiance",LootObservation=l.Snapshot(utc.AddSeconds(110))};
            check(OwnLootContext.For(ask,utc.AddSeconds(110)) is OwnLootSnapshot,"Bridge accepts fresh same-client structured own-loot evidence");
            var fresh=new OwnLootSnapshot("DreamWeave","Keyfer Sutherland","test-instance",utc,utc,"Legendary Weapon Chest",true,utc,155,2,1,2,[],[],"test observation");
            var delayed=new KeyferAsk{Server="DreamWeave",Character="Keyfer Sutherland",InstanceId="test-instance",Channel="Allegiance",LootObservation=fresh};
            check(OwnLootContext.For(delayed,utc.AddSeconds(10)) is OwnLootSnapshot {Running:false},"queued older activity may remain historical evidence but cannot claim the chest looter is running now");
            ask.InstanceId="wrong-instance";check(OwnLootContext.For(ask,utc.AddSeconds(110)) is not OwnLootSnapshot,"Bridge rejects loot evidence from a different native session");
            ask.InstanceId="test-instance";check(OwnLootContext.For(ask,utc.AddMinutes(10)) is not OwnLootSnapshot,"stale observations do not masquerade as current pulling activity");
            ask.Channel="Discord";check(OwnLootContext.For(ask,utc.AddSeconds(110)) is not OwnLootSnapshot,"Discord cannot inject registered game-client loot context");
            var defense=new OwnLootLedger("DreamWeave","Keyfer Sutherland","defense-test",root,utc);
            defense.Observe("[RynthAi] Looted [Keep] Diamond Frost Dericost Blade (rule \"(MOD25) Melee / Melee defense >=25%\"): +13%a, +18%md, Epic Defender",utc);
            check(defense.LastKeep is {MeleeDefense:18,ProfileMeleeDefenseEstimate:25},"Epic Defender adds seven points only to the profile estimate, preserving the printed +18% defense");
            l.Poll("not JSON",utc);l.Poll("{}",utc);check(l.KeepCount==103,"missing or malformed optional RynthAi snapshots do not break the ledger");
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
