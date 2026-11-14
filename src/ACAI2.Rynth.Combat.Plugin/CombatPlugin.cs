using System.Collections.Concurrent;
using System.Text.Json;
using ACAI2.Rynth.Combat.Core;
using RynthCore.PluginCore;
namespace ACAI2.Rynth.Combat.Plugin;

public sealed class CombatPlugin : RynthPluginBase
{
    public const string Version="0.1.2";
    public override uint MinimumApiVersion=>92;
    private readonly ConcurrentQueue<(string Text,int Type,DateTime Utc,double Now,long Epoch,uint EffectId,bool Added,double Duration)> incoming=new();
    private readonly ConcurrentQueue<string> commands=new();
    private readonly object settingsGate=new();
    private Appearance appearance=new();
    private readonly CombatPanel panel=new();
    private CombatEngine? engine;
    private CombatArchive? archive;
    private CombatView view=CombatView.Empty;
    private uint player;
    private string server="",character="",notice="Waiting for login";
    private bool waiting,profileApplied,renderFailed;
    private int lifecycle,queued,dropped;
    private long epoch,savedRevision=-1,droppedSinceTicks;
    private double nextEffects;
    private readonly uint[] effectIds=new uint[512];
    private readonly double[] effectExpiry=new double[512];
    private double nextProbe,nextView,nextScan,nextSave,nextProfile,previewUntil,nextPreview;
    private FloatStream preview=new();
    private readonly Dictionary<string,RunRecord> frozenRuns=[];
    private string snapshotCurrentId="";
    private Task closing=Task.CompletedTask;
    private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    private string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ACAI2-Combat");
    public override int Initialize(){Host.Log("[ACAI2 Combat] v"+Version+". /acai2combat opens the independent combat app. Disable RynthVision numbers separately; debuff icons stay with Rynth.");return 0;}
    public override void OnLoginComplete(){waiting=false;Interlocked.Exchange(ref lifecycle,1);}
    public override void OnLogout(){waiting=true;Interlocked.Exchange(ref lifecycle,1);}
    public override void OnChatWindowText(string? text,int type,ref int eat)
    {
        if(waiting||player==0||Volatile.Read(ref lifecycle)!=0||text==null||text.Length>4096||type is 2 or 3 or 4 or 17 or 18)return;
        bool relevant=text.Contains("you",StringComparison.OrdinalIgnoreCase)||text.StartsWith("Your ",StringComparison.Ordinal)||text.StartsWith("[REALM]",StringComparison.Ordinal)||text.StartsWith("The Thread Gem ",StringComparison.Ordinal)||text.StartsWith("Monsters killed ",StringComparison.Ordinal)||text.Contains(" is cleared.",StringComparison.Ordinal)||text.EndsWith("!",StringComparison.Ordinal)||text.EndsWith(" has expired.",StringComparison.Ordinal);
        if(!relevant)return;
        if(Interlocked.Increment(ref queued)<=512)incoming.Enqueue((text,type,DateTime.UtcNow,Now,Volatile.Read(ref epoch),0,false,0));
        else{Interlocked.Decrement(ref queued);Interlocked.Increment(ref dropped);Interlocked.CompareExchange(ref droppedSinceTicks,DateTime.UtcNow.Ticks,0);}
    }
    public override void OnEnchantmentAdded(uint id,double duration)=>QueueEffect(id,true,duration);
    public override void OnEnchantmentRemoved(uint id)=>QueueEffect(id,false,0);
    private void QueueEffect(uint id,bool added,double duration)
    {
        if(waiting||player==0||id==0||Volatile.Read(ref lifecycle)!=0)return;
        if(Interlocked.Increment(ref queued)<=512)incoming.Enqueue(("",0,DateTime.UtcNow,Now,Volatile.Read(ref epoch),id,added,duration));
        else{Interlocked.Decrement(ref queued);Interlocked.Increment(ref dropped);Interlocked.CompareExchange(ref droppedSinceTicks,DateTime.UtcNow.Ticks,0);}
    }
    private void SampleEffects(DateTime utc)
    {
        if(engine==null)return;
        try
        {
            if(!Host.HasReadPlayerEnchantments||!Host.IsPlayerReady()){engine.Effects.SampleAt(utc,false,[],"Player enchantment snapshot unavailable");return;}
            int n=Host.ReadPlayerEnchantments(effectIds,effectExpiry,512);double serverTime=Host.GetServerTime();
            if(n<0||n>=512||!double.IsFinite(serverTime)||serverTime<=0){engine.Effects.SampleAt(utc,false,[],"Native enchantment snapshot missing, truncated or clock unavailable");return;}
            var active=new List<ActiveEffect>();
            for(int i=0;i<n;i++)
            {
                double remaining=effectExpiry[i]-serverTime;if(double.IsNaN(remaining)||remaining<=0)continue;
                active.Add(new(effectIds[i],remaining<31536000?utc.AddSeconds(remaining):null));
            }
            engine.Effects.SampleAt(utc,true,active.ToArray());
        }
        catch(Exception ex){engine.Effects.SampleAt(utc,false,[],"Enchantment snapshot failed: "+ex.GetType().Name);}
    }
    public override void OnChatBarEnter(string? text,ref int eat)
    {
        string cmd=(text??"").Trim().ToLowerInvariant();if(cmd!="/acai2combat"&&!cmd.StartsWith("/acai2combat ",StringComparison.Ordinal))return;
        eat=1;if(commands.Count<16)commands.Enqueue(cmd.Length>12?cmd[12..].Trim():"show");
    }
    public override void OnTick()
    {
        double now=Now;
        if(Interlocked.Exchange(ref lifecycle,0)!=0){End();nextProbe=0;}
        if(!waiting&&now>=nextProbe)
        {
            nextProbe=now+1;uint id=Host.GetPlayerId();
            if(id==0||!Host.TryGetWorldName(out string world)||!Host.TryGetObjectName(id,out string name)){End();return;}
            if(player!=id||world!=server||name!=character)
            {
                End();if(!closing.IsCompleted)return;
                server=world;character=name;player=id;engine=new(world,name);archive=new(Root,world,name);profileApplied=false;renderFailed=false;dropped=0;notice="";previewUntil=0;preview=new();frozenRuns.Clear();snapshotCurrentId="";savedRevision=-1;nextSave=now+5;
            }
        }
        if(engine==null||archive==null)return;
        if(archive.Ready&&!profileApplied){lock(settingsGate){appearance=archive.LoadedAppearance;panel.Reset();}engine.RestoreRuns(archive.History);profileApplied=true;}
        if(!profileApplied){if(!archive.Completion.IsCompleted)return;profileApplied=true;notice=archive.Notice;}
        engine.BigHitMinimum=appearance.BigHitMinimum;
        long lostAt=Interlocked.Exchange(ref droppedSinceTicks,0);if(lostAt!=0)engine.Effects.Lost(new DateTime(lostAt,DateTimeKind.Utc));
        if(now>=nextEffects){nextEffects=now+.1;SampleEffects(DateTime.UtcNow);}
        for(int i=0;i<128&&incoming.TryDequeue(out var line);i++)
        {
            Interlocked.Decrement(ref queued);if(line.Epoch!=Volatile.Read(ref epoch))continue;
            try{if(line.EffectId!=0)engine.Effects.Changed(line.Utc,line.EffectId,line.Added,line.Duration);else engine.Observe(line.Text,line.Type,line.Utc,line.Now);}catch(Exception ex){notice="Combat line skipped: "+ex.GetType().Name;engine.Gap();}
        }
        if(dropped>0)engine.Gap();
        bool portaling=Host.HasIsPortaling&&Host.IsPortaling();if(portaling)engine.Pause(DateTime.UtcNow,true);else{if(engine.EntryConfirmed&&!engine.Inside)engine.Enter(DateTime.UtcNow);engine.Tick(DateTime.UtcNow);}
        while(commands.TryDequeue(out string? cmd))
        {
            if(cmd is "show" or "" or "setup"){lock(settingsGate)appearance.Visible=true;panel.Setup=cmd=="setup";renderFailed=false;}
            else if(cmd=="hide"){lock(settingsGate)appearance.Visible=false;panel.Dirty=true;}
            else if(cmd=="preview"){previewUntil=previewUntil>now?0:now+30;}
            else if(cmd=="entered")engine.Enter(DateTime.UtcNow);
            else if(cmd=="outside")engine.Pause(DateTime.UtcNow);
            else if(cmd=="new"){engine.NewSession();lock(settingsGate){appearance.AutoThread=false;panel.Dirty=true;}}
            else if(cmd=="status")Local("v"+Version+" | "+notice+" | "+archive.Notice+" | queued "+queued+" dropped "+dropped);
            else if(cmd=="export")Export();
        }
        if(now>=nextScan&&engine.Inside&&!portaling)
        {
            nextScan=now+1;
            if(Host.HasGetLiveObjectIds&&Host.HasObjectIsAttackable&&Host.TryGetObjectPosition(player,out uint playerCell,out _,out _,out _))foreach(uint id in Host.GetLiveObjectIds().Take(4096))
            {
                // AC dynamic creature GUID range excludes players and pack items.
                if(id<0x80000000u||id>=0xc0000000u||!Host.ObjectIsAttackable(id)||!Host.TryGetObjectPosition(id,out uint cell,out _,out _,out _)||(cell>>16)!=(playerCell>>16)||!Host.TryGetObjectName(id,out string name))continue;
                engine.Seen(id,name);
            }
        }
        if(now>=nextSave&&archive.Ready)
        {
            nextSave=now+5;if(savedRevision!=engine.Revision)
            {bool ok=true;foreach(var run in engine.Runs.Where(r=>r.Entered!=null||r.Ended!=null))ok&=archive.Save(run);if(ok)savedRevision=engine.Revision;else notice="Run save queue busy; retrying next interval.";}
        }
        if(profileApplied&&archive.Ready&&panel.Dirty&&now>=nextProfile)
        {
            nextProfile=now+2;
            try{lock(settingsGate){if(archive.ProfileReadable){appearance.Validate();Profiles.Save(Path.Combine(archive.Folder,"ui.json"),appearance);panel.Dirty=false;}else notice="Existing profile unreadable; not overwriting it.";}}catch(Exception ex){notice="UI save failed: "+ex.Message;}
        }
        if(now<previewUntil&&now>=nextPreview)
        {
            nextPreview=now+.4;int n=(int)(now*2.5)%11;
            string[] kinds=["Acid","Poison","Healing","Critical","Sneak","Ward","Taken","VulnLightning","VulnFrost","VulnAcid","Imperil"];
            string kind=kinds[n];string label=kind switch{"VulnLightning"=>"LIGHT VULN","VulnFrost"=>"FROST VULN","VulnAcid"=>"ACID VULN",_=>kind.ToUpperInvariant()};preview.Add(now,kind,label,amount:kind.StartsWith("Vuln")||kind=="Imperil"?0:kind=="Critical"?2313:kind=="Healing"?118:249,incoming:kind is "Healing" or "Ward" or "Taken" or "Imperil"||kind.StartsWith("Vuln"),critical:kind is "Critical" or "Taken");
        }
        if(now>=nextView)
        {
            nextView=now+.2;bool run=appearance.AutoThread&&engine.Current?.Entered!=null;
            var rates=engine.Rates(now,run);
            var current=engine.Current==null?null:JsonSerializer.Deserialize<RunRecord>(JsonSerializer.Serialize(engine.Current));
            if(snapshotCurrentId.Length>0&&snapshotCurrentId!=current?.Id&&engine.Runs.FirstOrDefault(r=>r.Id==snapshotCurrentId) is {} previous)frozenRuns[previous.Id]=JsonSerializer.Deserialize<RunRecord>(JsonSerializer.Serialize(previous))!;
            snapshotCurrentId=current?.Id??"";
            foreach(string id in frozenRuns.Keys.Where(id=>!engine.Runs.Any(r=>r.Id==id)).ToArray())frozenRuns.Remove(id);
            foreach(var r in engine.Runs.Where(r=>r.Entered!=null&&r.Id!=current?.Id))if(!frozenRuns.ContainsKey(r.Id))frozenRuns[r.Id]=JsonSerializer.Deserialize<RunRecord>(JsonSerializer.Serialize(r))!;
            var history=(current is {Entered:not null}?new[]{current}:Array.Empty<RunRecord>()).Concat(frozenRuns.Values).Concat(archive.History).GroupBy(r=>r.Id).Select(g=>g.First()).OrderByDescending(r=>r.Opened).Take(50).ToArray();
            Volatile.Write(ref view,new(engine.Session.Copy(),current,history,previewUntil>now?preview.Read(now):engine.Floats.Read(now),rates.Dps,rates.Taken,run,run?"Thread: "+engine.Current!.Name:"Session",notice.Length>0?notice:archive.Notice,dropped,previewUntil>now));
        }
    }
    private void Export()
    {
        try{if(archive==null||engine==null)return;string path=Path.Combine(archive.Folder,"export-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");Profiles.Save(path,new{Schema=1,Server=server,Character=character,Session=engine.Session,Runs=engine.Runs});notice="Export: "+path;Local(notice);}catch(Exception ex){Local("Export failed: "+ex.Message);}
    }
    public override void OnRender()
    {
        if(player==0||renderFailed)return;
        try{lock(settingsGate)panel.Draw(character,appearance,Volatile.Read(ref view),Now,s=>{if(commands.Count<16)commands.Enqueue(s);});}
        catch(Exception ex){renderFailed=true;Local("Combat panel hidden after display error: "+ex.GetType().Name+" "+ex.Message);}
    }
    private void End()
    {
        Interlocked.Increment(ref epoch);player=0;nextEffects=0;Interlocked.Exchange(ref droppedSinceTicks,0);
        while(incoming.TryDequeue(out _))Interlocked.Decrement(ref queued);while(commands.TryDequeue(out _)){}
        if(archive is {Ready:true,ProfileReadable:true}&&profileApplied&&panel.Dirty){try{lock(settingsGate){appearance.Validate();Profiles.Save(Path.Combine(archive.Folder,"ui.json"),appearance);}}catch(Exception ex){Host.Log("[ACAI2 Combat] Final UI save failed: "+ex.Message);}}
        if(engine!=null&&archive!=null){engine.Pause(DateTime.UtcNow);engine.Gap();if(engine.Current is {Entered:not null} active)archive.Save(active);foreach(var r in engine.Runs.Where(r=>r.Entered!=null||r.Ended!=null))archive.Save(r);archive.Dispose();closing=archive.Completion;}
        engine=null;archive=null;server="";character="";profileApplied=false;Volatile.Write(ref view,CombatView.Empty);
    }
    private void Local(string text){Host.Log("[ACAI2 Combat] "+text);if(Host.HasWriteToChat)Host.WriteToChat("[ACAI2 Combat] "+text,1);}
    public override void Shutdown()=>End();
}
