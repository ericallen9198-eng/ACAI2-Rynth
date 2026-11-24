namespace ACAI2.Rynth.MapHunt.Core;

// Called on the plugin pump only. Chat/render callbacks only enqueue explicit user requests.
public sealed class MapHuntController(IMapHuntBot bot,MapHuntProfile profile,Action prepareMeta,Action save)
{
    public const string MetaName="MapHunt App";
    private string startup="";
    private bool blocked;
    private double deadline,nextCheckpoint;
    private long lastWalk=long.MinValue;
    private (long Token,double At)? direct;
    public MapHuntView View {get;private set;}=MapHuntView.Idle;
    private double Num(string code)=>double.TryParse(bot.Evaluate(code),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double n)&&double.IsFinite(n)?n:0;
    private void Set(string key,double n)=>bot.Evaluate("setvar["+key+","+MapHuntGeometry.Number(n)+"]");
    private string State=>bot.Evaluate("vtgetmetastate[]");
    public bool Owns
    {
        get {string s=bot.Evaluate("vtgetmeta[]").Replace('\\','/').Split('/').Last();if(s.EndsWith(".af",StringComparison.OrdinalIgnoreCase)||s.EndsWith(".met",StringComparison.OrdinalIgnoreCase))s=Path.GetFileNameWithoutExtension(s);return s.Equals(MetaName,StringComparison.OrdinalIgnoreCase);}
    }
    public void ImportPreferences()
    {
        double Read(string key,double fallback)=>Num("testpvar["+key+"]")==1?Num("getpvar["+key+"]"):fallback;
        profile.Preferences=new(Read("meshnav",profile.Preferences.Mesh?1:0)==1,(int)Read("maphunt_close",profile.Preferences.Close),(int)Read("maphunt_far",profile.Preferences.Far));
        profile.Preferences=profile.Preferences.Validated();Publish("Ready","Follow maphunt.nav and hunt treasure maps.","",false,false);
    }
    public void Preferences(MapHuntPreferences value)
    {
        profile.Preferences=value.Validated();ApplyPreferences();save();View=View with {Preferences=profile.Preferences};
    }
    private void ApplyPreferences()
    {
        var p=profile.Preferences.Validated();
        bot.Evaluate("setpvar[meshnav,"+(p.Mesh?1:0)+"]");bot.Evaluate("setpvar[maphunt_close,"+p.Close+"]");bot.Evaluate("setpvar[maphunt_far,"+p.Far+"]");
    }
    public void Begin(double time,bool automatic=false)
    {
        if(startup.Length>0)return;
        blocked=false;
        if(Num("maraemissionapi[]")<6){blocked=true;Notice("Compatibility support required","This app needs the documented mission API 6 support. RynthAi has not been changed.");return;}
        if(automatic&&bot.Running&&!Owns){blocked=true;Notice("Resume available","Another macro is running. Press Start / Resume when you are ready.");return;}
        prepareMeta(); // install only this app's AF before stopping anything, so a bad path cannot interrupt a run
        profile.Active=true;save();Cleanup();Set("mh_schema",0);ApplyPreferences();
        bot.Command("/ra stop");bot.Command("/ra meta load MapHunt App.af");startup="load";deadline=time+15;lastWalk=long.MinValue;
        Notice("Starting","Loading the MapHunt workflow...");
    }
    public void Stop()
    {
        blocked=true;profile.Active=false;profile.Event=null;save();startup="";Cleanup();if(Owns)bot.Command("/ra stop");Notice("Stopped","Preferences saved. This run will not restart after login.");
    }
    public void Review(double time)
    {
        if(!Owns)return;
        bot.Evaluate("setpvar[mh_mm_pickup_unresolved,0]");profile.Event=null;save();Begin(time);
    }
    private void ClearMotion(){bot.Command("/ub clearmotion");direct=null;}
    public void Cleanup()
    {
        if(!Owns)return;
        ClearMotion();bot.Evaluate("maraenavrestore[]");
        bot.Evaluate("if[getpvar[mh_pt_cache_pickup_held]==1,rasetsetting[`LootChestsMacroOff`,getpvar[mh_pt_cache_old_pickup]]]");bot.Evaluate("setpvar[mh_pt_cache_pickup_held,0]");
    }
    public void Tick(double time)
    {
        if(blocked)return;
        if(startup.Length>0)
        {
            if(time>deadline){startup="";Notice("Startup not confirmed","Check RynthAi and the app's workflow folder, then press Start / Resume.");}
            else if(startup=="load"&&Owns&&!bot.Running)
            {
                bot.Evaluate("vtsetsetting[EnableMeta,1]");bot.Evaluate("vtsetmetastate[Default]");bot.Command("/ra start");startup="arm";deadline=time+15;
            }
            else if(startup=="arm"&&Owns&&bot.Running&&Num("getvar[mh_schema]")==1){startup="";RestoreEvent();}
        }
        if(!Owns){if(startup.Length==0)Notice(profile.Active?"Resume available":"Ready","MapHunt is not the loaded meta. Press Start / Resume to use it.");return;}
        double utc=Num("getunixtime[]");string state=State;Set("mh_heartbeat",utc);
        if(startup.Length>0)return;
        if(!bot.Running)
        {
            if(profile.Active){Cleanup();profile.Active=false;save();}
            Notice("Stopped","Press Start / Resume to continue.");return;
        }
        long token=(long)Num("getvar[mh_walk_token]");
        if(token!=lastWalk&&Num("getcharvital_current[1]")>0&&Num("maraerecoverybusy[]")==0)
        {
            if(state=="MAP_MESH_WAIT")
            {
                lastWalk=token;
                double heading=Num("getvar[tm_mesh_heading]"),step=Num("getvar[tm_mesh_step]");
                bool ok=heading>=0&&heading<360&&step>=4&&step<=180&&Num("maraemapwalk["+MapHuntGeometry.Number(heading)+","+MapHuntGeometry.Number(step)+"]")==1;
                Set("tm_walk_ack",ok?1:-1);
            }
            else if(state=="DIRECT_WAIT")
            {
                lastWalk=token;ClearMotion();double heading=Num("getvar[tm_mesh_heading]");
                if(heading<0||heading>=360){Set("tm_walk_ack",-1);}
                else{bot.Command("/ub face "+MapHuntGeometry.Number(heading));direct=(token,time+1);}
            }
            else if(state is "CACHE_RETURN_WAIT" or "CACHE_APPROACH_WAIT" or "DIGSITE_RETURN_WAIT")
            {
                lastWalk=token;MoveToTarget(state);
            }
        }
        if(direct is { } d)
        {
            if(state!="DIRECT_WAIT")ClearMotion();
            else if(time>=d.At){bot.Command("/ub setmotion Forward 1");Set("tm_walk_ack",1);direct=null;}
        }
        if(profile.Active&&time>=nextCheckpoint)
        {
            nextCheckpoint=time+5;
            string anchor=bot.Evaluate("getvar[tm_cache_anchor]");
            profile.Event=Num("getpvar[mh_tm_dig_active]")==1&&MapHuntGeometry.TryCoordinates(anchor,out _,out _)?new(anchor,Num("getvar[tm_event_deadline]"),Num("getvar[tm_doubloon_before]"),state):null;save();
        }
        bool review=Num("getpvar[mh_mm_pickup_unresolved]")==1;
        var current=new MapHuntPreferences(Num("getpvar[meshnav]")==1,(int)Num("getpvar[maphunt_close]"),(int)Num("getpvar[maphunt_far]")).Validated();
        if(current!=profile.Preferences){profile.Preferences=current;save();}
        Publish(Phase(state,review),bot.Evaluate("getvar[mh_status]"),state,true,review);
    }
    private void MoveToTarget(string state)
    {
        string target=bot.Evaluate("getvar[tm_walk_target]");
        (double NS,double EW)? goal=MapHuntGeometry.TryCoordinates(target,out double ns,out double ew)?(ns,ew):null;
        if(state=="CACHE_APPROACH_WAIT")goal=MapHuntGeometry.Approach(target,bot.Evaluate("getplayercoordinates[]"));
        bool ok=goal.HasValue&&Num("maraenavsave[]")==1&&bot.GoTo(goal.Value.NS,goal.Value.EW)&&Num("maraenavmark[]")==1;
        Set("tm_walk_ack",ok?1:-1);
    }
    private void RestoreEvent()
    {
        var e=profile.Event;
        if(e==null||e.Deadline<=Num("getunixtime[]")||Num("getcharvital_current[1]")<=0||Num("maraerecoverybusy[]")!=0||Num("getpvar[mh_mm_pickup_unresolved]")!=0||!MapHuntGeometry.TryCoordinates(e.Anchor,out _,out _))return;
        if(Num("coordinatedistanceflat[getplayercoordinates[],`"+e.Anchor+"`]")>55)return;
        bot.Evaluate("setvar[tm_cache_anchor,`"+e.Anchor+"`]");Set("tm_event_deadline",e.Deadline);Set("tm_doubloon_before",e.Doubloons);
        bot.Evaluate("setpvar[mh_tm_dig_active,1]");Set("tm_anchor_forced",0);Set("tm_anchor_retry_at",0);Set("tm_anchor_failures",0);Set("tm_aun_loot_active",0);
        bot.Command("/ra nav load maphunt.nav");
        if(e.State.StartsWith("CACHE_",StringComparison.Ordinal)){Set("tm_cache_deadline",Num("getunixtime[]")+300);bot.Evaluate("vtsetmetastate[CACHE_PREP]");}
        else bot.Evaluate("vtsetmetastate[ENCOUNTER]");
    }
    public void Fault(string message)
    {
        blocked=true;startup="";
        try{Cleanup();if(Owns)bot.Command("/ra stop");}catch{}
        Notice("Needs attention",message.Length>240?message[..240]:message);
    }
    private void Notice(string phase,string detail)=>Publish(phase,detail,"",false,false);
    private void Publish(string phase,string detail,string state,bool running,bool review)=>View=new(phase,detail,state,running,review,profile.Preferences);
    public static string Phase(string state,bool review)=>review?"Check inventory":state switch
    {
        "HOLD"=>"Needs attention","HUNT" or "HUNT_READY"=>"Hunting route","HUNT_LOAD" or "HUNT_VERIFY"=>"Preparing route",
        "ENCOUNTER" or "ENCOUNTER_INIT"=>"Fighting dig event","MAP_DEATH" or "DEATH_WAIT" or "DEATH_READY"=>"Recovering after death",
        _ when state.StartsWith("CACHE_",StringComparison.Ordinal)=>"Collecting treasure",
        _ when state.StartsWith("DIGSITE_",StringComparison.Ordinal)=>"Returning to digsite",
        _ when state.StartsWith("RECOVERY_",StringComparison.Ordinal)=>"Recovering map attempt",
        _ when state.StartsWith("DIG_",StringComparison.Ordinal)=>"Digging",
        _=>"Following map"
    };
}
