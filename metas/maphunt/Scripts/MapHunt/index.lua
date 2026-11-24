-- MapHunt v0.1.0. One-second bounded pump; drawing never evaluates expressions.
local ai,views,ImGui=require('rynth.ai'),require('views'),require('imgui')
local storage=require('storage')
local meta='MapHunt'
local hud=views.Huds.CreateHud('MapHunt')
hud.Title,hud.ShowInBar,hud.Visible='MapHunt',true,true
hud.RefreshRate=5
if Vector2 then hud.DefaultSize=Vector2(360,260) end
local inGame,nextPoll,request,startup,lastWalk=false,0,nil,nil,-1
local journal,snapshot,feedback={},nil,''
local direct=nil
local function val(s)return tostring(expr(s) or '')end
local function num(s)return tonumber(expr(s)) or 0 end
local function now()return num('getunixtime[]')end
local function owns()
 local n=val('vtgetmeta[]'):gsub('\\','/'):match('([^/]+)$') or ''
 return n:gsub('%.[aA][fF]$',''):gsub('%.[mM][eE][tT]$',''):lower()==meta:lower()
end
local function save()storage.Character.Set('run_v1',journal)end
local function clearMotion()command('/ub clearmotion');direct=nil end
local function cleanup()
 if owns() then
  clearMotion();expr('maraenavrestore[]')
  expr('if[getpvar[mh_pt_cache_pickup_held]==1,rasetsetting[`LootChestsMacroOff`,getpvar[mh_pt_cache_old_pickup]]]')
  expr('setpvar[mh_pt_cache_pickup_held,0]')
 end
end
local function begin(auto)
 if num('maraemissionapi[]')<6 then feedback='Install the documented MapHunt compatibility support first (mission API 6).';return end
 if auto and ai.MacroRunning and not owns() then feedback='Saved MapHunt run found; another macro is active. Press Start / Resume when ready.';return end
 journal.active=true;save();cleanup();expr('setvar[mh_schema,0]');command('/ra stop');command('/ra meta load MapHunt.af')
 startup={phase='load',deadline=now()+15};feedback='Loading MapHunt.af...'
end
local function checkpoint(t,state)
 -- Only bounded recovery context, no chat or transcript. Writes at most once per five seconds.
 if not journal.active or t<(journal.nextSave or 0) then return end
 journal.nextSave=t+5
 local encounter=num('getpvar[mh_tm_dig_active]')==1
 journal.event=encounter and {anchor=val('getvar[tm_cache_anchor]'),deadline=num('getvar[tm_event_deadline]'),doubloons=num('getvar[tm_doubloon_before]'),state=state} or nil
 save()
end
local function moveToTarget(state,token)
 local coords=val('getvar[tm_walk_target]')
 local ns,ew=coords:match('^([^|]+)|([^|]+)|');ns,ew=tonumber(ns),tonumber(ew)
 if state=='CACHE_APPROACH_WAIT' and ns and ew then
  local pn,pe=val('getplayercoordinates[]'):match('^([^|]+)|([^|]+)|');pn,pe=tonumber(pn),tonumber(pe)
  if pn and pe then
   local dn,de=pn-ns,pe-ew;local yards=math.sqrt(dn*dn+de*de)*240
   if yards>3 then ns,ew=ns+dn*2.5/yards,ew+de*2.5/yards else ns,ew=nil,nil end
  else ns,ew=nil,nil end
 end
 local accepted=ns and ew and num('maraenavsave[]')==1 and ai.GoTo(ns,ew) and num('maraenavmark[]')==1
 expr('setvar[tm_walk_ack,'..(accepted and 1 or -1)..']')
end

