"""Offline interpreter of the emitted cache-state rules. All game actions are fake recordings."""
import json,re,sys
from pathlib import Path
rules=json.load(open(sys.argv[1])); checks=0

def check(ok,msg):
 global checks
 assert ok,msg
 checks+=1;print('PASS:',msg)

def parse(s):
 tok=re.findall(r'`[^`]*`|\d+(?:\.\d+)?|[A-Za-z_$][\w.$]*|\|\||&&|==|!=|>=|<=|[^\s]',s);i=0
 prec={'||':1,'&&':2,'==':3,'!=':3,'>':4,'<':4,'>=':4,'<=':4,'+':5,'-':5,'*':6,'/':6}
 def expr(p=0):
  nonlocal i
  t=tok[i];i+=1
  if t in ('!','-'):left=('un',t,expr(7))
  elif t=='(':
   left=expr();assert tok[i]==')';i+=1
  elif t.startswith('`'):left=('lit',t[1:-1])
  elif t[0].isdigit():left=('lit',float(t) if '.' in t else int(t))
  elif i<len(tok) and tok[i]=='[':
   i+=1;aa=[]
   while tok[i]!=']':
    aa.append(expr())
    if tok[i]!=',':break
    i+=1
   assert tok[i]==']',(s,tok[i:]);i+=1;left=('call',t,aa)
  else:left=('lit',1 if t.lower()=='true' else 0 if t.lower()=='false' else t)
  while i<len(tok) and prec.get(tok[i],-1)>=p:
   op=tok[i];i+=1;left=('op',op,left,expr(prec[op]+1))
  return left
 tree=expr();assert i==len(tok),(s,tok[i:]);return tree

def numeric(x):
 try:return float(x)
 except (ValueError,TypeError):return None

def yes(x):return x not in ('',0,None,'false','False')

