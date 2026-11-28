-- TerrainSurvey v0.3.1. Stock RynthLua/RynthAi only; streamed local diagnostics.
local ai,fsmod,json,info=require('rynth.ai'),require('filesystem'),require('json'),require('rynthlua')
local fs=fsmod.GetData()
local META='Terrain Access Survey'
local VERSION=2
local SAMPLE_SECONDS=5
local LOG_CHARS=512*1024 -- eight ring slots; worst-case UTF-8 stays bounded
local SLOTS=8
local cp,key,phase,deadline,nextPoll,nextSample,previousState,wasInGame=nil,nil,'',0,0,0,'',false
local requests,events={},{}
local errorText=''
local repairAttempts=0
local repairAt=0
local movement=nil
local quietUntil=0
local stableSince=0
local lastPosition=nil
local transport=nil
local lootUntil=0
local settings={FollowNavMin=0.5,NavResumeNearest=0,StopMacroOnDeath=0,DeathRecallMode=0,LootOwnCorpse=1,TravelToOwnCorpse=1,EnableMeta=1}
local zones={[0]=275,[1]=180,[2]=225}
local planned={}
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
end
local function log(kind,data)
 if not cp then return end
 data=data or {};data.event=kind;data.utc_unix=num('getunixtime[]');data.run=cp.run
 data.stage=cp.stage;data.probe=cp.probe;data.attempt=cp.attempt
 local line=json.encode(data)..'\n'
 cp.logSlot=cp.logSlot or 1;cp.logChars=cp.logChars or 0
 local filename='diagnostics-'..key..'-'..cp.logSlot..'.jsonl'
 if cp.logChars+#line>LOG_CHARS then
  cp.logSlot=cp.logSlot%SLOTS+1;cp.logChars=0
  filename='diagnostics-'..key..'-'..cp.logSlot..'.jsonl'
  local ok,err=fs.WriteText(filename,'');if not ok then error('Log rotation failed: '..tostring(err))end
 end
 local ok,err=fs.AppendText(filename,line)
 if not ok then error('Diagnostic append failed: '..tostring(err))end
 cp.logChars=cp.logChars+#line
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
 errorText='';movement=nil;transport=nil;stableSince=0
 if game.State~='InGame' or game.Character.InPortalSpace or not ai.Available then notice('Wait until logged in and out of portal space.');return end
 if ai.MacroRunning and not owns()then notice('Stop the other macro/controller first.');return end
 local t=num('getunixtime[]')
 local priorSettings=cp and cp.oldSettings
 if fresh or not cp then
  cp={schema=VERSION,server=game.ServerName,character=game.Character.Name,run=key..'-'..t,started=t,active=false,stage=0,probe=1,attempt=0,sequence=0,results={},deaths=0,logSlot=1,logChars=0,oldSettings=priorSettings}
  local copied,copyErr=fs.WriteText('planned-'..key..'.json',raw);assert(copied,tostring(copyErr))
  -- An explicit new run replaces this character's previous bounded diagnostic set.
  for i=1,SLOTS do local ok,err=fs.WriteText('diagnostics-'..key..'-'..i..'.jsonl','');assert(ok,tostring(err))end
 elseif cp.stage>=3 or cp.completed then notice('Survey complete. Use /terrainsurvey new for another pass.');return end
 cp.recorderVersion=3;cp.active=true;cp.completed=false;save();ai.Command('/rnav avoid portals on');log(fresh and 'new_run' or 'resume_requested',{server=cp.server,character=cp.character})
 if not fresh and owns() and ai.MacroRunning and num('getvar[te_logger_schema]')==3 then
  -- Rejoin the live survey without stopping native corpse travel.
  applySettings()
  for n,v in pairs({te_logger_schema=3,te_logger_active=1,te_logger_heartbeat=t,te_stage=cp.stage,te_probe=cp.probe,te_attempt=cp.attempt,te_seq=cp.sequence,te_commit=0,te_finish=0,te_finish_ack=0,te_recovery_busy=1,te_movement_fail=0,te_can_travel=0,te_location_stable=0,te_transport_timeout=0,te_transport_generation=0})do set(n,v)end
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
 movement=nil;transport=nil
 if cp.recovery then return end
 cp.recovery={started=t,reason=reason,settleUntil=t+8,statusWaitSince=t,queryAt=0,status='',statusAt=0,sentGo=false,done=false,ended=false}
 if reason=='death'then cp.deaths=cp.deaths+1;enableRecoveryLoot()end
 log('recovery_started',{reason=reason,coordinates=tostring(val('getplayercoordinates[]'))});save()
 set('te_recovery_busy',1)
 if owns()then val('vtsetsetting[EnableNavigation,0]');val('vtsetmetastate[RECOVERY_WAIT]')end
