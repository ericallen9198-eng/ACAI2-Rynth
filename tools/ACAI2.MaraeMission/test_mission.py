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
  self.v.update(ml_run_mode=1,ml_area_180=1,ml_area_225=1,ml_area_275=1,tm_cache_preexisting=[],tm_cache_anchor=0)
  self.caches={1:{'distance':0,'openable':True},2:{'distance':0,'openable':True}}
  self.p.update(ml_run_mode=1,ml_area_180=1,ml_area_225=1,ml_area_275=1,ml_maps_180=0,ml_maps_225=1,ml_maps_275=1)
 def ev(self,t,scope):
  if t[0]=='call':
   if t[1]=='maraerecoverybusy':return int(self.busy)
   if t[1]=='maraenavrestore':return 1
   f=t[1]
   if getattr(self,'denyCacheMetadata',False) and f in ('wobjectgettemplatetype','wobjectfindalllandscapebytemplatetype'):
    raise AssertionError('cache discovery must not require template metadata')
   if f=='maraenavrestore':return 1
   if f=='min':return min(self.ev(x,scope) for x in t[2])
   if f=='getplayercoordinates':return 'player'
   if f=='getitemcountininventorybyname':return getattr(self,'mapCount',0)
   if f=='getfellowshipcount':return 0
   if f=='strlen':return len(str(self.ev(t[2][0],scope)))
   if f=='listcontains':
    values=self.ev(t[2][0],scope);return isinstance(values,list) and self.ev(t[2][1],scope) in values
   if f=='listadd':
    values=self.ev(t[2][0],scope);values.append(self.ev(t[2][1],scope));return 1
   if f=='wobjectfindalllandscapebynamerx':
    pattern=self.ev(t[2][0],scope);return [k for k,v in self.caches.items() if re.search(pattern,v.get('name','Unearthed Cache'))]
   if f=='wobjectgetname':
    obj=self.ev(t[2][0],scope);return self.caches.get(obj,{}).get('name','Unearthed Cache') if obj in self.caches else ''
   if f=='wobjectfindalllandscapebytemplatetype':return list(self.caches) if self.ev(t[2][0],scope)==1004150 else []
   if f=='wobjectgettemplatetype':return 1004150 if self.ev(t[2][0],scope) in self.caches else 0
   if f=='wobjectgetphysicscoordinates':
    obj=self.ev(t[2][0],scope);return 'cache:'+str(obj) if obj in self.caches and not self.caches[obj].get('no_position') else ''
   if f=='coordinatedistanceflat':
    coords=[self.ev(x,scope) for x in t[2]]
    for coord in coords:
     if isinstance(coord,str) and coord.startswith('cache:'):return self.caches.get(int(coord.split(':')[1]),{}).get('distance',0)
    return getattr(self,'digDistance',0)
   if f=='actiontrymove':
    a=[self.ev(x,scope) for x in t[2]]
    assert self.open==self.v['tm_cache_id'] and self.open in self.caches
    assert int(a[0]) in self.container(self.open) and a[1:3]==[999,0]
    self.moves.append(tuple(a));return self.dispatch
   if f=='actiontryuseitem':
    obj=self.ev(t[2][0],scope);self.used.append(obj)
    if self.autoopen and self.caches.get(obj,{}).get('openable'):self.open=obj
    return 0
   if t[1]=='listcreate':return [self.ev(v,scope) for v in t[2]]
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

# Exact live replies that left five confirmation flags at zero in the first test.
observed=[
 (1,'Hea Hineroa the Golem Hunter',50,'Drumtaken Ironstone Golem'),
 (4,'Hea Kahureka the Siraluun Hunter',50,'Drumtaken Timber Siraluun'),
 (7,'Hea Tamaneru the Aun Hunter',20,'Aun Elder Shaman'),
 (8,'Hea Ngarekau the Mattekar Hunter',50,'Drumtaken Greater Canescent Mattekar'),
 (9,'Hea Hakerea the Olthoi Hunter',50,'Olthoi Soldier')]
