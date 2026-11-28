using System.Runtime.InteropServices;
using System.Text;
using RynthCore.Plugin.Shared;
using RynthCore.PluginSdk;

namespace ACAI2.Rynth.Terrain.Plugin;
internal sealed unsafe class NativeBot(RynthCoreHost host)
{
    private RynthAiScriptApiV1* Table()
    {
        var t=(RynthAiScriptApiV1*)host.GetPluginInterface("RynthAi",RynthAiScriptApiV1.InterfaceName,1);
        if(t==null||t->Version<1||t->StructSize<sizeof(RynthAiScriptApiV1)||t->Evaluate==null||t->GetSetting==null||t->SubmitCommand==null||t->IsMacroRunning==null)
            throw new InvalidOperationException("RynthAi Script interface unavailable");
        return t;
    }
    public string Eval(string expression)
    {
        if(expression.Length>12288)throw new InvalidDataException("Expression too large");
        byte[] data=Encoding.UTF8.GetBytes(expression+'\0');byte* result=null;var t=Table();
        fixed(byte* b=data)if(t->Evaluate(b,&result)!=1)throw new InvalidOperationException("RynthAi expression refused");
        return Marshal.PtrToStringUTF8((IntPtr)result)??"";
    }
    public string Setting(string name)
    {
        byte[] data=Encoding.UTF8.GetBytes(name+'\0');byte* result=null;var t=Table();
        fixed(byte* b=data)if(t->GetSetting(b,&result)!=1)return "";
        return Marshal.PtrToStringUTF8((IntPtr)result)??"";
    }
    public bool Running=>Table()->IsMacroRunning()!=0;
    public void Load(string relative)
    {
        if(!relative.StartsWith("ACAI2-Terrain\\",StringComparison.Ordinal)||relative.Length>100||relative.Contains("..")||relative.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '\\' or '_' or '.' or '-')))
            throw new InvalidDataException("Foreign route refused");
        byte[] data=Encoding.UTF8.GetBytes("/ra nav load "+relative+'\0');var t=Table();
        fixed(byte* b=data)if(t->SubmitCommand(b)!=1)throw new InvalidOperationException("Route load refused");
    }
}
