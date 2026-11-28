-- SCENARIO fresh start is disk-backed, native recovery settles before survey travel
start();local c=cp();assert(c.active and c.stage==0 and c.probe==1)
assert(countCommand('/ra owncorpse')>=1);assert(countCommand('/ra owncorpse go')==0)
assert(sim.settings.StopMacroOnDeath==0 and sim.settings.TravelToOwnCorpse==1)
assert(sim.vars.te_logger_schema==3 and sim.vars.te_logger_active==1)
-- SCENARIO reached point commits durably before AF acknowledgment
start();report(1,1,1);local c=cp();assert(c.probe==2 and c.sequence==1 and c.results['275_01'].outcome=='reached');assert(sim.vars.te_commit==1)
step();c=cp();assert(c.probe==2,'repeated commit must not increment twice')
-- SCENARIO diagnostic write failure refuses commit, pauses movement, and bounds repair
start();sim.failWrites=true;report(1,1,1);assert(sim.vars.te_commit==0);assert(sim.settings.EnableNavigation==0 and require('rynth.ai').MacroRunning);local c=cp();assert(c.probe==1);step(120);assert(not require('rynth.ai').MacroRunning)
-- SCENARIO death preserves finished points and yields until native recovery completes
start();report(1,1,1);sim.vars.te_probe=2;sim.state='WAIT_275_02';step()
sim.rememberCorpse=true;sim.nativeRecovery=true;sim.hp=0;step();local c=cp();assert(c.probe==2 and c.deaths==1 and c.recovery)
sim.hp=429;step(25);assert(sim.state=='RECOVERY_WAIT');assert(countCommand('/ra meta load Terrain Access Survey.af')==1)
sim.nativeRecovery=false;send('[RynthAi] Own corpse: recovered everything (4 items).');step(20)
c=cp();assert(c.probe==2 and c.results['275_01'].outcome=='reached'and not c.recovery);assert(sim.state=='WAIT_275_02')
-- SCENARIO native death missed between samples is recognized by recovery state
start();sim.state='RECOVERY_WAIT';sim.rememberCorpse=true;sim.nativeRecovery=true;step();assert(cp().deaths==1);assert(cp().probe==1)
-- SCENARIO reconnect resumes saved point through recovery check
start();report(1,1,1);sim.vars.te_probe=2;sim.state='WAIT_275_02';step();game.State='CharacterSelect';step(3);game.State='InGame';step(40)
local c=cp();assert(c.probe==2 and c.results['275_01'].outcome=='reached');assert(sim.state=='WAIT_275_02')
-- SCENARIO another character never inherits Samm's run
start();local n=countCommand('/ra meta load Terrain Access Survey.af');game.Character.Name='Other';step(20);assert(countCommand('/ra meta load Terrain Access Survey.af')==n)
-- SCENARIO explicit stop is retained and restores owned settings
start();request('stop');local c=cp();assert(not c.active and not require('rynth.ai').MacroRunning);assert(sim.settings.FollowNavMin==1.5 and sim.settings.StopMacroOnDeath==1)
game.State='CharacterSelect';step();game.State='InGame';step(30);assert(not require('rynth.ai').MacroRunning)
-- SCENARIO native manual macro stop is respected
start();require('rynth.ai').MacroRunning=false;step();assert(not cp().active);step(30);assert(not require('rynth.ai').MacroRunning)
-- SCENARIO bounded logs and no ordinary player chat recording
start();send('[Allegiance] Someone says, "Private ordinary chat"');step();
for i=1,2300 do sim.time=sim.time+4;step()end
local n,total=0,0
for name,text in pairs(sim.files)do if name:find('^diagnostics%-')then n=n+1;total=total+#text;assert(#text<530000,name.." chars="..#text);assert(not text:find('Private ordinary chat',1,true))end end
assert(n==8 and total<8*530000)
-- SCENARIO log heartbeat remains valid throughout normal combat pauses
start();sim.targets=3;sim.busy=1;step(100);assert(require('rynth.ai').MacroRunning);assert(sim.vars.te_logger_heartbeat>=sim.time-1)
-- SCENARIO unresolved point and alternative retry use separate durable outcomes
start();sim.vars.te_reason='oscillation';report(2,1,1);local c=cp();assert(c.probe==1 and c.attempt==1 and c.results['275_01'].outcome=='hub_retry')
sim.vars.te_attempt=1;report(3,1,2);c=cp();assert(c.probe==2 and c.attempt==0 and c.results['275_01'].outcome=='unresolved')
-- SCENARIO completion is saved before terminal AF stop and never resumes on reconnect
start();sim.vars.te_stage=3;sim.vars.te_probe=1;sim.state='FINISH_COMMIT';step(2);local c=cp();assert(c.completed and not c.active);assert(not require('rynth.ai').MacroRunning)
game.State='CharacterSelect';step();game.State='InGame';step(20);assert(not require('rynth.ai').MacroRunning)
-- SCENARIO missing native recovery status causes a diagnostic stop rather than indefinite waiting
step();sim.suppressStatus=true;request('new');step(150);assert(not cp().active and not require('rynth.ai').MacroRunning)
-- SCENARIO recovery is not interrupted just because travel reaches corpse loot range
start();sim.hp=0;sim.nativeRecovery=true;sim.rememberCorpse=true;step();sim.hp=429;step(20)
sim.nativeRecovery=false;send('[RynthAi] Own corpse: travel back ended: your corpse (0x123) is in loot range; recovering it.');step(25);assert(sim.state=='RECOVERY_WAIT')
send('[RynthAi] Own corpse: recovered everything (2 items).');step(20);assert(sim.state=='WAIT_275_01')
-- SCENARIO repeated new run preserves the original settings snapshot
start();request('new');step(20);request('stop');assert(sim.settings.StopMacroOnDeath==1 and sim.settings.FollowNavMin==1.5)
-- SCENARIO reaching loot range does not issue a second corpse trip
start();sim.hp=0;sim.nativeRecovery=true;sim.rememberCorpse=true;step();sim.hp=429;step(20)
local n=countCommand('/ra owncorpse go');sim.nativeRecovery=false;send('[RynthAi] Own corpse: travel back ended: your corpse (0x123) is in loot range; recovering it.');step(30)
assert(countCommand('/ra owncorpse go')==n)
-- SCENARIO a delayed reconnect during recovery waits for fresh native status
start();sim.hp=0;sim.nativeRecovery=true;sim.rememberCorpse=true;step();sim.hp=429;game.State='CharacterSelect';step();sim.time=sim.time+300;game.State='InGame';step(40)
assert(cp().active and sim.state=='RECOVERY_WAIT')
-- SCENARIO switching to another meta does not restore settings into its active run
start();sim.meta='Another Meta';sim.settings.FollowNavMin=0.5;step();assert(not cp().active);assert(sim.settings.FollowNavMin==0.5)
-- SCENARIO script restart resumes durable progress even before previous AF commit was consumed
start();report(1,1,1);sim.vars.te_commit=0;reloadScript();local c=cp();assert(c.probe==2 and c.sequence==1 and c.results['275_01'].outcome=='reached');assert(sim.state=='WAIT_275_02')
-- SCENARIO explicit stop survives a complete recorder reload
start();request('stop');local n=countCommand('/ra meta load Terrain Access Survey.af');reloadScript();assert(countCommand('/ra meta load Terrain Access Survey.af')==n and not cp().active)
-- SCENARIO lost native corpse memory is logged as unconfirmed rather than recovered
start();local found=false
for name,text in pairs(sim.files)do if name:find('^diagnostics%-')then for line in text:gmatch('[^\n]+')do local r=decodeNative(line);if r.event=='recovery_finished'then assert(r.confirmed==false and r.no_native_death_memory==true);found=true end end end end
assert(found)
-- SCENARIO transient filesystem failure repairs and resumes the last durable point automatically
start();sim.failWrites=true;report(1,1,1);sim.failWrites=false;step(60);assert(cp().active and cp().probe==1);assert(sim.state=='WAIT_275_01')
-- SCENARIO healthy recorder notices AF watchdog hold and rebuilds the saved approach
start();report(1,1,1);sim.vars.te_probe=2;sim.state='RECORDER_AWAIT';step(40);assert(cp().active and cp().probe==2 and sim.state=='WAIT_275_02')
-- SCENARIO Stop works during a disk failure and cancels automatic repair
start();sim.failWrites=true;report(1,1,1);request('stop');assert(not require('rynth.ai').MacroRunning);assert(sim.pvars.te_user_stop==1)
sim.failWrites=false;step(150);assert(not require('rynth.ai').MacroRunning)
-- SCENARIO watchdog restart budget survives automatic recorder reload
start();sim.pvars.te_recorder_restarts=2;reloadScript();assert(sim.pvars.te_recorder_restarts==2);request('resume');assert(sim.pvars.te_recorder_restarts==0)
-- SCENARIO recorder reload during native corpse travel never stops its macro
start();sim.hp=0;sim.rememberCorpse=true;sim.nativeRecovery=true;step();sim.hp=429;step(20)
local n=countCommand('STOP');reloadScript();assert(countCommand('STOP')==n and sim.nativeRecovery and sim.state=='RECOVERY_WAIT')
-- SCENARIO recurring monitor pauses through long combat and does not mark a terrain failure
start();sim.targets=6;step(150);assert((sim.vars.te_movement_fail or 0)==0);sim.targets=0;step(15);assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO recurring monitor allows a full buff cycle and grace between casts
start();sim.buffs=16;step(180);assert((sim.vars.te_movement_fail or 0)==0);sim.buffs=0;step(15);assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO looting pauses movement checks while a corpse is open
start();game.World.OpenContainer={Id=123};step(120);assert((sim.vars.te_movement_fail or 0)==0);game.World.OpenContainer=nil;step(15);assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO genuine idle lack of progress still triggers after activity ends
start();sim.targets=3;step(80);sim.targets=0;step(60);assert(sim.vars.te_movement_fail==1)
-- SCENARIO advancing route points counts as progress despite close positions
start();for i=1,80 do sim.navIndex=i;step()end;assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO moving for longer than one old AF sample interval is repeatedly observed
start();for i=1,100 do sim.coords=tostring(45.2+i*.005)..'|-75.1|0.38';step()end;assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO accidental 180 arrival preserves the Plateau point rather than crediting cliff failure
start();sim.targets=2;sim.coords='43.5416666667|-85.4083333333|0.0833541667';step(10);assert(cp().probe==1 and sim.vars.te_movement_fail==0 and sim.settings.EnableNavigation==0)
sim.targets=0;step(15);assert(cp().probe==1 and sim.state=='WAIT_275_01')
-- SCENARIO current status appears in the user-facing chat even while recording is repairing
start();sim.failWrites=true;report(1,1,1);local n=#sim.logs;sim.request('status');assert(#sim.logs>n)
-- SCENARIO old one-shot no-progress outcomes are preserved as unverified and scheduled for recheck
start();report(1,1,1);sim.vars.te_probe=2;sim.vars.te_reason='no_progress';report(3,2,2);local c,n=cp();c.recorderVersion=nil;sim.files[n]=encodeNative(c);reloadScript();local d=cp();assert(d.results['275_01'].outcome=='reached'and d.results['275_02'].outcome=='unverified_v02'and d.probe==2)
-- SCENARIO survey requests native portal avoidance without disabling intentional hub portals
start();assert(sim.avoidPortals and countCommand('/rnav avoid portals on')>=1)
-- SCENARIO buffing does not consume hub-arrival timeout
start();sim.state='HUB_WAIT';sim.buffs=16;step(120);assert(sim.vars.te_transport_timeout==0);sim.buffs=0;step(15);assert(sim.vars.te_transport_timeout==0)
-- SCENARIO repeated recall in the same sampled wait state gets a fresh transport budget
start();sim.state='HUB_WAIT';sim.vars.te_transport_generation=1;step(50);assert(sim.vars.te_transport_timeout==1)
sim.vars.te_transport_generation=2;sim.vars.te_transport_timeout=0;step();assert(sim.vars.te_transport_timeout==0)
-- SCENARIO ordinary start and resume preserve a player's disabled looting setting
sim.settings.EnableLooting=0;start();assert(sim.settings.EnableLooting==0);request('resume');step(20);assert(sim.settings.EnableLooting==0)
-- SCENARIO death temporarily permits native corpse looting then restores the player's choice
start();sim.settings.EnableLooting=0;sim.hp=0;sim.rememberCorpse=true;sim.nativeRecovery=true;step();assert(sim.settings.EnableLooting==1 and cp().recovery.lootBefore==0)
sim.hp=429;sim.nativeRecovery=false;send('[RynthAi] Own corpse: recovered everything (2 items).');step(25);assert(sim.settings.EnableLooting==0 and not cp().recovery)
-- SCENARIO stopping during recovery restores temporarily enabled looting
start();sim.settings.EnableLooting=0;sim.hp=0;step();assert(sim.settings.EnableLooting==1);request('stop');assert(sim.settings.EnableLooting==0 and not require('rynth.ai').MacroRunning)
