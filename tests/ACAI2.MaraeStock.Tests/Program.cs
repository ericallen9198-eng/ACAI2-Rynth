using ACAI2.Rynth.Marae.Core;
int checks=0;
void Check(bool b,string why){if(!b)throw new Exception(why);checks++;}
void Refuses(Action a,string why){try{a();}catch(InvalidDataException){checks++;return;}throw new Exception(why);}
var h=new Hunter(1,"Hea Hineroa the Golem Hunter",225,53,-79,.3,"1");
var gate=new NpcGate(h,0);
Check(gate.Due(0),"NPC first attempt");Check(!gate.Observe(h.Name+" tells you, \"Slay 50 Anything. Return when the work is done.\""),"pre-request reply cannot satisfy gate");
gate.Issued(0);
Check(!gate.Observe("Toe tells you, \"Slay 50 Anything. Return when the work is done.\""),"other sender rejected");
Check(!gate.Observe(h.Name+" tells you, \"My hunts suit a hunter of about level 225.\""),"intro does not confirm task");
Check(gate.Observe(h.Name+" tells you, \"You have defeated 0 of 50 Drumtaken Ironstone Golem. The hunt is not yet finished.\""),"live reply confirms without brittle target match");
gate.Observe(h.Name+" tells you, \"To give up this hunt, send me a tell: abandon\"");Check(gate.Confirmed,"later informational reply cannot erase confirmation");
Check(!gate.Due(20),"no repeat after confirmed reply");
var timeout=new NpcGate(h,0);for(int i=0;i<3;i++){Check(timeout.Due(i*8),"bounded retry due");timeout.Issued(i*8);}
Check(timeout.Exhausted(24)&&!timeout.Due(24),"no-reply gate expires instead of stalling");
foreach(string text in new[]{"Your hunt for today is done. You may take another hunt in 6h","Return in 2 hours for another hunt.","You have served the Hea well. Accept this reward for your efforts.","The hunt is complete; return to me."}){var g=new NpcGate(h,0);g.Issued(0);Check(g.Observe(h.Name+" tells you, \""+text+"\""),"cooldown/reward/complete is a valid reply");}
Check(NpcDialogue.Parse("Slay 3 Changed Target. Return when the work is done.")?.Target=="Changed Target","new target counts do not block");
Check(NpcDialogue.Parse("You have defeated 5 of 20 Aun Elder Shaman. The hunt is not yet finished.")?.Target=="Aun Elder Shaman","in-progress target parse");
Check(NpcDialogue.CompletionTarget("You have killed 50 Virindi Sentries! Your task is complete!")=="Virindi Sentries","completion target excludes message punctuation");
Check(NpcDialogue.CompletionTarget("Toe says, \"You have killed 50 Virindi Sentries! Your task is complete!\"")==null,"quoted player text cannot complete task");
Check(NpcDialogue.SameTarget("Virindi Sentries","Virindi Sentry"),"completion plural");
var points=new[]{new RoutePoint("Point",1,2,.1),new RoutePoint("Point",3,4,.2),new RoutePoint("Chat",3,4,.2,"/ra mexec setvar[pt_travel_end,1]")};
Check(new RouteSnapshot("hunt",4,1,points).ResumeFile().Replace("\r","").Contains("\n2\n0\n4\n3\n"),"once restores suffix");
Check(new RouteSnapshot("hunt",1,2,points).ResumeFile().Replace("\r","").StartsWith("uTank2 NAV 1.2\n1\n3\n4\n"),"circular rotates without losing points");
Refuses(()=>new RouteSnapshot("hunt",2,0,points).ResumeFile(),"linear semantics cannot silently change");
Refuses(()=>RouteSnapshot.Serialize(4,[new("Npc",0,0,0)]),"NPC trailers cannot be fabricated");
Refuses(()=>RouteSnapshot.Serialize(4,[new("Point",double.NaN,0,0)]),"nonfinite route refused");
Refuses(()=>RouteSnapshot.Serialize(4,[new("Chat",0,0,0,"/ls\n/ls")]),"multiline route command refused");
foreach(double heading in new[]{0d,45,90,135,180,225,270,315})foreach(double distance in new[]{10d,30,60,130,180}){var(n,e)=MapGeometry.Goal(0,0,heading,distance);Check(Math.Abs(Math.Sqrt(n*n+e*e)*240-distance)<1e-8,"exact configured clue distance");}
Refuses(()=>MapGeometry.Goal(0,0,double.NaN,30),"nonfinite heading");Refuses(()=>MapGeometry.Goal(0,0,0,181),"bounded stride");
var stand=MapGeometry.StandOff(0,0,0,.1,2.5);Check(Math.Abs((.1-stand.EW)*240-2.5)<1e-9,"cache stop short of center");
var recovery=new RecoveryObserver();Check(!recovery.Busy(0,false,false,false),"idle recovery");Check(recovery.Busy(1,true,false,false),"death holds movement");
Check(recovery.Busy(10,false,false,true),"native corpse trip owns movement even after respawn");
recovery.Observe("You have retained all your items. You do not need to recover your corpse!",11);Check(!recovery.Busy(12,false,false,false),"no dropped items releases settled recovery");
var unknown=new RecoveryObserver();unknown.Busy(1,true,false,false);Check(unknown.Busy(30,false,false,false),"unknown recovery yields");Check(!unknown.Busy(932,false,false,false),"unknown recovery cannot stall forever");
var p=MaraeOptions.Default with {Close=1,Far=999};Check(p.Validate().Close==10&&p.Validate().Far==180,"UI option clamp");
string temp=Path.Combine(Path.GetTempPath(),"marae-policy-"+Guid.NewGuid().ToString("N"));
try{var profile=new MaraeProfile{Server="DreamWeave",Character="Toe",Active=true,PendingMove=new(1,2,1004101,1,0)};profile.Save(Path.Combine(temp,"profile.json"));var restored=MaraeProfile.Load(Path.Combine(temp,"profile.json"),"DreamWeave","Toe");Check(restored.Active&&restored.PendingMove?.Item==2,"reconnect retains intent and unresolved move");Refuses(()=>MaraeProfile.Load(Path.Combine(temp,"profile.json"),"DreamWeave","Samm"),"different character cannot inherit run");}finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
Check(CachePickupPolicy.CanIssue(true,2,1,2,1,1,true,true,1004101,false,true,false),"owned cache doubloon dispatch allowed");
Check(!CachePickupPolicy.CanIssue(true,2,1,2,1,3,true,true,1004101,false,true,false),"wrong open container refused");
Check(!CachePickupPolicy.CanIssue(true,2,1,2,1,1,true,true,1004101,false,true,true),"unresolved move never replayed");
Check(!CachePickupPolicy.CanIssue(true,2,1,2,1,1,true,true,99,false,true,false),"unrelated item refused");
Check(!CachePickupPolicy.Confirmed(new(1,2,1004101,10,0),10,false),"inventory growth alone cannot acknowledge move");
Check(CachePickupPolicy.Confirmed(new(1,2,1004101,10,0),10,true),"inventory and source change acknowledge move");
Check(!ResumePolicy.CanStart(true,true,true,false),"reconnect cannot interrupt native corpse recovery");
Check(!ResumePolicy.CanStart(true,true,false,true),"reconnect waits for portal completion");
Check(!ResumePolicy.CanStart(false,true,false,false),"manual Stop prevents reconnect restart");
Check(ResumePolicy.CanStart(true,true,false,false),"settled reconnect can resume");
Console.WriteLine($"PASS: {checks} Marae stock NPC, route, clue, recovery and profile checks (synthetic world data only).");
