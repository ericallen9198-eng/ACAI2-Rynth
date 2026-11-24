using ACAI2.Rynth.MapHunt.Core;
using System.Globalization;
int checks=0;
void Check(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);checks++;Console.WriteLine("PASS: "+message);}
MapHuntProfile Profile()=>new(){Server="DreamWeave",Character="Samm"};
var bot=new FakeBot();var p=Profile();int prepared=0,saved=0;
var c=new MapHuntController(bot,p,()=>prepared++,()=>saved++);
c.ImportPreferences();c.Begin(0);Check(bot.Commands.SequenceEqual(new[]{"/ra stop","/ra meta load MapHunt App.af"})&&prepared==1,"Start loads only the app's meta after preparing its own file");
c.Begin(.1);Check(prepared==1&&bot.Commands.Count==2,"double-click Start cannot restart an outstanding startup");
bot.Meta="MapHunt App";c.Tick(.5);Check(bot.State=="Default"&&bot.Commands.Last()=="/ra start","correct meta is enabled before the macro starts");
bot.Running=true;bot.Vars["mh_schema"]="1";bot.State="HUNT";c.Tick(1);
Check(p.Active&&c.View.Phase=="Hunting route"&&bot.Pvars["maphunt_close"]=="30"&&bot.Pvars["maphunt_far"]=="130","default steps are exact 30 / 130 yards and active intent is saved");
c.Preferences(new(true,1,999));Check(bot.Pvars["meshnav"]=="1"&&bot.Pvars["maphunt_close"]=="10"&&bot.Pvars["maphunt_far"]=="180","mesh and distances share pvars and enforce the configured bounds");
bot.State="MAP_MESH_WAIT";bot.Vars["mh_walk_token"]="1";bot.Vars["tm_mesh_heading"]="90";bot.Vars["tm_mesh_step"]="130";int commands=bot.Commands.Count;
c.Tick(2);c.Tick(3);Check(bot.MeshRequests==1&&bot.Vars["tm_walk_ack"]=="1","one strict mesh request per leg, not one per tick");
bot.MeshOk=false;bot.Vars["mh_walk_token"]="2";c.Tick(4);
Check(bot.Vars["tm_walk_ack"]=="-1"&&!bot.Commands.Skip(commands).Any(s=>s.Contains("setmotion")),"mesh failure is explicit and never falls back to raw movement");
bot.State="DIRECT_WAIT";bot.Vars["mh_walk_token"]="3";c.Tick(5);
Check(bot.Commands.Last()=="/ub face 90","direct mode faces first");c.Tick(5.5);Check(bot.Commands.Last()=="/ub face 90","direct mode waits for the turn to settle");
c.Tick(6);c.Tick(7);Check(bot.Commands.Count(s=>s=="/ub setmotion Forward 1")==1,"direct mode starts only once after settling");
bot.State="CACHE_APPROACH_WAIT";bot.Vars["mh_walk_token"]="4";bot.Vars["tm_walk_target"]="51.2|-84.0|0.2";c.Tick(8);
Check(bot.Goals.Count==1&&Math.Abs(bot.Goals[0].NS-(51.2-2.5/240))<1e-8,"cache approach stops 2.5 yards short of its center");
c.Stop();Check(!p.Active&&!bot.Running&&bot.Commands.Last()=="/ra stop","Stop clears active intent and stops the owned macro");
Check(bot.Commands.All(s=>!s.Contains("hunt start")&&!s.Contains("/lua")),"app never issues a re-anchoring Hunt start or starts a Lua script");
var other=new FakeBot{Meta="Other",Running=true};var old=Profile();old.Active=true;int autoPrepared=0;
var auto=new MapHuntController(other,old,()=>autoPrepared++,()=>{});auto.Begin(0,true);auto.Tick(1);
Check(other.Commands.Count==0&&autoPrepared==0&&auto.View.Phase=="Resume available","automatic resume never interrupts another running meta");
var unsupported=new FakeBot{Supported=false};var no=new MapHuntController(unsupported,Profile(),()=>throw new Exception("must not install"),()=>{});no.Begin(0);no.Tick(1);
Check(unsupported.Commands.Count==0&&no.View.Phase=="Compatibility support required","unsupported builds are explained without changing their DLL or starting actions");
var badFolder=new FakeBot();var install=new MapHuntController(badFolder,Profile(),()=>throw new IOException("bad path"),()=>{});
try{install.Begin(0);}catch(IOException){}
Check(badFolder.Commands.Count==0,"bad installation folder is detected before stopping any macro");
var timeoutBot=new FakeBot();var timeoutProfile=Profile();var timeout=new MapHuntController(timeoutBot,timeoutProfile,()=>{},()=>{});
timeout.Begin(0);timeout.Tick(16);timeout.Tick(17);
Check(timeout.View.Phase=="Startup not confirmed"&&!timeoutProfile.Active&&timeoutBot.Commands.Count==2,"failed meta load retains a clear explanation and does not stop a different meta");
var blankBot=new FakeBot();var blankProfile=Profile();var blank=new MapHuntController(blankBot,blankProfile,()=>{},()=>{});
blank.Begin(0);blankBot.Meta="MapHunt App";blank.Tick(.5);blankBot.Running=true;blank.Tick(16);
Check(blank.View.Phase=="Startup not confirmed"&&!blankBot.Running&&!blankProfile.Active,"blank/wrong workflow cannot masquerade as a running map hunt");
var eventBot=new FakeBot();var eventProfile=Profile();eventProfile.Active=true;eventProfile.Event=new("51.1|-84.0|0.2",5000,7,"ENCOUNTER");
var resume=new MapHuntController(eventBot,eventProfile,()=>{},()=>{});resume.Begin(0,true);eventBot.Meta="MapHunt App";resume.Tick(.5);eventBot.Running=true;eventBot.Vars["mh_schema"]="1";resume.Tick(1);
Check(eventBot.State=="ENCOUNTER"&&eventBot.Vars["tm_event_deadline"]=="5000"&&eventBot.Commands.Last()=="/ra nav load maphunt.nav","nearby active event resumes after reconnect without extending its deadline");
var reviewBot=new FakeBot();reviewBot.Pvars["mh_mm_pickup_unresolved"]="1";var reviewProfile=Profile();reviewProfile.Event=eventProfile.Event;
var review=new MapHuntController(reviewBot,reviewProfile,()=>{},()=>{});review.Begin(0);reviewBot.Meta="MapHunt App";review.Tick(.5);reviewBot.Running=true;reviewBot.Vars["mh_schema"]="1";reviewBot.State="HOLD";review.Tick(1);
Check(reviewBot.State=="HOLD"&&review.View.Review&&!reviewBot.Calls.Any(s=>s.StartsWith("maraecachepickup")),"unconfirmed currency move is never replayed on reconnect");
reviewBot.Running=false;review.Tick(2);Check(!reviewProfile.Active,"native macro Stop also clears saved run intent");
Check(!MapHuntGeometry.TryCoordinates("1|2|0`]/quit",out _,out _)&&!MapHuntGeometry.TryCoordinates("NaN|2|0",out _,out _),"malformed checkpoint coordinates cannot become expressions");
Check(MapHuntGeometry.Approach("51.1|-84|0.2","51.1|-84|0.2")==null,"already-near cache does not create a route through its center");
string folder=Path.Combine(Path.GetTempPath(),"maphunt-app-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
try
{
 string meta=Path.Combine(folder,"MetaFiles","MapHunt App.af");string nav=Path.Combine(folder,"NavProfiles","maphunt.nav");Directory.CreateDirectory(Path.GetDirectoryName(nav)!);File.WriteAllText(nav,"my route");
 MapHuntWorkflow.Install(folder,"first workflow");MapHuntWorkflow.Install(folder,"next workflow");
 Check(File.ReadAllText(meta)=="next workflow"&&File.ReadAllText(meta+".previous")=="first workflow"&&File.ReadAllText(nav)=="my route","only the app's workflow is installed, with a bounded backup; user nav is untouched");
 Check(MapHuntWorkflow.Folder("",meta,null)==folder,"RynthAi folder is discovered from its active meta path");
 string json=Path.Combine(folder,"profile.json");p.Preferences=new(true,30,130);p.Save(json);var loaded=MapHuntProfile.Load(json,"DreamWeave","Samm");
 Check(loaded.Preferences.Mesh&&loaded.Preferences.Close==30&&loaded.Preferences.Far==130,"per-character preferences persist through a profile reload");
 bool mismatch=false;try{MapHuntProfile.Load(json,"DreamWeave","Toe");}catch(InvalidDataException){mismatch=true;}
 Check(mismatch,"a different character cannot inherit the saved run");
 File.WriteAllText(json,"malformed");bool malformed=false;try{MapHuntProfile.Load(json,"DreamWeave","Samm");}catch{malformed=true;}
 Check(malformed&&File.ReadAllText(json)=="malformed","unreadable settings remain intact for review");
}
finally{Directory.Delete(folder,true);}
if(args.Length>0)File.WriteAllText(args[0],System.Text.Json.JsonSerializer.Serialize(FakeBot.All.SelectMany(b=>b.Calls).Distinct().ToArray()));
Console.WriteLine($"All {checks} MapHunt app checks passed. Fake RynthAi only; no game/AI/Bridge calls.");

sealed class FakeBot:IMapHuntBot
{
 public static List<FakeBot> All=new();
 public FakeBot(){All.Add(this);}
 public string Meta="Other",State="HOLD";public bool Running{get;set;}public bool Supported=true,MeshOk=true;public int MeshRequests;
 public Dictionary<string,string> Vars=new(),Pvars=new();public List<string> Commands=new(),Calls=new();public List<(double NS,double EW)> Goals=new();
 public string? Setting(string n)=>null;
 public void Command(string s){Commands.Add(s);if(s=="/ra stop")Running=false;}
 public bool GoTo(double ns,double ew){Goals.Add((ns,ew));return true;}
 public string Evaluate(string s)
 {
  Calls.Add(s);
  if(s=="vtgetmeta[]")return Meta;if(s=="vtgetmetastate[]")return State;
  if(s=="maraemissionapi[]")return Supported?"6":"0";
  if(s=="getunixtime[]")return "1000";
  if(s=="getcharvital_current[1]")return "100";
  if(s=="maraerecoverybusy[]")return "0";
  if(s=="getplayercoordinates[]")return "51.1|-84.0|0.2";
  if(s.StartsWith("coordinatedistanceflat["))return "1";
  if(s.StartsWith("maraemapwalk[")){MeshRequests++;return MeshOk?"1":"0";}
  if(s is "maraenavsave[]" or "maraenavmark[]" or "maraenavrestore[]")return "1";
  foreach(var pair in new[]{("getvar[",Vars),("getpvar[",Pvars)})if(s.StartsWith(pair.Item1)&&s.EndsWith(']'))return pair.Item2.GetValueOrDefault(s[pair.Item1.Length..^1],"0");
  if(s.StartsWith("testpvar["))return Pvars.ContainsKey(s[9..^1])?"1":"0";
  foreach(var pair in new[]{("setvar[",Vars),("setpvar[",Pvars)})if(s.StartsWith(pair.Item1)&&s.EndsWith(']')){string[] kv=s[pair.Item1.Length..^1].Split(',',2);pair.Item2[kv[0]]=kv[1].Trim('`');return "1";}
  if(s.StartsWith("vtsetmetastate[")){State=s[15..^1];return "1";}
  if(s.StartsWith("if[getpvar[mh_pt_cache_pickup_held]")||s=="vtsetsetting[EnableMeta,1]")return "1";
  throw new Exception("Unexpected expression: "+s);
 }
}
