using System.Text.Json;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ACAI2.Rynth.Combat.Core;
int checks=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
var utc=new DateTime(2026,11,14,0,0,0,DateTimeKind.Utc);
var e=new CombatEngine("DreamWeave","Toe");
bool Observe(string text,double second=0,int type=0)=>e.Observe(text,type,utc.AddSeconds(second),second);
Check(!Observe("[Allegiance] Samm says, \"Your acid deals 999 points of periodic damage to Mob!\"")&&!Observe("Your acid deals 999 points of periodic damage to Mob!",0,3),"player quotations and private chat do not spoof combat totals");
Observe("Your acid deals 249 points of periodic damage to Sepulchral Archon!");Observe("Your poison deals 100 additional points of damage to Sepulchral Archon!",.1);
Check(e.Session.Damage==349&&e.Session.Components["Acid"]==249&&e.Session.Components["Poison"]==100,"acid and poison retain separate actual damage totals");
Observe("Sneak Attack! You bash Sepulchral Archon for 169 points of bludgeoning damage!",.2);Observe("You bash Sepulchral Archon for 31 points of bludgeoning damage!",.3);Observe("You graze Sepulchral Archon for 0 points of bludgeoning damage!",.4);
Check(e.Session.DirectHits==3&&e.Session.Sneaks==1&&Math.Abs(e.Session.SneakPercent-100d/3)<.01,"sneak denominator uses landed direct physical hits including zero hits, excluding procs");
Observe("You burn Sepulchral Archon for 500 points with Incantation of Flame Bolt.",.5);
Check(e.Session.DirectHits==3,"direct magic does not inflate the sneak-attack denominator");
Observe("Toe's Fire Elemental hits Sepulchral Archon for 20 damage!",.6);
Check(e.Session.Components["Summons"]==20&&!Observe("Samm's Fire Elemental hits Sepulchral Archon for 999 damage!",.7),"summon damage belongs only to the current character");
Observe("Your thorns reflect 30 points of damage back at Sepulchral Archon!",.8);
Check(e.Session.Components["Thorns"]==30,"reflection stays distinct from direct and proc damage");
Observe("Sepulchral Archon scratches your chest for 0 points of slashing damage!",1);Observe("Sepulchral Archon scratches your arm for 20 points of slashing damage!",1.1);Observe("You evaded Sepulchral Archon!",1.2);
Check(e.Session.PhysicalHits==2&&e.Session.PhysicalDamage==20&&e.Session.PhysicalHigh==20&&Math.Abs(e.Session.EvadePercent-100d/3)<.01,"physical defense keeps zero hits and evades with an explicit observed denominator");
Observe("Sepulchral Archon evaded your attack.",1.3);Check(e.Session.Evades==1,"outgoing evades cannot inflate player defense");
Observe("Critical hit! Sepulchral Archon impales you for 102 points with Incantation of Force Bolt.",1.4);Observe("You resist the spell cast by Sepulchral Archon!",1.5);
Check(e.Session.MagicHits==1&&e.Session.MagicHigh==102&&e.Session.ResistPercent==50,"magic hits, maximum damage and resists have their own denominator");
Observe("You receive 118 points of periodic healing.",1.6);Observe("You catch your second wind.",1.7);
Check(e.Session.Healing==118&&e.Session.HealingSources["Periodic healing (source unspecified)"]==118&&e.Session.Procs["Second Wind"]==1&&!e.Session.Procs.ContainsKey("Bloodlust"),"Second Wind is a proc without an invented heal amount; generic periodic healing is not Bloodlust");
Observe("Your runic ward is fully inscribed, absorbing your next 119 points of damage.",1.8);Observe("Your runic ward shatters after absorbing 135 points of damage.",1.9);Observe("Your runic ward fades after absorbing 330 points of damage.",2);
Check(e.Session.WardAbsorbed==465&&e.Session.WardCapacity==0&&e.Session.Healing==118,"ward absorption and capacity are separate from healing");
Observe("Dirty Fighting! Toe delivers a Bleeding Blow to Sepulchral Archon!",2.1);Check(e.Session.Procs.ContainsKey("Debuffs"),"observed Dirty Fighting messages activate utility tracking");
Observe("The thunder of crushing Sepulchral Archon is followed by the deafening silence of death!",2.2);Check(e.Session.Kills==1&&e.Session.Mobs["Sepulchral Archon"].Kills==1,"known kill messages attribute kills to mob type");
const string death="Sepulchral Archon's attack stops you cold! (Killing blow: 151 damage. You had 111/462 health.)";
Observe(death,3);var d=e.Session.Deaths.Single();
Check(d.Killer=="Sepulchral Archon"&&d.Blow==151&&d.Health==111&&d.Maximum==462&&d.Overkill==40&&d.Message==death,"the actual supplied server death line preserves killer, blow, health and 40 overkill");
Observe("You are dead!",3.1);Observe(death,3.2);Check(e.Session.Deaths.Count==1,"generic and repeated notices do not duplicate the same recent death");
long before=e.Session.Damage;Observe("Your acid deals 249 points of periodic damage to Sepulchral Archon!",3.3);Check(e.Session.Damage==before+249&&e.Session.Deaths.Count==1,"posthumous outgoing acid remains damage, not a second death or healing");
Check(e.Rates(100,false).Dps==0&&e.Session.Damage>0,"the bounded sixty-second rate expires while session totals remain");