w=World(0x1BC2002B);w.v.update(ml_zone=225,pt_npc_end=0)
for k in (0,2,3,5,6):w.v['pt_pickup_'+str(k)]=1
s=Sim(w,'T225_NPC_TALK_WAIT')
for key,npc,count,target in observed:
 w.chat=npc+' tells you, "My hunts suit a hunter of about level 225."';s.tick()
 check(w.v.get('pt_pickup_'+str(key),0)==0,'intro alone does not confirm '+npc)
 w.chat=npc+f' tells you, "You have defeated 0 of {count} {target}. The hunt is not yet finished."';s.tick()
 check(w.v.get('pt_pickup_'+str(key))==1,'actual live task reply confirms '+target)
w.v['pt_npc_end']=1;s.tick()
check(s.state=='T225_HUNT_LOAD','all ten confirmed tasks plus route end start hunting')
for key,npc,count,target in observed:
 w=World();w.v['ml_zone']=225;w.chat=f'You have killed {count} {target}s! Your task is complete!';s=Sim(w,'ENCOUNTER');s.tick()
 check(w.p.get('mm_225_done_'+str(key))==1,'treasure fight records actual completion '+target)
w=World();w.v['ml_zone']=225;w.chat='You have killed 50 Aun Shamans! Your task is complete!';s=Sim(w,'ENCOUNTER');s.tick()
check(w.p.get('mm_225_done_7',0)==0,'obsolete fifty-Shaman task cannot complete Elder Shaman contract')
print(f"PASS: {ns['checks']} total mission simulations, including exact live Northern hunter replies.")
# NPC failures never become an idle hold or falsely completed quests.
w=World(0x1BC2002B);w.v.update(ml_zone=225,pt_npc_end=1,pt_pickup_0=1);s=Sim(w,'T225_NPC_TALK_WAIT')
s.until('T225_NPC_RETRY');s.tick()
check(w.v['pt_pickup_0']==1 and w.v['mm_npc_retry']==1,'retry preserves confirmed work')
w.v['pt_npc_end']=1;s.until('T225_NPC_DECIDE');s.until('T225_HUNT_LOAD')
check(w.p.get('mm_225_done_1',0)==0 and w.p.get('mm_area_225',0)==0,'missing hunter never becomes completed')
check('1' in w.v['mm_npc_log_missing'],'retry snapshots missing hunter identities for logging')
w=World();w.v.update(ml_zone=225,pt_npc_end=0,pt_pickup_0=1,mm_npc_retry=1);s=Sim(w,'T225_NPC_TALK_WAIT');s.tick(181);s.until('T225_HUNT_LOAD')
check(s.state=='T225_HUNT_LOAD','silent route eventually starts confirmed tasks without waiting for end marker')
w=World(0x14B5002F);w.p.update(mm_fallback_180=1,mm_area_180=1,mm_area_225=1,mm_area_275=1);s=Sim(w,'Default');s.until('T180_FARM_RUN')
check(w.v['ml_zone']==180 and w.v['ml_run_mode']==2 and w.v['ml_use_maps']==1,'all cooldowns choose active 180 treasure circuit rather than stopping')
w=World(0x1BC2002B);w.v.update(ml_zone=225,pt_npc_end=1,mm_npc_retry=1);w.p.update(mm_fallback_180=1,mm_area_180=1,mm_area_275=1);s=Sim(w,'T225_NPC_TALK_WAIT');s.until('MAP_FALLBACK_180')
check(w.p.get('mm_area_225',0)==0 and w.p['mm_attempted_225']==1,'no confirmed 225 tasks defer unresolved area and fall back without false completion')
w=World();w.v.update(ml_zone=225,pt_npc_end=1,mm_npc_retry=1);s=Sim(w,'T225_TURNIN_WAIT');s.until('AREA_DEFERRED')
check('HOLD'!=s.state,'unconfirmed turn-ins are deferred without idle hold')
w=World();w.v.update(ml_zone=225,pt_npc_end=0);s=Sim(w,'T225_NPC_TALK_WAIT')
w.chat='Hea Hineroa the Golem Hunter tells you, "You have defeated 17 of 999 A Newly Named Creature. The hunt is not yet finished."';s.tick()
check(w.v['pt_pickup_1']==1,'NPC in-progress status accepts changed total and target without counting')
print(f"PASS: {ns['checks']} mission simulations including bounded retries, partial task work and 180 fallback.")
# Concurrent dig sites: deny the first IDs, use the actual open container only.
for denied in (1,2):
 winner=denied+1
 w=World(items=[item(10,container=winner)]);w.open=0
 w.caches={i:{'distance':0.5,'openable':i==winner} for i in range(1,winner+1)}
 s=Sim(w,'CACHE_PREP');s.until('CACHE_CONFIRM',100)
 check(w.used==list(range(1,winner+1)) and w.v['tm_cache_id']==winner,'tries distinct IDs until cache '+str(winner)+' actually opens')
 check(w.moves==[(10,999,0,1)] and not w.inv,'only bound cache dispatches one uncredited inventory move')
 w.ack(10);s.until('CACHE_FINISH');check(w.inv[1004101]==7,'multi-cache reward requires real inventory acknowledgment')
