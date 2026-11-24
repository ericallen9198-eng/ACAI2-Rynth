sim={time=100,vars={},pvars={},commands={},stores={},state='InGame',meta='Other',route={name='maphunt',points=44},api=6,exprSeen={}}
ai={Available=true,MacroRunning=false,GoTo=function(ns,ew)sim.walks=(sim.walks or 0)+1;sim.walkNS,sim.walkEW=ns,ew;return true end}
local storage={Character={Get=function(k,d)return sim.stores[k] or d end,Set=function(k,v)sim.stores[k]=v end}}
hud={OnRender={Add=function(f)sim.render=f end}}
local imgui={Text=function()end,TextWrapped=function()end,TextDisabled=function()end,SameLine=function()end,Button=function(l)if sim.click==l then sim.click=nil;return true end return false end,Checkbox=function(l,v)return false,v end,SliderInt=function(l,v)return false,v end}
local modules={['rynth.ai']=ai,storage=storage,json={decode=function()return sim.route end},views={Huds={CreateHud=function()return hud end}},imgui=imgui}
function require(k)return assert(modules[k],k)end
game={State='InGame',OnTick={Add=function(f)sim.tick=f end},RegisterCommand=function(n,f)sim.show=f end}
function clock()return sim.time*1000 end
function command(s)sim.commands[#sim.commands+1]=s;sim.sent=sim.sent or {};sim.sent[#sim.sent+1]=s end
function expr(s)
 sim.exprSeen[s]=true;sim.calls=(sim.calls or 0)+1
 if s=='getunixtime[]' then return math.floor(sim.time) end
 if s=='vtgetmeta[]' then return sim.meta end
 if s=='vtgetmetastate[]' then return sim.metaState or 'HOLD' end
 if s=='ranav[]' then return '{}' end
 if s=='maraemissionapi[]' then return sim.api end
 if s=='maraerecoverybusy[]' then return sim.busy and 1 or 0 end
 if s=='maraenavsave[]' or s=='maraenavmark[]' or s=='maraenavrestore[]' then return 1 end
 if s=='getcharvital_current[1]' then return sim.dead and 0 or 100 end
 if s=='getplayercoordinates[]' then return sim.coords or '51.1|-84.0|0.2' end
 if s:match('^coordinatedistanceflat%[') then return sim.distance or 1 end
 if s:match('^maraemapwalk%[') then sim.meshWalks=(sim.meshWalks or 0)+1;return sim.meshFail and 0 or 1 end
 if s:match('^if%[getpvar%[mh_pt_cache_pickup_held%]') then return 1 end
 local k=s:match('^getvar%[([^%]]+)%]$');if k then return sim.vars[k] or 0 end
 k=s:match('^getpvar%[([^%]]+)%]$');if k then return sim.pvars[k] or 0 end
 local n,v=s:match('^setvar%[([^,]+),([^%]]+)%]$');if n then sim.vars[n]=tonumber(v) or v:gsub('^`',''):gsub('`$','');return 1 end
 n,v=s:match('^setpvar%[([^,]+),([^%]]+)%]$');if n then sim.pvars[n]=tonumber(v) or v;return 1 end
 if s=='vtsetmetastate[Default]' then sim.metaState='Default';return 1 end
 if s=='vtsetmetastate[ENCOUNTER]' then sim.metaState='ENCOUNTER';return 1 end
 if s=='vtsetmetastate[CACHE_PREP]' then sim.metaState='CACHE_PREP';return 1 end
 if s=='vtsetsetting[EnableMeta,1]' then return 1 end
 error('unexpected expression: '..s)
end
function step(n,delta)
 for i=1,n or 1 do
  sim.time=sim.time+(delta or 1.1);game.State=sim.state
  local q=sim.commands;sim.commands={}
  for _,c in ipairs(q) do
   if c=='/ra stop' then ai.MacroRunning=false
   elseif c=='/ra meta load MapHunt.af' then sim.meta='MapHunt';sim.metaState='Default'
   elseif c=='/ra start' then assert(sim.meta=='MapHunt');ai.MacroRunning=true;sim.vars.mh_schema=1;sim.metaState='HUNT_VERIFY'
   elseif c=='/ub clearmotion' or c=='/ra nav load maphunt.nav' or c=='/lua autostart MapHunt character' or c:match('^/ub face ') or c=='/ub setmotion Forward 1' then
   else error('unexpected command: '..c) end
  end
  sim.tick()
 end
end
function click(label)sim.click=label;sim.render();step(5)end
