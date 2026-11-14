using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RynthCore.PluginCore;
using RynthCore.PluginSdk;

namespace ACAI2.Rynth.Combat.Plugin;

// Public RynthCore C ABI. Same exports can be discovered by the CoreCLR engine.
public static unsafe class PluginExports
{
    private static readonly RynthPluginRuntime<CombatPlugin> Runtime = new();
    private static readonly IntPtr Name = Marshal.StringToHGlobalAnsi("ACAI2.Combat");
    private static readonly IntPtr Version = Marshal.StringToHGlobalAnsi(CombatPlugin.Version);
    [UnmanagedCallersOnly(EntryPoint="RynthPluginInit",CallConvs=[typeof(CallConvCdecl)])]
    public static int Init(RynthCoreApiNative* api) { try { return Runtime.Init(api); } catch { return 1; } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginShutdown",CallConvs=[typeof(CallConvCdecl)])]
    public static void Shutdown() { try { Runtime.Shutdown(); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginName",CallConvs=[typeof(CallConvCdecl)])]
    public static IntPtr GetName() => Name;
    [UnmanagedCallersOnly(EntryPoint="RynthPluginVersion",CallConvs=[typeof(CallConvCdecl)])]
    public static IntPtr GetVersion() => Version;
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnLoginComplete",CallConvs=[typeof(CallConvCdecl)])]
    public static void Login() { try { Runtime.OnLoginComplete(); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnLogout",CallConvs=[typeof(CallConvCdecl)])]
    public static void Logout() { try { Runtime.OnLogout(); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginTick",CallConvs=[typeof(CallConvCdecl)])]
    public static void Tick() { try { Runtime.OnTick(); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginRender",CallConvs=[typeof(CallConvCdecl)])]
    public static void Render() { try { Runtime.OnRender(); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginRenderOverlay",CallConvs=[typeof(CallConvCdecl)])]
    public static void Overlay() { try { Runtime.OnRender(); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnChatWindowText",CallConvs=[typeof(CallConvCdecl)])]
    public static void Chat(IntPtr text,int type,IntPtr eat) { try { Runtime.OnChatWindowText(text,type,eat); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnChatBarEnter",CallConvs=[typeof(CallConvCdecl)])]
    public static void Command(IntPtr text,IntPtr eat) { try { Runtime.OnChatBarEnter(text,eat); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnCreateObject",CallConvs=[typeof(CallConvCdecl)])]
    public static void Create(uint id) { try { Runtime.OnCreateObject(id); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnDeleteObject",CallConvs=[typeof(CallConvCdecl)])]
    public static void Delete(uint id) { try { Runtime.OnDeleteObject(id); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnUpdateObject",CallConvs=[typeof(CallConvCdecl)])]
    public static void Update(uint id) { try { Runtime.OnUpdateObject(id); } catch { } }
    [UnmanagedCallersOnly(EntryPoint="RynthPluginOnUpdateObjectInventory",CallConvs=[typeof(CallConvCdecl)])]
    public static void Inventory(uint id) { try { Runtime.OnUpdateObjectInventory(id); } catch { } }
}
