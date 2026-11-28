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
-- SCENARIO respawning candidates do not hide genuinely stationary travel forever
start();sim.targets=6;step(300);assert(sim.vars.te_movement_fail==1 and require('rynth.ai').MacroRunning)
-- SCENARIO recurring monitor allows a full buff cycle and grace between casts
start();sim.buffs=16;step(180);assert((sim.vars.te_movement_fail or 0)==0);sim.buffs=0;step(15);assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO active looting refreshes its bounded grace while the corpse is open
start();sim.state='HUB_WAIT';sim.settings.EnableLooting=1;game.World.OpenContainer={Id=123}
for i=1,12 do send('[RynthAi] Looted [Keep] Gold Pea');step(10)end
assert((sim.vars.te_movement_fail or 0)==0);game.World.OpenContainer=nil;step(15);assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO genuine idle lack of progress still triggers after activity ends
start();sim.targets=3;step(80);sim.targets=0;step(300);assert(sim.vars.te_movement_fail==1)
-- SCENARIO advancing route points counts as progress despite close positions
start();for i=1,80 do sim.navIndex=i;step()end;assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO moving for longer than one old AF sample interval is repeatedly observed
start();for i=1,100 do sim.coords=tostring(45.2+i*.005)..'|-75.1|0.38';step()end;assert((sim.vars.te_movement_fail or 0)==0)
-- SCENARIO accidental arrival recovers through the hub even with respawning targets
start();sim.navName='<embedded:Hub_275_01>';sim.targets=2;sim.coords='43.5416666667|-85.4083333333|0.0833541667'
sim.onResumeCoords='45.20794|-75.06323|0.384';step(10)
assert(cp().probe==1 and sim.state=='WAIT_275_01'and not cp().results['275_01'])
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
-- SCENARIO wrong-area recovery protects an 180 survey after taking the 225 portal
start();local c,n=cp();c.stage=1;sim.files[n]=encodeNative(c);sim.vars.te_stage=1;sim.navName='<embedded:Hub_180_01>';sim.targets=2;sim.coords='52.1650468826294|-80.568896484375|0.0888588825861613';sim.onResumeCoords='43.5416666667|-85.4083333333|0.0833541667';reloadScript()
assert(cp().stage==1 and cp().probe==1);sim.onResumeCoords='43.5416666667|-85.4083333333|0.0833541667';sim.targets=0;step(15);assert(cp().stage==1 and cp().probe==1 and next(cp().results)==nil)
-- SCENARIO wrong-area recovery protects a 225 survey after taking the Plateau portal
start();local c,n=cp();c.stage=2;sim.files[n]=encodeNative(c);sim.vars.te_stage=2;sim.navName='<embedded:Hub_225_01>';sim.targets=2;sim.coords='45.20794|-75.06323|0.383354';sim.onResumeCoords='53.528333|-79.802500|0.300021';reloadScript()
assert(cp().stage==2 and cp().probe==1);sim.onResumeCoords='53.528333|-79.802500|0.300021';sim.targets=0;step(15);assert(cp().stage==2 and cp().probe==1)
-- SCENARIO repeated misroutes are bounded without blacklisting the terrain
start();sim.navName='<embedded:Hub_275_01>';sim.coords='43.5416666667|-85.4083333333|0.0833541667';step(10);assert(cp().probe>1 and cp().results['275_01']and cp().results['275_01'].outcome=='deferred'and cp().routeRecoveries['275_01']==3,'probe '..cp().probe..' count '..tostring(cp().routeRecoveries and cp().routeRecoveries['275_01'])..' state '..sim.state)
-- SCENARIO native portal radius is raised to ten metres after a smaller reported setting
start();assert(sim.avoidRadius==10 and countCommand('/rnav avoid portals 10')==1)
-- SCENARIO a larger existing native portal avoidance radius is preserved
sim.avoidRadius=14;start();assert(sim.avoidRadius==14 and countCommand('/rnav avoid portals 10')==0)
-- SCENARIO a lower floor under a survey route is recovered without serializing infinity
start();sim.navName='<embedded:Hub_275_01>';sim.coords='45.20794|-75.06323|0.01';sim.targets=2;sim.onResumeCoords='45.20794|-75.06323|0.383354';step(3);assert(cp().probe==1)
-- SCENARIO an idle open container cannot block travel when ordinary looting is disabled
start();sim.settings.EnableLooting=0;game.World.OpenContainer={Id=123};step(12);assert(sim.vars.te_can_travel==1)

