"""Return between fights to the actual event anchor; native combat keeps ownership."""
import re

def apply(states,state,action,expr):
 # A failed preliminary dig can be elsewhere. The encounter start is authoritative.
 init=states['ENCOUNTER_INIT'].replace('if[getvar[tm_cache_anchor]==0,setvar[tm_cache_anchor,getplayercoordinates[]]]','setvar[tm_cache_anchor,getplayercoordinates[]]')
 init=init.replace('DoExpr {setpvar[tm_stat_encounter,',expr('setvar[tm_event_deadline,getunixtime[]+1800]')+'\n\t\t\t\t'+expr('setvar[tm_anchor_forced,0]')+'\n\t\t\t\t'+expr('setvar[tm_anchor_retry_at,0]')+'\n\t\t\t\t'+expr('setvar[tm_anchor_failures,0]')+'\n\t\t\t\tDoExpr {setpvar[tm_stat_encounter,',1)
 states['ENCOUNTER_INIT']=init
 b=states['ENCOUNTER']
 # Keep the original absolute event budget while re-entering ENCOUNTER between fights.
 b=re.sub(r'SecsInStateGE 1800', 'Expr {getvar[tm_event_deadline]>0 && getunixtime[]>=getvar[tm_event_deadline]}',b)
 chunks=re.split(r'(?=^\tIF:)',b,flags=re.M)
 rewards=''.join(c.split('~~ }')[0] for c in chunks if 'SetState {CACHE_PREP}' in c or ('tm_doubloon_before' in c and 'SetState {CACHE_FINISH}' in c))
 assert 'CACHE_PREP' in rewards and 'CACHE_FINISH' in rewards
 pos=b.rfind('~~ }')
 condition='getpvar[tm_dig_active]==1 && getvar[tm_cache_anchor]!=0 && strlen[getvar[tm_cache_anchor]]>0 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_cache_anchor]]>10 && ratargetcount[]==0 && getbusystate[]==0 && getvar[tm_aun_loot_active]!=1 && maraerecoverybusy[]==0 && getunixtime[]>=getvar[tm_anchor_retry_at]'
 outer='getpvar[tm_dig_active]==1 && getvar[tm_cache_anchor]!=0 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_cache_anchor]]>=55 && getcharvital_current[1]>0 && maraerecoverybusy[]==0'
 b=b[:pos]+'\tIF: Expr {'+outer+'}\n\t\tDO: SetState {DIGSITE_FORCE_RETURN}\n\tIF: Expr {'+condition+'}\n\t\tDO: DoAll\n\t\t\t\t'+expr('setvar[tm_anchor_forced,0]')+'\n\t\t\t\tSetState {DIGSITE_RETURN}\n'+b[pos:]
 states['ENCOUNTER']=b
 ready='getcharvital_current[1]>0 && maraerecoverybusy[]==0'
 guards='\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n'+rewards+'\tIF: Expr {'+ready+' && getvar[tm_event_deadline]>0 && getunixtime[]>=getvar[tm_event_deadline]}\n\t\tDO: SetState {ENCOUNTER_TIMEOUT}\n'
 state('DIGSITE_RETURN',guards+action(['SetOpt {EnableNav} {False}','Chat {/ub clearmotion}',expr('setvar[tm_walk_target,getvar[tm_cache_anchor]]'),expr('setvar[tm_walk_generation,getvar[tm_walk_generation]+1]'),expr('setvar[tm_walk_ack,0]'),expr('setvar[ml_status,iif[getvar[tm_anchor_forced]==1,`55-yard boundary: returning to the digsite with combat yielded`,`Returning to the digsite between fights`]]'),'SetState {DIGSITE_RETURN_WAIT}']))
 state('DIGSITE_FORCE_RETURN',guards+action(['SetOpt {EnableCombat} {False}',expr('setvar[tm_anchor_forced,1]'),expr('setvar[ml_status,`55-yard digsite boundary reached; returning before pursuing more targets`]'),'SetState {DIGSITE_RETURN}']))
 state('DIGSITE_RETURN_WAIT',guards+'\tIF: Expr {'+ready+' && getvar[tm_anchor_forced]!=1 && ratargetcount[]>0}\n\t\tDO: SetState {DIGSITE_RETURN_COMBAT}\n\tIF: Expr {'+ready+' && coordinatedistanceflat[getplayercoordinates[],getvar[tm_cache_anchor]]<=3}\n\t\tDO: SetState {DIGSITE_RETURN_DONE}\n\tIF: Expr {'+ready+' && getvar[tm_walk_ack]==-1}\n\t\tDO: SetState {DIGSITE_RETURN_DONE}\n\tIF: All\n\t\t\tSecsInStateGE 45\n\t\t\tExpr {'+ready+'}\n\t\tDO: SetState {DIGSITE_RETURN_DONE}')
 finish=['SetOpt {EnableNav} {False}','Chat {/ub clearmotion}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}']
 state('DIGSITE_RETURN_COMBAT',guards+action(finish+[expr('setvar[tm_anchor_retry_at,getunixtime[]+5]'),expr('setvar[ml_status,`Fighting; will return to the digsite when targets clear`]'),'SetState {ENCOUNTER}']))
 state('DIGSITE_RETURN_RETRY',guards+'\tIF: Expr {'+ready+' && getunixtime[]>=getvar[tm_anchor_retry_at]}\n\t\tDO: SetState {DIGSITE_RETURN}')
 forced='\tIF: Expr {'+ready+' && getvar[tm_anchor_forced]==1 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_cache_anchor]]>10}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in ['SetOpt {EnableNav} {False}','Chat {/ub clearmotion}',expr('setvar[tm_anchor_failures,getvar[tm_anchor_failures]+1]'),expr('setvar[tm_anchor_retry_at,getunixtime[]+iif[getvar[tm_anchor_failures]>=3,60,15]]'),expr('setvar[ml_status,`Digsite return path not confirmed; retrying with combat yielded to navigation`]'),'SetState {DIGSITE_RETURN_RETRY}'])
 state('DIGSITE_RETURN_DONE',guards+forced+action(finish+[expr('setvar[tm_anchor_failures,iif[coordinatedistanceflat[getplayercoordinates[],getvar[tm_cache_anchor]]<=10,0,getvar[tm_anchor_failures]+1]]'),expr('setvar[tm_anchor_retry_at,getunixtime[]+iif[getvar[tm_anchor_failures]>=3,60,15]]'),expr('setvar[tm_anchor_forced,0]'),expr('setvar[ml_status,`Dig event active; combat enabled and digsite retained`]'),'SetState {ENCOUNTER}']))

 for name in ('DIGSITE_RETURN','DIGSITE_FORCE_RETURN','DIGSITE_RETURN_COMBAT','DIGSITE_RETURN_DONE'):
  states[name]=states[name].replace('\tIF: Always','\tIF: Expr {'+ready+'}')

 # Respect native arrival tolerance, but never treat a finished route far from home as arrival.
 states['DIGSITE_RETURN_WAIT']=states['DIGSITE_RETURN_WAIT'].replace('\tIF: Expr {'+ready+' && getvar[tm_walk_ack]==-1}', '\tIF: All\n\t\t\tExpr {'+ready+' && getvar[tm_walk_ack]==1 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_cache_anchor]]<=10}\n\t\t\tNavEmpty\n\t\tDO: SetState {DIGSITE_RETURN_DONE}\n\tIF: Expr {'+ready+' && getvar[tm_walk_ack]==-1}',1)