end
local function drainEvents(t)
 local q=events;events={}
 for _,e in ipairs(q)do
  local s=e.message
  if s then
   if not cp.recovery and s:find('^%[RynthAi%] Your corpse is at ') and not s:find(', died ',1,true) then startRecovery(t,'death')end
   if s:find('^%[RynthAi%] Loot')or s:find('^%[RynthAi%] Nothing on Corpse')then lootUntil=t+15 end
   log('native_diagnostic',{message=s})
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
 local reason=s.hp<=0 and 'death' or s.portaling and 'portal' or s.targets>0 and 'combat' or s.busy>0 and 'native_busy' or ((buff==nil or buff==true or buff==1)and s.buffs_due>0)and 'buffing' or (s.open_container~=0 or t<lootUntil)and 'looting' or nil
 if reason then quietUntil=t+8 end
 local jumped=lastPosition and s.north and s.east and math.sqrt((s.north-lastPosition.north)^2+(s.east-lastPosition.east)^2)*240>80
 if s.portaling or s.hp<=0 or not s.north or not s.east then stableSince=0 elseif stableSince==0 or jumped then stableSince=t end
 if s.north and s.east then lastPosition={north=s.north,east=s.east}end
 set('te_location_stable',stableSince>0 and t-stableSince>=3 and 1 or 0)
 set('te_can_travel',not reason and t>=quietUntil and 1 or 0)
 return reason or (t<quietUntil and 'settling' or nil)
