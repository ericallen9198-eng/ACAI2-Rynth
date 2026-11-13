using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using ACAI2.Rynth.Core;
using ACAI2.Rynth.Core.Keyfer;

internal static class IsolatedBridgeChecks
{
    internal static async Task Run(Action<bool,string> check)
    {
        var context=new EngineLikeContext();
        try
        {
            var assembly=context.LoadFromAssemblyPath(typeof(IsolatedBridgeProbe).Assembly.Location);
            var method=assembly.GetType(typeof(IsolatedBridgeProbe).FullName!)!.GetMethod(nameof(IsolatedBridgeProbe.Run))!;
            try {await (Task)method.Invoke(null,null)!;}
            catch(TargetInvocationException ex) when(ex.InnerException!=null){throw ex.InnerException;}
            check(true,"Bridge registration and tell delivery work with Rynth-style private JSON assemblies and shared HTTP framework");
        }
        finally{context.Unload();}
    }
    // Mirrors the reviewed engine's ManagedPlugins.PluginLoadContext dependency policy.
    private sealed class EngineLikeContext() : AssemblyLoadContext("ACAI2 isolated Bridge check",isCollectible:true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if(name.Name is "System.Text.Json" or "System.Text.Encodings.Web" or "System.IO.Pipelines")
            {
                string path=Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,name.Name+".dll");
                return File.Exists(path)?LoadFromAssemblyPath(path):null;
            }
            if(name.Name?.StartsWith("ACAI2.",StringComparison.Ordinal)==true)
            {
                string path=Path.Combine(Path.GetDirectoryName(typeof(IsolatedBridgeProbe).Assembly.Location)!,name.Name+".dll");
                if(File.Exists(path))return LoadFromAssemblyPath(path);
            }
            return null;
        }
    }
}
public static class IsolatedBridgeProbe
{
    public static async Task Run()
    {
        using var lease=new SessionLease(new("DreamWeave","Keyfer Sutherland",1),DateTime.UtcNow);
        var frame=new TelemetryFrame(lease,new(lease.InstanceId,lease.Identity,lease.StartedUtc,DateTime.UtcNow,0,0,0,[],[]));
        var handler=new AckHandler();
        using var bridge=new ReadOnlyBridgeClient(new(){BridgeEnabled=true},handler);
        if(!await bridge.SendAsync(frame)||handler.Calls!=3)throw new Exception("Isolated Bridge probe did not finish all registration routes.");
        string root=Path.Combine(Path.GetTempPath(),"keyfer-isolated-"+Guid.NewGuid().ToString("N"));
        KeyferSession? keyfer=null;
        try
        {
            keyfer=new(new(){keyferEnabled=true,keyferWorldEvents=false,treasureEnabled=false,DataRoot=root,HandlerFactory=()=>new AckHandler()},lease);
            for(int i=0;!keyfer.Ready&&i<300;i++)await Task.Delay(10);
            if(!keyfer.Ready)throw new Exception("Isolated Keyfer failed to register: "+keyfer.Status);
            keyfer.Capture("Samm tells you, \"isolated hello\"",3,DateTime.UtcNow);
            var sent=new List<string>();
            for(int i=0;sent.Count==0&&i<300;i++){keyfer.Pump(sent.Add,_=>{});await Task.Delay(10);}
            if(!sent.SequenceEqual(new[]{"/t Samm, isolated answer"}))throw new Exception("Isolated Keyfer did not deliver the expected private answer.");
        }
        finally
        {
            keyfer?.Dispose();if(keyfer!=null)await keyfer.Shutdown;
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
    private sealed class AckHandler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/ask")?"{\"Ok\":true,\"Answer\":\"isolated answer\"}":request.RequestUri.AbsolutePath.EndsWith("/leaderboards/poll")?"null":"{\"Ok\":true}")});
        }
    }
}
