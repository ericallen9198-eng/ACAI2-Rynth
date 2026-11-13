using System.Text.Json;
using ACAI2.Rynth.Threads.Core;
int count=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
var now=new DateTime(2026,11,13,12,0,0,DateTimeKind.Utc);
var data=new ThreadGemJournal{Server="DreamWeave",Character="Toe"};var capture=new ThreadGemCapture(data);var runs=new ThreadRuns(data);
const string recipe="Yellow Taper x4, Willow Talisman, Colcothar, Powdered Lapis Lazuli, Mugwort, Gold Scarab";
Check(!capture.Observe("[Allegiance] Toe says, \"Pressed. It holds.\"",now)&&!ThreadText.Native("Samm tells you, \"Pressed. It holds.\"",3),"quoted/private chat cannot spoof Thread evidence");
var parsed=capture.ParseRecipe(recipe);Check(parsed.Single(x=>x.Name=="Yellow Taper").Count==4&&parsed.Sum(x=>x.Count)==9,"component dose quantities are structured, not separate literal x4 categories");
Check(capture.ObserveExaminedFragment(123,ThreadFragmentInspection.Read("Raw Fragment (Level 325)",[],null),now),"cached raw-fragment name provides a starting-level observation");
capture.Observe("The fragment takes the imprint: "+recipe+".",now,123,"Raw Fragment (Level 325)");capture.Observe("Pressed. It holds.",now,123,"Raw Fragment (Level 325)");
Check(data.Current().FragmentLevel==325&&data.Current().FragmentLevelBasis.Contains("unverified"),"a cached selection is not falsely claimed to identify the fragment actually pressed");
capture.Observe("Components selected: "+recipe,now);capture.Observe("The gem carries:",now);capture.Observe("Radiant - kills pay x1.68 luminance",now);capture.Observe("Amethyst Affinity - 13% of kills leave amethyst to salvage",now);capture.Observe("Level 335, 4 entries.",now);
Check(data.Current().Status=="Complete"&&data.Current().RecipeControl=="Fully Controlled"&&data.Current().Modifiers[0].Value==1.68,"complete press records preserve normalized recipe, result and numeric modifier evidence");
Check(data.Current().Modifiers[0].Raw.Contains("Radiant"),"recognized modifier descriptions remain available without a whole-chat transcript");
string saved=JsonSerializer.Serialize(data);Check(!saved.Contains("RawChat")&&!saved.Contains("tells you"),"journal serialization has no unbounded raw-chat field");
capture.Observe("Pressed. It holds.",now.AddSeconds(1));capture.Observe("Components selected: Mystery x9",now.AddSeconds(1));capture.Observe("The gem carries:",now.AddSeconds(1));capture.Observe("Hardy - monsters have x1.79 health",now.AddSeconds(1));capture.Observe("Level 335, 4 entries.",now.AddSeconds(1));
Check(data.Current().RecipeControl!="Fully Controlled","unknown component roles cannot masquerade as a fully controlled nine-slot recipe");
capture.Observe("Pressed. It holds.",now.AddSeconds(2));capture.Tick(now.AddMinutes(11));Check(data.Current().Status=="Incomplete","capture timeout completes even with no following chat");
const string open="The Thread Gem opens a way into Burun Fortress. It will bring you back 3 more time(s) while the dungeon stands.";
capture.Observe(open,now);Check(runs.Current!=null&&runs.Current.RollId=="","opening a Thread does not automatically attach the most recent crafted gem");
runs.Observe("[REALM] id=0 inst=123 eph=1 name=Base World",now.AddSeconds(1));runs.Tick(now.AddSeconds(2));runs.Tick(now.AddSeconds(3));
Check(runs.InThread&&runs.Current!.RealmInstance=="0:123"&&runs.Current.ObservedSeconds==2,"realm identity confirms entry and observed time accrues independently of the meter");
runs.Observe("Monsters killed 40/74. Boss still remaining.",now.AddSeconds(3));Check(runs.Current.MonstersKilled==40&&runs.Current.BossRemaining==true,"run progress and remaining boss are recorded from the native line");
runs.Observe("Burun Fortress is cleared. Your gem will bring you back if you fall; it crumbles when you leave by recall or the exit portal.",now.AddSeconds(4));
Check(runs.Current.ClearedUtc.HasValue&&!runs.Current.EndedUtc.HasValue,"dungeon clear and gem lifetime end remain separate milestones");
runs.Observe("[REALM] id=0 inst=0 eph=0 name=Base World",now.AddSeconds(5));capture.Observe(open,now.AddSeconds(7));runs.Observe("[REALM] id=0 inst=123 eph=1 name=Base World",now.AddSeconds(8));
Check(runs.Current.RealmInstance=="0:123"&&data.Runs.Count(x=>x.Status!="Superseded by known instance")==1,"re-entry uses realm/instance identity without duplicating the known run");
runs.Tick(now.AddSeconds(20));Check(runs.Current.ObservationGap,"long observation gaps are labeled instead of counted as uninterrupted timing");
runs.Observe("Your Thread Gem crumbles to dust.",now.AddSeconds(21));Check(runs.Current.EndedUtc.HasValue&&runs.Current.Status=="Finished — cleared","crumble finalizes the captured Thread");

