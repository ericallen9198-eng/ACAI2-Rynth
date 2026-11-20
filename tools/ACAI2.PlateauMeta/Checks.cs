using System.Text.RegularExpressions;
using RynthCore.Plugin.RynthAi.Meta;
using RynthCore.Plugin.RynthAi.LegacyUi;
static class Checks
{
 static int n;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;Console.WriteLine("PASS: "+name);}
 public static void Run(LoadedMeta meta,string folder)
 {
  foreach(var r in Flatten(meta.Rules))if(r.Condition is MetaConditionType.ChatMessage or MetaConditionType.ChatMessageCapture)_=new Regex(r.ConditionData,RegexOptions.None,TimeSpan.FromMilliseconds(100));
  Check(true,"all chat patterns compile in .NET");
  string[] keys=["zefir","ursuin","golem","gromnie","siraluun","carenzi","virindi","olthoi"];
  string[] names=["Hea Rukenua the Zefir Hunter","Hea Tamarau the Ursuin Hunter","Hea Koruani the Golem Hunter","Hea Weneria the Gromnie Hunter","Hea Taraneki the Siraluun Hunter","Hea Kurelan the Carenzi Hunter","Hea Ruatane the Virindi Hunter","Hea Varenkeh the Olthoi Hunter"];
  var sim=new Sim(meta.Rules);sim.ToWait();
  sim.Tick(0,names[0]+" tells you, \"My hunts suit a hunter of about level 275.\"");Check(sim.State=="VERIFY_ZEFIR_WAIT","introductory text does not count as an accepted task");
  sim.Tick(0,names[7]+" tells you, \"Slay 20 Olthoi Hive Guardian. Return when the work is done.\"");Check(sim.State=="VERIFY_ZEFIR_WAIT","another NPC's reply cannot advance the current hunter");
  string[] tasks=["Drumtaken Enku Zefir","Drumtaken Ursuin Slicer","Drumtaken Verdigris Golem","Drumtaken Brass Gromnie","Drumtaken Untamed Siraluun","Drumtaken Savage Carenzi","Virindi Sentry","Olthoi Hive Guardian"];
  for(int i=0;i<8;i++)
  {
   sim.ToWait();Check(sim.State=="VERIFY_"+keys[i].ToUpperInvariant()+"_WAIT","correct hunter order: "+keys[i]);
   string reply=i==6?"You have defeated 5 of 50 Virindi Sentry. The hunt is not yet finished.":i==7?"You have defeated 0 of 20 Olthoi Hive Guardian. The hunt is not yet finished.":"Slay 50 "+tasks[i]+". Return when the work is done.";
   sim.Tick(0,names[i]+" tells you, \""+reply+"\"");
   Check(sim.Get("pt_pickup_"+keys[i])=="1"&&sim.Get("pt_target_"+keys[i])==tasks[i],"actual assignment captured before leaving "+keys[i]);
   if(i<7)Check(!sim.Commands.Any(c=>c.Contains("PT Verify - "+System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(keys[i+1]))),"next NPC route is not submitted until its state is entered");
  }
  Check(sim.Get("pt_required_olthoi")=="20"&&sim.Get("pt_required_virindi")=="50","alternate Guardian/Sentry required counts are preserved");
  sim.Tick(0);Check(sim.Get("pt_npc_end")=="1"&&sim.State=="HUNT_INIT","only eight acknowledged hunters complete the NPC phase");
  foreach(string body in new[]{"Your hunt for today is done. You may take another hunt in 3 hours.","Return in 30 minutes for another hunt.","You have served the Hea well. Accept this reward for your efforts."})
  {var s=new Sim(meta.Rules);s.ToWait();s.Tick(0,names[0]+" tells you, \""+body+"\"");Check(s.State=="VERIFY_URSUIN"&&s.Get("pt_unavailable_zefir")=="1","cooldown/reward advances without inventing a new task: "+body);}
  var retry=new Sim(meta.Rules);retry.ToWait();
  for(int i=0;i<3;i++){retry.Tick(21);retry.Tick(0);if(i<2)retry.ToWait();}
  Check(retry.State=="DEATH_HOLD"&&retry.Get("pt_npc_attempt")=="3"&&!retry.Commands.Any(c=>c.Contains("PT Verify - Ursuin")),"three unanswered attempts hold at the same NPC, never skip ahead");
  var hunt=meta.Rules.Where(r=>r.State=="HUNT").ToList();
  Check(!Flatten(hunt).Any(r=>r.Action==MetaActionType.SetMetaState&&r.ActionData is "HUNT_INIT" or "TURNIN_START"),"task completion cannot transfer region or stop treasure hunting");
  Check(!Flatten(hunt).Any(r=>r.ConditionData.Contains("pt_last_progress")||r.ConditionData.Contains("pt_done_")),"empty, partial, full and cooldown task flags never gate the treasure loop");
  Check(!Flatten(meta.Rules).Any(r=>r.ConditionData.Contains("huntcount_plateau")),"no hardcoded Mutilator/Observer quest-flag completion remains");
  for(int i=0;i<8;i++)
  {var nav=NavRouteParser.Load(Path.Combine(folder,"PT Verify - "+System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(keys[i])+".nav"));Check(nav.RouteType==NavRouteType.Once&&nav.Points.Count==2&&nav.Points[0].Type==NavPointType.Point&&nav.Points[1].Type==NavPointType.Chat&&nav.Points[1].ChatCommand=="/ra usel "+names[i],"genuine nav parser verifies approach plus explicit NPC-use command: "+keys[i]);}
  var completion=meta.Rules.First(r=>r.State=="HUNT"&&r.Condition==MetaConditionType.All).Children[0];
  var m=Regex.Match("You have killed 20 Olthoi Hive Guardians! Your task is complete!",completion.ConditionData);
  Check(m.Success&&m.Groups["kills"].Value=="20"&&m.Groups["killed"].Value==tasks[7]+"s","actual Guardian completion can match the captured assignment");
  foreach(var group in meta.Rules.GroupBy(r=>r.State).Where(g=>g.Key is not ("HOLD" or "DEATH_HOLD")))
   Check(group.First().Condition==MetaConditionType.CharacterDeath,"death guard precedes actions in "+group.Key);
  var targets=Flatten(meta.Rules).Where(r=>r.Action==MetaActionType.SetMetaState).Select(r=>r.ActionData).Distinct();
  Check(targets.All(t=>meta.Rules.Any(r=>r.State==t)),"all state transitions have a declared destination");
  Check(Flatten(meta.Rules.Where(r=>r.State=="Default")).Any(r=>r.Action==MetaActionType.SetRAOption&&r.ActionData=="StopMacroOnDeath;True"),"native immediate stop on death remains enabled");
  Check(Flatten(meta.Rules.Where(r=>r.State=="DEATH_HOLD")).Any(r=>r.Action==MetaActionType.ChatCommand&&r.ActionData=="/ra stop"),"failure/death hold explicitly stops the macro");
  var getTo=NavRouteParser.Load(Path.Combine(folder,"275 - Get to.nav"));
  Check(getTo.Points[0].Type==NavPointType.Chat&&getTo.Points[0].ChatCommand=="/ls"&&getTo.Points[1].Type==NavPointType.Pause&&getTo.Points[1].PauseTimeMs==10000,"startup travel uses /ls and retains its ten-second wait");
  Check(Directory.GetFiles(folder,"*.nav").SelectMany(f=>NavRouteParser.Load(f).Points).All(p=>p.Type!=NavPointType.Recall||p.SpellId!=1636),"no Lifestone Sending recall step remains in any nav");
  Check(!Flatten(meta.Rules).Any(r=>r.Action==MetaActionType.ChatCommand&&r.ActionData=="/ra start"),"there is no automatic macro restart after a death/failure hold");
  var encounterXp=meta.Rules.Where(r=>r.State=="ENCOUNTER"&&Flatten([r]).Any(c=>c.ConditionData.Contains("You've earned"))).ToArray();
  Check(encounterXp.Length==1&&encounterXp[0].Condition==MetaConditionType.All&&encounterXp[0].Children.Any(c=>c.ConditionData.Contains("Unearthed Cache")),"ordinary XP cannot trigger cache pickup without a visible cache");
  Console.WriteLine($"{n} offline checks passed. Native/game commands were recorded only, never executed. Simulator is not a live Rynth frame.");
 }
 static IEnumerable<MetaRule> Flatten(IEnumerable<MetaRule> rules){foreach(var r in rules){yield return r;foreach(var c in Flatten(r.Children.Concat(r.ActionChildren)))yield return c;}}
 sealed class Sim(List<MetaRule> rules)
 {
  public string State="NPC_TALK";readonly Dictionary<string,string> vars=new();readonly HashSet<MetaRule> fired=[];double age;public List<string> Commands=[];
  public string Get(string key)=>vars.GetValueOrDefault(key,"0");
  public void ToWait(){for(int i=0;i<10&&!State.EndsWith("_WAIT");i++)Tick(0);if(!State.EndsWith("_WAIT"))throw new Exception("did not reach wait: "+State);}
  public void Tick(double seconds,string? chat=null)
  {age+=seconds;string start=State;foreach(var r in rules.Where(x=>x.State==start)){if(fired.Contains(r)||!Cond(r,chat))continue;fired.Add(r);Act(r);if(State!=start){age=0;fired.Clear();break;}}}
  bool Cond(MetaRule r,string? chat)
  {
   if(r.Condition==MetaConditionType.Always)return true;if(r.Condition==MetaConditionType.CharacterDeath)return false;
   if(r.Condition==MetaConditionType.SecondsInState_GE)return age>=double.Parse(r.ConditionData);
   if(r.Condition==MetaConditionType.Expression&&r.ConditionData=="getvar[pt_npc_attempt]<3")return int.Parse(Get("pt_npc_attempt"))<3;
   if(r.Condition is MetaConditionType.ChatMessage or MetaConditionType.ChatMessageCapture)
   {if(chat==null)return false;var m=Regex.Match(chat,r.ConditionData);if(!m.Success)return false;if(r.Condition==MetaConditionType.ChatMessageCapture)foreach(string g in m.Groups.Keys)vars["capturegroup_"+g]=m.Groups[g].Value;return true;}
   throw new Exception("unsupported simulated condition: "+r.Condition+" "+r.ConditionData);
  }
  void Act(MetaRule r)
  {
   if(r.Action==MetaActionType.All){foreach(var a in r.ActionChildren)Act(a);return;}
   if(r.Action==MetaActionType.SetMetaState){State=r.ActionData;return;}
   if(r.Action==MetaActionType.ChatCommand){Commands.Add(r.ActionData);return;}
   if(r.Action==MetaActionType.SetRAOption)return;
   if(r.Action==MetaActionType.ExpressionAction)
   {if(r.ActionData.StartsWith("echo["))return;var m=Regex.Match(r.ActionData,@"^setvar\[(?<key>[^,]+),(?<value>.*)\]$");if(!m.Success)throw new Exception(r.ActionData);string v=m.Groups["value"].Value;if(v.StartsWith('`')&&v.EndsWith('`'))v=v[1..^1];else if(v.StartsWith("getvar[")){bool plus=v.EndsWith("+1");string key=v[7..v.IndexOf(']')];v=plus?(int.Parse(Get(key))+1).ToString():Get(key);}vars[m.Groups["key"].Value]=v;return;}
   throw new Exception("unsupported simulated action "+r.Action);
  }
 }
}