# Native cache discovery must not require template or coordinate metadata.
w=World(items=[item(10,container=2)]);w.open=0;w.denyCacheMetadata=True
w.v['tm_cache_preexisting']=[1,2];w.caches[1]['openable']=False
w.caches[2]['no_position']=True;w.caches[2]['distance']=200
w.caches[3]={'distance':0,'openable':True,'name':'Other Cache'}
s=Sim(w,'CACHE_PREP');s.until('CACHE_CONFIRM',100)
check(w.used==[1,2] and w.v['tm_cache_id']==2,'exact cache names rotate without a template, old-cache or coordinate prerequisite')
# Once bound, closing the window must reopen that ID, never switch to another cache.
w=World(items=[item(10)]);s=Sim(w,'CACHE_PREP');s.until('CACHE_PICKUP');w.open=2
s.until('CACHE_CONFIRM',100)
check(w.used==[1] and w.v['tm_cache_id']==1,'reopens bound cache without rebinding to another open cache')
# All denied candidates get a fair second round; deadline still prevents infinite waiting.
w=World();w.open=0
for cache in w.caches.values():cache['openable']=False
s=Sim(w,'CACHE_PREP');s.until('MISSION_ERROR',400)
check(w.used[:4]==[1,2,1,2] and not w.moves and not w.inv,'all denied caches rotate fairly until bounded timeout without false pickup')
w=World();w.open=0;w.autoopen=False;s=Sim(w,'CACHE_PREP');s.until('CACHE_SEARCH');w.inv[1004101]=7
s.until('CACHE_FINISH');check(not w.moves,'actual corpse/direct doubloon payout completes without requiring a chest')
def action_text(rule):return rule['ActionData']+' '+ ' '.join(action_text(r) for r in rule.get('ActionChildren',[]))
scan=' '.join(action_text(r) for r in rules if r['State']=='CACHE_SCAN')
check('wobjectfindalllandscapebynamerx[`^Unearthed Cache$`]' in scan and 'physicscoordinates' not in scan and 'templatetype' not in scan,'cache scan preserves proven name discovery without added metadata gates')
check('/ub use Unearthed Cache' not in ' '.join(action_text(r) for r in rules),'cache opening never selects an ambiguous object by name')
print(f"PASS: {ns['checks']} total mission simulations including simultaneous cache ownership and acknowledgments.")