var blank=new PrepItem(10,"Raw Fragment (Level 325)",1,"Level: 325\nModifiers: none\nPressed: nothing\nEntries: 3 of 3\nUnpressed.",true);
var scarab=new PrepItem(11,"Lead Scarab",2,"",true);var herb=new PrepItem(12,"Hyssop",2,"",true);var powder=new PrepItem(13,"Powdered Amber",2,"",true);
PrepWorld World(params PrepItem[] inventory)=>new(1,true,false,inventory,20,21,22,0,[]);
var plan=new PrepPlan{Target=1,Reagent="",TaperCount=0};var machine=new PrepMachine();
Check(machine.Start(plan,World(blank,scarab,herb,powder),"DreamWeave","Toe",now,0),"gem batch starts only from an explicit valid plan");
Check(machine.Step(World(blank,scarab,herb,powder),0)?.Kind=="Identify","untouched fragment is identified before any component action");
machine.Step(World(blank,scarab,herb,powder),.1);
var first=machine.Step(World(blank,scarab,herb,powder),1.2);Check(first is{Kind:"Dose",Source:10,Target:11},"dose uses the fragment as source and exact component stack as target");
Check(machine.Step(World(blank,scarab with{Count=1},herb,powder),2)==null&&machine.Data.Dose==0,"inventory decrement alone cannot confirm a component dose");
machine.Observe("[General] Toe says, \"The fragment takes the Lead Scarab.\"");Check(machine.Data.Dose==0,"a player quote does not acknowledge a pending dose");
machine.Observe("The fragment takes the Lead Scarab. It changes the level.");machine.Step(World(blank,scarab with{Count=1},herb,powder),2.1);Check(machine.Data.Dose==1,"matching native load text plus exactly one consumed unit advances the dose");
machine.Step(World(blank,scarab with{Count=1},herb,powder),3.2);machine.Observe("The fragment takes the Hyssop. It may add a bonus modifier.");machine.Step(World(blank,scarab with{Count=1},herb with{Count=1},powder),3.3);
machine.Step(World(blank,scarab with{Count=1},herb with{Count=1},powder),4.4);machine.Observe("The fragment takes the Powdered Amber. It may add an affinity.");machine.Step(World(blank,scarab with{Count=1},herb with{Count=1},powder with{Count=1}),4.5);
var press=machine.Step(World(blank,scarab,herb,powder),5.6);Check(press is{Kind:"Press",Source:10,Target:20},"one press request follows only the confirmed recipe doses");
var gem=new PrepItem(30,"Thread Gem",1,"Level: 315\nModifiers:\nRadiant: kills pay x1.68 luminance\nEntries: 3 of 3\nUnbound. Use it to open a dungeon.",true);
machine.Observe("Pressed. It holds.");Check(machine.Step(World(gem,scarab,herb,powder),6)?.Kind=="Identify","new gem identity and press acknowledgment lead to result inspection");
machine.Step(World(gem,scarab,herb,powder),6.1);Check(machine.Data.State=="Completed"&&machine.Data.Done==1&&machine.Data.Finished.Single()==30,"success requires a confirmed usable new gem in inventory");
var uncertain=new PrepMachine();uncertain.Start(plan,World(blank,scarab,herb,powder),"DreamWeave","Toe",now,0);uncertain.Step(World(blank,scarab,herb,powder),0);uncertain.Step(World(blank,scarab,herb,powder),.1);uncertain.Step(World(blank,scarab,herb,powder),1.2);uncertain.Step(World(blank,scarab,herb,powder),40);
Check(uncertain.Data.State=="Needs review"&&uncertain.Step(World(blank,scarab,herb,powder),41)==null,"an uncertain issued dose times out without redosing");
var restored=new PrepMachine();var checkpoint=JsonSerializer.Deserialize<PrepCheckpoint>(JsonSerializer.Serialize(machine.Data))!;checkpoint.State="Awaiting press";restored.Restore(checkpoint);Check(restored.Data.State=="Needs review"&&!restored.Running,"restart never automatically repeats an interrupted press");
var competing=new PrepMachine();competing.Start(plan,World(blank,scarab,herb,powder),"DreamWeave","Toe",now,0);Check(competing.Step(World(blank,scarab,herb,powder) with{Safe=false},1)==null&&!competing.Running,"competing bot, movement, portal or death stops the builder before new actions");
var top=new PrepMachine();top.Start(new(){TopUp=true,Target=1},World(gem),"DreamWeave","Toe",now,0);Check(top.Data.State=="Completed"&&top.Step(World(gem),1)==null,"top-up satisfied by existing usable gems makes no purchases or crafted extras");
var buy=new PrepMachine();var shopping=plan;shopping.BuyMissing=true;buy.Start(shopping,World(),"DreamWeave","Toe",now,0);var openVendor=buy.Step(World(),0);Check(openVendor is{Kind:"Open vendor",Source:22}||openVendor is{Kind:"Open vendor",Source:21},"missing supplies open only the required known vendor");
var expensive=World() with{OpenVendor=openVendor!.Source,Stock=[new(99,openVendor.Source==21?"Raw Fragment (Level 325)":"Lead Scarab",100,9999999)]};buy.Step(expensive,.1);Check(buy.Step(expensive,.2)==null&&buy.Data.State=="Needs review","purchase cap rejects excessive quotes before any buy request");

