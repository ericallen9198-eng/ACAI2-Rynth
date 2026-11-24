using System.Runtime.InteropServices;
using System.Text;
using ACAI2.Rynth.MapHunt.Core;
using RynthCore.PluginSdk;
namespace ACAI2.Rynth.MapHunt.Plugin;

// The published RynthAi.Script v1 append-only ABI: uint version/size, then 12 Cdecl pointers.
// Offsets match the genuine RynthSuite Plugins/Shared/RynthAiScriptApi.cs (MIT).
internal sealed unsafe class NativeMapHuntBot(RynthCoreHost host):IMapHuntBot
{
    private byte* Table()
    {
        IntPtr p=host.GetPluginInterface("RynthAi","RynthAi.Script",1);
        if(p==IntPtr.Zero)throw new InvalidOperationException("Enable RynthAi first; its Script interface is unavailable.");
        uint* h=(uint*)p;
        if(h[0]!=1||h[1]<8+12*IntPtr.Size)throw new InvalidOperationException("RynthAi Script interface layout is unsupported.");
        return (byte*)p;
    }
    private IntPtr Function(int slot)
    {
        IntPtr p=*(IntPtr*)(Table()+8+slot*IntPtr.Size);
        if(p==IntPtr.Zero)throw new InvalidOperationException("Required RynthAi Script function is unavailable.");return p;
    }
    public string Evaluate(string expression)
    {
        if(expression.Length>4096)throw new InvalidOperationException("MapHunt expression exceeds its bound.");
        byte[] bytes=Encoding.UTF8.GetBytes(expression+'\0');byte* result=null;
        var f=(delegate* unmanaged[Cdecl]<byte*,byte**,int>)Function(0);
        fixed(byte* p=bytes){if(f(p,&result)!=1)throw new InvalidOperationException("RynthAi expression refused: "+(Marshal.PtrToStringUTF8((IntPtr)result)??"unknown"));}
        return Marshal.PtrToStringUTF8((IntPtr)result)??"";
    }
    public string? Setting(string name)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(name+'\0');byte* result=null;
        var f=(delegate* unmanaged[Cdecl]<byte*,byte**,int>)Function(1);
        fixed(byte* p=bytes){return f(p,&result)==1?Marshal.PtrToStringUTF8((IntPtr)result):null;}
    }
    public bool Running=>((delegate* unmanaged[Cdecl]<int>)Function(4))()!=0;
    public void Command(string command)
    {
        if(!Allowed(command))throw new InvalidOperationException("MapHunt refused a command outside its fixed catalog.");
        byte[] bytes=Encoding.UTF8.GetBytes(command+'\0');var f=(delegate* unmanaged[Cdecl]<byte*,int>)Function(11);
        fixed(byte* p=bytes){if(f(p)!=1)throw new InvalidOperationException("RynthAi did not accept the MapHunt command.");}
    }
    private static bool Allowed(string s)=>s is "/ra stop" or "/ra start" or "/ra meta load MapHunt App.af" or "/ra nav load maphunt.nav" or "/ub clearmotion" or "/ub setmotion Forward 1" ||
        s.StartsWith("/ub face ",StringComparison.Ordinal)&&double.TryParse(s[9..],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double n)&&double.IsFinite(n)&&n>=0&&n<360;
    public bool GoTo(double north,double east)
    {
        if(!double.IsFinite(north)||!double.IsFinite(east)||Math.Abs(north)>103||Math.Abs(east)>103)return false;
        return ((delegate* unmanaged[Cdecl]<double,double,int>)Function(6))(north,east)==1;
    }
}