w=World(items=[item(10)]);w.open=0;w.denyCacheMetadata=True
s=Sim(w,'CACHE_PREP');s.until('CACHE_CONFIRM',100)
check(w.used==[1] and len(w.moves)==1,'exact-name discovery needs no template metadata prerequisite')
# Preserve the proven Aun corpse branch; it does not require a cache to spawn.
w=World();w.open=0;w.caches={1:{'name':'Corpse of Aun Relaria','distance':1}}
s=Sim(w,'ENCOUNTER');s.tick()
check(w.options['EnableLooting']==1,'Aun corpse enables the existing profile looter')
w.inv[1004101]=7;s.until('CACHE_FINISH')
check(not w.used and not w.moves,'confirmed corpse doubloons finish without any cache-use or explicit duplicate pickup')
print(f"PASS: {ns['checks']} mission simulations, including single cache, concurrent caches and corpse payout.")

# A cache 24 yards away must be reached before opening or moving currency.
w=World(items=[item(10)]);w.open=0;w.caches={1:{'distance':22,'openable':True}}
s=Sim(w,'CACHE_PREP');s.until('CACHE_APPROACH_WAIT',100)
check(not w.used and not w.moves,'distant single cache enters approach without premature use or pickup')
w.v['tm_walk_ack']=1;s.tick(5)
check(s.state=='CACHE_APPROACH_WAIT' and not w.used,'accepted movement dispatch alone does not count as arrival')
w.caches[1]['distance']=0.5;s.until('CACHE_CONFIRM',100)
check(w.used==[1] and len(w.moves)==1,'arrival opens the only cache and dispatches its currency exactly once')
w.ack(10);s.until('CACHE_FINISH')
# Chase can change player position; the original dig anchor remains the return goal.
w=World();w.open=0;w.v['tm_cache_anchor']='dig';w.digDistance=22
s=Sim(w,'CACHE_PREP');s.until('CACHE_RETURN_WAIT',100)
check(w.v['tm_walk_target']=='dig' and not w.used,'post-fight collection returns to recorded digsite first')
w.v['tm_walk_ack']=1;w.digDistance=0;s.until('CACHE_SEARCH')
check(s.state=='CACHE_SEARCH','actual digsite arrival proceeds to cache search')
w=World();w.open=0;w.caches={1:{'distance':22,'openable':False},2:{'distance':0,'openable':True}}
s=Sim(w,'CACHE_PREP');s.until('CACHE_APPROACH_WAIT',100);s.tick(46);s.until('CACHE_OPEN_SELECTED',100)
check(w.v['tm_cache_probe_id']==2,'unreachable candidate times out and next local cache is tried')
w=World();w.open=0;w.caches={1:{'distance':22,'openable':True}}
s=Sim(w,'CACHE_PREP');s.until('CACHE_APPROACH_WAIT',100);w.dead=True;s.tick()
check(s.state=='MAP_DEATH' and not w.used and not w.moves,'death during approach yields to recovery without using or moving loot')
print(f"PASS: {ns['checks']} mission simulations including chase return, 24-yard single-cache approach, timeout and death.")

check(any('maraenavrestore[]' in action_text(r) for r in rules if r['State']=='MAP_DEATH'),'death delegates to recovery only after releasing mission navigation')

# Explicit hunt-only mode ignores finished tasks, cooldowns and the map fallback toggle.
for z,cell in ((180,0x14B5002F),(225,0x1BC2002B),(275,0x21B70028)):
 w=World(cell);w.p.update(mm_area_180=1,mm_area_225=1,mm_area_275=1,mm_attempted_180=1,mm_attempted_225=1,mm_attempted_275=1,mm_fallback_180=1)
 for area in (180,225,275):w.p['ml_area_'+str(area)]=int(area==z);w.p['ml_maps_'+str(area)]=0
 w.p['ml_hunt_only_'+str(z)]=1
 s=Sim(w,'Default');s.until(f'T{z}_FARM_RUN',150)
 check(w.v['ml_use_maps']==0 and w.options['EnableLooting']==1 and w.options['EnableCombat']==1,'hunt-only maps-off starts despite all cooldown/completion flags '+str(z))
 check('Talk_NPC' not in (w.route or '') and '/ra quests refresh' not in w.messages,'hunt-only never visits hunters or refreshes quests '+str(z))
 for lap in range(3):
  w.v['pt_travel_end']=1;s.until('CONTINUOUS_LAP_DONE');s.until(f'T{z}_FARM_RUN',80)
 check(w.v['ml_zone']==z and w.v['ml_use_maps']==0,'single hunt-only area repeats its own circuit '+str(z))
 # Hours without KT progress must not trip the old 45-minute quest timeout.
 for _ in range(4):s.tick(3600)
 check(s.state==f'T{z}_FARM_RUN','hours without quest progress do not stop pure hunting '+str(z))
 # A map in inventory, including stale active-map IDs, must not override Maps OFF.
 w.mapCount=10;w.v['tm_map_id']=1234;s.tick()
 check(s.state==f'T{z}_FARM_RUN','maps off never opens or searches even with maps in pack '+str(z))
