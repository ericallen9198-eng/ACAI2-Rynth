namespace ACAI2.Rynth.Combat.Core;

// A bounded, immutable snapshot captured when chat arrives, never a later pump stance.
// Retail/Rynth combat modes: peace=1, melee=2, missile=4, magic=8.
public sealed class WeaponModeContext
{
    private sealed record Snapshot(int Mode,double Time,double Since);
    private Snapshot? current;
    private readonly object gate=new();
    public void Reset(){lock(gate)Volatile.Write(ref current,null);}
    public void Sample(double now,int mode)
    {
        if(!double.IsFinite(now))return;
        lock(gate)
        {
            var old=Volatile.Read(ref current);
            if(old!=null&&now<old.Time)return;
            bool continuous=old!=null&&old.Mode==mode&&now-old.Time<=.5;
            Volatile.Write(ref current,new(mode,now,continuous?old!.Since:now));
        }
    }
    public bool MissileAt(double now)
    {
        var s=Volatile.Read(ref current);
        return s is {Mode:4}&&double.IsFinite(now)&&now>=s.Time&&now-s.Time<=.5&&now-s.Since>=.5;
    }
}
