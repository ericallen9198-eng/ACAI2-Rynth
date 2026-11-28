-- TerrainSurvey v0.4.1. Stock RynthLua/RynthAi only; streamed local diagnostics.
TerrainSurveyVersion='0.4.1' -- readable through /lua execin for support
local ai,fsmod,json,info=require('rynth.ai'),require('filesystem'),require('json'),require('rynthlua')
local fs=fsmod.GetData()
local META='Terrain Access Survey'
local VERSION=2
local SAMPLE_SECONDS=5
local CHECKPOINT_SECONDS=60
local lastCheckpointAt=0
local returning=nil
local lootActivity={count=0,nextFlush=0}
local nextResourceSample=0
local LOG_CHARS=512*1024 -- eight ring slots; worst-case UTF-8 stays bounded
local SLOTS=8
local cp,key,phase,deadline,nextPoll,nextSample,previousState,wasInGame=nil,nil,'',0,0,0,'',false
local requests,events={},{}
local errorText=''
local repairAttempts=0
local repairAt=0
local movement=nil
local mesh=nil
local meshMissingAt=nil
local quietUntil=0
local stableSince=0
local lastPosition=nil
local transport=nil
local lootUntil=0
local observedContainer=0
local containerOpenedAt=-1000000000
local lootDoneAt=-1000000000
local avoidanceRequested=false
local settings={FollowNavMin=0.5,NavResumeNearest=0,StopMacroOnDeath=0,DeathRecallMode=0,LootOwnCorpse=1,TravelToOwnCorpse=1,EnableMeta=1}
local zones={[0]=275,[1]=180,[2]=225}
local planned={}
local corridorText,corridorErr=fsmod.GetScript().ReadText('route-index.json')
assert(corridorText,'Missing route-index.json: '..tostring(corridorErr))
local corridors=json.decode(corridorText).corridors
assert(type(corridors)=='table','Invalid route-index.json')
local raw,readErr=fsmod.GetScript().ReadText('probe-index.json')
assert(raw,'Missing probe-index.json: '..tostring(readErr))
for _,p in ipairs(json.decode(raw).probes) do planned[p.id]=p end
local function val(e)return ai.Evaluate(e)end
local function num(e)return tonumber(val(e)) or 0 end
local function set(n,v)
 if type(v)=='number' then return val('setvar['..n..','..tostring(v)..']') end
 error('Only numeric survey variables may be assigned by the recorder')
end
local function owns()
 local n=tostring(val('vtgetmeta[]')):gsub('\\','/'):match('([^/]+)$') or ''
 return n:gsub('%.[aA][fF]$',''):lower()==META:lower()
end
local function notice(s)print('[TerrainSurvey] '..s);if chat then chat('[TerrainSurvey] '..s)end end
local function safeKey(server,character)
 local s=server..'|'..character;local h=7
 for i=1,#s do h=(h*131+s:byte(i))%2147483647 end
 return character:gsub('[^%w_-]','_'):sub(1,16)..'-'..string.format('%08x',h)
end
local function checkpointFile()return 'checkpoint-'..key..'.json'end
local function save()
 cp.updated=num('getunixtime[]')
 local ok,err=fs.WriteText(checkpointFile(),json.encode(cp,true))
 if not ok then error('Checkpoint write failed: '..tostring(err)) end
 lastCheckpointAt=cp.updated
end
local function log(kind,data)
 if not cp then return end
 data=data or {};data.software='0.4.1';data.event=kind;data.utc_unix=num('getunixtime[]');data.run=cp.run
 data.stage=cp.stage;data.probe=cp.probe;data.attempt=cp.attempt
 local line=json.encode(data)..'\n'
 cp.logSlot=cp.logSlot or 1;cp.logChars=cp.logChars or 0
 local filename='diagnostics-'..key..'-'..cp.logSlot..'.jsonl'
 local rotated=false
 if cp.logChars+#line>LOG_CHARS then
  cp.logSlot=cp.logSlot%SLOTS+1;cp.logChars=0;rotated=true
  filename='diagnostics-'..key..'-'..cp.logSlot..'.jsonl'
  local ok,err=fs.WriteText(filename,'');if not ok then error('Log rotation failed: '..tostring(err))end
 end
 local ok,err=fs.AppendText(filename,line)
 if not ok then error('Diagnostic append failed: '..tostring(err))end
 cp.logChars=cp.logChars+#line
 if rotated then save()end
end
local function enableRecoveryLoot()
 if not cp or not cp.recovery then return end
 local r=cp.recovery
 if r.lootBefore~=nil then return end
 local before=ai.GetSetting('EnableLooting')
 if before==false or before==0 then
  r.lootBefore=before;save() -- remember the user's choice before changing it
  assert(ai.SetSetting('EnableLooting',1),'Cannot enable native own-corpse recovery looting')
  log('recovery_loot_enabled')
 end
end
local function restoreRecoveryLoot()
 if not cp or not cp.recovery or cp.recovery.lootBefore==nil then return end
 local r=cp.recovery;local live=ai.GetSetting('EnableLooting')
 if live==1 or live==true then ai.SetSetting('EnableLooting',r.lootBefore)end
 r.lootBefore=nil
end
local function restoreSettings()
 if not cp or not cp.oldSettings then return end
 for n,v in pairs(cp.oldSettings)do
  local live=ai.GetSetting(n)
  local a=type(live)=='boolean' and (live and 1 or 0) or tonumber(live)
  if a==settings[n]then ai.SetSetting(n,v)end
 end
 cp.oldSettings=nil
end
local function deactivate(reason)
 if not cp then return end
 local owned=ai.Available and owns()
 cp.active=false;phase=''
 -- A Stop must work even if the log disk is full. Native intent also blocks watchdog restart.
 pcall(function()val('setpvar[te_user_stop,1]')end)
 if owned then pcall(function()set('te_logger_active',0);ai.Stop();restoreRecoveryLoot();restoreSettings()end)end
 local ok,err=pcall(function()log('stopped',{reason=reason});save()end)
 notice(reason..'. Progress retained. Logs: '..tostring(info.DataFolder))
 if not ok then notice('Stop is active, but its disk record could not be written: '..tostring(err))end