foreach(int level in new[]{300,325,350,375})
{
 var levelPlan=new PrepPlan{Level=level,Reagent="",TaperCount=0};
 var matching=blank with{Id=(uint)level,Name="Raw Fragment (Level "+level+")",Description=blank.Description.Replace("325",level.ToString())};
 var levelMachine=new PrepMachine();levelMachine.Start(levelPlan,World(blank,matching,scarab,herb,powder),"DreamWeave","Toe",now,0);
 Check(levelMachine.Step(World(blank,matching,scarab,herb,powder),0)?.Source==(level==325?10u:(uint)level),"selected level "+level+" identifies only the matching inventory blank");
 Check(PrepMachine.Blank(matching,level)&&!PrepMachine.Blank(matching with{Description=matching.Description.Replace("Level: "+level,"Level: 999")},level),"level "+level+" must match the fresh appraisal exactly");
 var levelBuyer=new PrepMachine();levelPlan.BuyMissing=true;levelBuyer.Start(levelPlan,World(scarab,herb,powder),"DreamWeave","Toe",now,0);
 levelBuyer.Step(World(scarab,herb,powder),0);
 var vendorWorld=World(scarab,herb,powder) with{OpenVendor=21,Stock=[new(999,levelPlan.BlankName,100,100)]};
 levelBuyer.Step(vendorWorld,.1);
 Check(levelBuyer.Step(vendorWorld,.2) is{Kind:"Buy",Source:999,Amount:1},"vendor shortages buy the chosen level "+level+" blank");
}
try{new PrepPlan{Level=400}.Validate();Check(false,"unsupported level rejected");}catch(InvalidDataException){Check(true,"vendor level cap rejects unsupported 400 blanks");}
bool macro=true;var macroCalls=new List<bool>();int releases=0;
var lease=new BotPauseLease(()=>macro,v=>{macro=v;macroCalls.Add(v);},()=>releases++);
lease.Acquire();Check(!macro&&lease.OwnsResume,"acquiring the batch lease pauses a running macro");
lease.Release(true);Check(macro&&macroCalls.SequenceEqual(new[]{false,true})&&releases==1,"normal completion restores only the macro this lease paused");
macro=false;macroCalls.Clear();lease.Acquire();lease.Release(true);Check(!macro&&macroCalls.Count==0,"an already paused macro remains paused after a normal batch");
macro=true;lease.Acquire();lease.Release(false);Check(!macro&&!lease.Active,"uncertainty or logout releases ownership without resuming");
macroCalls.Clear();lease.Release(true);Check(macroCalls.Count==0,"a released runtime lease cannot resume from stale ownership");