var genericFirst=new CombatEngine("DreamWeave","Toe");genericFirst.Observe("You are dead!",0,utc,0);genericFirst.Observe(death,0,utc.AddSeconds(.1),.1);
Check(genericFirst.Session.Deaths.Count==1&&genericFirst.Session.DeathCount==1&&genericFirst.Session.Deaths[0].Overkill==40,"a later detailed notice upgrades a generic death without inflating the death count");
for(int i=1;i<=70;i++)genericFirst.Observe(death,0,utc.AddSeconds(i*3),i*3);
Check(genericFirst.Session.DeathCount==71&&genericFirst.Session.Deaths.Count==64&&genericFirst.Session.Truncated,"total deaths remain accurate when the bounded death-notice history fills");

var run=new CombatEngine("DreamWeave","Toe");
const string open="The Thread Gem opens a way into Burun Fortress. It will bring you back 3 more time(s) while the dungeon stands.";
void Run(string text,double t)=>run.Observe(text,0,utc.AddSeconds(t),t);
Run(open,0);Check(run.Current is{Entered:null}&&!run.Inside,"opening a gem is not proof of dungeon entry");
Run("[REALM] id=0 inst=123 eph=1 name=Base World",1);run.Tick(utc.AddSeconds(2));run.Tick(utc.AddSeconds(3));
Check(run.Inside&&run.Current!.Observed==2,"confirmed realm entry starts observed run timing");
Run("You bash Burun for 100 points of bludgeoning damage!",3);run.Seen(10,"Burun");run.Seen(10,"Burun");run.Seen(11,"Burun");
Check(run.Current!.Stats.Damage==100&&run.Current.Stats.Mobs["Burun"].Seen==2,"run totals reset independently and sampled IDs count once per run");
Run("Monsters killed 40/74. Boss still remaining.",3.1);Check(run.Current.KilledProgress==40&&run.Current.MonsterTotal==74,"server progress is kept separate from attributed kill messages");
Run("Monsters killed 1/74. Boss still remaining.",3.2);Check(run.Current.KilledProgress==40,"out-of-order progress cannot rewind the count");
run.Tick(utc.AddSeconds(15));Check(run.Current.Gaps&&run.Current.Observed==2,"long sample gaps are labeled instead of invented as observed time");
Run("[REALM] id=0 inst=0 eph=0 name=Base World",16);double observed=run.Current.Observed;run.Tick(utc.AddSeconds(18));Check(run.Current.Observed==observed,"time outside the dungeon does not accrue as observed run time");
Run(open,19);Run("[REALM] id=0 inst=123 eph=1 name=Base World",20);Check(run.Runs.Count==1&&run.Current.Stats.Damage==100,"re-entering the same realm resumes its run rather than resetting it");
Run("Burun Fortress is cleared. Your gem will bring you back if you fall; it crumbles when you leave by recall or the exit portal.",21);double finished=run.Current.Observed;
run.Tick(utc.AddSeconds(22));Check(run.Current.Cleared==utc.AddSeconds(21)&&run.Current.CompletionSeconds==20&&run.Current.Observed==finished,"entry-to-clear wall time and observed time stay distinct and freeze at clear");
Run("Your Thread Gem crumbles to dust.",23);Check(run.Current.Ended.HasValue&&!run.Inside,"crumbling closes the run independently of its earlier clear");
Run(open,24);Run("[REALM] id=0 inst=124 eph=1 name=Base World",25);Check(run.Current.Stats.Damage==0&&run.Session.Damage==100,"the next Thread starts fresh while the session keeps its totals");
Run(death,26);Run("You are dead!",26.1);Check(run.Current.Stats.Deaths.Count==1&&run.Current.Stats.Deaths[0].Overkill==40,"run death summaries share the same exact death and de-duplication logic");
var restored=new CombatEngine("DreamWeave","Toe");restored.RestoreRuns(run.Runs);Check(restored.Current!.Gaps&&!restored.Inside,"restored records are marked gapped and never assume current entry");
restored.Observe("[REALM] id=0 inst=124 eph=1 name=Base World",0,utc.AddSeconds(30),30);Check(restored.Inside&&restored.Current.Stats.Deaths.Count==1,"a later confirmed realm can resume restored statistics");
restored.Pause(utc.AddSeconds(31),true);Check(restored.EntryConfirmed&&!restored.Inside,"transient portal pause retains a confirmed realm without claiming observation during loading");
var limited=new Totals();for(int i=0;i<400;i++)limited.Mob("Mob "+i).Kills++;Check(limited.Mobs.Count==257&&limited.Truncated,"mob detail is bounded and explicitly labels overflow");

