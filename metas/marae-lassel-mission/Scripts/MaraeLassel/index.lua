-- Marae Lassel mission controller: staged startup, per-character checkpoint, only bounded NPC mismatch diagnostics.
local views, ImGui, ai = require('views'), require('imgui'), require('rynth.ai')
local storage = require('storage')
local meta = 'Marae Lassel - Embedded'
local hud = views.Huds.CreateHud('MaraeLassel')
hud.Title, hud.ShowInBar, hud.Visible = 'Marae Lassel', true, true
hud.RefreshRate = 5
if Vector2 then hud.DefaultSize = Vector2(430, 350) end
local zones = {
 {level=180,name='Lowlands',keys={'0','1','2','3','4','5','6','7','8'}},
 {level=225,name='High Country',keys={'0','1','2','3','4','5','6','7','8','9'}},
 {level=275,name='Plateau',keys={'carenzi','golem','gromnie','olthoi','siraluun','ursuin','virindi','zefir'}}
}
-- BEGIN HUNTERS
local hunters = {
 [180] = {["0"]="Hea Tarekeh the Carenzi Hunter",["1"]="Hea Raukawe the Siraluun Hunter",["2"]="Hea Teruana the Gromnie Hunter",["3"]="Hea Talurea the Ursuin Hunter",["4"]="Hea Korenuad the Golem Hunter",["5"]="Hea Mereana the Olthoi Hunter",["6"]="Hea Tuhiraki the Siberite Hunter",["7"]="Hea Aroheka the Zefir Hunter",["8"]="Hea Wairena the Aun Hunter"},
 [225] = {["0"]="Hea Narekua the Ursuin Hunter",["1"]="Hea Hineroa the Golem Hunter",["2"]="Hea Orewhiti the Zefir Hunter",["3"]="Hea Pukerua the Gromnie Hunter",["4"]="Hea Kahureka the Siraluun Hunter",["5"]="Hea Marenua the Carenzi Hunter",["6"]="Hea Zanuah the Siberite Hunter",["7"]="Hea Tamaneru the Aun Hunter",["8"]="Hea Ngarekau the Mattekar Hunter",["9"]="Hea Hakerea the Olthoi Hunter"},
 [275] = {["zefir"]="Hea Rukenua the Zefir Hunter",["ursuin"]="Hea Tamarau the Ursuin Hunter",["golem"]="Hea Koruani the Golem Hunter",["gromnie"]="Hea Weneria the Gromnie Hunter",["siraluun"]="Hea Taraneki the Siraluun Hunter",["carenzi"]="Hea Kurelan the Carenzi Hunter",["virindi"]="Hea Ruatane the Virindi Hunter",["olthoi"]="Hea Varenkeh the Olthoi Hunter"},
}
-- END HUNTERS
local keys = {}
for _,z in ipairs(zones) do
 keys[#keys+1] = 'mm_area_'..z.level
 keys[#keys+1] = 'mm_attempted_'..z.level
 for _,k in ipairs(z.keys) do keys[#keys+1] = 'mm_'..z.level..'_done_'..k end
end
local function value(code) return tostring(expr(code) or '') end
local function number(code) return tonumber(expr(code)) or 0 end
local function now() return number('getunixtime[]') end
local function owns()
 local name=value('vtgetmeta[]'):gsub('\\','/'):match('([^/]+)$') or ''
 name=name:gsub('%.[aA][fF]$',''):gsub('%.[mM][eE][tT]$','')
 return name:lower()==meta:lower()
end
local settings, journal, request, startup, snapshot
local logCursor=0;local recentReplies={}
local walkGeneration=-1
local feedback=''; local inGame=false; local sampled=-1; local stopSince=nil
local nextPoll=0
local checkpointReads={}
for i,k in ipairs(keys) do checkpointReads[i]='getpvar['..k..']' end
local function save() storage.Character.Set('mission_v1',journal) end
local function saveSettings() storage.Character.Set('mission_options_v1',settings) end
local function selected() return settings.areas[1] or settings.areas[2] or settings.areas[3] end
local function preferences()
 expr('setpvar[ml_run_mode,1]')
 expr('setpvar[ml_mesh_maps,'..(settings.meshMaps and 1 or 0)..']')
 expr('setpvar[mm_hunt_cursor,'..(journal.huntCursor or 0)..']')
 expr('setpvar[mm_fallback_180,'..(settings.fallback180~=false and 1 or 0)..']')
 for i,z in ipairs(zones) do
  expr('setpvar[ml_area_'..z.level..','..(settings.areas[i] and 1 or 0)..']')
  expr('setpvar[ml_maps_'..z.level..','..(settings.maps[i] and 1 or 0)..']')
  expr('setpvar[ml_hunt_only_'..z.level..','..(settings.huntOnly[i] and 1 or 0)..']')
 end
 for _,k in ipairs(keys) do expr('setpvar['..k..','..((journal.progress or {})[k] == 1 and 1 or 0)..']') end
end
local function checkpoint()
 if not journal or not owns() or number('getvar[mm_schema]')~=1 then return end
 journal.progress=journal.progress or {}
 local changed=false
 for i,k in ipairs(keys) do
  local v=number(checkpointReads[i])==1 and 1 or 0
  if journal.progress[k]~=v then journal.progress[k]=v;changed=true end
 end
 local cursor=number('getpvar[mm_hunt_cursor]')
 if journal.huntCursor~=cursor then journal.huntCursor=cursor;changed=true end
 journal.zone=number('getvar[ml_zone]')
 if changed then save() end
end
local function restorePickup()
 if owns() and number('maraemissionapi[]')>=2 then expr('maraenavrestore[]') end
 if owns() and number('getpvar[pt_cache_pickup_held]')==1 then
  expr('rasetsetting[\x60LootChestsMacroOff\x60,getpvar[pt_cache_old_pickup]]')
  expr('setpvar[pt_cache_pickup_held,0]')
 end
end
local function fail(message)
 if owns() then restorePickup();command('/ra stop') end
 startup=nil; journal.active=false; journal.resumable=true; save();feedback=message
end
local function begin(fresh,automatic)
 if not selected() then feedback='Select at least one area.';return end
 if number('maraemissionapi[]')<2 then feedback='Install the mission-compatible RynthAi DLL from this package first.';return end
 if settings.meshMaps and number('maraemissionapi[]')<3 then feedback='Install the new mission DLL before enabling Mesh map steps.';return end
 if automatic and ai.MacroRunning and not owns() then
  feedback='Saved mission waiting: another macro is running. Stop it, then press Resume run.';return
 end
 if owns() then restorePickup() end
 if fresh or journal.finished then journal={progress={},active=false,resumable=false} end
 journal.active=true;journal.resumable=true;journal.finished=false;save()
 logCursor=0;recentReplies={}
 startup={phase='stop',deadline=now()+15}
 command('/ra stop')
 feedback='Stopping the old macro before loading '..meta..'...'
end

-- NPC status and diagnostics: only exact known hunters, never ordinary chat history.
local fs = require('filesystem').GetData()
local json = require('json')
local logWarning = ''
local function trim(s) return (s:gsub('^%s+',''):gsub('%s+$','')) end
local function sameTarget(killed,target)
 local a,b=trim(killed):lower(),trim(target):lower()
 return a==b or a==b..'s' or a==b..'es' or (b:sub(-1)=='y' and a==b:sub(1,-2)..'ies')
end
local function observeNpc(e)
 if not journal or not journal.active or game.State~='InGame' then return end
 local line=tostring(e.Message or '')
 -- Reject ordinary combat/player chat before crossing the expression bridge.
 if line:sub(1,16)~='You have killed ' and not line:find(' tells you, "',1,true) then return end
 if not owns() then return end
 local completed
 local killPrefix,killSuffix='You have killed ','! Your task is complete!'
 if line:sub(1,#killPrefix)==killPrefix and line:sub(-#killSuffix)==killSuffix then
  local split=line:find(' ',#killPrefix+1,true)
  if split then completed=line:sub(split+1,-#killSuffix-1) end
 end
 if completed then
  local zone=tostring(number('getvar[ml_zone]'))
  for key,c in pairs((journal.contracts or {})[zone] or {}) do
   if c.target and sameTarget(completed,c.target) then
    c.status='completed'
    expr('setpvar[mm_'..zone..'_done_'..key..',1]');save()
   end
  end
  return
 end
 local delimiter=' tells you, "'
 local split=line:find(delimiter,1,true)
 if not split or line:sub(-1)~='"' then return end
 local npc,body=line:sub(1,split-1),line:sub(split+#delimiter,-2)
 local zone=number('getvar[ml_zone]');local key
 for k,name in pairs(hunters[zone] or {}) do if name==npc then key=k;break end end
 if not key then return end
 local cacheKey=tostring(zone)..'/'..key
 local buffer=recentReplies[cacheKey] or {};recentReplies[cacheKey]=buffer
 line=line:sub(1,512)
 if buffer[#buffer]~=line then buffer[#buffer+1]=line;if #buffer>4 then table.remove(buffer,1) end end
 local status,target
 if body:match('^Slay %d+ ') and body:find('Return when the work is done.',1,true) then
  status='in progress'
  local start=body:find(' ',6,true)
  if start then target=body:sub(start+1,(body:find('.',start+1,true) or (#body+1))-1) end
 elseif body:match('^You have defeated %d+ of %d+ ') and body:find('The hunt is not yet finished',1,true) then
  status='in progress'
  local of=body:find(' of ',1,true);local start=of and body:find(' ',of+4,true)
  if start then target=body:sub(start+1,(body:find('.',start+1,true) or (#body+1))-1) end
 elseif body:match('^Your hunt for today is done%.') or (body:match('^Return in ') and body:find('for another hunt',1,true)) then
  status='cooldown'
 elseif body:match('^You have served the Hea well%.') then status='rewarded'
 elseif body:match('^The hunt is complete') then status='completed' end
 if not status then return end
 journal.contracts=journal.contracts or {};local z=tostring(zone)
 journal.contracts[z]=journal.contracts[z] or {}
 local c=journal.contracts[z][key] or {};journal.contracts[z][key]=c
 c.status=status;if target then c.target=target:sub(1,160) end
 expr('setvar[pt_pickup_'..key..',1]')
 if status=='cooldown' or status=='rewarded' then
  expr('setvar[pt_unavailable_'..key..',1]');expr('setpvar[mm_'..zone..'_done_'..key..',1]')
 elseif status=='completed' then expr('setpvar[mm_'..zone..'_done_'..key..',1]') end
 save()
end
game.World.OnChatText.Add(function(e)
 local ok,err=pcall(observeNpc,e)
 if not ok then logWarning='NPC observation error: '..tostring(err) end
end)
local function captureMismatch()
 local generation=number('getvar[mm_npc_log_generation]')
 if generation==0 or generation==logCursor then return end
 logCursor=generation
 local zone=number('getvar[mm_npc_log_zone]')
 local context=number('getvar[mm_npc_log_turnin]')==1 and 'turn-in' or 'pickup'
 local logs=storage.Character.Get('npc_mismatches_v1',{})
 for key in value('getvar[mm_npc_log_missing]'):gmatch('[%w_]+') do
  local npc=(hunters[zone] or {})[key]
  if npc then
   local replies=recentReplies[tostring(zone)..'/'..key] or {'No reply captured from this hunter.'}
   local signature=tostring(zone)..'/'..npc..'/'..context..'/'..table.concat(replies,'\n')
   local existing
   for _,row in ipairs(logs) do if row.zone==zone and row.hunter==npc and row.context==context and table.concat(row.replies or {},'\n')==table.concat(replies,'\n') then existing=row;break end end
   if existing then existing.lastSeen=now();existing.repeats=(existing.repeats or 1)+1
   else
    local copy={};for _,line in ipairs(replies) do copy[#copy+1]=line end
    logs[#logs+1]={zone=zone,hunter=npc,context=context,replies=copy,lastSeen=now(),repeats=1}
    if #logs>16 then table.remove(logs,1) end
   end
  end
 end
 storage.Character.Set('npc_mismatches_v1',logs)
 local server=tostring(game.ServerName or 'server'):gsub('[^%w_-]','_'):sub(1,48)
 local character=tostring(game.Character and game.Character.Name or 'character'):gsub('[^%w_-]','_'):sub(1,48)
 local filename='npc-mismatches-'..server..'-'..character..'.json'
 local ok,written,err=pcall(function()return fs.WriteText(filename,json.encode({entries=logs,limit=16},true))end)
 if not ok or not written then logWarning='Could not export NPC mismatch log: '..tostring(err or written)
 else logWarning='NPC mismatches saved to '..filename..' (latest 16 entries).' end
end

game.OnTick.Add(function()
 if game.State~='InGame' or not ai.Available then
  if inGame then inGame=false;startup=nil;snapshot=nil;recentReplies={};logCursor=0;walkGeneration=-1;nextPoll=0 end
  return
 end
 -- clock() is RynthLua's monotonic millisecond clock; no expression allocation.
 local ms=clock()
 if inGame and not request and ms<nextPoll then return end
 nextPoll=ms+(startup and 200 or 1000)
 local ok,err=pcall(function()
  if not inGame then
   inGame=true; sampled=-1
   local old=storage.Get('options_v2',{areas={true,true,true},maps={false,false,false}})
   settings=storage.Character.Get('mission_options_v1',{areas=old.areas,maps=old.maps})
   settings.huntOnly=settings.huntOnly or {false,false,false}
   journal=storage.Character.Get('mission_v1',{progress={},active=false,resumable=false})
   if journal.active then begin(false,true) end
  end
  local t=now()
  local r=request;request=nil
  if r=='stop' then
   checkpoint();journal.active=false;journal.resumable=true;save();startup=nil
   if owns() then restorePickup();command('/ra stop');command('/ub clearmotion') end
   feedback='Stopped. Progress saved; Resume run continues this mission.'
  elseif r=='start' or r=='new' then
   command('/lua autostart MaraeLassel character')
   begin(r=='new',false)
  end
  if startup then
   if t>startup.deadline then
    fail('Startup timed out during '..startup.phase..'. Expected '..meta..'; loaded '..value('vtgetmeta[]')..'.')
   elseif startup.phase=='stop' and not ai.MacroRunning then
    command('/ra meta load '..meta..'.af')
    startup={phase='load',deadline=t+15};feedback='Loading the embedded mission...'
   elseif startup.phase=='load' and owns() and not ai.MacroRunning then
    preferences()
    expr('vtsetsetting[EnableMeta,1]');expr('vtsetmetastate[Default]')
    startup={phase='arm',deadline=t+10};feedback='Preparing saved progress...'
   elseif startup.phase=='arm' and owns() and value('vtgetmetastate[]')=='Default' then
    command('/ra start')
    startup={phase='start',deadline=t+15};feedback='Starting the selected areas...'
   elseif startup.phase=='start' and owns() and ai.MacroRunning and number('getvar[mm_schema]')==1 then
    startup=nil;feedback=''
   end
  end
  local ours=owns()
  local state=ours and value('vtgetmetastate[]') or ''
  if ours and journal.active and ai.MacroRunning and not startup and
    (state=='CACHE_RETURN_WAIT' or state=='CACHE_APPROACH_WAIT' or state=='MAP_MESH_WAIT') then
   local generation=number('getvar[tm_walk_generation]')
   if generation~=walkGeneration and number('getcharvital_current[1]')>0 and number('maraerecoverybusy[]')==0 then
    walkGeneration=generation
    if state=='MAP_MESH_WAIT' then
     local accepted=number('maraemapwalk['..number('getvar[tm_mesh_heading]')..','..number('getvar[tm_mesh_step]')..']')==1
     expr('setvar[tm_walk_ack,'..(accepted and 1 or -1)..']')
    else
    local coords=value('getvar[tm_walk_target]')
    local ns,ew=coords:match('^([^|]+)|([^|]+)|')
    ns,ew=tonumber(ns),tonumber(ew)
    local accepted=ns and ew and number('maraenavsave[]')==1 and ai.GoTo(ns,ew) and number('maraenavmark[]')==1
    expr('setvar[tm_walk_ack,'..(accepted and 1 or -1)..']')
    end
   end
  else walkGeneration=-1 end
  if ours and journal.active and not startup and not ai.MacroRunning and state~='HOLD' then
   stopSince=stopSince or t
   if t-stopSince>=3 then
    checkpoint();restorePickup();journal.active=false;journal.resumable=true;save()
    feedback='Macro stopped. Progress saved; press Resume run to continue.'
   end
  else stopSince=nil end
  if ours and not startup then captureMismatch() end
  if ours and (sampled<0 or t-sampled>=5) then checkpoint();sampled=t end
  if ours and not startup and state=='HOLD' then
   local result=value('getvar[ml_outcome]')
   if journal.active then
    checkpoint();journal.active=false;journal.finished=result=='Finished';journal.resumable=not journal.finished;save()
   end
   if ai.MacroRunning then command('/ra stop') end
   feedback=value('getvar[ml_status]')
  end
  snapshot=snapshot or {}
  snapshot.running=ours and ai.MacroRunning; snapshot.other=not ours and ai.MacroRunning; snapshot.state=state
  snapshot.zone=ours and number('getvar[ml_zone]') or 0
  snapshot.detail=ours and value('getvar[ml_status]') or ''
  snapshot.loaded=value('vtgetmeta[]');snapshot.recovery=ours and number('maraerecoverybusy[]')==1
  if startup then nextPoll=math.min(nextPoll,ms+200) end
 end)
 if not ok then
  startup=nil;feedback='Controller error: '..tostring(err)
  if journal then journal.active=false;journal.resumable=true;save() end
  if owns() then restorePickup();command('/ra stop') end
 end
end)

hud.OnRender.Add(function()
 if not snapshot or not settings then ImGui.TextWrapped('Waiting for character login and RynthAi.');return end
 ImGui.TextWrapped('Marae Lassel: tasks or continuous hunting')
 ImGui.TextDisabled('Loaded: '..snapshot.loaded)
 if snapshot.running or startup then
  ImGui.Text('Area: '..tostring(snapshot.zone~=0 and snapshot.zone or 'choosing'))
  ImGui.TextWrapped(snapshot.recovery and 'Waiting for respawn / corpse recovery' or snapshot.state)
  if snapshot.detail~='0' then ImGui.TextWrapped(snapshot.detail) end
  if ImGui.Button('Stop run') then request='stop' end
 else
  ImGui.TextDisabled('Area                  Maps      Hunt only')
  for i,z in ipairs(zones) do
   local changed
   changed,settings.areas[i]=ImGui.Checkbox(z.level..' '..z.name,settings.areas[i]);if changed then saveSettings() end
   ImGui.SameLine(220,8)
   changed,settings.maps[i]=ImGui.Checkbox('##maps'..z.level,settings.maps[i]);if changed then saveSettings() end
   ImGui.SameLine(305,8)
   changed,settings.huntOnly[i]=ImGui.Checkbox('##huntOnly'..z.level,settings.huntOnly[i]);if changed then saveSettings() end
  end
  ImGui.TextWrapped('Hunt only skips tasks and repeats the nav. Maps remain optional. Task areas run first; hunt-only areas then rotate each circuit.')
  local meshChanged,meshValue=ImGui.Checkbox('Mesh map steps (needs RynthNav)',settings.meshMaps==true)
  if meshChanged then settings.meshMaps=meshValue;saveSettings() end
  local fallbackChanged,fallbackValue=ImGui.Checkbox('Fallback to 180 treasure hunting',settings.fallback180~=false)
  if fallbackChanged then settings.fallback180=fallbackValue;saveSettings() end
  if snapshot.other then ImGui.TextWrapped('Start will stop the current RynthAi macro before loading this mission.') end
  if selected() and ImGui.Button(journal.resumable and not journal.finished and 'Resume run' or 'Start run') then request='start' end
  if journal.resumable and not journal.finished then
   ImGui.SameLine();if ImGui.Button('New run') then request='new' end
  end
 end
 if feedback~='' then ImGui.TextWrapped(feedback) end
 if logWarning~='' then ImGui.TextWrapped(logWarning) end
end)
game.RegisterCommand('/marae',function() hud.Visible=not hud.Visible end)