var playerPos=new StationPosition(0x12340001,0,0,0);
StationCandidate Station(uint id,double x,double? radius=3)=>new(id,"Fragment Press",new(0x12340001,x,0,0),radius);
Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,2.8)]).Id==1,"Fragment Press uses its real 3m radius instead of the old 2.5m cutoff");
Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,3)]).Id==1&&StationLocator.Find("Fragment Press",playerPos,[Station(1,3.01)]).Id==1,"native use is allowed beyond the advertised center radius");
Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,2.8,2)]).Id==1,"advertised center radius does not falsely block a native-use attempt");
Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,2.8,null)]).Id==1,"missing radius uses an explicit 3m fallback");
Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,1),Station(2,2)]).Problem.Contains("multiple"),"multiple reachable presses are diagnosed without guessing");
Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,1),Station(2,50)]).Id==1,"a distant duplicate does not block the unique reachable press");
Check(StationLocator.Find("Fragment Press",playerPos,[]).Problem.Contains("live object list"),"missing live object is distinguished from out of range");
Check(StationLocator.Find("Fragment Press",playerPos,[new(1,"Fragment Press",null,3)]).Problem.Contains("position snapshots"),"missing cached position is reported separately");
Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,13)]).Problem.Contains("13.00m away; local-use limit 12.00m; advertised use radius 3.00m"),"local boundary diagnostics include distance and advertised radius");
Check(StationLocator.Distance(new(0x12340001,191,0,0),new(0x13340001,1,0,0))==2,"adjacent outdoor landblocks use common world coordinates");
Check(StationLocator.Distance(new(0x12340100,0,0,0),new(0x13340100,0,0,0))==null,"unrelated interior landblocks are never treated as adjacent");
Check(StationLocator.Find("Fragment Press",playerPos,[new(1,"Fragment Press",new(0x12340001,0,0,13),3)]).Id==0,"local use boundary includes vertical distance");

Check(StationLocator.Find("Fragment Press",playerPos,[Station(1,3.63)]).Id==1,"the reported 3.63m press remains available for native interaction");
Check(StationLocator.ExpectedApproach("Awaiting vendor",playerPos,new(0x12340001,4,0,0)),"client auto-approach is permitted while waiting for the intended vendor");
Check(!StationLocator.ExpectedApproach("Loading",playerPos,new(0x12340001,4,0,0))&&!StationLocator.ExpectedApproach("Awaiting vendor",playerPos,new(0x12340001,13,0,0)),"component loading and travel outside the batch area do not permit approach movement");

var bankPlan=new PrepPlan{Target=1,Reagent="",TaperCount=0,BuyMissing=true,Budget=10};
var bankMachine=new PrepMachine();bankMachine.Start(bankPlan,World(scarab,herb,powder),"DreamWeave","Toe",now,0);bankMachine.Step(World(scarab,herb,powder),0);
var bankWorld=World(scarab,herb,powder,new(777,"Trade Notes (250,000)",1,"",true,12345)) with{OpenVendor=21,Stock=[new(999,bankPlan.BlankName,100,1,12345)]};
bankMachine.Step(bankWorld,.1);
Check(bankMachine.Step(bankWorld,.2) is{Kind:"Buy",Amount:1}&&bankMachine.Data.ReservedCurrency[12345]==1&&bankMachine.Data.ReservedSpend==0,"native vendor quotes remain in their original currency without denomination conversion");
Check(StationLocator.ExpectedApproach("Awaiting press",playerPos,new(0x12340002,3,0,0)),"a cell transition during a short native press approach is not mistaken for leaving the batch");

