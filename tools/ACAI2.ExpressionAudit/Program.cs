using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
string dll=Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving+=(_,n)=>{string p=Path.Combine(Path.GetDirectoryName(dll)!,n.Name+".dll");return File.Exists(p)?AssemblyLoadContext.Default.LoadFromAssemblyPath(p):null;};
var asm=AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
var engineType=asm.GetType("RynthCore.Plugin.RynthAi.Meta.ExpressionEngine",true)!;
var engine=Activator.CreateInstance(engineType,F,null,new object?[]{null},null)!;
var parserType=engineType.GetNestedType("Parser",F)!;
var split=engineType.GetMethod("SplitArgs",F)!;
var strip=engineType.GetMethod("StripBackticks",F)!;
string source=File.ReadAllText(args[2]);
var known=Regex.Matches(source,"\"([a-z0-9_]+)\"\\s*=>").Select(m=>m.Groups[1].Value).ToHashSet();known.UnionWith(new[]{"if","iif","ifthen"});
known.UnionWith(Regex.Matches(source,"\"([a-z0-9_]+)\"(?=\\s*or)").Select(m=>m.Groups[1].Value));
var texts=new Dictionary<string,HashSet<int>>();var templates=new Dictionary<string,HashSet<int>>();
foreach(Match m in Regex.Matches(source,"\"([a-z0-9_]+)\"\\s*=>[^\\r\\n]+")) {
 texts[m.Groups[1].Value]=Regex.Matches(m.Value,@"Text\((\d+)\)").Select(x=>int.Parse(x.Groups[1].Value)).ToHashSet();
 templates[m.Groups[1].Value]=Regex.Matches(m.Value,@"Tmpl\((\d+)\)").Select(x=>int.Parse(x.Groups[1].Value)).ToHashSet();
}
int count=0;var failures=new HashSet<string>();
void Audit(string input,string where) {
 string s=input.Trim();if(s.Length==0)return;
 try {
  var p=Activator.CreateInstance(parserType,F,null,new[]{engine,(object)s},null)!;
  parserType.GetField("_skip",F)!.SetValue(p,1); // No functions, assignments, game actions or file writes.
  parserType.GetMethod("Run",F)!.Invoke(p,null);
  int consumed=(int)parserType.GetField("_p",F)!.GetValue(p)!;
  if(consumed!=s.Length)throw new Exception("unconsumed suffix: "+s[consumed..]);
  count++;
  for(int i=0;i<s.Length;i++) {
   if(s[i]=='\\'){i++;continue;}
   if(s[i]=='`'){bool closed=false;while(++i<s.Length){if(s[i]=='\\'){i++;continue;}if(s[i]=='`'){closed=true;break;}}if(!closed)throw new Exception("Unclosed quote");continue;}
   if(!(char.IsLetter(s[i])||s[i]=='_'))continue;
   int start=i;while(i<s.Length&&(char.IsLetterOrDigit(s[i])||s[i]=='_'))i++;
   string name=s[start..i].ToLowerInvariant();if(i==s.Length||s[i]!='['){i--;continue;}
   int open=i,depth=1;bool quote=false;
   while(++i<s.Length){if(s[i]=='\\'){i++;continue;}if(s[i]=='`'){quote=!quote;continue;}if(!quote){if(s[i]=='[')depth++;if(s[i]==']')depth--; }if(depth==0)break;}
   if(depth!=0)throw new Exception("Unclosed call");
   if(!known.Contains(name))throw new Exception("Unknown function "+name);
   var parts=(List<string>)split.Invoke(null,new object[]{s[(open+1)..i]})!;
   for(int a=0;a<parts.Count;a++) {
    string part=parts[a].Trim();
    if(templates.TryGetValue(name,out var ts)&&ts.Contains(a))part=(string)strip.Invoke(null,new object[]{part})!;
    else if(texts.TryGetValue(name,out var tx)&&tx.Contains(a)&&part.Length>0&&part[0] is not ('`' or '$' or '@' or '&')&&!part.Contains('\\')&&!Regex.IsMatch(part,@"^\w+\["))continue;
    Audit(part,where+"/"+name+"["+a+"]");
   }
  }
 }catch(Exception ex){failures.Add(where+": "+(ex.InnerException??ex).Message+" :: "+s);}
}
using var doc=JsonDocument.Parse(File.ReadAllText(args[1]));
void Rule(JsonElement r) {
 string state=r.GetProperty("State").GetString()!;
 if(r.GetProperty("Condition").GetInt32()==26)Audit(r.GetProperty("ConditionData").GetString()!,state+" condition");
 if(r.GetProperty("Action").GetInt32() is 7 or 8)Audit(r.GetProperty("ActionData").GetString()!,state+" action");
 foreach(string key in new[]{"Children","ActionChildren"})foreach(var c in r.GetProperty(key).EnumerateArray())Rule(c);
}
foreach(var r in doc.RootElement.EnumerateArray())Rule(r);
if(args.Length>3)foreach(string s in JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[3]))!)Audit(s,"Lua controller");
foreach(string f in failures)Console.Error.WriteLine(f);
if(failures.Count>0)throw new Exception($"{failures.Count} parser audit failures");
Console.WriteLine($"PASS: {count} actual-engine expression, argument and quoted-template syntax checks with execution disabled.");
var pv=new Dictionary<string,string>();engineType.GetField("_pvars",F)!.SetValue(engine,pv);
var vars=(Dictionary<string,string>)engineType.GetField("_variables",F)!.GetValue(engine)!;
string Eval(string s)=>(string)engineType.GetMethod("Evaluate",F)!.Invoke(engine,new object[]{s})!;
bool genericMapHunt=doc.RootElement.EnumerateArray().Any(r=>r.GetProperty("State").GetString()=="HUNT_VERIFY");
if(!genericMapHunt) foreach(int z in new[]{180,225,275}) {
 vars[$"ml_area_{z}"]="1";vars[$"ml_hunt_only_{z}"]="0";pv["mm_camp_limit"]="1";
 string expression=doc.RootElement.EnumerateArray().First(r=>r.GetProperty("State").GetString()=="DISPATCH"&&r.GetProperty("ConditionData").GetString()!.Contains($"getpvar[mm_attempted_{z}]")).GetProperty("ConditionData").GetString()!;
 if(Eval(expression)!="1")throw new Exception("Valid task selection failed "+z+": "+Eval(expression));
 pv[$"mm_timed_{z}"]="1";if(Eval(expression)!="0")throw new Exception("Expired task camp selected");
 pv["mm_camp_limit"]="0";if(Eval(expression)!="1")throw new Exception("Disabled timer blocks task");pv[$"mm_timed_{z}"]="0";
}
if(Eval("listfilter[listcreate[1,2],`listcontains[listcreate[1],$1]==0`]")!="[2]")throw new Exception("Quoted cache exclusion failed");
if(Eval("maraecachepickup[1]")!="0")throw new Exception("Unbound cache pickup was not refused");
Console.WriteLine("PASS: actual-engine cache exclusion and hostless pickup refusal; in-memory fixtures only.");