# Multiple continuous areas rotate each completed nav, preserving each area's map toggle.
w=World();w.p.update(ml_hunt_only_180=1,ml_hunt_only_225=1,ml_area_275=0,ml_maps_180=0,ml_maps_225=1,mm_hunt_cursor=180)
s=Sim(w,'Default');s.until('T225_START_LOCATION',100)
check(w.v['ml_zone']==225 and w.v['ml_use_maps']==1,'continuous cursor chooses next area and its independent map preference')
s.state='CONTINUOUS_LAP_DONE';s.fired.clear();s.until('T180_START_LOCATION',100)
check(w.p['mm_hunt_cursor']==225 and w.v['ml_use_maps']==0,'last continuous area wraps to Lowlands with maps still off')
# Task areas retain ordinary NPC handling; no checkboxes changed means old task behavior.
w=World(0x1BC2002B);w.p.update(ml_area_180=0,ml_area_275=0);s=Sim(w,'Default');s.until('T225_NPC_TALK_WAIT',100)
check(w.route.endswith('Talk_NPCs'),'task mode still talks to hunters')
# Native death recovery returns a hunt-only character directly to hunting after respawn.
w=World();w.p.update(ml_hunt_only_180=1,ml_maps_180=0);w.v.update(ml_zone=180,ml_hunt_only=1,ml_use_maps=0)
s=Sim(w,'T180_DEATH_RECOVERY');s.until('T180_GET_TO_WAIT',100);w.cell=0x14B5002F;s.until('T180_FARM_RUN',100)
check(w.v['ml_use_maps']==0 and 'Talk_NPC' not in w.route,'hunt-only recovery resumes without a quest check or enabling maps')
print(f"PASS: {ns['checks']} simulations including independent per-area continuous hunting and overnight loops.")

# Optional mesh steps use clue bearings; raw movement is not issued when enabled.
for clue,heading,step in (('FAR_NORTH',0,24),('FAR_EAST',90,24),('CLOSE_SOUTHWEST',225,8)):
 w=World();w.p['ml_mesh_maps']=1;s=Sim(w,clue);s.until('MAP_MESH_WAIT')
 check(w.v['tm_mesh_heading']==heading and w.v['tm_mesh_step']==step,'clue selects bounded mesh step '+clue)
 check(not any('setmotion' in m for m in w.messages),'enabled mesh clue never issues raw forward motion '+clue)
 w.v.update(tm_walk_ack=1,tm_walk_target='player');s.until('MAP_RESUME')
 check(s.state=='MAP_RESUME','mesh arrival re-reads the map rather than running on a timer')
w=World();s=Sim(w,'FAR_EAST');s.until('RUN_WAIT')
check(any('setmotion Forward 1' in m for m in w.messages),'mesh option off preserves the original map movement')
w=World();w.v.update(tm_mesh_step=24,tm_mesh_failures=0);s=Sim(w,'MAP_MESH_FAILED');s.until('MAP_MESH_WAIT');w.v['tm_walk_ack']=-1;s.until('MAP_MESH_FAILED');s.until('MAP_MESH_WAIT');w.v['tm_walk_ack']=-1;s.until('MISSION_ERROR')
check(w.v['tm_mesh_failures']==3 and w.v['tm_mesh_step']==6,'missing mesh/path retries shorter steps then stops explicitly')
w=World();w.v['tm_walk_ack']=1;w.health=50;s=Sim(w,'MAP_MESH_WAIT');s.tick()
check(s.state=='MAP_COMBAT_RESUME','low HP interrupts mesh movement for normal combat/recovery')
print(f"PASS: {ns['checks']} total mission simulations, including continuous hunting and optional mesh clue movement.")

