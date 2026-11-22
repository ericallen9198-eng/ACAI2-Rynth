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
step(25)
sim.toggle='Hunt only##huntOnly180';sim.render()
check(sim.stores.A.mission_options_v1.huntOnly[1]==true,'per-area hunt-only choice is saved for this character')
click('Resume run');step(40)
check(sim.pvars.ml_hunt_only_180==1 and sim.pvars.ml_maps_180==0,'hunt-only preference does not force maps on')
click('Stop run');step(25)
sim.state='CharacterSelect';step();sim.state='InGame';step(30)
check(sim.stores.A.mission_options_v1.huntOnly[1]==true,'per-character hunt preference survives reconnect')
-- New options are separate and persist per character; maps off is not overridden.
step(25);sim.render()
check(sim.stores.A.mission_options_v1.huntOnly[1]==true,'per-area hunt-only checkbox saves character settings')
click('Resume run');step(40)
check(sim.pvars.ml_hunt_only_180==1 and sim.pvars.ml_maps_180==0,'Hunt only applies without forcing maps on')
sim.metaState='MAP_MESH_WAIT';sim.vars.tm_walk_generation=12;sim.vars.tm_mesh_heading=45;sim.vars.tm_mesh_step=24
local meshWalks=sim.meshWalks or 0;step(25)
check(sim.meshWalks==meshWalks+1 and sim.vars.tm_walk_ack==1,'map mesh request uses native planner once')
step(25);check(sim.meshWalks==meshWalks+1,'mesh route is not rebuilt every tick')
click('Stop run');step(25)
sim.toggle='Mesh map steps (needs RynthNav)';sim.render()
check(sim.stores.A.mission_options_v1.meshMaps==true,'mesh map steps is a separate opt-in preference')
click('Resume run');step(40)
check(sim.pvars.ml_mesh_maps==1 and sim.pvars.ml_hunt_only_180==1 and sim.pvars.ml_maps_180==0,'mesh preference never enables maps for pure hunting')
click('Stop run');step(25)
sim.state='CharacterSelect';step();sim.state='InGame';step(30)
check(sim.stores.A.mission_options_v1.huntOnly[1] and sim.stores.A.mission_options_v1.meshMaps,'new character preferences survive reconnect')
-- Camp limits are opt-in; deadlines and pending partial turn-ins are not booleanized.
check(sim.pvars.mm_camp_limit==0,'camp time limit defaults off')
step(25);sim.toggle='Limit task hunting to 2 hours per camp';sim.render()
check(sim.stores.A.mission_options_v1.campLimit==true,'optional two-hour policy is saved per character')
click('Resume run');step(40)
check(sim.pvars.mm_camp_limit==1,'two-hour preference is applied only when enabled')
local originalDeadline=math.floor(sim.time)+7200
sim.pvars.mm_deadline_180=originalDeadline;sim.pvars.mm_cap_pending_180=1
step(120)
check(sim.stores.A.mission_v1.deadlines['180']==originalDeadline,'deadline checkpoint preserves the full Unix timestamp')
click('Stop run');step(25)
sim.state='CharacterSelect';step();sim.pvars={};sim.vars={};sim.meta=nil;sim.state='InGame';step(30)
click('Resume run');step(40)
check(sim.pvars.mm_deadline_180==originalDeadline and sim.pvars.mm_cap_pending_180==1,'resume restores deadline and outstanding partial turn-in')
click('Stop run');step(25);click('New run');step(40)
check(sim.pvars.mm_deadline_180==0 and sim.pvars.mm_cap_pending_180==0 and sim.pvars.mm_timed_180==0,'New run clears quotas and pending camp flags')
check(sim.pvars.mm_camp_limit==1,'New run preserves the chosen optional policy')
click('Stop run');step(25)
-- Both sliders remain usable during a run; writes are on tick, not inside rendering.
click('Resume run');step(40)
local commandsBefore=#sim.commands
sim.vars.tm_mesh_step=24;sim.vars.tm_walk_target='51.2|-84|0.2'
sim.slider='Far clue travel';sim.sliderValue=90;sim.render()
check(sim.pvars.mm_mesh_far==60,'render callback does not dispatch native stride writes')
sim.tick()
check(sim.pvars.mm_mesh_far==90 and sim.pvars.mm_mesh_close==10,'far slider applies live on the next pump tick')
check(ai.MacroRunning and sim.vars.tm_mesh_step==24 and sim.vars.tm_walk_target=='51.2|-84|0.2','live tuning neither stops nor retargets the active leg')
sim.slider='Close clue travel';sim.sliderValue=40;sim.render();sim.tick()
check(sim.pvars.mm_mesh_close==40 and sim.pvars.mm_mesh_far==90,'near slider is independent of far distance')
click('Stop run');step(25)
sim.state='CharacterSelect';step();sim.pvars={};sim.vars={};sim.meta=nil;sim.state='InGame';step(30)
click('Resume run');step(40)
check(sim.pvars.mm_mesh_far==90 and sim.pvars.mm_mesh_close==40,'both travel distances persist per character through reconnect')
click('Stop run');step(25)
-- Recoverable native states stay active and therefore recover on the next login.
click('Resume run');step(40)
sim.metaState='RECOVERY_WAIT';sim.vars.ml_outcome='Running';sim.vars.ml_status='Recovering from CACHE_SEARCH'
step(25);check(sim.stores.A.mission_v1.active,'recoverable native failure keeps the reconnect checkpoint active')
sim.state='CharacterSelect';ai.MacroRunning=false;step();sim.state='InGame';step(40)
check(ai.MacroRunning and sim.stores.A.mission_v1.active,'reconnect resumes a recovering run automatically')
-- A real unconfirmed inventory action remains blocked until explicit local review.
sim.metaState='HOLD';sim.vars.ml_outcome='Needs attention';sim.vars.ml_status='Unconfirmed cache pickup'
sim.pvars.mm_pickup_unresolved=1;step(25)
check(not ai.MacroRunning and not sim.stores.A.mission_v1.active,'inventory review HOLD deactivates automatic restart')
sim.state='CharacterSelect';step();sim.state='InGame';step(30)
check(not ai.MacroRunning and sim.pvars.mm_pickup_unresolved==1,'logout does not clear the inventory review marker')
click('Resume after reviewing pickup');step(40)
check(ai.MacroRunning and sim.pvars.mm_pickup_unresolved==0,'explicit reviewed resume clears the pickup hold and retains task progress')
sim.vars.ml_zone=225;sim.vars.pt_unavailable_1=1;sim.pvars.mm_225_done_1=1
sim.chat({Message='Hea Hineroa the Golem Hunter tells you, "You have defeated 4 of 50 A New Golem. The hunt is not yet finished."'})
check(sim.vars.pt_unavailable_1==0 and sim.pvars.mm_225_done_1==0 and sim.stores.A.mission_v1.progress.mm_225_done_1==0,'live unfinished task overrides stale saved completion in native and controller checkpoints')
click('Stop run');step(25)
return checks
