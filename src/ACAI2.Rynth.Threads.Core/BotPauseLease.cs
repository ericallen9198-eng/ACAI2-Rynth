namespace ACAI2.Rynth.Threads.Core;

// Runtime ownership only. A restart never resumes a bot from a historical lease.
public sealed class BotPauseLease(Func<bool> running,Action<bool> setRunning,Action releaseHold)
{
    public bool Active {get;private set;}
    public bool OwnsResume {get;private set;}
    public void Acquire()
    {
        if(Active)throw new InvalidOperationException("A bot pause is already owned.");
        bool wasRunning=running();
        if(wasRunning)setRunning(false);
        OwnsResume=wasRunning;Active=true;
    }
    public void Release(bool resume)
    {
        if(!Active)return;
        bool restore=resume&&OwnsResume;
        Active=false;OwnsResume=false;
        try{releaseHold();}finally{if(restore&&!running())setRunning(true);}
    }
}
