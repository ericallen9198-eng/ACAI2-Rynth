local views, ImGui, ai = require('views'), require('imgui'), require('rynth.ai')
local storage = require('storage')
local meta = 'Marae Lassel'
local zones = {
  { level=180, name='Lowlands', tasks=9 },
  { level=225, name='High Country', tasks=10 },
  { level=275, name='Plateau', keys={'zefir','ursuin','golem','gromnie','siraluun','carenzi','virindi','olthoi'} },
}
local farmAreas = {'180  Lowlands', '225  High Country', '275  Plateau'}
local settings = storage.Get('options_v2', {areas={true,true,true}, maps={false,false,false}, mode=1, farm=0})
local hud = views.Huds.CreateHud('MaraeLassel')
hud.Title, hud.ShowInBar, hud.Visible = 'Marae Lassel', true, true
if Vector2 then hud.DefaultSize = Vector2(430, 320) end

local gold = {0.96, 0.79, 0.47, 1}
local teal = {0.52, 0.88, 0.77, 1}
local rose = {0.98, 0.62, 0.58, 1}
local function primaryButton(label)
  return ImGui.Button(label, Vector2 and Vector2(-1, 33) or nil)
end

local pending, loading, snapshot
local feedback = ''
local function value(code)
  return tostring(expr(code) or '')
end
local function var(name)
  return value('getvar[' .. name .. ']')
end
local function number(name)
  return tonumber(var(name)) or 0
end
local function ownsMeta()
  return value('vtgetmeta[]'):lower() == meta:lower()
end
local function save()
  storage.Set('options_v2', settings)
end
local function writePreferences()
  expr('setpvar[ml_run_mode,' .. settings.mode .. ']')
  for i, zone in ipairs(zones) do
    local chosen = settings.mode == 2 and settings.farm == i-1 or settings.mode == 1 and settings.areas[i]
    local maps = settings.mode == 1 and settings.maps[i]
    expr('setpvar[ml_area_' .. zone.level .. ',' .. (chosen and 1 or 0) .. ']')
    expr('setpvar[ml_maps_' .. zone.level .. ',' .. (maps and 1 or 0) .. ']')
  end
end
local function selected()
  return settings.mode == 2 or settings.areas[1] or settings.areas[2] or settings.areas[3]
end
local function stop()
  ai.MacroRunning = false
  command('/ub clearmotion')
  expr('vtsetsetting[StopMacroOnDeath,1]')
  expr('vtsetsetting[OpenDoors,0]')
  expr('setpvar[tm_dig_active,0]')
end

game.OnTick.Add(function()
  if not ai.Available then
    snapshot = nil
    return
  end
  local ok, err = pcall(function()
    local ours, running = ownsMeta(), ai.MacroRunning
    if loading then
      local attempts = loading
      loading = nil
      if running then
        feedback = 'The macro started before the panel could load this run.'
      elseif ours then
        writePreferences()
        expr('vtsetmetastate[Default]')
        expr('vtsetsetting[EnableMeta,1]')
        command('/ra start')
        feedback = ''
      elseif attempts < 20 then
        loading = attempts + 1
      else
        feedback = 'Could not load Marae Lassel. Check the MetaFiles installation.'
      end
    end
    local request = pending
    pending = nil
    if request == 'start' and not running and not loading and selected() then
      command('/vt meta load ' .. meta)
      loading = 1
      feedback = 'Loading Marae Lassel'
    elseif request == 'stop' and ours and running then
      stop()
      expr('setvar[ml_outcome,`Stopped`]')
      expr('setvar[ml_status,`Stopped from the panel.`]')
      feedback = ''
    end

    if not ours then
      snapshot = {otherRunning=running}
      return
    end
    local state = value('vtgetmetastate[]')
    if state == 'HOLD' and ai.MacroRunning then ai.MacroRunning = false end
    local level, mode = number('ml_zone'), number('ml_run_mode')
    local progress, area
    for _, zone in ipairs(zones) do
      if zone.level == level then
        area = zone.level .. '  ' .. zone.name
        if mode == 1 and (state:match('_HUNT') or state:match('_TURNIN')) then
          local total = zone.tasks or #zone.keys
          local done = 0
          for i=1,total do
            if number('pt_done_' .. (zone.keys and zone.keys[i] or (i-1))) == 1 then done=done+1 end
          end
          progress = string.format('Tasks remaining  %d', total-done)
        end
        break
      end
    end
    local phase = 'Traveling to camp'
    if state:match('DEATH_RECOVERY') or state:match('DEATH_RETURN') then phase = 'Recovering after death'
    elseif state:match('TURNIN') then phase = 'Turning in kill tasks'
    elseif state:match('NPC_TALK') then phase = 'Checking hunters'
    elseif state:match('FARM_') then phase = 'Farming treasure maps'
    elseif state:match('_HUNT') then phase = mode == 2 and 'Farming treasure maps' or 'Hunting'
    elseif state:match('^MAP_') or state:match('^DIG_') or state:match('^CACHE_') or state:match('^ENCOUNTER')
      or state:match('^FAR_') or state:match('^CLOSE_') or state:match('^RUN_')
      or state == 'TURN_SETTLE' or state == 'RETREAT_WAIT' or state == 'REROLL_WAIT' or state == 'STUCK'
      then phase = 'Working a treasure map'
    end
    local result = var('ml_outcome')
    if result == '' or result == '0' then result = 'Ready'
    elseif result == 'Running' and not ai.MacroRunning then result = 'Stopped' end
    snapshot = {running=ai.MacroRunning, area=area, phase=phase, progress=progress, result=result,
      maps=number('ml_use_maps') == 1,
      detail=var('ml_status'), done=number('ml_done'), cooldown=number('ml_cooldown')}
  end)
  if not ok then
    pending, loading = nil, nil
    feedback = 'Controller error: ' .. tostring(err)
  end
end)

