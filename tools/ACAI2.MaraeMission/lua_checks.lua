local checks=0
local function check(b,s)assert(b,s);checks=checks+1 end
step();ai.MacroRunning=true
click('Start run');step(30)
check(ai.MacroRunning and sim.meta=='Marae Lassel - Embedded','Start replaces old macro only after verified load')
check(sim.metaState~='HOLD','stale HOLD cannot cancel pending startup')
check(sim.pvars.ml_run_mode==1 and sim.pvars.ml_area_180==1,'preferences applied before starting')
-- High tick and ordinary chat rates must not cause expression polling.
local calls=sim.exprCalls
for i=1,5000 do sim.tick() end
check(sim.exprCalls==calls,'five thousand same-time ticks perform no expression reads')
for i=1,5000 do sim.chat({Message='You graze Tower Guardian for 85 points of bludgeoning damage!'}) end
check(sim.exprCalls==calls,'ordinary combat chat does not cross expression bridge')
check(hud.RefreshRate==5,'HUD uses a bounded five updates per second')
calls=sim.exprCalls
step(100)
check(sim.exprCalls-calls<150,'five seconds of running controller stays within expression call budget')
-- Status parsing learns the target, but never computes a required kill total.
sim.vars.ml_zone=225;sim.vars.pt_pickup_1=0
sim.chat({Message='Hea Hineroa the Golem Hunter tells you, "You have defeated 17 of 999 A New Golem. The hunt is not yet finished."'})
check(sim.vars.pt_pickup_1==1,'server in-progress words confirm task regardless of target and total')
sim.chat({Message='You have killed 2 A New Golems! Your task is complete!'})
check(sim.pvars.mm_225_done_1==1,'completion notice identifies learned task without comparing kill count')
sim.chat({Message='Player says, "Hea Hineroa the Golem Hunter tells you something"'})
sim.chat({Message='Hea Hineroa the Golem Hunter tells you, "Unrecognized new task wording."'})
sim.vars.mm_npc_log_zone=225;sim.vars.mm_npc_log_missing='[1]';sim.vars.mm_npc_log_generation=1
step(25)
check(sim.logReport and #sim.logReport.entries==1,'unrecognized NPC reply creates a bounded diagnostic entry')
check(sim.logPath:match('^npc%-mismatches%-')~=nil,'diagnostic export uses a dedicated NPC mismatch file')
sim.vars.mm_npc_log_generation=2;step(25)
check(#sim.logReport.entries==1 and sim.logReport.entries[1].repeats==2,'duplicate mismatch merges instead of growing the log')
sim.failLog=true;sim.vars.mm_npc_log_generation=3;step(25)
check(ai.MacroRunning,'log write failure never stops the mission')
sim.failLog=false
for i=1,20 do
 sim.chat({Message='Hea Hineroa the Golem Hunter tells you, "New unmatched response '..i..'."'})
 sim.vars.mm_npc_log_generation=3+i;step(25)
end
check(#sim.logReport.entries==16,'mismatch file remains capped at sixteen entries')
sim.pvars.mm_area_180=1;sim.pvars.mm_180_done_0=1;step(120)
local saved=sim.stores.A.mission_v1
check(saved.active and saved.progress.mm_area_180==1,'character checkpoint stores completed area')
check(saved.progress.mm_180_done_0==1,'checkpoint retains completed task')
sim.state='CharacterSelect';ai.MacroRunning=false;step()
sim.pvars={};sim.vars={};sim.meta=nil;sim.state='InGame';step(30)
check(ai.MacroRunning and sim.pvars.mm_area_180==1,'reconnect automatically reloads and restores this character')
sim.pvars.pt_cache_pickup_held=1;sim.pvars.pt_cache_old_pickup=1
click('Stop run');step(3)
check(sim.restoredPickup==1 and sim.pvars.pt_cache_pickup_held==0,'explicit Stop restores the temporary pickup setting')
check(not ai.MacroRunning and not saved.active,'explicit Stop is durable')
sim.state='CharacterSelect';step();sim.state='InGame';step(30)
check(not ai.MacroRunning,'explicit Stop cannot auto-resume on login')
-- Loading can legitimately take more than twenty frames.
sim.ignoreLoad=true;sim.meta='Another meta';click('Resume run');step(40)
check(not ai.MacroRunning,'delayed load must not start the other meta')
check(sim.stores.A.mission_v1.active,'load waits by wall time rather than twenty frames')
sim.meta='Marae Lassel - Embedded';sim.metaState='HOLD';step(30)
check(ai.MacroRunning,'delayed matching load can complete startup')
click('Stop run');step(2);sim.ignoreLoad=true;sim.meta='Another meta';click('Resume run');step(400)
check(not ai.MacroRunning and not sim.stores.A.mission_v1.active,'load timeout does not start wrong meta or create reconnect loop')
check(sim.stores.A.mission_v1.resumable,'timeout preserves progress for review')
-- Another character cannot inherit A's run, and auto-resume must not take over another macro.
sim.state='CharacterSelect';step();sim.character='B';sim.state='InGame';step(30)
check(not ai.MacroRunning and not (sim.stores.B.mission_v1 or {}).active,'another character does not inherit mission')
sim.state='CharacterSelect';step();sim.character='A';sim.stores.A.mission_v1.active=true;sim.meta='Other work';ai.MacroRunning=true;sim.state='InGame';step(30)
check(sim.meta=='Other work' and ai.MacroRunning,'automatic resume never stops a different running macro')
-- Walk dispatch is owned, once per request, and never competes with recovery.
sim.ignoreLoad=false;click('Resume run');step(40)
sim.route='saved hunt route';sim.metaState='CACHE_APPROACH_WAIT'
sim.vars.tm_walk_target='51.2|-84.0|0.2';sim.vars.tm_walk_generation=1
sim.busy=true;local walks=sim.walks or 0;step(25)
check((sim.walks or 0)==walks,'native recovery blocks temporary navigation')
sim.busy=false;step(25)
check(sim.walks==walks+1 and sim.savedRoute=='saved hunt route','walk saves the hunt route before GoTo')
step(25);check(sim.walks==walks+1,'same movement generation does not replace the route every tick')
click('Stop run');step(3)
check(sim.route=='saved hunt route' and not ai.MacroRunning,'Stop restores hunt route without resuming macro')
return checks
