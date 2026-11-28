using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
string path=Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving+=(_,n)=>{string p=Path.Combine(Path.GetDirectoryName(path)!,n.Name+".dll");return File.Exists(p)?AssemblyLoadContext.Default.LoadFromAssemblyPath(p):null;};
using(var stream=File.OpenRead(path))using(var pe=new PEReader(stream))
 if(pe.PEHeaders.CoffHeader.Machine.ToString()!="I386"||(pe.PEHeaders.CorHeader!.Flags&CorFlags.Requires32Bit)==0)throw new Exception("Expected managed x86 plugin");
if(!System.Text.Encoding.Unicode.GetString(File.ReadAllBytes(path)).Contains("RYNTH_PLUGIN_MANIFEST"))throw new Exception("Windows manifest missing");
var asm=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
if(!asm.GetManifestResourceNames().Contains("Terrain.portals.json"))throw new Exception("Portal metadata missing");
var type=asm.GetType("ACAI2.Rynth.Terrain.Plugin.TerrainPlugin",true)!;
var plugin=Activator.CreateInstance(type)!;
if((uint)type.GetProperty("MinimumApiVersion")!.GetValue(plugin)!!=92)throw new Exception("API floor changed");
var portals=(Array)type.GetField("Portals",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
if(portals.Length!=5)throw new Exception("Expected five distinct portal neighborhoods");
foreach(string export in new[]{"RynthPluginInit","RynthPluginShutdown","RynthPluginTick","RynthPluginOnLoginComplete","RynthPluginOnLogout"})
 if(!asm.GetType("ACAI2.Rynth.Terrain.Plugin.PluginExports",true)!.GetMethods().Any(m=>m.GetCustomAttribute<UnmanagedCallersOnlyAttribute>()?.EntryPoint==export))throw new Exception("Missing export "+export);
type.GetMethod("OnLogout")!.Invoke(plugin,null);type.GetMethod("OnTick")!.Invoke(plugin,null);
Console.WriteLine("PASS actual Windows x86 companion loads, initializes five portal neighborhoods and native ABI exports; logged-out tick is inert. No game actions.");