end
local function applySettings()
 cp.oldSettings=cp.oldSettings or {}
 for n,v in pairs(settings)do
  local old=ai.GetSetting(n);assert(old~=nil,'Required native setting unavailable: '..n)
  if cp.oldSettings[n]==nil then cp.oldSettings[n]=old end
 end
 save() -- preserve original settings before any edits
 for n,v in pairs(settings)do assert(ai.SetSetting(n,v),'Cannot set '..n)end
end
local function begin(fresh)
 errorText='';movement=nil;mesh=nil;meshMissingAt=nil;transport=nil;returning=nil;stableSince=0;avoidanceRequested=false
 if game.State~='InGame' or game.Character.InPortalSpace or not ai.Available then notice('Wait until logged in and out of portal space.');return end
 if ai.MacroRunning and not owns()then notice('Stop the other macro/controller first.');return end
 local t=num('getunixtime[]')
 lootActivity={count=0,nextFlush=t+60}
 local priorSettings=cp and cp.oldSettings
 local priorMeshGeneration=cp and cp.meshGeneration or 0
 if fresh or not cp then
  cp={schema=VERSION,server=game.ServerName,character=game.Character.Name,run=key..'-'..t,started=t,active=false,stage=0,probe=1,attempt=0,sequence=0,results={},deaths=0,logSlot=1,logChars=0,oldSettings=priorSettings,meshGeneration=priorMeshGeneration}
  local copied,copyErr=fs.WriteText('planned-'..key..'.json',raw);assert(copied,tostring(copyErr))
  -- An explicit new run replaces this character's previous bounded diagnostic set.
  for i=1,SLOTS do local ok,err=fs.WriteText('diagnostics-'..key..'-'..i..'.jsonl','');assert(ok,tostring(err))end
 elseif cp.completed or (cp.stage>=3 and not cp.finishing)then notice('Survey complete. Use /terrainsurvey new for another pass.');return end
 local copied,copyErr=fs.WriteText('corridors-'..key..'.json',corridorText);assert(copied,tostring(copyErr))
 if cp.home then cp.home.last=t end
 cp.recorderVersion=3;cp.active=true;cp.completed=false;save();ai.Command('/rnav avoid portals on');log(fresh and 'new_run' or 'resume_requested',{server=cp.server,character=cp.character})
 if not fresh and owns() and ai.MacroRunning and num('getvar[te_logger_schema]')==3 and num('getvar[te_controller_revision]')==5 then
  -- Rejoin the live survey without stopping native corpse travel.
  applySettings()
  for n,v in pairs({te_controller_revision=5,te_return_timeout=0,te_mesh_request=0,te_mesh_walking=0,te_mesh_ack=0,te_mesh_status=0,te_logger_schema=3,te_logger_active=1,te_logger_heartbeat=t,te_stage=cp.stage,te_probe=cp.probe,te_attempt=cp.attempt,te_seq=cp.sequence,te_commit=0,te_finish=0,te_finish_ack=0,te_recovery_busy=1,te_movement_fail=0,te_can_travel=0,te_location_stable=0,te_transport_timeout=0,te_transport_generation=0})do set(n,v)end
  val('vtsetsetting[EnableNavigation,0]');val('vtsetmetastate[RECOVERY_WAIT]');phase='arm';deadline=t+15;return
 end
 if owns() and ai.MacroRunning then ai.Stop()end
 ai.Command('/ra meta load '..META..'.af');phase='load';deadline=t+20