hud.OnRender.Add(function()
  if not snapshot then
    ImGui.TextWrapped('Waiting for RynthAi.')
    return
  end
  if snapshot.running then
    ImGui.TextColored(gold, snapshot.area or 'Starting')
    ImGui.TextColored(teal, snapshot.phase)
    if snapshot.progress then ImGui.Text(snapshot.progress) end
    ImGui.TextDisabled(snapshot.maps and 'Treasure maps enabled' or 'Treasure maps saved in inventory')
    ImGui.Spacing()
    if primaryButton('Stop run') then pending = 'stop' end
  else
    local changed
    if ImGui.Button(settings.mode == 1 and 'Kill tasks  [Selected]##kt' or 'Kill tasks##kt') then
      settings.mode = 1
      save()
    end
    ImGui.SameLine()
    if ImGui.Button(settings.mode == 2 and 'Maps only  [Selected]##farm' or 'Maps only##farm') then
      settings.mode = 2
      save()
    end
    ImGui.Spacing()
    if settings.mode == 2 then
      ImGui.SeparatorText('Treasure circuit')
      changed, settings.farm = ImGui.Combo('Area', settings.farm, farmAreas)
      if changed then save() end
      ImGui.TextDisabled('Repeats until stopped')
    else
      ImGui.SeparatorText('Hunter contracts')
      ImGui.TextColored(gold, 'Run kill tasks')
      ImGui.SameLine(235, 8)
      ImGui.TextColored(teal, 'Use maps')
      for i, zone in ipairs(zones) do
        changed, settings.areas[i] = ImGui.Checkbox(farmAreas[i], settings.areas[i])
        if changed then save() end
        ImGui.SameLine(235, 8)
        if settings.areas[i] then
          changed, settings.maps[i] = ImGui.Checkbox('##maps' .. zone.level, settings.maps[i])
          if changed then save() end
        else
          ImGui.TextDisabled('—')
        end
      end
      ImGui.Spacing()
      if ImGui.Button('Select all areas') then settings.areas = {true,true,true}; save() end
    end
    ImGui.Spacing()
    if snapshot.otherRunning then ImGui.TextWrapped('Stop the other macro before starting.')
    elseif not selected() then ImGui.TextWrapped('Select at least one area.')
    elseif not loading and primaryButton('Start run') then pending = 'start' end
    if snapshot.result and snapshot.result ~= 'Ready' then
      ImGui.Spacing()
      ImGui.TextColored(snapshot.result == 'Needs attention' and rose or teal, snapshot.result)
      if snapshot.detail and snapshot.detail ~= '0' and snapshot.detail ~= '' then ImGui.TextWrapped(snapshot.detail) end
      ImGui.Text(string.format('Areas finished  %d   On cooldown  %d',snapshot.done,snapshot.cooldown))
    end
  end
  if feedback ~= '' then ImGui.TextWrapped(feedback) end
end)

game.RegisterCommand('/marae', function() hud.Visible = not hud.Visible end)
