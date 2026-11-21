-- Marae Lassel mission controller: staged startup, per-character checkpoint, no raw-chat history.
local views, ImGui, ai = require('views'), require('imgui'), require('rynth.ai')
local storage = require('storage')
local meta = 'Marae Lassel - Embedded'
local hud = views.Huds.CreateHud('MaraeLassel')
hud.Title, hud.ShowInBar, hud.Visible = 'Marae Lassel', true, true
if Vector2 then hud.DefaultSize = Vector2(430, 350) end
local zones = {
 {level=180,name='Lowlands',keys={'0','1','2','3','4','5','6','7','8'}},
 {level=225,name='High Country',keys={'0','1','2','3','4','5','6','7','8','9'}},
 {level=275,name='Plateau',keys={'carenzi','golem','gromnie','olthoi','siraluun','ursuin','virindi','zefir'}}
}
local keys = {}
for _,z in ipairs(zones) do
 keys[#keys+1] = 'mm_area_'..z.level
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
local feedback=''; local inGame=false; local sampled=-1; local stopSince=nil
local function save() storage.Character.Set('mission_v1',journal) end
local function saveSettings() storage.Character.Set('mission_options_v1',settings) end
local function selected() return settings.areas[1] or settings.areas[2] or settings.areas[3] end
local function preferences()
 expr('setpvar[ml_run_mode,1]')
 for i,z in ipairs(zones) do
  expr('setpvar[ml_area_'..z.level..','..(settings.areas[i] and 1 or 0)..']')
  expr('setpvar[ml_maps_'..z.level..','..(settings.maps[i] and 1 or 0)..']')
 end
 for _,k in ipairs(keys) do expr('setpvar['..k..','..((journal.progress or {})[k] == 1 and 1 or 0)..']') end
end
local function checkpoint()
 if not journal or not owns() or number('getvar[mm_schema]')~=1 then return end
 journal.progress=journal.progress or {}
 local changed=false
 for _,k in ipairs(keys) do
  local v=number('getpvar['..k..']')==1 and 1 or 0
  if journal.progress[k]~=v then journal.progress[k]=v;changed=true end
 end
 journal.zone=number('getvar[ml_zone]')
 if changed then save() end
end
local function restorePickup()
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
 if number('maraemissionapi[]')~=1 then feedback='Install the mission-compatible RynthAi DLL from this package first.';return end
 if automatic and ai.MacroRunning and not owns() then
  feedback='Saved mission waiting: another macro is running. Stop it, then press Resume run.';return
 end
 if fresh or journal.finished then journal={progress={},active=false,resumable=false} end
 journal.active=true;journal.resumable=true;journal.finished=false;save()
 startup={phase='stop',deadline=now()+15}
 command('/ra stop')
 feedback='Stopping the old macro before loading '..meta..'...'
end

game.OnTick.Add(function()
 if game.State~='InGame' or not ai.Available then
  inGame=false;startup=nil;snapshot=nil;return
 end
 local ok,err=pcall(function()
  if not inGame then
   inGame=true; sampled=-1
   local old=storage.Get('options_v2',{areas={true,true,true},maps={false,false,false}})
   settings=storage.Character.Get('mission_options_v1',{areas=old.areas,maps=old.maps})
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
  if ours and journal.active and not startup and not ai.MacroRunning and state~='HOLD' then
   stopSince=stopSince or t
   if t-stopSince>=3 then
    checkpoint();restorePickup();journal.active=false;journal.resumable=true;save()
    feedback='Macro stopped. Progress saved; press Resume run to continue.'
   end
  else stopSince=nil end
  if ours and t~=sampled then checkpoint();sampled=t end
  if ours and not startup and state=='HOLD' then
   local result=value('getvar[ml_outcome]')
   if journal.active then
    checkpoint();journal.active=false;journal.finished=result=='Finished';journal.resumable=not journal.finished;save()
   end
   if ai.MacroRunning then command('/ra stop') end
   feedback=value('getvar[ml_status]')
  end
  snapshot={running=ours and ai.MacroRunning,other=not ours and ai.MacroRunning,state=state,
    zone=ours and number('getvar[ml_zone]') or 0,detail=ours and value('getvar[ml_status]') or '',
    loaded=value('vtgetmeta[]'),recovery=number('maraerecoverybusy[]')==1}
 end)
 if not ok then
  startup=nil;feedback='Controller error: '..tostring(err)
  if journal then journal.active=false;journal.resumable=true;save() end
  if owns() then restorePickup();command('/ra stop') end
 end
end)

hud.OnRender.Add(function()
 if not snapshot or not settings then ImGui.TextWrapped('Waiting for character login and RynthAi.');return end
 ImGui.TextWrapped('Marae Lassel: hunter tasks + optional treasure maps')
 ImGui.TextDisabled('Loaded: '..snapshot.loaded)
 if snapshot.running or startup then
  ImGui.Text('Area: '..tostring(snapshot.zone~=0 and snapshot.zone or 'choosing'))
  ImGui.TextWrapped(snapshot.recovery and 'Waiting for respawn / corpse recovery' or snapshot.state)
  if snapshot.detail~='0' then ImGui.TextWrapped(snapshot.detail) end
  if ImGui.Button('Stop run') then request='stop' end
 else
  ImGui.TextDisabled('Area                         Use maps')
  for i,z in ipairs(zones) do
   local changed
   changed,settings.areas[i]=ImGui.Checkbox(z.level..' '..z.name,settings.areas[i]);if changed then saveSettings() end
   ImGui.SameLine(240,8)
   changed,settings.maps[i]=ImGui.Checkbox('##maps'..z.level,settings.maps[i]);if changed then saveSettings() end
  end
  if snapshot.other then ImGui.TextWrapped('Start will stop the current RynthAi macro before loading this mission.') end
  if selected() and ImGui.Button(journal.resumable and not journal.finished and 'Resume run' or 'Start run') then request='start' end
  if journal.resumable and not journal.finished then
   ImGui.SameLine();if ImGui.Button('New run') then request='new' end
  end
 end
 if feedback~='' then ImGui.TextWrapped(feedback) end
end)
game.RegisterCommand('/marae',function() hud.Visible=not hud.Visible end)
