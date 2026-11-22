-- Small local setup panel. The Keyfer adapter enforces character/server ownership.
local views,ImGui,ai=require('views'),require('imgui'),require('rynth.ai')
local hud=views.Huds.CreateHud('KeyferChest')
hud.Title,hud.ShowInBar,hud.Visible='Keyfer Chest',true,true
hud.RefreshRate=5
if Vector2 then hud.DefaultSize=Vector2(360,230) end
local pending=nil
local key,chest='Not selected this session','Not selected this session'
local function pick(kind)
 local selected=game.World.Selected
 if not selected then return end
 local name=tostring(selected.Name or 'Selected object')
 if kind=='key' then key=name else chest=name end
 pending='/acai2r keyfer chest '..kind
end
hud.OnRender.Add(function()
 ImGui.TextWrapped('On Keyfer: select a key, then select a chest. Confirm each in game chat.')
 ImGui.TextWrapped('Key selection: '..key)
 if ImGui.Button('Set selected key') then pick('key') end
 ImGui.TextWrapped('Chest selection: '..chest)
 if ImGui.Button('Set selected chest') then pick('set') end
 if ImGui.Button('Start pulling') then pending='/acai2r keyfer chest start' end
 ImGui.SameLine()
 if ImGui.Button('Stop pulling') then pending='/acai2r keyfer chest stop' end
 if ImGui.Button('Show native status in chat') then pending='/acai2r keyfer chest status' end
 ImGui.TextWrapped('Toe/Samm: Keyfer pull keys / Keyfer stop pulling / Keyfer pull status. Tells or allegiance.')
end)
game.OnTick.Add(function()
 if game.State~='InGame' then pending=nil;key='Not selected this session';chest='Not selected this session';return end
 if pending then local cmd=pending;pending=nil;ai.Command(cmd) end
end)
game.RegisterCommand('/keyferchest',function() hud.Visible=true end)