class World:
 def __init__(self,items,opened=1):
  self.now=0;self.v={'tm_cache_deadline':300,'tm_doubloon_before':0};self.p={'tm_dig_active':1};self.options={'LootChestsMacroOff':1};self.open=opened;self.items={i['id']:dict(i) for i in items};self.inv={};self.moves=[];self.used=[];self.messages=[];self.dispatch=1;self.autoopen=True;self.dead=False;self.health=100;self.maxhealth=100;self.portaling=False;self.cell=565641256;self.portal_exit=False
 def item(self,i):return self.items.get(int(i),{})
 def container(self,i):return [k for k,v in self.items.items() if v.get('container',1)==i]
 def ack(self,item,amount=None):
  a=self.items[item];q=a['amount'] if amount is None else amount;assert 0<q<=a['amount'];self.inv[a['template']]=self.inv.get(a['template'],0)+q;a['amount']-=q
  if a['amount']==0:del self.items[item]
 def eval(self,s,scope=None):return self.ev(parse(s),scope or {})
 def ev(self,t,scope):
  kind=t[0]
  if kind=='lit':return scope.get(t[1],t[1]) if isinstance(t[1],str) else t[1]
  if kind=='un':return not yes(self.ev(t[2],scope)) if t[1]=='!' else -float(self.ev(t[2],scope))
  if kind=='op':
   op=t[1];a=self.ev(t[2],scope)
   if op=='&&':return yes(a) and yes(self.ev(t[3],scope))
   if op=='||':return yes(a) or yes(self.ev(t[3],scope))
   b=self.ev(t[3],scope);aa,bb=numeric(a),numeric(b)
   if aa is not None and bb is not None:a,b=aa,bb
   if op=='+':return a+b if isinstance(a,(int,float)) and isinstance(b,(int,float)) else str(a)+str(b)
   if op=='-':return a-b
   if op=='*':return a*b
   if op=='/':return a/b
   return {'==':lambda:a==b,'!=':lambda:a!=b,'>':lambda:a>b,'<':lambda:a<b,'>=':lambda:a>=b,'<=':lambda:a<=b}[op]()
  f,raw=t[1],t[2]
  if f in ('if','iif'):
   return self.ev(raw[1],scope) if yes(self.ev(raw[0],scope)) else self.ev(raw[2],scope) if len(raw)>2 else 0
  a=[self.ev(x,scope) for x in raw]
  if f in ('getvar','getpvar'):return (self.v if f=='getvar' else self.p).get(a[0],0)
  if f in ('setvar','setpvar'):(self.v if f=='setvar' else self.p)[a[0]]=a[1];return a[1]
  if f=='getunixtime':return self.now
  if f=='getcharvital_current':return 0 if self.dead else self.health
  if f=='getcharvital_buffedmax':return self.maxhealth
  if f=='isportaling':return int(self.portaling)
  if f=='getplayerlandcell':return self.cell
  if f=='getplayerlandblock':return self.cell & 0xffff0000
  if f=='wobjectgetopencontainer':return self.open
  if f=='wobjectgetplayer':return 999
  if f=='wobjectgetname':return 'Unearthed Cache' if a[0] in (1,2) else 'Other container' if a[0] else ''
  if f in ('wobjectgetid','wobjectfindbyid'):return a[0]
  if f=='wobjectfindallbycontainer':return self.container(a[0]) if a[0] else []
  if f=='wobjectfindalllandscapebynamerx':return []
  if f=='wobjectgetintprop':return self.item(a[0]).get({218103808:'template',12:'amount'}.get(a[1],''),0)
  if f=='wobjectgetobjectclass':return self.item(a[0]).get('class',0)
  if f=='listfilter':return [v for v in a[0] if yes(self.eval(a[1],{'$1':v}))]
  if f=='listcount':return len(a[0])
  if f=='listgetitem':return a[0][int(a[1])]
  if f=='max':return max(a)
  if f=='getinventorycountbytemplatetype':return self.inv.get(a[0],0)
  if f=='ragetsetting':return self.options.get(a[0],0)
  if f in ('rasetsetting','uboptset'):self.options[a[0]]=a[1];return 1
  if f=='actiontrymove':
   assert self.open==self.v['tm_cache_id'] and self.open in (1,2)
   assert int(a[0]) in self.container(self.open) and a[1:3]==[999,0]
   self.moves.append(tuple(a));return self.dispatch
  if f=='actiontryuseitem':
   self.used.append(a[0])
   if self.autoopen:self.open=a[0]
   return 0
  if f=='chatbox':
   self.used.append(a[0]);assert a[0]=='/ub use Unearthed Cache'
   if self.autoopen:self.open=1
   return 0
  if f=='echo':self.messages.append(a[0]);return 0
  raise AssertionError('unimplemented simulated function: '+f)

class Sim:
 def __init__(self,w,state='CACHE_PREP'):self.w=w;self.state=state;self.entered=w.now;self.fired=set();self.changed=False
 def cond(self,r):
  c=r['Condition']
  if c==0:return False
  if c==1:return True
  if c==7:return self.w.dead
  if c==19:return self.w.portal_exit
  if c==2:return all(self.cond(x) for x in r['Children'])
  if c==6:return self.w.now-self.entered>=float(r['ConditionData'])
  if c==26:return yes(self.w.eval(r['ConditionData']))
  if c in (4,27):return False
  raise AssertionError(c)
 def act(self,r):
  a=r['Action'];d=r['ActionData']
  if a==0:return
  if a==4:
   for c in r['ActionChildren']:self.act(c)
  elif a==2:self.state=d;self.changed=True
  elif a==7:self.w.eval(d)
  elif a==12:
   k,v=d.split(';',1);self.w.options[k]=self.w.eval(v)
  elif a==1:self.w.messages.append(d)
  else:raise AssertionError(a)
 def tick(self,seconds=1):
  self.w.now+=seconds;self.changed=False;current=self.state
  for i,r in enumerate(rules):
   if r['State']!=current or i in self.fired or not self.cond(r):continue
   self.fired.add(i);self.act(r)
   if self.changed:self.entered=self.w.now;self.fired.clear();break
 def until(self,state,limit=30):
  for _ in range(limit):
   if self.state==state:return
   self.tick()
  raise AssertionError((self.state,state,self.w.now,self.w.v))

def item(i,template=1004101,amount=7,cls=7,container=1):return dict(id=i,template=template,amount=amount,**{'class':cls},container=container)
def finish(s):s.until('CACHE_FINISH');s.tick();assert s.state=='HUNT_RESUME'