foreach(var tuple in new[]{("FAR_EAST","mm_mesh_far",1,60),("FAR_EAST","mm_mesh_far",999,180),("CLOSE_EAST","mm_mesh_close",1,10),("CLOSE_EAST","mm_mesh_close",999,60)}) {
 string Find(JsonElement r) {
  if(r.GetProperty("Action").GetInt32()==7&&r.GetProperty("ActionData").GetString()!.StartsWith("setvar[tm_mesh_step,"))return r.GetProperty("ActionData").GetString()!;
  foreach(var c in r.GetProperty("ActionChildren").EnumerateArray()){var found=Find(c);if(found!="")return found;}return "";
 }
 string step=doc.RootElement.EnumerateArray().Where(r=>r.GetProperty("State").GetString()==tuple.Item1).Select(Find).First(s=>s!="");
 string key=genericMapHunt?(tuple.Item2=="mm_mesh_far"?"maphunt_far":"maphunt_close"):tuple.Item2;
 pv[key]=tuple.Item3.ToString();Eval(step);
 if(vars["tm_mesh_step"]!=tuple.Item4.ToString())throw new Exception("Actual-engine travel clamp failed");
}
Console.WriteLine("PASS: actual-engine travel range boundaries and hostless currency-action refusal.");

if(genericMapHunt) {
 pv["meshnav"]="1";
 Eval("if[testpvar[meshnav]==0,setpvar[meshnav,0]]");
 if(pv["meshnav"]!="1")throw new Exception("MapHunt startup overwrote saved mesh preference");
 foreach(var pair in new[]{("FAR_NORTH","maphunt_far",130),("CLOSE_NORTH","maphunt_close",30)}) {
  pv[pair.Item2]=pair.Item3.ToString();
  string FindStep(JsonElement r) {
   if(r.GetProperty("Action").GetInt32()==7&&r.GetProperty("ActionData").GetString()!.StartsWith("setvar[tm_mesh_step,"))return r.GetProperty("ActionData").GetString()!;
   foreach(var c in r.GetProperty("ActionChildren").EnumerateArray()){var f=FindStep(c);if(f!="")return f;}return "";
  }
  var step=doc.RootElement.EnumerateArray().Where(r=>r.GetProperty("State").GetString()==pair.Item1).Select(FindStep).First(s=>s!="");Eval(step);
  if(vars["tm_mesh_step"]!=pair.Item3.ToString())throw new Exception("MapHunt 30/130 stride was changed");
 }
 Console.WriteLine("PASS: actual MapHunt engine pvar preservation and exact 30/130-yard steps.");
}