end
local function monitor(t,s,pause)
 if s.state=='HUB_WAIT' or s.state:match('^LAND_%d+$')then
  if not transport or transport.state~=s.state or transport.generation~=s.transport_generation then transport={state=s.state,generation=s.transport_generation,elapsed=0,last=t}end
  if not pause then transport.elapsed=transport.elapsed+math.min(2,math.max(0,t-transport.last))end
  transport.last=t;set('te_transport_timeout',transport.elapsed>=(s.state=='HUB_WAIT'and 45 or 90)and 1 or 0)
 else transport=nil;set('te_transport_timeout',0)end
 if not s.state:match('^WAIT_%d+_%d+$') or not s.north or not s.east then movement=nil;return end
 -- A different known camp while surveying Plateau is a misroute, not a cliff failure.
 if cp.stage==0 and not s.portaling then
  for _,at in ipairs({{43.5416666667,-85.4083333333,20.005},{53.5283333458,-79.802500025,72.005}})do
   if math.sqrt((s.north-at[1])^2+(s.east-at[2])^2)*240<100 and math.abs(s.world_height-at[3])<15 then
    if not movement or not movement.wrongArea then
     movement={state=s.state,wrongArea=true};log('unexpected_camp',{coordinates=s.coordinates,target=s.target});save();val('vtsetsetting[EnableNavigation,0]')
    end
    if not pause then log('misroute_hub_recovery',{target=s.target});save();val('vtsetmetastate[RESUME]');movement=nil end
    return
   end
  end
 end
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
local function tick()
 if clock()/1000<nextPoll then return end
 nextPoll=clock()/1000+1
 if game.State~='InGame' or not ai.Available then
  if wasInGame and cp and cp.active then log('disconnected');save()end
  wasInGame=false;return
 end
 local newKey=safeKey(game.ServerName,game.Character.Name)
 if newKey~=key then
  key=newKey;cp=readCheckpoint();phase='';previousState='';events={};errorText=''
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
   for n,v in pairs({te_logger_schema=3,te_logger_active=1,te_logger_heartbeat=t,te_stage=cp.stage,te_probe=cp.probe,te_attempt=cp.attempt,te_seq=cp.sequence,te_commit=0,te_finish=0,te_finish_ack=0,te_recovery_busy=1,te_movement_fail=0,te_can_travel=0,te_location_stable=0,te_transport_timeout=0,te_transport_generation=0})do set(n,v)end
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
 local s=snapshot();local state=s.state
 local pause=travelReadiness(t,s);s.pause_reason=pause
 if not cp.recovery then monitor(t,s,pause)end
 if t>=nextSample or state~=previousState then
  log(state~=previousState and 'state_change' or 'sample',s);nextSample=t+SAMPLE_SECONDS;previousState=state;cp.lastSample=s;save()
 end
 if s.hp<=0 and not cp.recovery then startRecovery(t,'death')end
 if cp.recovery then recoveryTick(t,s);return end
 if state=='RECOVERY_WAIT'then startRecovery(t,'death');return end
 if state=='LOGGER_LOST' or state=='RECORDER_WINDOW' or state=='RECORDER_RETRY' or state=='RECORDER_AWAIT' then log('watchdog_rejoined');save();begin(false);return end
 if state=='RECORDER_STOPPED'or state=='RECORDER_FAILED'then deactivate('Recorder restart budget exhausted; progress retained');return end
 if not ai.MacroRunning then deactivate('Macro stopped by player or native safety setting');return end
 if state:find('^COMMIT_')then commit(t,state);return end
 if state=='FINISH_COMMIT'then
  cp.stage=num('getvar[te_stage]');cp.probe=num('getvar[te_probe]')
  cp.completed=cp.stage>=3;cp.active=false;log('survey_ended',{completed=cp.completed});save();set('te_finish_ack',1)
  -- AF will stop itself on its next pulse; restore settings on the following tick.
  phase='finish';deadline=t+5;return
 end
 local stage,probe,attempt=num('getvar[te_stage]'),num('getvar[te_probe]'),num('getvar[te_attempt]')
 if stage~=cp.stage or probe~=cp.probe or attempt~=cp.attempt then cp.stage=stage;cp.probe=probe;cp.attempt=attempt;save()end
end
local function recorderFailure(err)
 pcall(function()log('recorder_interrupted',{error=tostring(err):sub(1,600)})end)
 errorText=tostring(err);repairAttempts=0;repairAt=clock()/1000+15;phase='repair'
 notice('Recorder interrupted: '..errorText..'. Survey movement paused; automatic repair will retry.')
 if ai.Available then pcall(function()if owns()then set('te_logger_active',0);val('vtsetsetting[EnableNavigation,0]')end end)end
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
     if ai.Available then pcall(function()if owns()then set('te_logger_active',0);ai.Stop()end end)end
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
  if ai.Available and (not ai.MacroRunning or num('getunixtime[]')>=deadline)then if ai.MacroRunning then ai.Stop()end;restoreSettings();save();phase='';notice('Survey ended. Logs and checkpoint: '..tostring(info.DataFolder))end
  return
 end
 local ok,err=pcall(tick)
 if not ok then recorderFailure(err)end
end
game.RegisterCommand('/terrainsurvey',function(args)
 local r=tostring(args or ''):lower():match('^%s*(%S+)')or'status'
 if r=='status'then local last=cp and cp.lastSample or {};notice('v0.3.1; state '..tostring(last.state or phase)..'; wait '..tostring(last.pause_reason or 'none')..'; '..(cp and cp.active and 'Active' or 'Stopped')..'; phase '..phase..'; '..(cp and ('zone '..tostring(zones[cp.stage])..', point '..cp.probe)or'no checkpoint')..'; logs: '..tostring(info.DataFolder));return end
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
notice('Ready. /terrainsurvey new starts a recorded pass; /terrainsurvey resume continues. Logs: '..tostring(info.DataFolder))