var appearance=new Appearance{Curve=160,Gap=40,Opacity=float.NaN,Colors=new(){["unexpected"]=0}};appearance.Validate();
Check(appearance.Opacity==.72f&&!appearance.Colors.ContainsKey("unexpected")&&appearance.Colors.ContainsKey("Poison"),"profile validation repairs non-finite values and restores known palette entries");
var stream=new FloatStream();stream.Add(0,"Acid","ACID","Mob",10);stream.Add(.1,"Acid","ACID","Mob",20);
Check(stream.Read(.1) is[{Amount:30,Count:2}],"rapid same-target proc ticks coalesce without changing true aggregate amounts");
var left=stream.Read(.1)[0] with{Incoming=true};var arc=FloatStream.Arc(left,2,appearance);Check(arc.X<=-24,"even maximum inward curvature cannot cross the character center");
for(int i=0;i<100;i++)stream.Add(.2+i*.02,"Poison","POISON","Mob "+i,1);Check(stream.Read(2.3).Length<=48&&stream.Read(10).Length==0,"floating event memory is bounded and expired effects disappear");
long real=e.Session.Damage;var preview=new FloatStream();preview.Add(4,"Critical","PREVIEW",amount:9999);Check(e.Session.Damage==real,"preview effects do not modify real session or run statistics");

string root=Path.Combine(Path.GetTempPath(),"acai2-combat-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
 string folder=Profiles.Folder(root,"DreamWeave","Toe");Check(folder!=Profiles.Folder(root,"DreamWeave","Samm")&&folder!=Profiles.Folder(root,"Other","Toe"),"profiles and run storage isolate both server and character");
 string profile=Path.Combine(folder,"ui.json");appearance.Locked=true;appearance.Bold=true;appearance.Modules["Sneak"]=true;Profiles.Save(profile,appearance);var loaded=Profiles.Load(profile);
 Check(loaded.Locked&&loaded.Bold&&loaded.Modules["Sneak"]&&loaded.Curve==160,"appearance, module overrides and position locks survive reload");
 var archive=new CombatArchive(root,"DreamWeave","Toe");for(int i=0;i<300&&!archive.Ready;i++)await Task.Delay(10);Check(archive.Ready,"local compact archive becomes ready without a game, Bridge or network");
 var duplicate=new CombatArchive(root,"DreamWeave","Toe");await duplicate.Completion;Check(!duplicate.Ready&&duplicate.Notice.Contains("unavailable"),"an exclusive archive owner prevents two instances writing the same character records");
 var record=run.Current!;Check(archive.Save(record),"a completed snapshot can be queued without blocking native observation");archive.Dispose();await archive.Completion;
 string raw=File.ReadAllText(Path.Combine(folder,"run-"+record.Id+".json"));
 Check(raw.Contains("Killing blow")&&!raw.Contains("RawChat")&&!raw.Contains("You bash Burun"),"saved run records retain explicit death notices but no ordinary combat transcript");
 var reloaded=new CombatArchive(root,"DreamWeave","Toe");for(int i=0;i<300&&!reloaded.Ready;i++)await Task.Delay(10);Check(reloaded.History.Single().Stats.Deaths.Single().Overkill==40,"compact run files roundtrip the server's death details");reloaded.Dispose();await reloaded.Completion;
}
finally{Directory.Delete(root,true);}
string dll=Path.GetFullPath("src/ACAI2.Rynth.Combat.Plugin/bin/Release/net10.0-windows/win-x86/ACAI2.Rynth.Combat.Plugin.dll");
if(File.Exists(dll))
{
 using var pe=new PEReader(File.OpenRead(dll));var reader=pe.GetMetadataReader();var refs=reader.AssemblyReferences.Select(h=>reader.GetString(reader.GetAssemblyReference(h).Name)).ToArray();
 Check(refs.Contains("ACAI2.Rynth.Combat.Core")&&!refs.Any(r=>r.Contains("Threads")||r.Contains("Combat.Future")||r=="ACAI2.Rynth.Core"),"the real-SDK plugin has an independent combat dependency graph with no Keyfer or Threads");
 Check(pe.PEHeaders.CoffHeader.Machine==Machine.I386,"the managed plugin targets the actual x86 client ABI");
}
Console.WriteLine($"All {checks} Combat checks passed. No game actions, production network or AI calls were made.");