-- SCENARIO a stale open container stops holding travel after the bounded grace
start();sim.settings.EnableLooting=1;game.World.OpenContainer={Id=123};local resumed=false;for i=1,70 do step();if sim.vars.te_can_travel==1 then resumed=true end end;assert(resumed)
-- SCENARIO nothing-matched loot completion does not continually extend the grace
start();sim.settings.EnableLooting=1;game.World.OpenContainer={Id=123};step();for i=1,20 do send('[RynthAi] Nothing on Corpse of Siberite Fragment matches the loot profile.');step()end;assert(sim.vars.te_can_travel==1)
-- SCENARIO the running recorder exposes its exact version for short diagnostics
assert(TerrainSurveyVersion=='0.4.2')
-- SCENARIO runtime mesh route needs an exact acknowledgment even with persistent targets
sim.ignoreMesh=true;sim.targets=20;start();for i=1,5 do if sim.vars.te_mesh_request>0 then break end;step()end;assert(sim.settings.EnableNavigation==0);local req=sim.vars.te_mesh_request;assert(req>0)
sim.vars.te_mesh_ack=req-1;sim.vars.te_mesh_status=1;step();assert(sim.settings.EnableNavigation==0)
sim.ignoreMesh=false;step(3);assert(sim.settings.EnableNavigation==1 and sim.vars.te_mesh_walking==1)
-- SCENARIO missing mesh companion holds movement then stops with progress intact
sim.noMeshCompanion=true;start();step(25);assert(not cp().active and cp().probe==1 and sim.settings.EnableNavigation==0)
-- SCENARIO a blocked live raycast triggers a checked side approach
start();freshMesh();local first=sim.vars.te_mesh_request;local prior=sim.vars.te_mesh_variant;sim.vars.te_mesh_blocked=first;step(6)
assert(cp().active and sim.vars.te_mesh_request>first and sim.vars.te_mesh_variant==prior+1)
-- SCENARIO stalled movement still exhausts bounded side approaches
start();step(300);assert(sim.vars.te_mesh_variant==4 and sim.vars.te_movement_fail==1)
-- SCENARIO persistent combat candidates do not prevent the first checked travel request
sim.targets=20;step();request('new');step(14)
assert(sim.vars.te_mesh_request>0 and sim.settings.BoostNavPriority==1 and sim.settings.EnableNavigation==1)
-- SCENARIO planner rejects every approach without enabling a straight fallback
sim.meshFailure=-1;start();step(40);assert(sim.vars.te_movement_fail==1 and sim.settings.EnableNavigation==0 and cp().probe==1)
-- SCENARIO a foreign route replacing an accepted mesh walk stops the survey
start();freshMesh();sim.navName='someone-elses-route';step();assert(not cp().active and sim.settings.EnableNavigation==0)
-- SCENARIO mesh generations survive a recorder restart and cannot accept an old acknowledgment
start();step(5);local first=sim.vars.te_mesh_request;reloadScript();step(5)
assert(sim.vars.te_mesh_request>first and cp().meshGeneration>=sim.vars.te_mesh_request)
-- SCENARIO legacy controller revision is reloaded rather than adopting fixed paths
start();sim.vars.te_controller_revision=0;local n=countCommand('/ra meta load Terrain Access Survey.af');request('resume');step(20)
assert(countCommand('/ra meta load Terrain Access Survey.af')==n+1 and sim.vars.te_controller_revision==5)
-- SCENARIO companion reload forces a fresh checked request rather than adopting stale ownership
start();freshMesh();local n=sim.vars.te_mesh_request;sim.vars.te_mesh_instance=22;step(4)
assert(sim.vars.te_mesh_request>n and sim.vars.te_mesh_walking==1)
-- SCENARIO New retains increasing mesh generations while replacing survey results
start();local n=cp().meshGeneration;request('new');step(20);assert(cp().meshGeneration>n)
-- SCENARIO local return pauses for buffs but can resume with combat candidates present
start();sim.state='RETURN_WAIT_275';sim.vars.te_corridor='Return_275';sim.buffs=12;step(150);assert(sim.vars.te_return_timeout==0)
sim.buffs=0;sim.targets=20;step(20);assert(sim.vars.te_return_timeout==0 and sim.vars.te_can_travel==1)
-- SCENARIO local return eventually expires its active travel budget
start();sim.state='RETURN_WAIT_275';sim.vars.te_corridor='Return_275';step(180);assert(sim.vars.te_return_timeout==1)
-- SCENARIO final return confirms the live Plateau position before finishing
start();sim.vars.te_stage=3;sim.vars.te_probe=1;sim.state='HOME_RETURN';step(10)
assert(cp().completed and cp().home.status=='confirmed'and countCommand('/ls')==0)
-- SCENARIO final return journals and sends one recall then verifies its landing
start();sim.coords='51.074|-80.875|0.1754';sim.vars.te_stage=3;sim.state='HOME_RETURN';step(15)
assert(cp().completed and cp().home.status=='confirmed'and countCommand('/ls')==1)
-- SCENARIO final recall waits through native buffing
start();sim.coords='51.074|-80.875|0.1754';sim.vars.te_stage=3;sim.state='HOME_RETURN';sim.buffs=12;step(90)
assert(countCommand('/ls')==0 and cp().active and cp().finishing)
sim.buffs=0;step(20);assert(countCommand('/ls')==1 and cp().completed)
-- SCENARIO final return failures exhaust two attempts instead of looping or claiming arrival
start();sim.ignoreHomeRecall=true;sim.coords='51.074|-80.875|0.1754';sim.vars.te_stage=3;sim.state='HOME_RETURN';step(120)
assert(countCommand('/ls')==2 and cp().completed and cp().home.status=='unconfirmed')
-- SCENARIO home recall attempt survives a script restart without immediate replay
start();sim.ignoreHomeRecall=true;sim.coords='51.074|-80.875|0.1754';sim.vars.te_stage=3;sim.state='HOME_RETURN';step(3)
assert(countCommand('/ls')==1 and cp().home.attempts==1)
reloadScript();assert(cp().active and cp().finishing and countCommand('/ls')==1)
step(90);assert(countCommand('/ls')==2 and cp().home.status=='unconfirmed')
-- SCENARIO a failed home journal cannot dispatch a recall
start();sim.coords='51.074|-80.875|0.1754';sim.vars.te_stage=3;sim.state='HOME_RETURN';sim.failWrites=true;step(4)
assert(countCommand('/ls')==0 and sim.settings.EnableNavigation==0)
-- SCENARIO planned geometry logs once per accepted request with distinct observed evidence
start();freshMesh();local req=sim.vars.te_mesh_request;step(3);local geometry,arrivals=0,0
for name,text in pairs(sim.files)do if name:find('^diagnostics%-')then for line in text:gmatch('[^\n]+')do local d=decodeNative(line)
 if d.event=='mesh_route_geometry'and d.request==req then geometry=geometry+1;assert(#d.points==2 and d.evidence=='planned_mesh_not_proof_of_traversal')end
 if d.event=='mesh_step_reached'and d.request==req then arrivals=arrivals+1 end
end end end
assert(geometry==1 and arrivals==0)
-- SCENARIO wrong-generation geometry acknowledgment never enables movement
sim.geometryOverride='{"schema":1,"request":999,"instance":0,"units":"world_yards","points":[[1,2,3],[4,5,6]]}'
start();step(30);assert(sim.settings.EnableNavigation==0 and sim.vars.te_movement_fail==1)
-- SCENARIO ordinary loot chatter is aggregated instead of recording every corpse or item name
start();sim.targets=3
for i=1,100 do send('[RynthAi] Nothing on Corpse of private-name matches the loot profile.');step()end
local summaries=0
for name,text in pairs(sim.files)do if name:find('^diagnostics%-')then assert(not text:find('private%-name'));for line in text:gmatch('[^\n]+')do if decodeNative(line).event=='loot_activity_summary'then summaries=summaries+1 end end end end
assert(summaries>=1 and summaries<=3)
-- SCENARIO unchanged buffing samples checkpoint at most once a minute
start();sim.buffs=12;step(10);local initial=sim.writes;step(120);assert(sim.writes-initial<=3,'unnecessary checkpoint writes: '..(sim.writes-initial))
-- SCENARIO old companion protocol is rejected rather than silently losing geometry
start();sim.noMeshCompanion=true;sim.vars.te_mesh_schema=1;step(25);assert(not cp().active and cp().probe==1)
-- SCENARIO another native navigation trip stops survey dispatch without stopping that trip
start();freshMesh();sim.vars.te_external_nav=1;step();assert(not cp().active and sim.vars.te_external_nav==1 and countCommand('/rnav stop')==0)
-- SCENARIO final return never recalls over another native navigation trip
start();sim.coords='51.074|-80.875|0.1754';sim.vars.te_stage=3;sim.state='HOME_RETURN';sim.vars.te_external_nav=1;step(5)
assert(not cp().active and countCommand('/ls')==0)
-- SCENARIO corrupted other-character checkpoint cannot inherit the previous character run
start();local c=cp();game.Character.Name='Other';local h=7;local name='DreamWeave|Other';for i=1,#name do h=(h*131+name:byte(i))%2147483647 end
local path='checkpoint-Other-'..string.format('%08x',h)..'.json';sim.files[path]='invalid json';step(3)
assert(sim.files[path]=='invalid json','another character checkpoint must not be overwritten with Samm progress')
-- SCENARIO a checked runtime detour outside the predicted corridor is not mistaken for a wrong camp
start();local n,e,h=45.8,-75.4,91.2
sim.geometryFactory=function(request,instance)return encodeNative({schema=1,request=request,instance=instance,units='world_yards',points={{6444,35316,91.2},{e*240+24468,n*240+24468,h},{sim.vars.te_mesh_east*240+24468,sim.vars.te_mesh_north*240+24468,sim.vars.te_mesh_height}}})end
freshMesh();sim.coords=n..'|'..e..'|'..(h/240);sim.targets=1;step(5)
assert(cp().active and cp().probe==1 and not cp().routeRecoveries)
-- SCENARIO whole-client resource samples are bounded and do not gate navigation
start();sim.targets=3;sim.vars.te_resources=encodeNative({schema=1,utc_unix=sim.time,scope='whole_client',managed_heap_bytes=900000000,private_bytes=1700000000,virtual_bytes=3000000000,cpu_seconds=100})
step(110);assert(cp().active);local count=0
for name,text in pairs(sim.files)do if name:find('^diagnostics%-')then for line in text:gmatch('[^\n]+')do if decodeNative(line).event=='client_resources'then count=count+1 end end end end
assert(count>=1 and count<=2 and sim.vars.te_movement_fail==0)
-- SCENARIO malformed optional resource telemetry never fails the recorder
start();sim.vars.te_resources='broken json';sim.targets=3;step(70);assert(cp().active and sim.vars.te_logger_active==1)
-- SCENARIO moving through populated packs alternates native travel and bounded fight loot windows
sim.targets=20;start(12);local before=sim.vars.te_mesh_request
for i=1,25 do sim.coords=(45.2+i*.0025)..'|-75.1|0.38';step()end
assert(sim.settings.BoostNavPriority==0 and sim.settings.EnableNavigation==0,'must release native combat and loot after travel')
local resumed=false;for i=1,31 do step();if sim.vars.te_mesh_request>before and sim.settings.BoostNavPriority==1 and sim.settings.EnableNavigation==1 then resumed=true end end;assert(sim.targets==20 and resumed,'must travel again without empty radar')
-- SCENARIO stopping from the fight loot window restores an originally enabled priority
sim.settings.BoostNavPriority=1;sim.targets=20;start(12)
for i=1,25 do sim.coords=(45.2+i*.0025)..'|-75.1|0.38';step()end
assert(sim.settings.BoostNavPriority==0);request('stop');assert(sim.settings.BoostNavPriority==1)
-- SCENARIO stopping from travel restores the original disabled priority
start();assert(sim.settings.BoostNavPriority==1);request('stop');assert(sim.settings.BoostNavPriority==0)
-- SCENARIO buffs still pause a travel burst while native busy heals retain native ownership
start();sim.buffs=12;step(30);assert(sim.settings.EnableNavigation==0 and sim.vars.te_can_travel==0)
sim.buffs=0;sim.busy=1;step(5);assert(sim.settings.EnableNavigation==1 and sim.vars.te_can_travel==1)
