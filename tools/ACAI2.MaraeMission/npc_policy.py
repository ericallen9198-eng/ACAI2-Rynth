"""Bounded NPC attempts, status-based acceptance, and an explicit 180-map fallback."""
import re

def apply(states,keys,state,action,expr):
 hunters={}
 for z in (180,225,275):
  p=f'T{z}_';body=states[p+'NPC_TALK_WAIT']
  pairs=re.findall(r'ChatMatch \{\^(.+?) tells you, [^\n]+\}\n\s*DO: (?:DoAll\s+DoExpr|DoExpr) \{setvar\[pt_pickup_([^,]+),1\]\}',body)
  hunters[z]={key:name for name,key in pairs};assert len(hunters[z])==len(keys[z]),(z,pairs)
  # NPC identity and the server's status words matter, not a guessed total/monster label.
  for key,name in hunters[z].items():
   pattern='^'+re.escape(name)+r' tells you, "(?:Slay \d+ [^"]+\. Return when the work is done\.|You have defeated \d+ of \d+ [^"]+\. The hunt is not yet finished\.)"$'
   rx=r'(ChatMatch \{)\^'+re.escape(name)+r' tells you, [^\n]+(\}\n\s*DO: (?:DoAll\s+DoExpr|DoExpr) \{setvar\[pt_pickup_'+re.escape(key)+r',1\]\})'
   body,n=re.subn(rx,lambda m:m[1]+pattern+m[2],body,count=1);assert n==1,(z,key)
  states[p+'NPC_TALK_WAIT']=body
  def log(turnin):
   # Snapshot missing identities before another area's init can clear temporary flags.
   raw='listcreate['+','.join('`'+k+'`' for k in keys[z])+']'
   prefix='pt_unavailable_' if turnin else 'pt_pickup_'
   return [expr(f'setvar[mm_npc_log_zone,{z}]'),expr(f'setvar[mm_npc_log_turnin,{int(turnin)}]'),
    expr('setvar[mm_npc_log_missing,listfilter['+raw+',`getvar['+prefix+'+$1]!=1`]]'),
    expr('setvar[mm_npc_log_generation,getvar[mm_npc_log_generation]+1]')]
  for phase in ('NPC','TURNIN'):
   turnin=phase=='TURNIN';wait=p+('TURNIN_WAIT' if turnin else 'NPC_TALK_WAIT');start=p+('TURNIN_TALK' if turnin else 'NPC_TALK')
   states[start]=states[start].replace('DoExpr {setvar[pt_npc_end,0]}',expr('setvar[mm_npc_retry,0]')+'\n\t\t\t\tDoExpr {setvar[pt_npc_end,0]}',1)
   target=p+('TURNIN_DECIDE' if turnin else 'NPC_DECIDE')
   state(p+phase+'_RETRY',action(log(turnin)+['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}',expr('setvar[mm_npc_retry,1]'),expr('setvar[pt_npc_end,0]'),expr('setvar[ml_status,`Retrying unconfirmed hunter replies once`]'),f'EmbedNav {("180 KT - Talk NPCs" if z==180 else "225 KT - Talk NPCs" if z==225 else "275 - Talk NPCs").replace(" ","_")} {{once}}','SetOpt {EnableNav} {True}','SetState {'+wait+'}']))
   b=states[wait]
   # Keep exact successful/all-cooldown branches; replace the generic stop/timeout tail.
   marker='\tIF: Expr {getvar[pt_npc_end]==1}\n' if turnin else '\tIF: SecsInStateGE 300\n'
   cut=b.index(marker);b=b[:cut]
   b+='\tIF: Expr {getvar[pt_npc_end]==1 && getvar[mm_npc_retry]==0}\n\t\tDO: SetState {'+p+phase+'_RETRY}\n'
   b+='\tIF: Expr {getvar[pt_npc_end]==1}\n\t\tDO: SetState {'+target+'}\n'
   b+='\tIF: All\n\t\t\tSecsInStateGE 180\n\t\t\tExpr {getvar[mm_npc_retry]==0}\n\t\tDO: SetState {'+p+phase+'_RETRY}\n'
   b+='\tIF: SecsInStateGE 180\n\t\tDO: SetState {'+target+'}\n~~ }\n\n';states[wait]=b
   if turnin:state(target,action(log(True)+[expr('setvar[ml_status,`Unconfirmed turn-ins saved for review; continuing other work`]'),'SetState {AREA_DEFERRED}']))
   else:
    active=' || '.join(f'(getvar[pt_pickup_{k}]==1 && getvar[pt_unavailable_{k}]!=1 && getpvar[mm_{z}_done_{k}]!=1)' for k in keys[z])
    state(target,'\tIF: Expr {'+active+'}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in log(False)+[expr('setvar[ml_status,`Continuing confirmed tasks; unresolved hunters logged`]'),'SetState {'+p+('HUNT_INIT' if z==275 else 'HUNT_LOAD')+'}'])+'\tIF: Always\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in log(False)+['SetState {AREA_DEFERRED}']))
  # Unconfirmed tasks are excluded from this pass, never written as completed.
  for n in (p+'HUNT',p+'HUNT_INIT'):
   if n not in states:continue
   def eligibility(m):
    line=m[0]
    for k in keys[z]:
     active=f'(getvar[pt_pickup_{k}]==1 && getvar[pt_unavailable_{k}]!=1 && getpvar[mm_{z}_done_{k}]!=1)'
     line=line.replace(f'getpvar[mm_{z}_done_{k}]==1','('+active+'==0)').replace(f'getpvar[mm_{z}_done_{k}]==0',active)
    return line
   states[n]=re.sub(r'^\tIF: Expr \{[^\n]+',eligibility,states[n],flags=re.M)
  states['DISPATCH']=states['DISPATCH'].replace(f'getpvar[mm_area_{z}]!=1 &&',f'getpvar[mm_area_{z}]!=1 && getpvar[mm_attempted_{z}]!=1 &&')
 state('AREA_DEFERRED',action([expr('setpvar[`mm_attempted_`+getvar[ml_zone],1]'),expr('setvar[ml_status,`Hunter checks unresolved; trying remaining areas before 180 treasures`]'),'SetState {DISPATCH}']))
 # Final dispatcher branch: work can continue even if every quest is on cooldown.
 b=states['DISPATCH'];pos=b.rindex('\tIF: Always\n')
 b=b[:pos]+'\tIF: Expr {getpvar[mm_fallback_180]!=0}\n\t\tDO: SetState {MAP_FALLBACK_180}\n'+b[pos:];states['DISPATCH']=b
 state('MAP_FALLBACK_180',action([expr('setvar[ml_run_mode,2]'),expr('setvar[ml_zone,180]'),expr('setvar[ml_use_maps,1]'),expr('setvar[ml_status,`No confirmed task work remains; treasure hunting in 180`]'),'SetState {T180_Default}']))
 states['Default']=states['Default'].replace('DoExpr {setvar[mm_schema,1]}',expr('setvar[mm_npc_log_generation,0]')+'\n\t\t\t\tDoExpr {setvar[mm_schema,1]}',1)
 # Server completion notices are authoritative; never compare a required kill count.
 for n,b in list(states.items()):
  b=re.sub(r'(\^You have killed )\d+(?= [^\n]+Your task is complete)',lambda m:m[1]+r'\d+',b)
  states[n]=b
 return hunters
