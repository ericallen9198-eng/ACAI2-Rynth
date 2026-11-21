sim={time=100,character='A',state='InGame',vars={},pvars={},commands={},click=nil,ignoreLoad=false,stores={},global={},texts={}}
ai={Available=true,MacroRunning=false}
local function charstore() sim.stores[sim.character]=sim.stores[sim.character] or {};return sim.stores[sim.character] end
local storage={Get=function(k,d)return sim.global[k] or d end,Set=function(k,v)sim.global[k]=v end,Character={Get=function(k,d)return charstore()[k] or d end,Set=function(k,v)charstore()[k]=v end}}
hud={OnRender={Add=function(f)sim.render=f end}}
local imgui={Button=function(label)if sim.click==label then sim.click=nil;return true end return false end,Checkbox=function(l,v)return false,v end,Text=function(s)sim.texts[#sim.texts+1]=s end,TextWrapped=function(s)sim.texts[#sim.texts+1]=s end,TextDisabled=function(s)sim.texts[#sim.texts+1]=s end,SameLine=function()end}
local modules={filesystem={GetData=function() return {WriteText=function(path,text)sim.logPath=path;sim.logText=text;return not sim.failLog end} end},json={encode=function(v)sim.logReport=v;return 'fake-json' end},views={Huds={CreateHud=function()return hud end}},imgui=imgui,['rynth.ai']=ai,storage=storage}
function require(k)return assert(modules[k],k)end
game={World={OnChatText={Add=function(f)sim.chat=f end}},State='InGame',OnTick={Add=function(f)sim.tick=f end},RegisterCommand=function()end}
function expr(s)
 if s=='getunixtime[]' then return math.floor(sim.time) end
 if s=='maraemissionapi[]' then return 1 end
 if s=='maraerecoverybusy[]' then return sim.busy and 1 or 0 end
 if s=='vtgetmeta[]' then return sim.meta or '275 Plateau Treasure - Verified' end
 if s=='vtgetmetastate[]' then return sim.metaState or 'HOLD' end
 local k=s:match('^getvar%[(.-)%]$');if k then return sim.vars[k] or 0 end
 k=s:match('^getpvar%[(.-)%]$');if k then return sim.pvars[k] or 0 end
 local name,v=s:match('^setpvar%[([^,]+),([^%]]+)%]$');if name then sim.pvars[name]=tonumber(v) or v;return 1 end
 if s=='rasetsetting[`LootChestsMacroOff`,getpvar[pt_cache_old_pickup]]' then sim.restoredPickup=sim.pvars.pt_cache_old_pickup;return 1 end
 local vk,vv=s:match('^setvar%[([^,]+),([^%]]+)%]$');if vk then sim.vars[vk]=tonumber(vv) or vv;return 1 end
 if s=='vtsetmetastate[Default]' then sim.metaState='Default';return 1 end
 if s=='vtsetsetting[EnableMeta,1]' then sim.metaEnabled=true;return 1 end
 error('Unexpected expression '..s)
end
function command(s)sim.commands[#sim.commands+1]=s end
function step(n)
 for i=1,n or 1 do
  sim.time=sim.time+.05;game.State=sim.state
  local q=sim.commands;sim.commands={}
  for _,c in ipairs(q) do
   if c=='/ra stop' then ai.MacroRunning=false
   elseif c=='/ra meta load Marae Lassel - Embedded.af' then
    if not sim.ignoreLoad then sim.meta='Marae Lassel - Embedded';sim.metaState='HOLD' end
   elseif c=='/ra start' then
    assert(sim.meta=='Marae Lassel - Embedded','must never start wrong meta')
    assert(sim.metaState=='Default','must reset state before starting')
    assert(sim.metaEnabled,'meta must be enabled')
    ai.MacroRunning=true;sim.vars.mm_schema=1;sim.metaState='T180_START_LOCATION'
   elseif c=='/ub clearmotion' or c=='/lua autostart MaraeLassel character' then
   else error('Unexpected command '..c) end
  end
  sim.tick()
 end
end
function click(label)sim.click=label;sim.render();step()end
