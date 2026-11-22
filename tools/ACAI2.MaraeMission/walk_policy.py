"""Explicit, bounded return-to-dig and selected-cache approach via the Lua/native nav bridge."""
def apply(states,state,action,expr):
 import re
 # A new map owns a new anchor; the first successful dig fixes its location before combat chase.
 states['MAP_LOCK']=states['MAP_LOCK'].replace('DoExpr {setpvar[tm_dig_active,0]}',expr('setvar[tm_cache_anchor,0]')+'\n\t\t\t\tDoExpr {setpvar[tm_dig_active,0]}',1)
 for n in ('DIG_FIRST','ENCOUNTER_INIT'):
  marker='DoExpr {setpvar[tm_stat_'+('dig1' if n=='DIG_FIRST' else 'encounter')
  pos=states[n].index(marker)
  states[n]=states[n][:pos]+expr('if[getvar[tm_cache_anchor]==0,setvar[tm_cache_anchor,getplayercoordinates[]]]')+'\n\t\t\t\t'+states[n][pos:]
 # Native snapshot preserves the original route/index before ai.GoTo replaces its route.
 states['CACHE_PREP']=states['CACHE_PREP'].replace('SetState {CACHE_SEARCH}',expr('setvar[tm_walk_generation,0]')+'\n\t\t\t\t'+expr('setvar[tm_walk_ack,0]')+'\n\t\t\t\tSetState {CACHE_RETURN_CHECK}',1)
 def goto(name,target,after,timeout,status):
  reach='0.9' if name=='CACHE_APPROACH' else '2'
  state(name,action([expr('setvar[tm_walk_target,'+target+']'),expr('setvar[tm_walk_generation,getvar[tm_walk_generation]+1]'),expr('setvar[tm_walk_ack,0]'),expr('setvar[ml_status,`'+status+'`]'),'SetState {'+name+'_WAIT}']))
  # A coordinate can guide approach, but missing coordinates never hide an otherwise usable cache.
  state(name+'_WAIT','\tIF: Expr {getvar[tm_walk_ack]==1 && strlen[getplayercoordinates[]]>0 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_walk_target]]<='+reach+'}\n\t\tDO: DoAll\n\t\t\t\tSetOpt {EnableNav} {False}\n\t\t\t\tChat {/ub clearmotion}\n\t\t\t\tSetState {'+after+'}\n\tIF: Expr {getvar[tm_walk_ack]==-1}\n\t\tDO: SetState {'+timeout+'}\n\tIF: SecsInStateGE 45\n\t\tDO: DoAll\n\t\t\t\tSetOpt {EnableNav} {False}\n\t\t\t\tChat {/ub clearmotion}\n\t\t\t\tSetState {'+timeout+'}')
 state('CACHE_RETURN_CHECK','\tIF: Expr {getvar[tm_cache_anchor]!=0 && strlen[getvar[tm_cache_anchor]]>0 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_cache_anchor]]>3}\n\t\tDO: SetState {CACHE_RETURN}\n'+action(['SetState {CACHE_SEARCH}']))
 goto('CACHE_RETURN','getvar[tm_cache_anchor]','CACHE_SEARCH','CACHE_SEARCH','Returning to the recorded digsite')
 # Split selection from using: reaching an object is not acknowledgment that it opened.
 b=states['CACHE_TRY_NEXT'];a=b.index('\t\t\t\tDoExpr {setvar[tm_cache_probe_until');b=b[:a]+'\t\t\t\tSetState {CACHE_APPROACH_CHECK}\n~~ }\n\n';states['CACHE_TRY_NEXT']=b
 state('CACHE_APPROACH_CHECK','\tIF: Expr {strlen[wobjectgetphysicscoordinates[wobjectfindbyid[getvar[tm_cache_probe_id]]]]>0 && coordinatedistanceflat[getplayercoordinates[],wobjectgetphysicscoordinates[wobjectfindbyid[getvar[tm_cache_probe_id]]]]>3}\n\t\tDO: SetState {CACHE_APPROACH}\n'+action(['SetState {CACHE_OPEN_SELECTED}']))
 goto('CACHE_APPROACH','wobjectgetphysicscoordinates[wobjectfindbyid[getvar[tm_cache_probe_id]]]','CACHE_OPEN_SELECTED','CACHE_SKIP_SELECTED','Walking to the selected Unearthed Cache')
 states['CACHE_APPROACH_WAIT']=states['CACHE_APPROACH_WAIT'].replace('getvar[tm_walk_ack]==1 && strlen[getplayercoordinates[]]>0 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_walk_target]]<=0.9','strlen[wobjectgetphysicscoordinates[wobjectfindbyid[getvar[tm_cache_probe_id]]]]>0 && coordinatedistanceflat[getplayercoordinates[],wobjectgetphysicscoordinates[wobjectfindbyid[getvar[tm_cache_probe_id]]]]<=3')
 states['CACHE_APPROACH_WAIT']=states['CACHE_APPROACH_WAIT'].replace('\tIF: Expr {strlen', '\tIF: Expr {getvar[tm_cache_probe_id]!=0 && wobjectgetid[wobjectgetopencontainer[]]==getvar[tm_cache_probe_id]}\n\t\tDO: DoAll\n\t\t\t\tSetOpt {EnableNav} {False}\n\t\t\t\tChat {/ub clearmotion}\n\t\t\t\tSetState {CACHE_SEARCH}\n\tIF: Expr {strlen',1)
 states['CACHE_APPROACH_WAIT']=states['CACHE_APPROACH_WAIT'].replace('\tIF: Expr {getvar[tm_walk_ack]==-1}', '\tIF: All\n\t\t\tExpr {getvar[tm_walk_ack]==1}\n\t\t\tNavEmpty\n\t\tDO: DoAll\n\t\t\t\tSetOpt {EnableNav} {False}\n\t\t\t\tChat {/ub clearmotion}\n\t\t\t\tSetState {CACHE_OPEN_SELECTED}\n\tIF: Expr {getvar[tm_walk_ack]==-1}',1)
 state('CACHE_SKIP_SELECTED',action(['SetOpt {EnableNav} {False}',expr('setvar[tm_cache_probe_id,0]'),expr('setvar[ml_status,`Could not reach that cache; trying another ID`]'),'SetState {CACHE_SEARCH}']))
 state('CACHE_OPEN_SELECTED',action(['SetOpt {EnableNav} {False}','Chat {/ub clearmotion}',expr('setvar[tm_cache_probe_until,getunixtime[]+8]'),expr('setvar[ml_status,`Waiting for the selected cache to open`]'),expr('actiontryuseitem[wobjectfindbyid[getvar[tm_cache_probe_id]]]'),'SetState {CACHE_SEARCH}']))
 state('CACHE_REOPEN_BOUND',action([expr('setvar[tm_cache_probe_id,getvar[tm_cache_id]]'),'SetState {CACHE_APPROACH_CHECK}']))
 states['CACHE_CHOOSE']=states['CACHE_CHOOSE'].replace('Waiting for another local cache candidate','No unopened cache confirmed; refreshing local cache IDs')
 states['CACHE_PICKUP']=states['CACHE_PICKUP'].replace('DoExpr {setvar[tm_cache_sent,',expr('setvar[ml_status,`Collecting currency from the opened cache`]')+'\n\t\t\t\tDoExpr {setvar[tm_cache_sent,')
 # Restore only route data, never resume a macro. Death recovery continues to own movement.
 for n in ('CACHE_FINISH','MAP_DEATH','MISSION_ERROR'):
  pos=states[n].index('DO:')
  # Existing first death guard in CACHE_FINISH must stay first; inject into its Always action.
  if n=='CACHE_FINISH':
   states[n]=states[n].replace('DoExpr {setpvar[tm_dig_active,0]}',expr('maraenavrestore[]')+'\n\t\t\t\tDoExpr {setpvar[tm_dig_active,0]}',1)
  else:
   states[n]=states[n].replace('DoExpr {',expr('maraenavrestore[]')+'\n\t\t\t\tDoExpr {',1)
 states['MAP_DEATH']=re.sub(r'DO: SetState \{([^}]+)\}',lambda m:'DO: DoAll\n\t\t\t\tDoExpr {maraenavrestore[]}\n\t\t\t\tSetState {'+m[1]+'}',states['MAP_DEATH'])
 for name in ('CACHE_RETURN_CHECK','CACHE_RETURN','CACHE_RETURN_WAIT','CACHE_APPROACH_CHECK','CACHE_APPROACH','CACHE_APPROACH_WAIT','CACHE_SKIP_SELECTED','CACHE_OPEN_SELECTED'):
  header='STATE: {'+name+'} ~~ {\n'
  states[name]=states[name].replace(header,header+'\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Expr {getunixtime[]>=getvar[tm_cache_deadline]}\n\t\tDO: SetState {MISSION_ERROR}\n',1)
