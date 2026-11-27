using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Runtime.InteropServices;
unsafe
{
    string path=Path.GetFullPath(args[0]);
    AssemblyLoadContext.Default.Resolving+=(_,n)=>{string candidate=Path.Combine(Path.GetDirectoryName(path)!,n.Name+".dll");return File.Exists(candidate)?AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate):null;};
    using(var stream=File.OpenRead(path))using(var pe=new PEReader(stream))
        if(pe.PEHeaders.CoffHeader.Machine.ToString()!="I386"||(pe.PEHeaders.CorHeader!.Flags&CorFlags.Requires32Bit)==0||pe.PEHeaders.PEHeader!.ResourceTableDirectory.Size==0)throw new Exception("Windows x86 app/manifest required");
    var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    var exports=assembly.GetTypes().Single(t=>t.Name=="PluginExports");
    if(exports.GetMethod("Sections")!.GetCustomAttribute<UnmanagedCallersOnlyAttribute>()?.EntryPoint!="RynthPluginGetSectionsJson"||exports.GetMethod("Embed")!.GetCustomAttribute<UnmanagedCallersOnlyAttribute>()?.EntryPoint!="RynthPluginRenderEmbed")throw new Exception("Incorrect engine export names");
    var manifest=(delegate* unmanaged[Cdecl]<IntPtr>)exports.GetMethod("Sections")!.MethodHandle.GetFunctionPointer();
    using var doc=JsonDocument.Parse(Marshal.PtrToStringUTF8(manifest())!);
    var window=doc.RootElement.GetProperty("windows").EnumerateArray().Single();
    if(!window.GetProperty("bar").GetBoolean()||window.GetProperty("icon").GetString()!.Length!=1)throw new Exception("Missing native bar icon");
    if(window.GetProperty("title").GetString()!=args[1])throw new Exception("Unexpected panel key");
    var render=(delegate* unmanaged[Cdecl]<byte*,float,float,float*,float*,int>)exports.GetMethod("Embed")!.MethodHandle.GetFunctionPointer();
    byte[] surface=System.Text.Encoding.UTF8.GetBytes("window:unknown\0");float w=123,h=456;
    fixed(byte* s=surface)if(render(s,400,300,&w,&h)!=0||w!=0||h!=0)throw new Exception("Uninitialized/unknown embed must stay inert");
    Console.WriteLine("PASS: "+args[1]+" — genuine Windows DLL loads; UI manifest and Cdecl embed exports work without game actions.");
}
