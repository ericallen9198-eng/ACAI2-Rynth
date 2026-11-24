"""Execute genuine-parser output against fake game observations, never live actions."""
import sys,re
from pathlib import Path
lib=Path(__file__).resolve().parents[1]/'ACAI2.MaraeMission/test_mission.py'
ns={'__file__':str(lib),'sys':sys};exec(compile(lib.read_text().split('for zone,cell in ')[0],str(lib),'exec'),ns)
LegacyWorld,LegacySim,check,item=ns['World'],ns['Sim'],ns['check'],ns['item']
class Sim(LegacySim):
 def cond(self,r):
  if r['Condition']==25:return getattr(self.w,'navValid',True)
  return super().cond(r)
 def tick(self,seconds=1):
  if getattr(self.w,'helper',True):self.w.v['mh_heartbeat']=self.w.now+seconds
  return super().tick(seconds)
class World(LegacyWorld):
 def __init__(self,items=(),opened=1):
  super().__init__(items=items);self.open=opened;self.v.update(tm_walk_generation=0,mh_walk_token=0,tm_resume_needed=0,tm_map_gate_until=0,tm_map_id=0,tm_cache_pending=0,tm_cache_pickup_retry_at=0)
  self.p={'mh_tm_dig_active':1,'maphunt_close':30,'maphunt_far':130,'meshnav':0}
 def ev(self,t,scope):
  if t[0]=='call':
   if t[1]=='maraemissionapi':return 6
   if t[1] in ('maraenavsave','maraenavrestore'):return 1
   if t[1]=='testpvar':return self.ev(t[2][0],scope) in self.p
   if t[1]=='maraecachepickup':
    assert self.p.get('mh_mm_pickup_unresolved')==1,'durable checkpoint must precede currency move'
    obj=self.ev(t[2][0],scope);assert self.open==self.v['tm_cache_id'] and obj in self.container(self.open)
    self.moves.append((obj,999,0,self.v.get('tm_cache_stackmode',0)));return self.dispatch
  return super().ev(t,scope)

w=World();s=Sim(w,'Default');s.until('HUNT_VERIFY')
check(w.p['maphunt_close']==30 and w.p['maphunt_far']==130 and w.p['meshnav']==0,'new-character defaults: measured close 30 / far 130 and mesh off')
check(w.messages.count('/ra nav load maphunt.nav')==1,'loads only the user-supplied maphunt.nav')
w.v['mh_heartbeat']=w.now+1;s.until('HUNT');check(w.options['EnableLooting']==1 and w.options['EnableCombat']==1,'normal hunting uses native looting and combat')
w=World();w.navValid=False;s=Sim(w,'HUNT_VERIFY');s.until('HOLD');check(w.options['EnableNav']==0,'missing/empty route never starts wandering')
w=World();w.helper=False;s=Sim(w,'HUNT_VERIFY');s.until('HOLD');check(w.options['EnableNav']==0,'missing Lua heartbeat prevents starting a route without its movement helper')
for close in (True,False):
 for direction,heading in [('NORTH',0),('NORTHEAST',45),('EAST',90),('SOUTHEAST',135),('SOUTH',180),('SOUTHWEST',225),('WEST',270),('NORTHWEST',315)]:
  for mesh in (0,1):
   w=World();w.p['meshnav']=mesh;s=Sim(w,('CLOSE_' if close else 'FAR_')+direction);s.until('MAP_MESH_WAIT' if mesh else 'DIRECT_WAIT')
   check(w.v['tm_mesh_heading']==heading and w.v['tm_mesh_step']==(30 if close else 130),'same configured distance and bearing in both modes')
w=World();s=Sim(w,'DIRECT_WAIT');w.v.update(tm_step_origin='origin',tm_mesh_step=30);w.digDistance=29;s.tick(1);check(s.state=='DIRECT_WAIT','direct travel does not finish before measured displacement')
w.digDistance=30;s.tick();s.until('MAP_RESUME');check('/ub clearmotion' in w.messages,'direct travel clears motion at the measured distance')
w=World();s=Sim(w,'MAP_MESH_RETRY');w.v.update(tm_mesh_failures=3,tm_mesh_step=130);s.until('HUNT');check(w.v['tm_map_gate_until']>w.now,'failed mesh retries resume hunting with a map backoff')
for contents in ([item(10)], [item(11,30001,2,15)], [item(10),item(11,30001,2,15)]):
 w=World(contents);s=Sim(w,'CACHE_PREP')
 for obj in [x['id'] for x in contents]:
  s.until('CACHE_CONFIRM');check(w.moves[-1][0]==obj and w.p['mh_mm_pickup_unresolved']==1,'currency pickup is serial with persistent pre-action checkpoint');w.ack(obj);s.tick()
 s.until('CACHE_FINISH');check(w.inv and w.p['mh_mm_pickup_unresolved']==0,'confirmed doubloons/optional notes finish without demanding absent currency')
w=World([item(10)]);s=Sim(w,'CACHE_PREP');s.until('CACHE_CONFIRM');s.tick(21);s.until('HOLD');check(len(w.moves)==1 and w.p['mh_mm_pickup_unresolved']==1,'unconfirmed item move requires review without blind resend')
w=World([item(10)]);w.p['mh_mm_pickup_unresolved']=1;s=Sim(w,'Default');s.until('HOLD');check(not w.moves,'reconnect does not replay an unresolved move')
w=World([item(10,container=4)],opened=0);w.caches={1:{'openable':False},2:{'openable':False},3:{'openable':False},4:{'openable':True}};s=Sim(w,'CACHE_PREP');s.until('CACHE_CONFIRM',200)
check(w.used[:4]==[1,2,3,4] and w.v['tm_cache_id']==4,'tries all candidate IDs until the owned cache opens, including a single usable cache')
w=World([item(10)],opened=0);w.caches={1:{'openable':True,'distance':24}};s=Sim(w,'CACHE_PREP');s.until('CACHE_APPROACH_WAIT');check(w.v['tm_walk_target']=='cache:1','a cache 24 yards away receives an approach instead of being hidden')
w.caches[1]['distance']=2.9;s.until('CACHE_CONFIRM');check(w.used==[1],'stops short and uses selected cache once inside native interaction range')
w=World();w.open=0;w.chat="You've earned 850,000,000 experience.";s=Sim(w,'ENCOUNTER');s.tick();check(s.state=='ENCOUNTER','ordinary XP and mob kills cannot trigger cache search')
w.chat='Something buried at the dig gives way, and a cache settles into the loose earth. It answers only to you.';s.tick();check(s.state=='CACHE_PREP','actual reward completion triggers cache collection')
w=World();w.v.update(tm_cache_anchor='anchor',tm_event_deadline=1800,tm_aun_loot_active=0);w.digDistance=55;w.targets=3;s=Sim(w,'ENCOUNTER');s.until('DIGSITE_RETURN_WAIT');check(w.options['EnableCombat']==0 and w.v['tm_walk_target']=='anchor','55-yard boundary yields combat and returns to the actual digsite')
w=World();w.busy=True;s=Sim(w,'MAP_DEATH');s.tick();s.tick(60);check(s.state=='DEATH_WAIT' and '/ls' not in w.messages,'native corpse recovery keeps movement ownership and never issues a hardcoded recall')
w.busy=False;s.until('HUNT_VERIFY');check('/ra nav load maphunt.nav' in w.messages,'respawn/recovery returns to the supplied hunt route')
print('MapHunt fake-world scenarios passed.')
