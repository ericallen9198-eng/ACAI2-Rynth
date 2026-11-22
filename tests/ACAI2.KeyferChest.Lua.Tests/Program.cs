using MoonSharp.Interpreter;
var vm=new Script(CoreModules.Preset_Complete);
vm.DoString("""
commands={};click=nil;hud={OnRender={Add=function(f)render=f end}}
local imgui={TextWrapped=function()end,SameLine=function()end,Button=function(label)if label==click then click=nil;return true end return false end}
local modules={views={Huds={CreateHud=function()return hud end}},imgui=imgui,['rynth.ai']={Command=function(s)commands[#commands+1]=s end}}
function require(n)return assert(modules[n])end
game={State='InGame',World={Selected={Name='Legendary Key'}},OnTick={Add=function(f)tick=f end},RegisterCommand=function(n,f)show=f end}
""");
vm.DoString(File.ReadAllText(args[0]));
vm.DoString("""
click='Set selected key';render();assert(#commands==0);tick();assert(commands[1]=='/acai2r keyfer chest key')
game.World.Selected={Name='Legendary Weapon Chest'};click='Set selected chest';render();tick();assert(commands[2]=='/acai2r keyfer chest set')
click='Start pulling';render();tick();assert(commands[3]=='/acai2r keyfer chest start')
click='Stop pulling';render();tick();assert(commands[4]=='/acai2r keyfer chest stop')
click='Show native status in chat';render();tick();assert(commands[5]=='/acai2r keyfer chest status')
game.World.Selected=nil;click='Set selected key';render();tick();assert(#commands==5)
click='Start pulling';render();game.State='CharacterSelect';tick();game.State='InGame';tick();assert(#commands==5)
hud.Visible=false;show();assert(hud.Visible)
""");
Console.WriteLine("PASS: genuine MoonSharp chest panel tests: selection, fixed pump commands, missing selection, logout cancellation and show command. Fake game/UI only.");
