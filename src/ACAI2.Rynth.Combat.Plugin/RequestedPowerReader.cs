using System.Diagnostics;
using System.Runtime.InteropServices;
namespace ACAI2.Rynth.Combat.Plugin;

/// <summary>Read-only retail x86 ClientCombatSystem binding, sampled on the pump.
/// Published layout used by our Decal reader and RynthCore's ClientCombatHooks.
/// No function calls into the game, hooks, memory writes, power changes or attacks.</summary>
internal sealed class RequestedPowerReader
{
    private IntPtr image,global;
    private int imageSize;
    private bool probed;
    public string Status {get;private set;}="Power read not sampled";
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] buffer,nuint count,out nuint read);
    private static byte[]? Read(IntPtr address,int count)
    {
        if(address==IntPtr.Zero)return null;byte[] bytes=new byte[count];
        return ReadProcessMemory(new IntPtr(-1),address,bytes,(nuint)count,out nuint n)&&n==(nuint)count?bytes:null;
    }
    private bool Inside(IntPtr pointer,int count)=>pointer.ToInt64()>=image.ToInt64()&&pointer.ToInt64()-image.ToInt64()+count<=imageSize;
    private bool Probe()
    {
        probed=true;Status="Power unavailable: unsupported client layout";
        if(!OperatingSystem.IsWindows()||IntPtr.Size!=4)return false;
        using var process=Process.GetCurrentProcess();var module=process.MainModule;
        if(module==null||!module.ModuleName.Equals("acclient.exe",StringComparison.OrdinalIgnoreCase))return false;
        image=module.BaseAddress;imageSize=module.ModuleMemorySize;
        const int getter=0x16B210;
        foreach(int site in new[]{0xAB8D2,0xCD05B})
        {
            if(!Inside(IntPtr.Add(image,site),5))return false;
            var call=Read(IntPtr.Add(image,site),5);
            if(call==null||call[0]!=0xE8||(long)site+5+BitConverter.ToInt32(call,1)!=getter)return false;
        }
        if(!Inside(IntPtr.Add(image,getter),6))return false;
        var code=Read(IntPtr.Add(image,getter),6);
        // mov eax,[absolute global]; ret. Decode the pointer without executing the getter.
        if(code==null||code[0]!=0xA1||code[5]!=0xC3)return false;
        global=new IntPtr(BitConverter.ToInt32(code,1));if(!Inside(global,4)){global=IntPtr.Zero;return false;}
        Status="Requested attack power: validated read-only client state";return true;
    }
    public float? Sample(int mode)
    {
        try
        {
            if(!probed&&!Probe()||global==IntPtr.Zero)return null;
            if(mode is not (2 or 4)){Status="Power unavailable outside melee/missile mode";return null;}
            var ptr=Read(global,4);if(ptr==null)return null;var state=new IntPtr(BitConverter.ToInt32(ptr,0));
            var bytes=Read(state,0x50);if(bytes==null)return null;
            var vtable=new IntPtr(BitConverter.ToInt32(bytes,0));if(!Inside(vtable,4))return null;
            int actual=BitConverter.ToInt32(bytes,0x1C),pending=BitConverter.ToInt32(bytes,0x20),height=BitConverter.ToInt32(bytes,0x24);
            float power=BitConverter.ToSingle(bytes,0x4C);
            if(actual!=mode||pending!=0&&pending!=mode||height is <1 or >3||!float.IsFinite(power)||power is <0 or >1)return null;
            Status="Requested attack power: validated read-only client state";return power*100;
        }
        catch(Exception ex){Status="Power unavailable: "+ex.GetType().Name;return null;}
    }
}
