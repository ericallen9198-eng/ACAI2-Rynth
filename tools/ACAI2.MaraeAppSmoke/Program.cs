using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.PortableExecutable;
using System.Text.Json;
const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
string path=Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving+=(_,n)=>{string candidate=Path.Combine(Path.GetDirectoryName(path)!,n.Name+".dll");return File.Exists(candidate)?AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate):null;};
using(var stream=File.OpenRead(path))using(var pe=new PEReader(stream))
 if(pe.PEHeaders.CoffHeader.Machine.ToString()!="I386"||(pe.PEHeaders.CorHeader!.Flags&CorFlags.Requires32Bit)==0)throw new Exception("Expected managed x86 app.");
var asm=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
foreach(string name in new[]{"Marae.hunters.json","Marae.workflow.af","Marae.keys.json"})if(!asm.GetManifestResourceNames().Contains(name))throw new Exception("Missing embedded app resource: "+name);
var type=asm.GetType("ACAI2.Rynth.Marae.Plugin.MaraePlugin",true)!;
var plugin=Activator.CreateInstance(type)!;
if((uint)type.GetProperty("MinimumApiVersion")!.GetValue(plugin)!!=92)throw new Exception("Unexpected app API floor.");
var hunters=(Array)type.GetField("Hunters",F)!.GetValue(null)!;
if(hunters.Length!=27)throw new Exception("Bundled NPC metadata did not initialize.");
using(var s=asm.GetManifestResourceStream("Marae.workflow.af")!)using(var r=new StreamReader(s))
{
 string af=r.ReadToEnd();if(af.Contains("maraemissionapi[")||af.Contains("maraecachepickup[")||af.Contains("maraenavrestore[")||af.Contains("#rynth-talk:"))throw new Exception("Bundled AF still requires patched DLL.");
}
var adapter=asm.GetType("ACAI2.Rynth.Marae.Plugin.NativeMaraeBot",true)!;var allowed=adapter.GetMethod("Allowed",F)!;
bool Allowed(string s)=>(bool)allowed.Invoke(null,new object[]{s})!;
if(!Allowed("/ra meta load Marae Lassel - Embedded.af")||!Allowed("/ra nav load ACAI2-Marae\\ABC_resume.nav")||Allowed("/ls")||Allowed("/ra cast 1")||Allowed("/ra nav load ACAI2-Marae\\..\\foreign.nav"))throw new Exception("App command catalog mismatch.");
// Lifecycle callback and post-logout tick must not dereference the uninitialized host or emit actions.
type.GetMethod("OnLogout")!.Invoke(plugin,null);type.GetMethod("OnTick")!.Invoke(plugin,null);
Console.WriteLine("PASS: actual Windows x86 app loads, initializes bundled NPC/workflow resources, rejects foreign commands, and performs an inert post-logout tick. No game/native actions invoked.");
