"""Execute the genuine parser's emitted rules with recorded/fake game actions only."""
import sys,json,re
from pathlib import Path
lib=Path(__file__).resolve().parents[1]/'ACAI2.PlateauMeta/test_cache.py'
# Reuse the small expression/rule interpreter, not its other meta's scenario suite.
code=lib.read_text().split('w=World([item(10)]);')[0]
ns={'__name__':'mission_harness','sys':sys};exec(compile(code,str(lib),'exec'),ns)
rules=ns['rules'];BaseWorld=ns['World'];BaseSim=ns['Sim'];check=ns['check'];item=ns['item']
class World(BaseWorld):
 def __init__(self,cell=0x21B70028,items=()):
  super().__init__(items);self.cell=cell;self.busy=False;self.chat='';self.route=None
  self.p.update(ml_run_mode=1,ml_area_180=1,ml_area_225=1,ml_area_275=1,ml_maps_180=0,ml_maps_225=1,ml_maps_275=1)
 def ev(self,t,scope):
  if t[0]=='call':
   if t[1]=='maraerecoverybusy':return int(self.busy)
   if t[1]=='listcreate':return []
   if t[1] in ('uboptset','vtsetsetting'):return 1
  return super().ev(t,scope)
class Sim(BaseSim):
 def cond(self,r):
  if r['Condition']==4:return bool(self.w.chat and re.search(r['ConditionData'],self.w.chat))
  return super().cond(r)
 def act(self,r):
  if r['Action']==3:self.w.route=r['ActionData'];return
  return super().act(r)
for zone,cell in ((180,0x14B5002F),(225,0x1BC2002B),(275,0x21B70028)):
 w=World(cell);s=Sim(w,f'T{zone}_START_LOCATION');s.until(f'T{zone}_NPC_TALK_WAIT')
 check(w.route.endswith('Talk_NPCs'),'matching camp selects hunter route '+str(zone))
 check('/ls' not in w.messages,'already at selected camp does not recall '+str(zone))
 w=World(0x01F50001);s=Sim(w,f'T{zone}_START_LOCATION');s.until(f'T{zone}_LS_WAIT')
 check(w.messages.count('/ls')==1,'remote dungeon issues one /ls '+str(zone))
 w.busy=True;w.cell=0;s.tick(10);check(s.state==f'T{zone}_LS_WAIT','portal transition cannot count as arrival '+str(zone))
 w.busy=False;w.cell=0x21B70028
 target=f'T{zone}_GET_TO_WAIT' if zone!=275 else 'T275_NPC_TALK_WAIT';s.until(target)
 check(w.route==f'Hub_to_{zone}' if zone!=275 else w.route.endswith('Talk_NPCs'),'hub chooses exact next link '+str(zone))
 if zone!=275:
  for _ in range(8):s.tick()
  check(s.state==target,'wrong portal destination cannot start NPC sequence '+str(zone))
  w.cell=cell;s.until(f'T{zone}_NPC_TALK_WAIT')
  check(w.route.endswith('Talk_NPCs'),'verified destination reaches NPCs '+str(zone))
 # Native corpse recovery owns movement; neither /ls nor a mission nav may compete.
 w=World();w.busy=True;w.v['ml_zone']=zone;s=Sim(w,f'T{zone}_DEATH_RECOVERY');s.tick()
 for _ in range(40):s.tick()
 check(s.state==f'T{zone}_DEATH_RETURN_WAIT' and '/ls' not in w.messages and not w.route,'waits throughout native corpse recovery '+str(zone))
 w.busy=False;s.until(f'T{zone}_NPC_TALK_WAIT' if zone==275 else f'T{zone}_GET_TO_WAIT')
 check('/ls' not in w.messages,'natural Plateau respawn needs no second recall '+str(zone))
 s.state=f'T{zone}_DEATH_RECOVERY';s.fired.clear();s.tick();check(s.state==f'T{zone}_DEATH_RETURN_WAIT','second death also continues '+str(zone))