w=World();w.p['ml_mesh_maps']=1;s=Sim(w,'STUCK');s.until('MISSION_ERROR')
check(not any('setmotion' in m for m in w.messages),'mesh search never falls back to a blind backward setmotion')

# Optional task-camp quotas: fixed deadlines, partial turn-ins, and safe fallback.
for z,key in ((180,'0'),(225,'0'),(275,'zefir')):
 w=World();w.p['mm_camp_limit']=1;w.v.update(ml_zone=z,ml_hunt_only=0,ml_run_mode=1,pt_turnin=0,ml_use_maps=0,ml_fship_attempted=2)
 w.v['pt_pickup_'+key]=1
 s=Sim(w,f'T{z}_HUNT_RESUME');s.tick();deadline=w.p['mm_deadline_'+str(z)]
 check(deadline==w.now+7200 and w.v['mm_budget_active']==1,'quota starts on first task hunt '+str(z))
 s.tick(3000)
 check(s.state==f'T{z}_HUNT','timed mode replaces the old 45-minute KT-progress stop '+str(z))
 s.state=f'T{z}_HUNT_RESUME';s.fired.clear();s.tick()
 check(w.p['mm_deadline_'+str(z)]==deadline,'resuming a task hunt never grants another two hours '+str(z))
 w.now=deadline;s.until('BUDGET_EXPIRED');s.until(f'T{z}_GET_TO')
 check(w.p['mm_timed_'+str(z)]==1 and w.p['mm_cap_pending_'+str(z)]==1 and w.v['pt_turnin']==1,'expired quota queues persistent partial turn-in '+str(z))
 check(w.p.get(f'mm_{z}_done_{key}',0)==0,'quota expiry never completes an unfinished task '+str(z))
 s.state=f'T{z}_TURNIN_WAIT';s.fired.clear();w.v['pt_npc_end']=1
 s.until(f'T{z}_BUDGET_SETTLE');s.until('BUDGET_AREA_DONE');s.tick()
 check(w.p['mm_cap_pending_'+str(z)]==0 and s.state=='DISPATCH','partial turn-in advances without all hunters being complete '+str(z))
