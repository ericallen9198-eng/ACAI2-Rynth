"""Recover idempotent/search failures; only unresolved item dispatches require review."""
import re

def apply(states,state,action,expr,restore):
 # Stamp every error edge before the common classifier overwrites the active state.
 for name,body in list(states.items()):
  if name=='MISSION_ERROR' or 'SetState {MISSION_ERROR}' not in body:continue
  reason=('Cache pickup acknowledgment timed out' if name=='CACHE_CONFIRM' else
          'Cache search or approach timed out' if name.startswith('CACHE_') else
          'Map mesh route unavailable' if name in ('MAP_MESH_RETRY','STUCK') else 'Map action timed out')
  alias='FAIL_FROM_'+name
  states[name]=body.replace('SetState {MISSION_ERROR}','SetState {'+alias+'}')
  state(alias,action([expr('setpvar[mm_last_failure_state,`'+name+'`]'),expr('setpvar[mm_last_failure_reason,`'+reason+'`]'),'SetState {MISSION_ERROR}']))
 # Checkpoint before dispatch, not in the slower Lua poll. This marker survives logout.
 needle='DoExpr {setvar[tm_cache_sent,'
 states['CACHE_PICKUP']=states['CACHE_PICKUP'].replace(needle,'\n\t\t\t\t'.join([
  expr('setpvar[mm_pickup_unresolved,1]'),
  expr('setpvar[mm_pickup_item,getvar[tm_cache_pending]]'),
  expr('setpvar[mm_pickup_container,getvar[tm_cache_id]]'),needle]),1)
 states['CACHE_CONFIRM']=states['CACHE_CONFIRM'].replace('DoExpr {setvar[tm_cache_pending,0]}',expr('setpvar[mm_pickup_unresolved,0]')+'\n\t\t\t\tDoExpr {setvar[tm_cache_pending,0]}',1)
 refused='\t\t\tExpr {getvar[tm_cache_sent]==0}\n\t\tDO: DoAll\n'
 assert refused in states['CACHE_CONFIRM']
 states['CACHE_CONFIRM']=states['CACHE_CONFIRM'].replace(refused,refused+'\t\t\t\t'+expr('setpvar[mm_pickup_unresolved,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_pending,0]')+'\n',1)
 states['CACHE_CONFIRM']=states['CACHE_CONFIRM'].replace(expr('setvar[tm_cache_pending,0]'),expr('setvar[tm_cache_sent,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_pending,0]'))
 states['CACHE_PREP']=states['CACHE_PREP'].replace(expr('setvar[tm_cache_pending,0]'),expr('setvar[tm_cache_sent,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_pending,0]'),1)
 # An uncertain move must not be forgotten by reloading the meta after reconnect.
 default=states['Default'];pos=default.index('\tIF: Always')
 states['Default']=default[:pos]+'\tIF: Expr {getpvar[mm_pickup_unresolved]==1}\n\t\tDO: DoAll\n\t\t\t\t'+expr('setvar[mm_schema,1]')+'\n\t\t\t\tSetState {INVENTORY_REVIEW}\n'+default[pos:]
 blocked='getpvar[mm_pickup_unresolved]==1'
 state('MISSION_ERROR','\tIF: Expr {'+blocked+'}\n\t\tDO: SetState {INVENTORY_REVIEW}\n'+action(['SetState {RECOVERY_CLEANUP}']))
 state('INVENTORY_REVIEW',action([expr('setpvar[mm_pickup_unresolved,1]'),restore,expr('setpvar[pt_cache_pickup_held,0]'),expr('maraenavrestore[]'),expr('setvar[ml_outcome,`Needs attention`]'),expr('setvar[ml_status,`Unconfirmed cache pickup; item `+getpvar[mm_pickup_item]+`. Inspect inventory/cache before choosing Resume after reviewing pickup.`]'),'SetState {STOP_CLEANUP}']))
 # Do not mark an uncollected cache successful, consume another map immediately,
 # or retain temporary travel movement/settings. Keep saved map preferences unchanged.
 cleanup=[restore,expr('setpvar[pt_cache_pickup_held,0]'),expr('maraenavrestore[]'),'Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}','SetOpt {EnableLooting} {True}',
  expr('setpvar[`mm_map_retry_after_`+getvar[ml_zone],getunixtime[]+300]'),expr('setpvar[tm_dig_active,0]'),expr('setvar[tm_map_id,0]'),expr('setvar[tm_resume_needed,0]'),expr('setvar[tm_cache_pending,0]'),expr('setvar[tm_cache_probe_id,0]'),expr('setvar[tm_cache_id,0]'),expr('setvar[tm_cache_deadline,0]'),expr('setvar[ml_outcome,`Running`]'),expr('setvar[ml_status,`Recovering from `+getpvar[mm_last_failure_state]+`: `+getpvar[mm_last_failure_reason]+`. Hunting continues; maps retry in five minutes.`]'),expr('echo[getvar[ml_status]]'),'SetState {RECOVERY_WAIT}']
 state('RECOVERY_CLEANUP',action(cleanup))
 state('RECOVERY_WAIT','\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: All\n\t\t\tSecsInStateGE 10\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: SetState {RECOVERY_ROUTE}')
 body=''
 for z in (180,225,275):
  body+='\tIF: Expr {getvar[ml_zone]=='+str(z)+' && (getvar[ml_hunt_only]==1 || getvar[ml_run_mode]==2)}\n\t\tDO: SetState {T'+str(z)+'_FARM_LOAD}\n'
  body+='\tIF: Expr {getvar[ml_zone]=='+str(z)+'}\n\t\tDO: DoAll\n\t\t\t\t'+expr('setvar[pt_region,0]')+'\n\t\t\t\tSetState {T'+str(z)+('_HUNT_INIT' if z==275 else '_HUNT_LOAD')+'}\n'
 state('RECOVERY_ROUTE',body+'\tIF: Always\n\t\tDO: SetState {DISPATCH}')
 # Guard only entry from hunting. Active encounter, pickup and corpse recovery are untouched.
 cooldown='getunixtime[]>=getpvar[`mm_map_retry_after_`+getvar[ml_zone]]'
 for name in list(states):
  if re.match(r'T(?:180|225|275)_(?:HUNT|FARM_RUN)$',name):
   states[name]=states[name].replace('getvar[ml_use_maps]==1', 'getvar[ml_use_maps]==1 && '+cooldown)
 # Two travel retries, then attempt another selected task camp. In fallback/continuous
 # mode, use a five-minute retry backoff rather than recall/portal spam or an idle HOLD.
 for z in (180,225,275):
  p=f'T{z}_';counter=f'mm_route_retries_{z}'
  wait=states[p+'DEATH_RETURN_WAIT'];at=wait.index('\tIF:')
  states[p+'DEATH_RETURN_WAIT']=wait[:at]+'\tIF: All\n\t\t\tSecsInStateGE 6\n\t\t\tExpr {getpvar[mm_pickup_unresolved]==1 && getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: SetState {INVENTORY_REVIEW}\n'+wait[at:]

  retries=action([expr('setpvar[mm_last_failure_state,`'+p+'ROUTE_HOLD`]'),expr('setpvar[mm_last_failure_reason,`Camp travel timed out`]'),'Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}','SetOpt {EnableLooting} {True}',expr('setvar[ml_status,`Camp travel timed out; retrying after a short backoff`]'),'SetState {'+p+'TRAVEL_BACKOFF}'])
  state(p+'ROUTE_HOLD',retries)
  state(p+'TRAVEL_BACKOFF','\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: All\n\t\t\tSecsInStateGE 15\n\t\t\tExpr {getvar['+counter+']<2 && getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: DoAll\n\t\t\t\t'+expr('setvar['+counter+',getvar['+counter+']+1]')+'\n\t\t\t\tSetState {'+p+'START_LOCATION}\n\tIF: All\n\t\t\tSecsInStateGE 15\n\t\t\tExpr {getvar['+counter+']>=2 && getvar[ml_hunt_only]!=1 && getvar[ml_run_mode]!=2 && getvar[mm_budget_fallback]!=1}\n\t\tDO: SetState {AREA_DEFERRED}\n\tIF: All\n\t\t\tSecsInStateGE 300\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: DoAll\n\t\t\t\t'+expr('setvar['+counter+',0]')+'\n\t\t\t\tSetState {'+p+'START_LOCATION}')
  # Reaching a camp resets only that camp's transient retry burst.
  body=states[p+'CAMP_SETTLE'];pos=body.index('\tIF:')
  states[p+'CAMP_SETTLE']=body[:pos]+'\tIF: Expr {getvar['+counter+']>0 && getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: '+expr('setvar['+counter+',0]')+'\n'+body[pos:]
  state(p+'DEATH_HOLD',action([expr('setpvar[mm_last_failure_state,`'+p+'DEATH_HOLD`]'),expr('setpvar[mm_last_failure_reason,`No task progress; trying other work`]'),'SetState {AREA_DEFERRED}']))
  active=re.search(r'\tIF: Expr \{(\(getvar\[pt_pickup_[^\n]+)\}\n',states[p+'NPC_DECIDE'])[1]
  wait=states[p+'TURNIN_DECIDE'];at=wait.index('\tIF: Always')
  wait=wait[:at]+'\tIF: Expr {getvar[mm_cap_return]!=1 && ('+active+')}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in [expr('setvar[pt_turnin,0]'),expr('setvar[pt_region,0]'),expr('setvar[ml_status,`Hunter confirms unfinished work; resuming the hunt without another recall`]'),'SetState {'+p+('HUNT_INIT' if z==275 else 'HUNT_LOAD')+'}'])+wait[at:]
  states[p+'TURNIN_DECIDE']=wait

 # Unresolved task camps are not falsely reported as finished if the user disabled
 # the optional Lowlands fallback. Retry them later without clearing actual completions.
 b=states['DISPATCH'];pos=b.rindex('\tIF: Always')
 attempted=' || '.join(f'getpvar[mm_attempted_{z}]==1' for z in (180,225,275))
 states['DISPATCH']=b[:pos]+'\tIF: Expr {'+attempted+'}\n\t\tDO: SetState {RECOVERY_RETRY_AREAS}\n'+b[pos:]
 lines=[expr('setpvar[mm_attempted_'+str(z)+',0]') for z in (180,225,275)]
 body='\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Expr {getvar[ml_status]!=`Unresolved camps; retrying task checks after five minutes.`}\n\t\tDO: DoAll\n'
 body+=''.join('\t\t\t\t'+x+'\n' for x in ['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}','SetOpt {EnableLooting} {True}',expr('setvar[ml_status,`Unresolved camps; retrying task checks after five minutes.`]')])
 body+='\tIF: All\n\t\t\tSecsInStateGE 300\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in lines+['SetState {DISPATCH}'])
 state('RECOVERY_RETRY_AREAS',body)