w=World([item(10)]);s=Sim(w);s.until('CACHE_CONFIRM')
check(len(w.moves)==1 and w.inv.get(1004101,0)==0,'dispatch alone is not counted as a pickup')
w.ack(10);finish(s);check(w.inv[1004101]==7 and w.options['LootChestsMacroOff']==1,'doubloon-only cache finishes without requiring any trade notes and restores the prior pickup option')

w=World([item(10),item(11,30001,2,15),item(12,30002,3,15),item(13,777,1,11)]);s=Sim(w)
for expected in (10,11,12):
 s.until('CACHE_CONFIRM');check(w.moves[-1][0]==expected,'serial explicit pickup for item '+str(expected));w.ack(expected);s.tick()
finish(s);check(w.inv=={1004101:7,30001:2,30002:3} and 13 in w.items,'both currencies collected; unrelated cache loot remains untouched')

w=World([item(11,30001,2,15)]);s=Sim(w);s.until('CACHE_CONFIRM');w.ack(11);finish(s)
check(w.inv.get(30001)==2,'notes-only cache does not require a doubloon payout')

w=World([item(10)]);s=Sim(w);s.until('CACHE_CONFIRM');w.ack(10);s.until('CACHE_SETTLE');w.items[11]=item(11,30001,2,15);s.tick()
check(s.state=='CACHE_SEARCH','a late-arriving note interrupts empty-cache settling')
s.until('CACHE_CONFIRM');w.ack(11);finish(s)
check(w.inv.get(30001)==2,'late note is picked up before hunt resumes')

w=World([item(10,amount=10)]);s=Sim(w);s.until('CACHE_CONFIRM');w.ack(10,4);s.tick();s.until('CACHE_CONFIRM')
check(w.moves[-1][-1]==0 and len(w.moves)==2,'partial merge is acknowledged and remaining stack uses a free slot')
w.ack(10);finish(s);check(w.inv[1004101]==10,'partial plus final pickup counts only actual inventory additions')

w=World([item(10)]);s=Sim(w);s.until('CACHE_CONFIRM');s.tick(21)
check(s.state=='ERROR_HOLD' and len(w.moves)==1,'unacknowledged dispatched move holds without a blind resend')
s.tick();check(w.options['LootChestsMacroOff']==1,'failure handler restores prior pickup option')

w=World([item(10)]);w.dispatch=0;s=Sim(w);s.until('CACHE_CONFIRM');s.tick(2);w.dispatch=1;s.until('CACHE_CONFIRM')
check(len(w.moves)==2,'a locally rejected dispatch can retry after waiting')

w=World([item(10,container=2)]);s=Sim(w);s.until('CACHE_SEARCH');s.tick();w.open=2
for _ in range(8):s.tick()
check(not w.moves,'a different open cache cannot supply items to the bound pickup')

w=World([]);s=Sim(w);s.until('CACHE_SEARCH');s.tick();s.tick(301)
check(s.state=='ERROR_HOLD','an initially empty/unobserved cache is not falsely reported as a successful pickup')

w=World([],opened=0);w.inv[1004101]=7;s=Sim(w,'ENCOUNTER');s.tick()
check(s.state=='CACHE_FINISH' and not w.moves and not w.used,'existing corpse doubloon payout completes without opening or waiting for a cache')

# Verify the bound-cache reopen path, rather than selecting a new same-named cache.
w=World([item(10)],opened=0);s=Sim(w);s.until('CACHE_CONFIRM');w.ack(10);s.tick();w.open=0
for _ in range(8):s.tick()
check(1 in w.used,'a closed known cache is reopened by bound id')
for phase in ['CACHE_PREP','CACHE_PICKUP','CACHE_CONFIRM','CACHE_SETTLE','HUNT','VERIFY_ZEFIR_WAIT','GET_TO','REGION_START','ROUTE_HOLD']:
 w=World([item(10)]);w.v.update(pt_region=3,pt_npc_end=1,tm_cache_pending=10,tm_map_id=123);w.dead=True;s=Sim(w,phase);s.tick()
 check(s.state=='DEATH_RECOVER' and not w.moves,'death takes recovery priority in '+phase)
 w.dead=False;s.tick();check(s.state=='RESPAWN_WAIT' and '/ra stop' not in w.messages and not w.used,'death cleanup leaves the macro running and does not recall after '+phase)
 check(w.v['tm_cache_pending']==0 and w.v['tm_map_id']==0 and w.p.get('tm_stat_success_search',0)==0,'interrupted pickup/map is cleared without recording success after '+phase)
