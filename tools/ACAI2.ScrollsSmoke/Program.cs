using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.PortableExecutable;
using System.Text.Json;
string path=Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving+=(_,n)=>{string candidate=Path.Combine(Path.GetDirectoryName(path)!,n.Name+".dll");return File.Exists(candidate)?AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate):null;};
using(var stream=File.OpenRead(path))using(var pe=new PEReader(stream))
{
    if(pe.PEHeaders.CoffHeader.Machine.ToString()!="I386"||(pe.PEHeaders.CorHeader!.Flags&CorFlags.Requires32Bit)==0)throw new Exception("Expected x86 managed app");
    if(pe.PEHeaders.PEHeader!.ResourceTableDirectory.Size==0)throw new Exception("Windows plugin manifest resource missing");
}
var asm=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
var type=asm.GetType("ACAI2.Rynth.Scrolls.Plugin.ScrollsPlugin",true)!;var plugin=Activator.CreateInstance(type)!;
if((uint)type.GetProperty("MinimumApiVersion")!.GetValue(plugin)!!=92)throw new Exception("Unexpected API floor");
var core=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(path)!,"ACAI2.Rynth.Scrolls.Core.dll"));
using(var s=core.GetManifestResourceStream("Scrolls.catalog.json")!)using(var doc=JsonDocument.Parse(s))
    if(doc.RootElement.GetProperty("Recipes").GetArrayLength()!=197)throw new Exception("Recipe payload missing");
// An unbound plugin must remain inert after logout. No native/game calls in smoke test.
type.GetMethod("OnLogout")!.Invoke(plugin,null);type.GetMethod("OnTick")!.Invoke(plugin,null);
Console.WriteLine("PASS: Windows x86 plugin manifest, managed load, embedded catalog and inert logout tick.");
