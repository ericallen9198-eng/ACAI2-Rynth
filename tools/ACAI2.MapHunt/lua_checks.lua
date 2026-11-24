local count=0
local function check(ok,m)assert(ok,m);count=count+1;print('PASS: '..m)end
step();click('Start / Resume')
check(ai.MacroRunning and sim.meta=='MapHunt' and sim.vars.mh_route_ack==1,'staged startup loads the correct meta and validates the real hunt route')
check(sim.stores.run_v1.active,'active intent persists per character')
sim.metaState='MAP_MESH_WAIT';sim.vars.mh_walk_token=1;sim.vars.tm_mesh_heading=45;sim.vars.tm_mesh_step=130
step(8);check(sim.meshWalks==1 and sim.vars.tm_walk_ack==1,'one mesh request per distinct generation, never once per tick')
sim.vars.mh_walk_token=2;sim.meshFail=true;step(2)
check(sim.meshWalks==2 and sim.vars.tm_walk_ack==-1,'mesh refusal becomes an explicit failed acknowledgment')
sim.metaState='CACHE_APPROACH_WAIT';sim.vars.mh_walk_token=3;sim.vars.tm_walk_target='51.2|-84.0|0.2';step(2)
check(sim.walks==1 and math.abs(sim.walkNS-(51.2-2.5/240))<0.000001,'cache target stops 2.5 yards short of its center')
sim.metaState='DIRECT_WAIT';sim.vars.mh_walk_token=4;sim.vars.tm_mesh_heading=90
local before=#sim.sent;step(4)
local faces,forward=0,0
for i=before+1,#sim.sent do if sim.sent[i]:match('^/ub face ') then faces=faces+1 end;if sim.sent[i]=='/ub setmotion Forward 1' then forward=forward+1 end end
check(faces==1 and forward==1,'non-mesh movement faces first, settles, then starts once')
click('Stop');check(not ai.MacroRunning and not sim.stores.run_v1.active,'Stop cancels intent and motion without restarting a macro')
local commands=#sim.sent;sim.state='LoggedOut';step(3);check(#sim.sent==commands,'no native actions while logged out')
sim.state='InGame';step(4);check(not ai.MacroRunning,'explicitly stopped runs do not auto-resume on login')
click('Start / Resume');sim.metaState='ENCOUNTER';sim.pvars.mh_tm_dig_active=1;sim.vars.tm_cache_anchor='51.1|-84.0|0.2';sim.vars.tm_event_deadline=10000;sim.vars.tm_doubloon_before=7;step(6)
check(sim.stores.run_v1.event and sim.stores.run_v1.event.deadline==10000,'checkpoint stores a bounded encounter anchor and absolute deadline')
sim.state='LoggedOut';step();ai.MacroRunning=false;sim.state='InGame';step(8)
check(ai.MacroRunning and sim.metaState=='ENCOUNTER' and sim.vars.tm_event_deadline==10000,'reconnect near the dig resumes the event without renewing its budget')
sim.meta='Other';ai.MacroRunning=true;sim.state='LoggedOut';step();sim.state='InGame';commands=#sim.sent;step(5)
check(#sim.sent==commands,'automatic resume does not interrupt a different running macro')
sim.meta='MapHunt';sim.metaState='HUNT';sim.pvars.mh_tm_dig_active=0;step()
local beforeCalls=sim.calls
step(2500,0.002)
check(sim.calls-beforeCalls<150,'high-frequency game ticks are throttled before expression allocation')
local beforeRender=sim.calls
for i=1,100 do sim.render() end
check(sim.calls==beforeRender,'unchanged HUD drawing reads cached snapshots without expression polling')
ai.MacroRunning=false;step(2)
check(not sim.stores.run_v1.active and sim.sent[#sim.sent]=='/ub clearmotion','native macro stop also clears movement and active intent')
return count
