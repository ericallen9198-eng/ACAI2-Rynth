sim={time=1000,files={},settings={FollowNavMin=1.5,NavResumeNearest=1,StopMacroOnDeath=1,DeathRecallMode=1,LootOwnCorpse=0,TravelToOwnCorpse=0,EnableLooting=0,EnableMeta=0,EnableBuffing=1,EnableNavigation=1},vars={},pvars={},state='HOLD',meta='other',commands={},logs={},hp=429,coords='45.2|-75.1|0.38',failWrites=false,nativeRecovery=false,rememberCorpse=false}
local fs={}
fs.ReadText=function(n)return sim.files[n],sim.files[n]and nil or 'missing'end
fs.FileExists=function(n)return sim.files[n]~=nil end
fs.WriteText=function(n,s)if sim.failWrites then return false,'disk failure'end sim.files[n]=s;return true end
fs.AppendText=function(n,s)if sim.failWrites then return false,'disk failure'end sim.files[n]=(sim.files[n]or'')..s;return true end
local ai={Available=true,MacroRunning=false}
ai.GetSetting=function(n)return sim.settings[n]end
ai.SetSetting=function(n,v)if sim.settings[n]==nil then return false end sim.settings[n]=v;return true end
ai.Stop=function()sim.commands[#sim.commands+1]='STOP';ai.MacroRunning=false end
local function emit(s)if sim.chat then sim.chat({Message=s})end end
ai.Command=function(c)
 sim.commands[#sim.commands+1]=c
 if c=='/ra meta load Terrain Access Survey.af'then if not sim.ignoreLoad then sim.meta='Terrain Access Survey' end
 elseif c=='/rnav avoid portals on'then sim.avoidPortals=true;emit('[RynthNav] avoid: portals on, '..(sim.avoidRadius or 4)..' m; walls off; corners reached at 2 m')
 elseif c=='/rnav avoid portals 10'then sim.avoidRadius=10;emit('[RynthNav] avoid: portals on, 10 m; walls off; corners reached at 2 m')
 elseif c=='/ra start'then ai.MacroRunning=true
 elseif c=='/ra owncorpse'then
  if not sim.suppressStatus then
   if not sim.rememberCorpse then emit('[RynthAi] Own corpse: no death remembered this session.')end
   emit('[RynthAi] Recover My Corpse: on, Travel Back To My Corpse: on; travel: '..(sim.nativeRecovery and 'walking (RynthNav)'or'off')..'. /ra owncorpse go|stop')
  end
 elseif c=='/ra owncorpse go'then sim.nativeRecovery=true;emit('[RynthAi] Own corpse: travelling back to 45.00N, 75.00W with RynthNav once nothing else needs doing.')
 elseif c=='/ra owncorpse stop'then sim.nativeRecovery=false;emit('[RynthAi] Own corpse: travel back ended: stopped by /ra owncorpse stop.')
 else error('Unexpected command '..c)end
end
ai.Evaluate=function(e)
 if e=='getunixtime[]'then return sim.time end
 if e=='vtgetmeta[]'then return sim.meta end
 if e=='vtgetmetastate[]'then return sim.state end
 if e=='getplayercoordinates[]'then return sim.coords end
 if e=='getplayerlandcell[]'then return 565641256 end
 if e=='getcharvital_current[1]'then return sim.hp end
 if e=='ranav[]'then return encodeNative({name=sim.navName or 'synthetic',index=sim.navIndex or 0,points=15,finished=false})end
 if e=='getbusystate[]'then return sim.busy or 0 end
 if e=='ratargetcount[]'then return sim.targets or 0 end
 if e=='rabuffsdue[0]'then return sim.buffs or 0 end
 local pn=e:match('^getpvar%[(.-)%]$');if pn then return sim.pvars[pn]or 0 end
 local pk,pv=e:match('^setpvar%[([^,]+),([^%]]+)%]$');if pk then sim.pvars[pk]=assert(tonumber(pv));return 1 end
 local n=e:match('^getvar%[(.-)%]$');if n then return sim.vars[n]or 0 end
 local k,v=e:match('^setvar%[([^,]+),([^%]]+)%]$');if k then sim.vars[k]=v:match('^`(.*)`$')or assert(tonumber(v));return 1 end
 local state=e:match('^vtsetmetastate%[(.-)%]$');if state then sim.state=state;return 1 end
 local setting,value=e:match('^vtsetsetting%[([^,]+),([^%]]+)%]$');if setting then sim.settings[setting]=tonumber(value);return 1 end
 error('Unexpected expression '..e)
end
local mods={['rynth.ai']=ai,filesystem={GetData=function()return fs end,GetScript=function()return {ReadText=function(n)return n=='route-index.json'and routeText or indexText end}end},json={encode=function(v)return encodeNative(v)end,decode=function(s)return decodeNative(s)end},rynthlua={DataFolder='C:\\Test\\RynthLua\\Data\\TerrainSurvey'}}
require=function(n)return assert(mods[n],n)end
clock=function()return sim.time*1000 end
chat=function(s)sim.logs[#sim.logs+1]=s end
print=function(s)sim.logs[#sim.logs+1]=s end
game={State='InGame',ServerName='DreamWeave',Character={Name='Samm',InPortalSpace=false},World={OnChatText={Add=function(f)sim.chat=f end}},OnTick={Add=function(f)sim.tick=f end},RegisterCommand=function(n,f)sim.request=f end}
function step(n)
 for i=1,n or 1 do
  sim.time=sim.time+1;sim.tick()
  -- Minimal host macro lifecycle only. AF parsing/structure is checked separately.
  if ai.MacroRunning and sim.state=='RESUME'and sim.vars.te_recovery_busy==0 then if sim.onResumeCoords then sim.coords=sim.onResumeCoords;sim.onResumeCoords=nil end;sim.state='WAIT_'..({[0]=275,[1]=180,[2]=225})[sim.vars.te_stage]..'_'..string.format('%02d',sim.vars.te_probe)end
  if ai.MacroRunning and sim.state=='FINISH_COMMIT'and sim.vars.te_finish_ack==1 then ai.MacroRunning=false;sim.state='FINISHED'end
 end
end
function request(s)sim.request(s);step()end
function start()step();request('new');step(20);assert(ai.MacroRunning,'must run');assert(sim.state=='WAIT_275_01','start checkpoint: '..sim.state)end
function cp()
 for n,s in pairs(sim.files)do if n:find('^checkpoint%-')then return decodeNative(s),n end end
 error('no checkpoint')
end
function countCommand(s)local n=0;for _,c in ipairs(sim.commands)do if c==s then n=n+1 end end;return n end
function send(s)emit(s)end
function report(kind,probe,seq)
 sim.vars.te_probe=probe;sim.vars.te_report=kind;sim.vars.te_seq=seq;sim.vars.te_commit=0;sim.vars.te_report_coords=sim.coords
 sim.state=kind==1 and 'COMMIT_REACHED_275_01'or'COMMIT_FAILED_275_01';step()
end

function reloadScript()assert(load(scriptText))();step(40)end