var note=new PrepItem(777,"Trade Notes (250,000)",1,"",true,12345);
PrepWorld NoteWorld(int count)=>World(scarab,herb,powder,note with{Count=count}) with{OpenVendor=21,Stock=[new(999,"Raw Fragment (Level 325)",100,2,12345,"Trade Notes (250,000)")]};
PrepMachine NoteMachine(bool auto)
{
 var m=new PrepMachine();m.Start(new(){BuyMissing=true,AutoWithdrawNotes=auto,Reagent="",TaperCount=0},NoteWorld(1),"DreamWeave","Toe",now,0);return m;
}
var noWithdraw=NoteMachine(false);Check(noWithdraw.Step(NoteWorld(1),0)==null&&noWithdraw.Data.Notice.Contains("Currency shortage")&&!noWithdraw.Running,"auto-withdraw off reports the shortage and sends no withdrawal or purchase");
var withdraw=NoteMachine(true);Check(withdraw.Step(NoteWorld(1),0) is{Kind:"Withdraw notes",Amount:1}&&withdraw.Data.WithdrawalCount==1,"auto-withdraw requests only the single missing note, not the full purchase cost");
withdraw.Observe("[Allegiance] Toe says, \"[BANK] Withdrew 1 trade note(s) worth 250,000 pyreals. Balance: 1,000,000\"");
Check(withdraw.Step(NoteWorld(2),.1)==null&&withdraw.Data.State=="Awaiting bank withdrawal","a quoted bank message plus currency gain cannot spoof withdrawal confirmation");
withdraw.Observe("[BANK] Withdrew 1 trade note(s) worth 250,000 pyreals. Balance: 1,000,000");
Check(withdraw.Step(NoteWorld(1),.2)==null&&withdraw.Data.State=="Awaiting bank withdrawal","bank confirmation alone does not proceed before the note reaches inventory");
withdraw.Step(NoteWorld(2),.3);Check(withdraw.Data.State=="Supply check"&&withdraw.Data.WithdrawalCount==0,"matching bank acknowledgment and inventory increase complete the withdrawal");
Check(withdraw.Step(NoteWorld(2),1.4) is{Kind:"Buy",Amount:1},"confirmed withdrawal continues the original purchase without another bank command");
var enough=NoteMachine(true);Check(enough.Step(NoteWorld(2),0) is{Kind:"Buy"},"enough carried notes skip auto-withdraw entirely");
var withdrawalTimeout=NoteMachine(true);withdrawalTimeout.Step(NoteWorld(1),0);withdrawalTimeout.Step(NoteWorld(1),31);Check(!withdrawalTimeout.Running&&withdrawalTimeout.Step(NoteWorld(1),32)==null,"a missing bank reply times out without automatic withdrawal retry");
var withdrawalRestart=new PrepMachine();withdrawalRestart.Restore(new(){State="Awaiting bank withdrawal",WithdrawalCount=1,WithdrawalCurrency=12345});Check(!withdrawalRestart.Running&&withdrawalRestart.Data.State=="Needs review","restart never repeats an interrupted withdrawal");
var failedBank=NoteMachine(true);failedBank.Step(NoteWorld(1),0);failedBank.Observe("[BANK] Insufficient funds.");Check(!failedBank.Running&&failedBank.Data.Notice.Contains("Bank withdrawal refused"),"insufficient bank funds stop the batch with the bank's reason");

string root=Path.Combine(Path.GetTempPath(),"threads-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
 var store=new ThreadGemStore(root,"DreamWeave","Toe",v=>JsonSerializer.Serialize(v),(t,type)=>JsonSerializer.Deserialize(t,type)!);
 store.Save(data,data.Rolls.ToArray());foreach(var run in data.Runs)store.SaveRun(run);
 var loaded=store.Load();Check(loaded.Rolls.Count==data.Rolls.Count&&loaded.Runs.Any(r=>r.EndedUtc.HasValue),"compact press/run files roundtrip with scope and lifecycle intact");
 Check(!Directory.GetFiles(root,"*.json",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p).Contains("RawChat")),"persistent records contain no raw chat transcript");
 var who=new ThreadIdentity("DreamWeave","Toe");Check(who.Folder(root)!=new ThreadIdentity("DreamWeave","Samm").Folder(root),"Thread storage is isolated by server and character");
 var worker=new ThreadWorker(who,root);
 for(int i=0;!worker.Ready&&i<300;i++)await Task.Delay(10);
 Check(worker.Ready,"standalone managed journal starts without Bridge or Keyfer");
 worker.Submit(new("prep-recipe",recipe,now,321,StartingLevel:375));worker.Submit(new("chat","Pressed. It holds.",now,321));
 worker.Submit(new("chat",open,now));worker.Submit(new("chat","[REALM] id=0 inst=99 eph=1 name=Base World",now.AddSeconds(1)));
 for(int i=0;worker.View.Runs==0&&i<300;i++)await Task.Delay(10);
 worker.Dispose();await worker.Completion;
 Check(worker.View.Runs==1,"background worker records a scoped run and flushes on shutdown");
 var scoped=new ThreadGemStore(who.Folder(root),who.Server,who.Character,v=>JsonSerializer.Serialize(v),(t,type)=>JsonSerializer.Deserialize(t,type)!);
 Check(scoped.Load().Rolls.Any(r=>r.FragmentLevel==375&&r.PrepRecipeConfirmed),"native prep records the actual selected starting level rather than hardcoding 325");
}
finally{Directory.Delete(root,true);}
Console.WriteLine($"All {count} Thread checks passed; no native game actions or network calls were made.");