for region,dest in [(1,'CAMP_EAST'),(2,'CAMP_NORTHEAST'),(3,'CAMP_WEST')]:
 w=World([]);w.v.update(pt_region=region,pt_npc_end=1);s=Sim(w,'DEATH_RECOVER');s.tick();s.until(dest)
 check('/ls' not in w.messages and '/ra stop' not in w.messages,'natural camp respawn returns to the prior region '+str(region))
 s.tick();check(s.state=='TRAVEL_WAIT',"recovery starts that region's existing nav")
 w.v['pt_travel_end']=1;s.until('HUNT');check(all(w.options.get(x)==1 for x in ['EnableNav','EnableCombat','EnableLooting','EnableBuffing']),'normal hunting/buffing/looting resumes after returning to region '+str(region))
w=World([]);w.v.update(pt_region=2,pt_npc_end=1);s=Sim(w,'DEATH_RECOVER');s.tick();w.portaling=True
for _ in range(12):s.tick()
check(s.state=='RESPAWN_WAIT' and not w.used,'no movement or route selection while still in portal space')
w.portaling=False;w.health=0
for _ in range(8):s.tick()
check(s.state=='RESPAWN_WAIT','a stale/dead zero-health sample cannot resume the route')
w.health=100;s.until('CAMP_NORTHEAST');check('/ls' not in w.messages,'resuming after portal/health readiness does not cast another recall')
w=World([]);w.cell=(0x14b5<<16)|0x28;w.v.update(pt_region=3,pt_npc_end=1);s=Sim(w,'DEATH_RECOVER');s.tick();s.until('GET_TO_WAIT')
check('/ra nav load 275 - From Lifestone' in w.messages and '/ls' not in w.messages,'recorded town respawn uses only the approach route, without /ls')
w.cell=565641256;s.until('CAMP_WEST');check(s.state=='CAMP_WEST','actual camp location confirms portal arrival even if portal chat/event was missed')
w=World([]);w.v.update(pt_region=0,pt_npc_end=0);s=Sim(w,'DEATH_RECOVER');s.tick();s.until('NPC_TALK')
check('/ls' not in w.messages,'an interrupted initial NPC sequence is verified before hunting')
w=World([]);w.cell=0x12340001;w.v.update(pt_region=1,pt_npc_end=1);s=Sim(w,'DEATH_RECOVER');s.tick();s.tick(91)
check(s.state=='ERROR_HOLD' and not w.used and '/ls' not in w.messages,'unknown respawn locations do not trigger blind walking or repeated recalls')
w=World([]);w.v.update(pt_region=1,pt_npc_end=1);s=Sim(w,'DEATH_RECOVER');s.tick();w.dead=True;s.tick();w.dead=False;s.tick();s.until('CAMP_EAST')
check(w.p['tm_stat_death']==2,'a second death during recovery restarts the same recovery path')
# Default also restores an interrupted cache hold after a manual stop or reload.
w=World([]);w.p={'pt_cache_pickup_held':1,'pt_cache_old_pickup':1};w.options['LootChestsMacroOff']=0
sim=Sim(w,'Default');first=next(r for r in rules if r['State']=='Default' and r['Condition']==1)
for a in first['ActionChildren'][:2]:sim.act(a)
check(w.options['LootChestsMacroOff']==1 and w.p['pt_cache_pickup_held']==0,'Default restores a cache-only setting left held by death or an interrupted macro')
w=World([]);w.cell=0x12340001;s=Sim(w,'GET_TO');s.tick();w.cell=565641256;s.until('NPC_TALK')
check(w.messages.count('/ls')==1,'startup recall is issued once when needed')
check('/ra nav load 275 - From Lifestone' not in w.messages and '/ra nav load 275 - Get to' not in w.messages,'startup recall landing at camp does not walk the recorded town approach')
print(f'{checks} cache/death-state simulations passed. No game or network actions executed.')