end
local function readCheckpoint()
 local path=checkpointFile()
 if not fs.FileExists(path)then return nil end
 local text,err=fs.ReadText(path);assert(text,tostring(err));assert(#text<256*1024,'Checkpoint exceeds expected size')
 local d=json.decode(text)
 assert(d.schema==VERSION and d.server==game.ServerName and d.character==game.Character.Name,'Checkpoint identity/version mismatch')
 assert(type(d.stage)=='number' and d.stage>=0 and d.stage<=3 and d.stage%1==0,'Invalid saved stage')
 assert(type(d.probe)=='number' and d.probe>=1 and d.probe<=13 and d.probe%1==0,'Invalid saved probe')
 assert(d.attempt==0 or d.attempt==1,'Invalid saved retry')
 assert(type(d.sequence)=='number' and type(d.results)=='table','Invalid saved progress')
 if d.home then
  assert(type(d.home)=='table'and type(d.home.attempts)=='number'and d.home.attempts>=0 and d.home.attempts<=2 and d.home.attempts%1==0,'Invalid saved home-return attempts')
  assert(type(d.home.elapsed)=='number'and d.home.elapsed>=0 and d.home.elapsed<=47,'Invalid saved home-return timer')
 end
 if d.recorderVersion~=3 then
  -- v0.2's one-shot monitor could misclassify combat/buffing as no progress.
  -- Keep the old evidence but recheck its unresolved points rather than crediting it.
  local stages={[275]=0,[180]=1,[225]=2};local earliest=d.stage*100+d.probe
  for id,r in pairs(d.results)do
   if (r.outcome=='unresolved' or r.outcome=='hub_retry')and r.reason=='no_progress'then
    r.priorOutcome=r.outcome;r.outcome='unverified_v02';r.needs_recheck=true;d.completed=false
    local z,i=id:match('^(%d+)_(%d+)$');local st=stages[tonumber(z)]
    if st then earliest=math.min(earliest,st*100+tonumber(i))end
   end
  end
  if earliest<d.stage*100+d.probe then d.stage=math.floor(earliest/100);d.probe=earliest%100;d.attempt=0 end
  d.recorderVersion=3
 end
 local tail=fs.ReadText('diagnostics-'..key..'-'..(d.logSlot or 1)..'.jsonl')
 if tail then assert(#tail<=LOG_CHARS,'Diagnostic slot exceeds its bound');d.logChars=#tail end
 return d
end
local function snapshot()
 local coords=tostring(val('getplayercoordinates[]'))
 local a,b,c=coords:match('^([^|]+)|([^|]+)|([^|]+)$')
 a,b,c=tonumber(a),tonumber(b),tonumber(c)
 local zone=zones[cp.stage];local id=zone and (zone..'_'..string.format('%02d',cp.probe))or''
 local target=planned[id];local dist=nil
 if a and b and target then dist=math.sqrt((a-target.approach.north)^2+(b-target.approach.east)^2)*240 end
 local ok,nav=pcall(function()return json.decode(tostring(val('ranav[]')))end)
 local container=game.World.OpenContainer
 return {transport_generation=num('getvar[te_transport_generation]'),nav=ok and nav or {},open_container=container and tonumber(container.Id)or 0,coordinates=coords,north=a,east=b,world_height=c and c*240 or nil,cell=num('getplayerlandcell[]'),target=id,distance_yards=dist,targets=num('ratargetcount[]'),busy=num('getbusystate[]'),buffs_due=num('rabuffsdue[0]'),hp=num('getcharvital_current[1]'),state=tostring(val('vtgetmetastate[]')),macro_running=ai.MacroRunning,portaling=game.Character.InPortalSpace}
end
local function startRecovery(t,reason)
 movement=nil;mesh=nil;transport=nil;set('te_mesh_request',0);set('te_mesh_walking',0)
 if cp.recovery then return end
 cp.recovery={started=t,reason=reason,settleUntil=t+8,statusWaitSince=t,queryAt=0,status='',statusAt=0,sentGo=false,done=false,ended=false}
 if reason=='death'then cp.deaths=cp.deaths+1;enableRecoveryLoot()end
 log('recovery_started',{reason=reason,coordinates=tostring(val('getplayercoordinates[]'))});save()
 set('te_recovery_busy',1)
 if owns()then val('vtsetsetting[EnableNavigation,0]');val('vtsetmetastate[RECOVERY_WAIT]')end
end
local function drainEvents(t)
 local q=events;events={}
 if lootActivity.count>0 and t>=lootActivity.nextFlush then log('loot_activity_summary',{messages=lootActivity.count});lootActivity.count=0;lootActivity.nextFlush=t+60 end
 for _,e in ipairs(q)do
  local s=e.message
  if s then
   local metres=tonumber(s:match('^%[RynthNav%] avoid: portals on, ([%d%.]+) m'))
   if metres and metres<10 and not avoidanceRequested then avoidanceRequested=true;ai.Command('/rnav avoid portals 10');log('native_portal_clearance_requested',{previous_metres=metres,new_metres=10})end
   if not cp.recovery and s:find('^%[RynthAi%] Your corpse is at ') and not s:find(', died ',1,true) then startRecovery(t,'death')end
   if s:find('^%[RynthAi%] Nothing on Corpse')then lootUntil=0;lootDoneAt=t
   elseif s:find('^%[RynthAi%] Loot')then lootUntil=t+15 end
   if s:find('^%[RynthAi%] Loot')or s:find('^%[RynthAi%] Nothing on Corpse')then lootActivity.count=lootActivity.count+1 else log('native_diagnostic',{message=s})end
   if cp.recovery then
    local r=cp.recovery
    if s:find('^%[RynthAi%] Your corpse is at ') and not s:find(', recovered',1,true) and not s:find('nothing dropped',1,true) then enableRecoveryLoot()end
    if s:find('^%[RynthAi%] Recover My Corpse:')then
     r.status=s:match('; travel: (.-)%. /ra owncorpse')or'';r.statusAt=t
    end
    if s:find('no death remembered this session',1,true)then r.noCorpseMemory=true end
    if s:find('recovered everything',1,true) or s:find('there was nothing on it',1,true) or s:find('no death remembered this session',1,true) or s:find('nothing dropped',1,true) or s:find(', recovered',1,true) or s:find('already recovered',1,true) or s=='You have retained all your items. You do not need to recover your corpse!' then r.done=true end
    if s:find('Own corpse: travel back ended:',1,true)then
     r.ended=true;r.settleUntil=t+10
     -- Reaching loot range is not a completed item recovery.
     if s:find('recovering it',1,true)then r.ended=false;r.lootStarted=true end
    end
   end
  else log('connection',{state=e.state})end
 end
end
local function recoveryTick(t,s)
 local r=cp.recovery
 set('te_recovery_busy',1)
 if s.hp<=0 or s.portaling then r.settleUntil=t+8;r.statusAt=0;return end
 if not ai.MacroRunning then deactivate('Native macro stopped during recovery; progress retained');return end
 if t>=r.queryAt then ai.Command('/ra owncorpse');r.queryAt=t+15 end
 if r.statusAt==0 and t-(r.statusWaitSince or r.started)>120 then deactivate('Native corpse status could not be read; recovery cannot be coordinated');return end
 if t<r.settleUntil or r.statusAt<t-20 then return end
 if r.status~='off'then
  if t-r.started>1080 and not r.sentStop then
   log('recovery_timeout',{reason='native recovery still active after 18 minutes'});ai.Command('/ra owncorpse stop');r.sentStop=true;save()
  end
  return
 end
 if not r.done and not r.ended and not r.sentGo and not r.lootStarted then
  enableRecoveryLoot();ai.Command('/ra owncorpse go');r.sentGo=true;r.settleUntil=t+10;save();return
 end
 if not r.done and not r.ended and t-r.started<960 then return end
 log('recovery_finished',{confirmed=r.done and not r.noCorpseMemory,no_native_death_memory=r.noCorpseMemory or false,reason=r.reason,native_ended=r.ended})
 restoreRecoveryLoot();cp.recovery=nil;save();set('te_recovery_busy',0)
 -- Rebuild the unfinished point's approach from the known hub, never restart point 1.
 set('te_stage',cp.stage);set('te_probe',cp.probe);set('te_attempt',cp.attempt);set('te_seq',cp.sequence)
 val('vtsetmetastate[RESUME]')
end
local function commit(t,state)
 local seq=num('getvar[te_seq]');local kind=num('getvar[te_report]')
 local id=zones[cp.stage]..'_'..string.format('%02d',cp.probe)
 if seq>cp.sequence then
  assert(kind>=1 and kind<=3 and seq==cp.sequence+1,'Invalid report sequence')
  local result=kind==1 and 'reached' or kind==2 and 'hub_retry' or 'unresolved'
  local record={outcome=result,utc_unix=t,coordinates=tostring(val('getvar[te_report_coords]')),reason=tostring(val('getvar[te_reason]')),attempt=cp.attempt}
  log('probe_result',{id=id,result=record})
  cp.results[id]=record;cp.sequence=seq
  if kind==2 then cp.attempt=1 else cp.probe=cp.probe+1;cp.attempt=0 end
  save() -- disk checkpoint must succeed before AF advances
 end
 set('te_commit',seq)
end
local function travelReadiness(t,s)
 local buff=ai.GetSetting('EnableBuffing')
 local loot=ai.GetSetting('EnableLooting');local lootEnabled=loot~=false and loot~=0
 if s.open_container~=observedContainer then observedContainer=s.open_container;containerOpenedAt=t end
 local recentContainer=s.open_container~=0 and containerOpenedAt>lootDoneAt and t-containerOpenedAt<15
 local reason=s.hp<=0 and 'death' or s.portaling and 'portal' or s.targets>0 and 'combat' or s.busy>0 and 'native_busy' or ((buff==nil or buff==true or buff==1)and s.buffs_due>0)and 'buffing' or (lootEnabled and (recentContainer or t<lootUntil))and 'looting' or nil
 if reason then quietUntil=t+8 end
 local jumped=lastPosition and s.north and s.east and math.sqrt((s.north-lastPosition.north)^2+(s.east-lastPosition.east)^2)*240>80
 if s.portaling or s.hp<=0 or not s.north or not s.east then stableSince=0 elseif stableSince==0 or jumped then stableSince=t end
 if s.north and s.east then lastPosition={north=s.north,east=s.east}end
 set('te_location_stable',stableSince>0 and t-stableSince>=3 and 1 or 0)
 set('te_can_travel',not reason and t>=quietUntil and 1 or 0)
 return reason or (t<quietUntil and 'settling' or nil)
end
local function corridorDistance(s)
 local name=tostring(val('getvar[te_corridor]'))
 if not corridors[name]then name=tostring(s.nav.name or ''):match('^<embedded:(.-)>$')or tostring(s.nav.name or '')end
 local points=corridors[name];if not points or #points<2 then return nil end
 local best=math.huge;local closest=math.huge
 local sx,sy=s.east*240+24468,s.north*240+24468
 local function measure(path,world)
  for i=2,#path do
   local a,b=path[i-1],path[i]
   local x,y,z,bx,by,bz
   if world then x,y,z,bx,by,bz=a[1],a[2],a[3],b[1],b[2],b[3]
   else x,y,z,bx,by,bz=a.east*240+24468,a.north*240+24468,a.z,b.east*240+24468,b.north*240+24468,b.z end
   local dx,dy,ax,ay=bx-x,by-y,sx-x,sy-y
   local length=dx*dx+dy*dy
   local f=length>0 and math.max(0,math.min(1,(ax*dx+ay*dy)/length))or 0
   local flat=math.sqrt((ax-dx*f)^2+(ay-dy*f)^2)
   closest=math.min(closest,flat)
   if math.abs(s.world_height-(z+(bz-z)*f))<=15 then best=math.min(best,flat)end
  end
 end
 measure(points,false)
 -- A valid live detour may leave the original predicted corridor. Keep only
 -- this state's current accepted plan, never a growing route history.
 if mesh and mesh.state==s.state and mesh.geometry then measure(mesh.geometry,true)end
 return best==math.huge and closest or best,best==math.huge
end
local meshTick
local function monitor(t,s,pause,meshPause)
 if s.state:match('^RETURN_WAIT_')then
  if not returning or returning.state~=s.state then returning={state=s.state,last=t,elapsed=0}end
  if not pause and not meshPause then returning.elapsed=returning.elapsed+math.min(2,math.max(0,t-returning.last))end
  returning.last=t;set('te_return_timeout',returning.elapsed>=120 and 1 or 0)
 else returning=nil;set('te_return_timeout',0)end
 if s.state=='HUB_WAIT' or s.state:match('^LAND_%d+$')then
  if not transport or transport.state~=s.state or transport.generation~=s.transport_generation then transport={state=s.state,generation=s.transport_generation,elapsed=0,last=t}end
  if not pause then transport.elapsed=transport.elapsed+math.min(2,math.max(0,t-transport.last))end
  transport.last=t;set('te_transport_timeout',transport.elapsed>=(s.state=='HUB_WAIT'and 45 or 90)and 1 or 0)
 else transport=nil;set('te_transport_timeout',0)end
 if not(s.state:match('^WAIT_%d+_%d+$')or s.state:match('^RETURN_WAIT_%d+$'))or not s.north or not s.east then movement=nil;return end
 -- All-sector route guard: catch a different camp even after wandering away from its landing.
 local away,wrongLevel=corridorDistance(s)
 if away and (away>55 or wrongLevel) and not s.portaling then
  if not movement or not movement.wrongArea then
   movement={state=s.state,wrongArea=true};log('off_corridor',{coordinates=s.coordinates,target=s.target,distance_yards=away,wrong_level=wrongLevel,nav=s.nav.name});save();val('vtsetsetting[EnableNavigation,0]')
   notice('Outside the intended survey route; holding navigation until combat/buffs settle, then returning through the hub.')
  end
  if not pause then
   cp.routeRecoveries=cp.routeRecoveries or {};local id=s.target;local count=(cp.routeRecoveries[id]or 0)+1;cp.routeRecoveries[id]=count
   log('misroute_hub_recovery',{target=id,attempt=count,distance_yards=away})
   if count>=3 and planned[id]then
    cp.results[id]={outcome='deferred',reason='repeated_route_displacement',does_not_prove_impassable=true,utc_unix=t,coordinates=s.coordinates};cp.probe=cp.probe+1;cp.attempt=0
    log('misroute_deferred',{target=id});set('te_probe',cp.probe);set('te_attempt',0)
   end
   save();val('vtsetmetastate[RESUME]');movement=nil
  end
  return
 end
 pause=pause or meshPause
 if movement and movement.wrongArea then movement=nil end
 if s.state:match('^RETURN_WAIT_')then movement=nil;return end
 if not movement or movement.state~=s.state then movement={state=s.state,last=t,elapsed=0,stationary=0,at=s,trace={},sampleAt=t+6,index=s.nav.index}end
 local m=movement;local dt=math.min(2,math.max(0,t-m.last));m.last=t
 if pause then
  if m.pause~=pause then log('movement_paused',{reason=pause,state=s.state})end
  m.pause=pause;m.stationary=0;m.at=s;m.trace={};m.sampleAt=t+6;m.index=s.nav.index;return
 end
 if m.pause then log('movement_resumed',{prior=m.pause,state=s.state});m.pause=nil;m.at=s;m.stationary=0;dt=0 end
 m.elapsed=m.elapsed+dt
 local distance=math.sqrt((s.north-m.at.north)^2+(s.east-m.at.east)^2)*240
 if distance>=2 or s.nav.index~=m.index then if s.nav.index~=m.index then m.trace={}end;m.at=s;m.stationary=0;m.index=s.nav.index else m.stationary=m.stationary+dt end
 local reason=nil
 if m.stationary>=12 and mesh and mesh.phase=='walking' and mesh.variant<4 then
  mesh.variant=mesh.variant+1;mesh.phase='retry';mesh.retryAt=t+2;set('te_mesh_request',0);set('te_mesh_walking',0);val('vtsetsetting[EnableNavigation,0]');m.stationary=0;m.at=s
  log('mesh_replan',{reason='stationary',variant=mesh.variant,target=s.target,coordinates=s.coordinates})
 end
 if m.stationary>=40 then reason='no_progress' elseif m.elapsed>=300 then reason='active_travel_budget' end
 if t>=m.sampleAt then
  local repeats=0
  for _,p in ipairs(m.trace)do if math.sqrt((s.north-p.north)^2+(s.east-p.east)^2)*240<3 then repeats=repeats+1 end end
  if repeats>=3 then reason=reason or 'oscillation'end
  if #m.trace>=12 then table.remove(m.trace,1)end
  m.trace[#m.trace+1]={north=s.north,east=s.east};m.sampleAt=t+6
 end
 if reason and not m.reported then
  m.reported=true;log('movement_failure',{reason=reason,coordinates=s.coordinates,target=s.target,active_seconds=m.elapsed,stationary_seconds=m.stationary,nav=s.nav});save()
  -- Fixed strings only; no chat/player text is evaluated.
  val('setvar[te_reason,`'..reason..'`]');set('te_movement_fail',1)
 end
end
meshTick=function(t,s,pause)
 local waiting=s.state:match('^WAIT_%d+_%d+$')or s.state:match('^RETURN_WAIT_%d+$')
 if not waiting then
  if mesh then set('te_mesh_request',0);set('te_mesh_walking',0);mesh=nil end
  meshMissingAt=nil;return
 end
 local name=tostring(val('getvar[te_corridor]'))
 local points=corridors[name]
 if not points then
  val('vtsetsetting[EnableNavigation,0]')
  deactivate('Survey controller is older than v0.4.1; install both the meta and Lua files, then Resume');return 'controller_mismatch'
 end
 local age=t-num('getvar[te_mesh_heartbeat]')
 local fresh=num('getvar[te_mesh_schema]')==2 and age<=5 and age>=-5
 if not fresh then
  val('vtsetsetting[EnableNavigation,0]');set('te_mesh_walking',0)
  if mesh then mesh.phase='idle';mesh.goal=nil end;set('te_mesh_request',0)
  meshMissingAt=meshMissingAt or t
  if t-meshMissingAt>=15 then deactivate('TerrainMesh v0.4.1 companion is unavailable; install the complete update, enable ACAI2.Terrain and Resume')end
  return 'waiting_for_mesh_companion'
 end
 meshMissingAt=nil
 if num('getvar[te_external_nav]')==1 then deactivate('Another native navigation trip is active; survey progress retained');return 'external_navigation'end
 if not s.north or not s.east or not s.world_height then return 'waiting_for_pose'end
 local function flat(p)return math.sqrt((s.north-p.north)^2+(s.east-p.east)^2)*240 end
 if not mesh or mesh.state~=s.state or mesh.corridor~=name then
  local best,first=math.huge,2
  for i=2,#points do
   local a,b=points[i-1],points[i]
   local dx,dy=(b.east-a.east)*240,(b.north-a.north)*240
   local ax,ay=(s.east-a.east)*240,(s.north-a.north)*240
   local length=dx*dx+dy*dy
   local f=length>0 and math.max(0,math.min(1,(ax*dx+ay*dy)/length))or 0
   local d=math.sqrt((ax-dx*f)^2+(ay-dy*f)^2)
   if math.abs(s.world_height-(a.z+(b.z-a.z)*f))<=15 and d<best then best=d;first=i end
  end
  mesh={state=s.state,corridor=name,cursor=first,variant=0,phase='idle',instance=num('getvar[te_mesh_instance]')}
 end
 if mesh.instance~=num('getvar[te_mesh_instance]')then
  log('mesh_companion_reloaded');mesh.instance=num('getvar[te_mesh_instance]');mesh.phase='idle';mesh.goal=nil
  val('vtsetsetting[EnableNavigation,0]');set('te_mesh_request',0);set('te_mesh_walking',0)
 end
 if pause then
  val('vtsetsetting[EnableNavigation,0]');set('te_mesh_walking',0);set('te_mesh_request',0)
  mesh.phase='idle';mesh.goal=nil;return pause
 end
 local away,wrongLevel=corridorDistance(s)
 if (away and (away>55 or wrongLevel))or(movement and movement.wrongArea)then val('vtsetsetting[EnableNavigation,0]');set('te_mesh_request',0);set('te_mesh_walking',0);return 'outside_corridor'end
 if mesh.goal and flat(mesh.goal)<=2 and math.abs(s.world_height-mesh.goal.z)<4 then
  log('mesh_step_reached',{request=mesh.generation,coordinates=s.coordinates,start=mesh.start,goal=mesh.goal,variant=mesh.variant,evidence='observed_endpoint_arrival'})
  mesh.cursor=math.min(#points,mesh.goalIndex+1);mesh.goal=nil;mesh.phase='idle';mesh.variant=0
  val('vtsetsetting[EnableNavigation,0]');set('te_mesh_walking',0);set('te_mesh_request',0)
 end
 local function retry(reason,penalty)
  val('vtsetsetting[EnableNavigation,0]');set('te_mesh_request',0);set('te_mesh_walking',0)
  if penalty then mesh.variant=mesh.variant+1 end
  log('mesh_replan',{reason=reason,detail=tostring(val('getvar[te_mesh_error]')),variant=mesh.variant,target=s.target,coordinates=s.coordinates})
  if mesh.variant>4 then
   mesh.phase='failed';val('setvar[te_reason,`mesh_approaches_exhausted`]');set('te_movement_fail',1)
   if s.state:match('^RETURN_WAIT_')then val('vtsetmetastate[NEXT_SECTOR]')end
  else mesh.phase='retry';mesh.retryAt=t+3 end
 end
 if mesh.phase=='failed'then return 'mesh_approaches_exhausted'end
 if mesh.phase=='retry'then if t<mesh.retryAt then return 'mesh_retry_delay'else mesh.phase='idle'end end
 if mesh.phase=='planning'then
  if num('getvar[te_mesh_ack]')==mesh.generation then
   local status=num('getvar[te_mesh_status]')
   if status~=1 then retry('planner_status_'..status,status~=-2);return 'mesh_replanning'end
   local expected=tostring(val('getvar[te_mesh_name]'))
   if s.nav.name~=expected or (s.nav.points or 0)<2 then retry('route_load_mismatch',true);return 'mesh_replanning'end
   if math.sqrt((s.north-mesh.start.north)^2+(s.east-mesh.start.east)^2)*240>3 or math.abs(s.world_height-mesh.start.z)>4 then retry('moved_while_planning',false);return 'mesh_replanning'end
   local encoded=tostring(val('getvar[te_mesh_geometry]'))
   local ok,geometry=pcall(function()assert(#encoded<=12288);return json.decode(encoded)end)
   if not ok or type(geometry)~='table' or geometry.schema~=1 or geometry.request~=mesh.generation or geometry.instance~=mesh.instance or geometry.units~='world_yards' or type(geometry.points)~='table' or #geometry.points<2 or #geometry.points>256 then retry('geometry_ack_mismatch',true);return 'mesh_replanning'end
   for _,p in ipairs(geometry.points)do
    if type(p)~='table'or #p~=3 then retry('invalid_mesh_geometry',true);return 'mesh_replanning'end
    for _,v in ipairs(p)do if type(v)~='number'or v~=v or math.abs(v)>100000 then retry('invalid_mesh_geometry',true);return 'mesh_replanning'end end
   end
   log('mesh_route_geometry',{request=mesh.generation,instance=mesh.instance,variant=mesh.variant,units='world_yards',points=geometry.points,evidence='planned_mesh_not_proof_of_traversal'})
   mesh.geometry=geometry.points
   mesh.phase='walking';set('te_mesh_walking',1);val('vtsetsetting[EnableNavigation,1]')
   log('mesh_route_accepted',{request=mesh.generation,variant=mesh.variant,goal=mesh.goal,corners=num('getvar[te_mesh_points]'),route=expected})
  elseif t-mesh.requested>=8 then retry('planner_ack_timeout',true);return 'mesh_replanning'
  else return 'mesh_planning'end
 end
 if mesh.phase=='walking'then
  if num('getvar[te_mesh_blocked]')==mesh.generation then retry('live_raycast_blocked',true);return 'mesh_replanning'end
  if s.nav.name~=tostring(val('getvar[te_mesh_name]'))then
   val('vtsetsetting[EnableNavigation,0]');deactivate('The survey route was replaced while walking; progress retained');return 'route_replaced'
  end
  if s.nav.finished then retry('ended_short_of_step',true);return 'mesh_replanning'end
  return
 end
 if mesh.phase=='idle'then
  if not mesh.goal then
   local i=mesh.cursor
   while i<#points and flat(points[i])<3 do i=i+1 end
   local distance=flat(points[i])
   while i<#points and distance<40 do
    local a,b=points[i],points[i+1];distance=distance+math.sqrt((a.north-b.north)^2+(a.east-b.east)^2)*240;i=i+1
   end
   mesh.goal=points[i];mesh.goalIndex=i
   if flat(mesh.goal)<2 and math.abs(s.world_height-mesh.goal.z)<4 then return 'awaiting_arrival_commit'end
  end
  cp.meshGeneration=(cp.meshGeneration or 0)+1;save()
  mesh.generation=cp.meshGeneration;mesh.start={north=s.north,east=s.east,z=s.world_height};mesh.requested=t;mesh.phase='planning'
  val('vtsetsetting[EnableNavigation,0]');set('te_mesh_walking',0);set('te_mesh_ack',0);set('te_mesh_status',0)
  set('te_mesh_north',mesh.goal.north);set('te_mesh_east',mesh.goal.east);set('te_mesh_height',mesh.goal.z);set('te_mesh_variant',mesh.variant)
  log('mesh_requested',{request=mesh.generation,variant=mesh.variant,start=mesh.start,goal=mesh.goal,corridor=name})
  set('te_mesh_request',mesh.generation)
  return 'mesh_planning'
 end
end
local function finalHomeTick(t,s,pause)
 if num('getvar[te_external_nav]')==1 then deactivate('Another native navigation trip is active; final return remains saved');return end
 cp.finishing=true;cp.stage=3;cp.probe=1
 if not cp.home then cp.home={attempts=0,elapsed=0,last=t,status='preparing'};save()end
 local h=cp.home
 if s.hp>0 and not s.portaling and s.north and s.east and s.world_height and num('getvar[te_location_stable]')==1
  and math.sqrt((s.north-45.2079477958333)^2+(s.east+75.0632305458333)^2)*240<15 and math.abs(s.world_height-92.005)<8 then
  h.status='confirmed';h.coordinates=s.coordinates;h.confirmed_at=t;h.last=t
  log('home_return_confirmed',{coordinates=s.coordinates,attempts=h.attempts});save()
  val('vtsetmetastate[FINISH]');return
 end
 local dt=math.min(2,math.max(0,t-(h.last or t)));h.last=t
 if h.status=='awaiting'then
  if not pause then h.elapsed=h.elapsed+dt end
  if h.elapsed<45 then if t-lastCheckpointAt>=CHECKPOINT_SECONDS then save()end;return end
  log('home_return_timeout',{attempt=h.attempts,active_seconds=h.elapsed,coordinates=s.coordinates})
  h.status='preparing';save()
 end
 if pause then return end
 if h.attempts>=2 then
  h.status='unconfirmed';h.coordinates=s.coordinates
  log('home_return_unconfirmed',{attempts=h.attempts,coordinates=s.coordinates});save()
  notice('Inspection pass ended, but two /ls attempts did not confirm the Plateau hub. Results retained.')
  val('vtsetmetastate[FINISH]');return
 end
 h.attempts=h.attempts+1;h.elapsed=0;h.last=t;h.sent_at=t;h.status='awaiting'
 log('home_return_requested',{attempt=h.attempts,coordinates=s.coordinates});save() -- durable attempt before dispatch
 ai.Command('/ls')
end
local function tick()
 if clock()/1000<nextPoll then return end
 nextPoll=clock()/1000+1
 if game.State~='InGame' or not ai.Available then
  if wasInGame and cp and cp.active then log('disconnected');save()end
  wasInGame=false;return
 end
 local newKey=safeKey(game.ServerName,game.Character.Name)
 if newKey~=key then
  key=newKey;cp=nil;mesh=nil;movement=nil;lastPosition=nil;lootActivity={count=0,nextFlush=0}
  cp=readCheckpoint();phase='';previousState='';events={};errorText=''
  if cp and cp.active then phase='rejoin';deadline=num('getunixtime[]')+8 end
 elseif not wasInGame and cp and cp.active then phase='rejoin';deadline=num('getunixtime[]')+8 end
 wasInGame=true
 if #requests==0 and cp and cp.active and num('getpvar[te_user_stop]')==1 then deactivate('Saved player/native stop remains active');return end
 local t=num('getunixtime[]')
 local req=requests;requests={}
 for _,r in ipairs(req)do
  if r=='new' or r=='resume' or r=='start' then
   val('setpvar[te_user_stop,0]');val('setpvar[te_recorder_window,'..t..']');val('setpvar[te_recorder_restarts,0]');begin(r=='new')
  elseif r=='stop'then deactivate('Stopped by player')
  else notice((cp and cp.active and 'Active' or 'Stopped')..'; '..(cp and ('zone '..tostring(zones[cp.stage])..', point '..cp.probe)or'no survey yet')..'; logs: '..tostring(info.DataFolder))end
 end
 if not cp or not cp.active then return end
 if t-cp.started>14*3600 then deactivate('14-hour survey window ended');return end
 if phase=='rejoin'then
  if game.Character.InPortalSpace or t<deadline then return end
  if ai.MacroRunning and not owns()then deactivate('Another macro owns the client after reconnect');return end
  if cp.recovery then cp.recovery.statusAt=0;cp.recovery.queryAt=0;cp.recovery.statusWaitSince=t;cp.recovery.settleUntil=t+8 end
  begin(false);return
 end
 if phase=='load'then
  if t>deadline then deactivate('Survey meta did not load; check the install folder');return end
  if owns() and not ai.MacroRunning then
   applySettings()
   for n,v in pairs({te_controller_revision=5,te_return_timeout=0,te_mesh_request=0,te_mesh_walking=0,te_mesh_ack=0,te_mesh_status=0,te_logger_schema=3,te_logger_active=1,te_logger_heartbeat=t,te_stage=cp.stage,te_probe=cp.probe,te_attempt=cp.attempt,te_seq=cp.sequence,te_commit=0,te_finish=0,te_finish_ack=0,te_recovery_busy=1,te_movement_fail=0,te_can_travel=0,te_location_stable=0,te_transport_timeout=0,te_transport_generation=0})do set(n,v)end
   val('vtsetmetastate[RECOVERY_WAIT]');ai.Command('/ra start');phase='arm';deadline=t+15
  end
  return
 end
 if not owns()then deactivate('A different meta was selected');return end
 set('te_logger_heartbeat',t)
 if phase=='arm'then
  if ai.MacroRunning then
   phase='';log('running',{stage=cp.stage,probe=cp.probe});save()
   -- Reconnect may have happened during corpse recovery. Query native owner before travel.
   startRecovery(t,'resume_check')
  elseif t>deadline then deactivate('Macro start was not confirmed')end
  return
 end
 drainEvents(t)
 if t>=nextResourceSample then
  nextResourceSample=t+60
  local encoded=tostring(val('getvar[te_resources]'))
  if #encoded<=2048 then
   local ok,resource=pcall(function()return json.decode(encoded)end)
   if ok and type(resource)=='table'and resource.schema==1 and resource.scope=='whole_client'and type(resource.utc_unix)=='number'and math.abs(t-resource.utc_unix)<=120 then
    local valid=true
    for _,k in ipairs({'managed_heap_bytes','private_bytes','virtual_bytes','cpu_seconds'})do
     local v=resource[k];if type(v)~='number'or v~=v or v<0 or v>1e15 then valid=false end
    end
    if valid then resource.sampled_at=resource.utc_unix;log('client_resources',resource)end
   end
  end
 end
 local s=snapshot();local state=s.state
 s.mesh_phase=mesh and mesh.phase or '';s.mesh_variant=mesh and mesh.variant or 0;s.mesh_request=num('getvar[te_mesh_request]')
 local pause=travelReadiness(t,s);s.pause_reason=pause
 if not cp.recovery then local meshPause=meshTick(t,s,pause);if not cp.active then return end;monitor(t,s,pause,meshPause)end
 if t>=nextSample or state~=previousState then
  local changed=state~=previousState;s.evidence='observed_pose_only'
  log(changed and 'state_change' or 'sample',s);nextSample=t+SAMPLE_SECONDS;previousState=state;cp.lastSample=s
  if changed or t-lastCheckpointAt>=CHECKPOINT_SECONDS then save()end
 end
 if s.hp<=0 and not cp.recovery then startRecovery(t,'death')end
 if cp.recovery then recoveryTick(t,s);return end
 if state=='RECOVERY_WAIT'then startRecovery(t,'death');return end
 if state=='LOGGER_LOST' or state=='RECORDER_WINDOW' or state=='RECORDER_RETRY' or state=='RECORDER_AWAIT' then log('watchdog_rejoined');save();begin(false);return end
 if state=='RECORDER_STOPPED'or state=='RECORDER_FAILED'then deactivate('Recorder restart budget exhausted; progress retained');return end
 if not ai.MacroRunning then deactivate('Macro stopped by player or native safety setting');return end
 if state=='HOME_RETURN'then finalHomeTick(t,s,pause);return end
 if state:find('^COMMIT_')then commit(t,state);return end
 if state=='FINISH_COMMIT'then
  cp.stage=num('getvar[te_stage]');cp.probe=num('getvar[te_probe]')
  cp.completed=cp.stage>=3;cp.finishing=false;cp.active=false;log('survey_ended',{completed=cp.completed,home=cp.home and cp.home.status or 'not_requested'});save();set('te_finish_ack',1)
  -- AF will stop itself on its next pulse; restore settings on the following tick.
  phase='finish';deadline=t+5;return
 end
 local stage,probe,attempt=num('getvar[te_stage]'),num('getvar[te_probe]'),num('getvar[te_attempt]')
 if stage~=cp.stage or probe~=cp.probe or attempt~=cp.attempt then cp.stage=stage;cp.probe=probe;cp.attempt=attempt;if stage>=3 then cp.finishing=true end;save()end
end
local function recorderFailure(err)
 pcall(function()log('recorder_interrupted',{error=tostring(err):sub(1,600)})end)
 errorText=tostring(err);repairAttempts=0;repairAt=clock()/1000+15;phase='repair'
 notice('Recorder interrupted: '..errorText..'. Survey movement paused; automatic repair will retry.')
 if cp and ai.Available and cp.server==game.ServerName and cp.character==game.Character.Name then pcall(function()if owns()then set('te_logger_active',0);val('vtsetsetting[EnableNavigation,0]')end end)end
end
local function guardedTick()
 if cp and game.State=='InGame' and safeKey(game.ServerName,game.Character.Name)==key then
  for _,r in ipairs(requests)do if r=='stop'then requests={};deactivate('Stopped by player');return end end
 end
 if phase=='repair'then
  if game.State~='InGame' or not ai.Available then return end
  if key and safeKey(game.ServerName,game.Character.Name)~=key then phase='';cp=nil;key=nil;errorText=''
  elseif clock()/1000<repairAt then return
  else
   repairAttempts=repairAttempts+1
   local ok,err=pcall(function()
    assert(key,'No bound character for diagnostic repair')
    local healthy,why=fs.WriteText('recorder-health-'..key..'.txt','TerrainSurvey filesystem check\n');assert(healthy,tostring(why))
    healthy,why=fs.AppendText('recorder-health-'..key..'.txt','append ok\n');assert(healthy,tostring(why))
    -- Discard any in-memory change whose durable commit failed. Never credit it.
    local restored=readCheckpoint();assert(restored,'No valid durable checkpoint to resume')
    cp=restored;errorText=''
    local tail=fs.ReadText('diagnostics-'..key..'-'..(cp.logSlot or 1)..'.jsonl');if tail then cp.logChars=#tail end
    log('recorder_repaired',{attempts=repairAttempts});save()
    phase='rejoin';deadline=num('getunixtime[]')+2
   end)
   if not ok then
    errorText=tostring(err)
    if repairAttempts>=3 then
     notice('Recorder repair failed three times: '..errorText..'. Progress retained for manual resume.')
     if cp and ai.Available and cp.server==game.ServerName and cp.character==game.Character.Name then pcall(function()if owns()then set('te_logger_active',0);ai.Stop()end end)end
     if cp then cp.active=false;pcall(save)end
     phase='';return
    end
    repairAt=clock()/1000+(repairAttempts==1 and 30 or 60)
   end
   return
  end
 end
 if phase=='finish' and cp and not cp.active then
  if ai.Available and not owns()then phase='';return end
  if ai.Available and (not ai.MacroRunning or num('getunixtime[]')>=deadline)then if ai.MacroRunning then ai.Stop()end;restoreSettings();save();phase='';notice('Survey ended; home '..tostring(cp.home and cp.home.status or 'not requested')..'. Logs and checkpoint: '..tostring(info.DataFolder))end
  return
 end
 local ok,err=pcall(tick)
 if not ok then recorderFailure(err)end
end
game.RegisterCommand('/terrainsurvey',function(args)
 local r=tostring(args or ''):lower():match('^%s*(%S+)')or'status'
 if r=='status'then local last=cp and cp.lastSample or {};notice('v0.4.1; state '..tostring(last.state or phase)..'; mesh '..tostring(last.mesh_phase or 'waiting')..'; wait '..tostring(last.pause_reason or 'none')..'; '..(cp and cp.active and 'Active' or 'Stopped')..'; phase '..phase..'; '..(cp and ('zone '..tostring(zones[cp.stage])..', point '..cp.probe)or'no checkpoint')..'; logs: '..tostring(info.DataFolder));return end
 if #requests<4 then requests[#requests+1]=r end
end)
game.OnTick.Add(guardedTick)
game.World.OnChatText.Add(function(e)
 if not cp or not cp.active or #events>=32 then return end
 local s=tostring(e.Message or '')
 if s:find('^%[RynthAi%] Own corpse:')or s:find('^%[RynthAi%] Your corpse is at ')or s:find('^%[RynthAi%] Recover My Corpse:')or s:find('^Terrain survey:')or s:find('^%[TERRAIN')or s:find('^%[TerrainSurvey%] Hub recall:')or s:find('^%[RynthAi%] Loot')or s:find('^%[RynthAi%] Nothing on Corpse')or s:find('^%[RynthNav%] avoid')or s:find('^Nav: ')or s:find('^%[RynthAi%] Nav:')or s=='You have retained all your items. You do not need to recover your corpse!'then
  events[#events+1]={message=s:sub(1,600)}
 end
end)
notice('v0.4.1 ready. /terrainsurvey new starts a recorded pass; /terrainsurvey resume continues. Logs: '..tostring(info.DataFolder))