game.OnTick.Add(function()
 if game.State~='InGame' or not ai.Available then
  if inGame then inGame=false;startup=nil;snapshot=nil;direct=nil;lastWalk=-1;nextPoll=0 end
  return
 end
 local ms=clock()
 if inGame and not request and ms<nextPoll then return end
 nextPoll=ms+1000
 local ok,err=pcall(function()
  local t=now()
  if not inGame then
   inGame=true;journal=storage.Character.Get('run_v1',{active=false});journal.nextSave=0
   if journal.active then begin(true) end
  end
  local r=request;request=nil
  if r=='stop' then
   journal.active=false;journal.event=nil;save();cleanup();if owns() then command('/ra stop') end
   startup=nil;feedback='Stopped. Saved pvar preferences remain.'
  elseif r=='start' then begin(false)
  elseif r=='review' then
   if owns() then expr('setpvar[mh_mm_pickup_unresolved,0]');journal.event=nil;save();begin(false) end
  end
  if startup then
   if t>startup.deadline then startup=nil;feedback='Startup was not confirmed. Check RynthAi and MapHunt.af; press Start / Resume to retry.'
   elseif startup.phase=='load' and owns() and not ai.MacroRunning then
    expr('vtsetsetting[EnableMeta,1]');expr('vtsetmetastate[Default]');command('/ra start')
    startup={phase='arm',deadline=t+15};feedback='Starting map hunting...'
   elseif startup.phase=='arm' and owns() and ai.MacroRunning and num('getvar[mh_schema]')==1 then
    command('/lua autostart MapHunt character');startup=nil;feedback=''
    -- Never replay a currency move after reconnect. The meta checks its persistent unresolved marker first.
    local e=journal.event
    if num('getcharvital_current[1]')>0 and num('maraerecoverybusy[]')==0 and e and type(e.deadline)=='number' and e.deadline>t and type(e.anchor)=='string' and e.anchor:match('^[-%d%.]+|[-%d%.]+|[-%d%.]+$') and num('getpvar[mh_mm_pickup_unresolved]')==0 then
     local distance=num('coordinatedistanceflat[getplayercoordinates[],`'..e.anchor..'`]')
     if distance<=55 and e.anchor:match('^[-%d%.]+|[-%d%.]+|[-%d%.]+$') then
      expr('setvar[tm_cache_anchor,`'..e.anchor..'`]');expr('setvar[tm_event_deadline,'..e.deadline..']')
      expr('setvar[tm_doubloon_before,'..e.doubloons..']');expr('setpvar[mh_tm_dig_active,1]')
      expr('setvar[tm_anchor_forced,0]');expr('setvar[tm_anchor_retry_at,0]');expr('setvar[tm_anchor_failures,0]');expr('setvar[tm_aun_loot_active,0]')
      -- Reload the real hunt route first; transient route snapshots do not survive login.
      command('/ra nav load maphunt.nav');expr('setvar[mh_event_restore,1]')
      if e.state:match('^CACHE_') then expr('setvar[tm_cache_deadline,'..(t+300)..']');expr('vtsetmetastate[CACHE_PREP]')
      else expr('vtsetmetastate[ENCOUNTER]') end
     end
    end
   end
  end
  if not owns() then snapshot=nil;return end
  local state=val('vtgetmetastate[]')
  expr('setvar[mh_heartbeat,'..t..']')
  if startup then return end
  if not ai.MacroRunning then
   if journal.active then cleanup();journal.active=false;save();feedback='Macro stopped. Press Start / Resume to continue.' end
   return
  end
  local token=num('getvar[mh_walk_token]')
  if token~=lastWalk and num('getcharvital_current[1]')>0 and num('maraerecoverybusy[]')==0 then
   if state=='MAP_MESH_WAIT' then
    lastWalk=token
    local accepted=num('maraemapwalk['..num('getvar[tm_mesh_heading]')..','..num('getvar[tm_mesh_step]')..']')==1
    expr('setvar[tm_walk_ack,'..(accepted and 1 or -1)..']')
   elseif state=='DIRECT_WAIT' then
    lastWalk=token;clearMotion();command('/ub face '..num('getvar[tm_mesh_heading]'));direct={token=token,at=ms+1000}
   elseif state=='CACHE_RETURN_WAIT' or state=='CACHE_APPROACH_WAIT' or state=='DIGSITE_RETURN_WAIT' then
    lastWalk=token;moveToTarget(state,token)
   end
  end
  if direct then
   if state~='DIRECT_WAIT' then clearMotion()
   elseif ms>=direct.at then command('/ub setmotion Forward 1');expr('setvar[tm_walk_ack,1]');direct=nil end
  end
  checkpoint(t,state)
  snapshot={state=state,status=val('getvar[mh_status]'),running=ai.MacroRunning,mesh=num('getpvar[meshnav]')==1,close=num('getpvar[maphunt_close]'),far=num('getpvar[maphunt_far]'),review=num('getpvar[mh_mm_pickup_unresolved]')==1}
 end)
 if not ok then cleanup();feedback='MapHunt helper error: '..tostring(err):sub(1,220) end
end)

hud.OnRender.Add(function()
 ImGui.TextWrapped('Follow maphunt.nav, hunt maps, collect rewards, resume the route.')
 if snapshot then
  ImGui.Text(snapshot.state);ImGui.TextWrapped(snapshot.status)
  local changed,on=ImGui.Checkbox('Mesh clue travel',snapshot.mesh)
  if changed then expr('setpvar[meshnav,'..(on and 1 or 0)..']');snapshot.mesh=on end
  local c,cv=ImGui.SliderInt('Close clue (yd)',snapshot.close,10,60,'%d yd')
  if c then expr('setpvar[maphunt_close,'..cv..']');snapshot.close=cv end
  local f,fv=ImGui.SliderInt('Far clue (yd)',snapshot.far,60,180,'%d yd')
  if f then expr('setpvar[maphunt_far,'..fv..']');snapshot.far=fv end
  ImGui.TextDisabled('Travel changes apply to the next clue.')
 end
 if ImGui.Button('Start / Resume') then request='start' end
 ImGui.SameLine();if ImGui.Button('Stop') then request='stop' end
 if snapshot and snapshot.review then
  ImGui.TextWrapped('Inspect inventory and the cache before clearing the unconfirmed move.')
  if ImGui.Button('Review complete') then request='review' end
 end
 if feedback~='' then ImGui.TextWrapped(feedback) end
end)
game.RegisterCommand('/maphunt',function()hud.Visible=not hud.Visible end)