# All zones progress using durable, per-area flags.
w=World();w.p.update(mm_area_180=1);s=Sim(w,'Default');s.until('T225_START_LOCATION')
check(w.v['ml_zone']==225 and w.v['ml_use_maps']==1,'reconnect skips completed 180 and preserves map preference for 225')
w=World();w.p.update(mm_area_180=1,mm_area_225=1);s=Sim(w,'Default');s.until('T275_START_LOCATION')
check(w.v['ml_zone']==275,'finishing 225 proceeds to 275')
w=World();w.p.update(mm_area_180=1,mm_area_225=1,mm_area_275=1);s=Sim(w,'Default');s.until('HOLD')
check(w.v['ml_outcome']=='Finished','all selected areas complete exactly once')
w=World();w.p.update(mm_180_done_0=1);s=Sim(w,'T180_Default');s.until('T180_START_LOCATION')
check(w.p['mm_180_done_0']==1,'area initialization cannot erase a completed task')
check(w.options['LootOwnCorpse']==1 and w.options['TravelToOwnCorpse']==1 and w.options['StopMacroOnDeath']==0,'mission enables corpse recovery and disables death stop')
# Vital fluctuation / missing maximum HP is irrelevant to location readiness.
w=World(0x1BC2002B);w.health=1;w.maxhealth=0;s=Sim(w,'T225_START_LOCATION');s.until('T225_NPC_TALK_WAIT')
check(s.state=='T225_NPC_TALK_WAIT','positive HP alone is sufficient; buff maximum never gates startup')
# Completion messages are observed while travelling and inside a map encounter too.
for state in ('T275_TRAVEL_WAIT','ENCOUNTER','CACHE_SEARCH','TURN_SETTLE'):
 w=World();w.v['ml_zone']=275;w.chat='You have killed 20 Olthoi Hive Guardian! Your task is complete!'
 s=Sim(w,state);s.tick()
 check(w.p.get('mm_275_done_olthoi')==1,'actual Guardian completion retained in '+state)
# Currency collection is explicit, serial and acknowledged. Notes remain optional.
for zone,monster,key in ((180,'Drumtaken Carenzi Stalkers','0'),(225,'Drumtaken Woodland Ursuins','0')):
 for phase in (f'T{zone}_GET_TO_WAIT','ENCOUNTER','CACHE_SEARCH','TURN_SETTLE'):
  w=World();w.v['ml_zone']=zone;w.chat=f'You have killed 50 {monster}! Your task is complete!';s=Sim(w,phase);s.tick()
  check(w.p.get(f'mm_{zone}_done_{key}')==1,'task completion retained in '+str(zone)+' '+phase)
w=World(items=[item(10)]);w.v['ml_zone']=180;s=Sim(w,'CACHE_PREP');s.until('CACHE_CONFIRM')
check(len(w.moves)==1 and w.inv.get(1004101,0)==0,'cache dispatch is not credited as a pickup')
w.ack(10);s.until('CACHE_FINISH');s.tick();check(s.state=='MAP_RETURN' and w.inv[1004101]==7,'doubloon-only cache finishes without requiring notes')
w=World(items=[item(10),item(11,30001,2,15)]);s=Sim(w,'CACHE_PREP')
for expected in (10,11):
 s.until('CACHE_CONFIRM');check(w.moves[-1][0]==expected,'one currency move at a time');w.ack(expected);s.tick()
s.until('CACHE_FINISH');check(w.inv=={1004101:7,30001:2},'optional notes and doubloons both acknowledged')
w=World(items=[item(10)]);s=Sim(w,'CACHE_PREP');s.until('CACHE_CONFIRM');s.until('MISSION_ERROR')
check(len(w.moves)==1 and not w.inv,'unacknowledged sent pickup holds without blind retry or credit')
print(f"PASS: {ns['checks']} mission simulations; native actions were fake recordings only.")
