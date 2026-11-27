using RynthCore.PluginSdk;
namespace ACAI2.Rynth.Scrolls.Plugin;

// Stock, append-only RynthAi interfaces, invoked exclusively by the game pump.
internal sealed unsafe class CraftGuard(RynthCoreHost host)
{
    private bool owned,active;
    private byte* Table(string name,int pointers)
    {
        IntPtr p=host.GetPluginInterface("RynthAi",name,1);
        if(p==IntPtr.Zero||((uint*)p)[0]!=1||((uint*)p)[1]<8+pointers*IntPtr.Size)throw new InvalidOperationException("Enable/update RynthAi: "+name+" v1 is required.");
        return (byte*)p;
    }
    private IntPtr Fn(byte* p,int slot)=>*(IntPtr*)(p+8+slot*IntPtr.Size);
    public bool Running
    {get{var f=(delegate* unmanaged[Cdecl]<int>)Fn(Table("RynthAi.Script",6),4);if(f==null)throw new InvalidOperationException("Macro query unavailable");return f()!=0;}}
    private void Macro(bool running)
    {var f=(delegate* unmanaged[Cdecl]<int,void>)Fn(Table("RynthAi.Script",6),5);if(f==null)throw new InvalidOperationException("Macro control unavailable");f(running?1:0);}
    public bool Idle()
    {var f=(delegate* unmanaged[Cdecl]<uint*,int>)Fn(Table("RynthAi.Activity",1),0);uint flags=0;return f!=null&&f(&flags)==1&&(flags&~(64u|256u))==0;}
    public bool Hold()
    {var f=(delegate* unmanaged[Cdecl]<uint,int>)Fn(Table("RynthAi.Yield",2),0);return f!=null&&f(3000)==1;}
    public void Acquire()
    {
        if(active)throw new InvalidOperationException("Craft pause already owned");
        if(!Hold())throw new InvalidOperationException("RynthAi did not acknowledge the crafting hold.");
        active=true;owned=Running;if(owned)Macro(false);
    }
    public void Release(bool resume)
    {
        if(!active)return;active=false;bool restore=resume&&owned;owned=false;
        var f=(delegate* unmanaged[Cdecl]<int>)Fn(Table("RynthAi.Yield",2),1);if(f!=null)f();
        if(restore&&!Running)Macro(true);
    }
}
