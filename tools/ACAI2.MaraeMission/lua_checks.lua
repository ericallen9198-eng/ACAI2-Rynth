local checks=0
local function check(b,s)assert(b,s);checks=checks+1 end
step();ai.MacroRunning=true
click('Start run');step(12)
check(ai.MacroRunning and sim.meta=='Marae Lassel - Embedded','Start replaces old macro only after verified load')
check(sim.metaState~='HOLD','stale HOLD cannot cancel pending startup')
check(sim.pvars.ml_run_mode==1 and sim.pvars.ml_area_180==1,'preferences applied before starting')
sim.pvars.mm_area_180=1;sim.pvars.mm_180_done_0=1;step(25)
local saved=sim.stores.A.mission_v1
check(saved.active and saved.progress.mm_area_180==1,'character checkpoint stores completed area')
check(saved.progress.mm_180_done_0==1,'checkpoint retains completed task')
sim.state='CharacterSelect';ai.MacroRunning=false;step()
sim.pvars={};sim.vars={};sim.meta=nil;sim.state='InGame';step(15)
check(ai.MacroRunning and sim.pvars.mm_area_180==1,'reconnect automatically reloads and restores this character')
sim.pvars.pt_cache_pickup_held=1;sim.pvars.pt_cache_old_pickup=1
click('Stop run');step(3)
check(sim.restoredPickup==1 and sim.pvars.pt_cache_pickup_held==0,'explicit Stop restores the temporary pickup setting')
check(not ai.MacroRunning and not saved.active,'explicit Stop is durable')
sim.state='CharacterSelect';step();sim.state='InGame';step(15)
check(not ai.MacroRunning,'explicit Stop cannot auto-resume on login')
-- Loading can legitimately take more than twenty frames.
sim.ignoreLoad=true;sim.meta='Another meta';click('Resume run');step(40)
check(not ai.MacroRunning,'delayed load must not start the other meta')
check(sim.stores.A.mission_v1.active,'load waits by wall time rather than twenty frames')
sim.meta='Marae Lassel - Embedded';sim.metaState='HOLD';step(10)
check(ai.MacroRunning,'delayed matching load can complete startup')
click('Stop run');step(2);sim.ignoreLoad=true;sim.meta='Another meta';click('Resume run');step(400)
check(not ai.MacroRunning and not sim.stores.A.mission_v1.active,'load timeout does not start wrong meta or create reconnect loop')
check(sim.stores.A.mission_v1.resumable,'timeout preserves progress for review')
-- Another character cannot inherit A's run, and auto-resume must not take over another macro.
sim.state='CharacterSelect';step();sim.character='B';sim.state='InGame';step(10)
check(not ai.MacroRunning and not (sim.stores.B.mission_v1 or {}).active,'another character does not inherit mission')
sim.state='CharacterSelect';step();sim.character='A';sim.stores.A.mission_v1.active=true;sim.meta='Other work';ai.MacroRunning=true;sim.state='InGame';step(10)
check(sim.meta=='Other work' and ai.MacroRunning,'automatic resume never stops a different running macro')
return checks