w=World();w.p.update(mm_camp_limit=1,mm_timed_180=1,mm_timed_225=1,mm_timed_275=1,mm_fallback_180=0)
s=Sim(w,'Default');s.until('BUDGET_LOWLANDS');w.cell=0x14B5002F;s.until('T180_FARM_RUN',120)
check(w.v['ml_zone']==180 and w.v['ml_hunt_only']==1 and w.v['ml_use_maps']==0,'timed task mode ends in continuous Lowlands salvage hunting with maps off')
w=World();w.p.update(mm_camp_limit=1,mm_area_180=1,mm_area_225=1,mm_area_275=1)
s=Sim(w,'Default');s.until('BUDGET_LOWLANDS')
check(s.state=='BUDGET_LOWLANDS','early completion of every camp uses the same Lowlands fallback')
w=World();w.p.update(mm_camp_limit=0,mm_timed_180=1,mm_deadline_180=1);s=Sim(w,'Default');s.until('T180_START_LOCATION')
check(w.v['ml_zone']==180,'option off ignores stale expired quotas')
w=World();w.p.update(mm_camp_limit=1,ml_hunt_only_180=1,mm_deadline_180=1,mm_timed_180=1,ml_area_225=0,ml_area_275=0,ml_maps_180=0);w.cell=0x14B5002F
s=Sim(w,'Default');s.until('T180_FARM_RUN',120);s.tick(10000)
check(s.state=='T180_FARM_RUN' and w.v['mm_budget_active']==0,'Hunt only is never limited by task-camp quotas')
w=World();w.p.update(mm_camp_limit=1,mm_deadline_225=1,mm_timed_225=1,mm_cap_pending_225=1,ml_hunt_only_225=1);w.cell=0x1BC2002B
s=Sim(w,'Default');s.until('T225_TURNIN_WAIT',120)
check(w.v['ml_zone']==225 and w.v['mm_cap_return']==1,'reconnect finishes the pending partial turn-in before choosing another camp')
w=World();w.p.update(mm_camp_limit=1,mm_deadline_180=1);w.v.update(mm_budget_active=1,ml_zone=180,ml_hunt_only=0,pt_turnin=0,tm_cache_pending=1234);w.open=0;w.caches={};w.now=10;w.v['tm_cache_deadline']=1000
s=Sim(w,'CACHE_SEARCH');s.tick()
check(s.state=='CACHE_SEARCH' and w.v['tm_cache_pending']==1234,'quota cannot interrupt a possibly issued inventory move')
w.v['tm_cache_pending']=0;s.until('BUDGET_EXPIRED')
check(s.state=='BUDGET_EXPIRED','quota is enforced as soon as the pending action settles')
print(f"PASS: {ns['checks']} mission simulations including optional two-hour camps, partial turn-in and Lowlands fallback.")
w=World();w.p.update(mm_camp_limit=1,mm_timed_225=1,mm_cap_pending_225=1);w.v.update(ml_zone=225,mm_cap_return=1,pt_npc_end=0,mm_npc_retry=1)
s=Sim(w,'T225_TURNIN_WAIT');s.tick(181);s.until('BUDGET_AREA_DONE',100);s.tick()
check(w.p.get('mm_attempted_225',0)==0 and w.p['mm_cap_pending_225']==0,'silent partial turn-in has bounded retries and does not falsely complete/defer task state')
w=World();w.p.update(mm_camp_limit=1,mm_timed_180=1,mm_deadline_180=7200);w.now=10000
s=Sim(w,'Default');s.until('T225_START_LOCATION',100);s.state='T225_HUNT_RESUME';s.fired.clear();s.tick()
check(w.p['mm_deadline_225']==w.now+7200 and w.p['mm_deadline_180']==7200,'next camp receives its own quota without rewriting the previous deadline')
print(f"PASS: {ns['checks']} total mission simulations with optional camp budgets and bounded partial turn-in.")

# Live stride preferences are read at each new clue; active retry legs keep their own step.
for configured,expected in ((30,30),(72,72),(1000,120),(-1,24)):
 w=World();w.p.update(ml_mesh_maps=1,mm_mesh_far=configured);s=Sim(w,'FAR_EAST');s.until('MAP_MESH_WAIT')
 check(w.v['tm_mesh_step']==expected,'far stride preference is bounded and used by the next clue '+str(configured))
w=World();w.p.update(ml_mesh_maps=1,mm_mesh_far=80,mm_mesh_close=20);s=Sim(w,'CLOSE_NORTH');s.until('MAP_MESH_WAIT')
check(w.v['tm_mesh_step']==20,'close clue has its own live stride preference')
w.p['mm_mesh_close']=12;s.tick()
check(w.v['tm_mesh_step']==20,'changing preference does not change a leg already planned')
s.state='CLOSE_NORTH';s.fired.clear();s.until('MAP_MESH_WAIT')
check(w.v['tm_mesh_step']==12,'next close clue reads the updated preference')
print(f"PASS: {ns['checks']} simulations including live far/close stride preferences.")

w=World();w.p.update(ml_mesh_maps=1,mm_mesh_far=80,mm_mesh_close=60);s=Sim(w,'CLOSE_EAST');s.until('MAP_MESH_WAIT')
check(w.v['tm_mesh_step']==60,'close stride can also be tuned beyond the original short-distance range')
