"""Optional persistent two-hour task-camp budgets; no fabricated quest completion."""
import re
def apply(states,state,action,expr):
 enabled='getpvar[mm_camp_limit]==1'
 active='getvar[mm_budget_active]==1'
 for z in (180,225,275):
  p=f'T{z}_'
  states[p+'Default']=states[p+'Default'].replace('SetOpt {StopMacroOnDeath} {False}',expr('setvar[mm_budget_active,0]')+'\n\t\t\t\t'+expr('setvar[mm_cap_return,0]')+'\n\t\t\t\t'+expr('if[getvar[mm_budget_fallback]==1,setvar[ml_hunt_only,1]]')+'\n\t\t\t\tSetOpt {StopMacroOnDeath} {False}',1)
  states[p+'Default']=states[p+'Default'].replace('SetState {'+p+'START_LOCATION}',expr(f'if[getpvar[mm_cap_pending_{z}]==1,setvar[ml_hunt_only,0]]')+'\n\t\t\t\t'+expr(f'if[getpvar[mm_cap_pending_{z}]==1,setvar[pt_turnin,1]]')+'\n\t\t\t\t'+expr(f'if[getpvar[mm_cap_pending_{z}]==1,setvar[mm_cap_return,1]]')+'\n\t\t\t\tSetState {'+p+'START_LOCATION}',1)
  start=f'if[{enabled} && getvar[ml_hunt_only]!=1 && getvar[ml_run_mode]==1,if[getpvar[mm_deadline_{z}]==0,setpvar[mm_deadline_{z},getunixtime[]+7200]]]'
  states[p+'HUNT_RESUME']=states[p+'HUNT_RESUME'].replace('Chat {/ub clearmotion}',expr(start)+'\n\t\t\t\t'+expr(f'setvar[mm_budget_active,{enabled} && getvar[ml_hunt_only]!=1 && getvar[ml_run_mode]==1]')+'\n\t\t\t\tChat {/ub clearmotion}',1)
  # The optional quota replaces the old no-KT-progress stop while it is enabled.
  states[p+'HUNT']=states[p+'HUNT'].replace('getitemcountininventorybyname[Treasure Map]','getitemcountininventorybyname[`Treasure Map`]').replace('getunixtime[]-getvar[pt_last_progress]>=2700',f'getpvar[mm_camp_limit]!=1 && getunixtime[]-getvar[pt_last_progress]>=2700')
  states['DISPATCH']=states['DISPATCH'].replace(f'getpvar[mm_area_{z}]!=1 &&',f'(getpvar[mm_camp_limit]!=1 || getpvar[mm_timed_{z}]!=1) && getpvar[mm_area_{z}]!=1 &&',1)
  state(p+'BUDGET_RETURN','\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n'+action([expr('setvar[mm_budget_active,0]'),expr('setvar[mm_cap_return,1]'),expr(f'setpvar[mm_timed_{z},1]'),expr(f'setpvar[mm_cap_pending_{z},1]'),expr('maraenavrestore[]'),expr('if[getpvar[pt_cache_pickup_held]==1,rasetsetting[`LootChestsMacroOff`,getpvar[pt_cache_old_pickup]]]'),expr('setpvar[pt_cache_pickup_held,0]'),'Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}','SetOpt {EnableLooting} {True}',expr('setvar[tm_map_id,0]'),expr('setpvar[tm_dig_active,0]'),expr('setvar[pt_turnin,1]'),expr('setvar[ml_status,`Two-hour camp limit reached; returning for completed rewards`]'),'SetState {'+p+'GET_TO}']))
  # In-progress replies are expected on a partial turn-in, not an NPC failure.
  wait=states[p+'TURNIN_WAIT'];pos=wait.index('\tIF: Expr {getvar[pt_npc_end]==1')
  wait=wait[:pos]+'\tIF: Expr {getvar[mm_cap_return]==1 && getvar[pt_npc_end]==1}\n\t\tDO: SetState {'+p+'BUDGET_SETTLE}\n'+wait[pos:];states[p+'TURNIN_WAIT']=wait
  state(p+'BUDGET_SETTLE','\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: SecsInStateGE 3\n\t\tDO: SetState {BUDGET_AREA_DONE}')
 # Snapshot deadline state survives reconnect; a new run clears it in the Lua preferences.
 states['Default']=states['Default'].replace('DoExpr {setvar[mm_schema,1]}',expr('setvar[mm_budget_active,0]')+'\n\t\t\t\t'+expr('setvar[mm_cap_return,0]')+'\n\t\t\t\t'+expr('setvar[mm_budget_fallback,0]')+'\n\t\t\t\tDoExpr {setvar[mm_schema,1]}',1)
 for n in ('AREA_DONE','AREA_COOLDOWN','AREA_DEFERRED'):
  states[n]=states[n].replace('DoExpr {',expr('setvar[mm_budget_active,0]')+'\n\t\t\t\tDoExpr {',1)
 # Existing bounded NPC retry logs mismatches, then this intercept avoids falsely marking
 # the unfinished camp as complete or unavailable when the partial turn-in was uncertain.
 at=states['AREA_DEFERRED'].index('\tIF: Always')
 states['AREA_DEFERRED']=states['AREA_DEFERRED'][:at]+'\tIF: Expr {getvar[mm_cap_return]==1}\n\t\tDO: SetState {BUDGET_AREA_DONE}\n'+states['AREA_DEFERRED'][at:]
 state('BUDGET_AREA_DONE',action([expr('setpvar[`mm_cap_pending_`+getvar[ml_zone],0]'),'SetOpt {EnableNav} {False}','Chat {/ub clearmotion}',expr('setvar[mm_budget_active,0]'),expr('setvar[mm_cap_return,0]'),expr('setvar[ml_status,`Partial camp turn-in finished; trying the next selected task area`]'),'SetState {DISPATCH}']))
 body=''
 for z in (180,225,275):body+='\tIF: Expr {getvar[ml_zone]=='+str(z)+'}\n\t\tDO: SetState {T'+str(z)+'_BUDGET_RETURN}\n'
 state('BUDGET_EXPIRED',body)
 state('BUDGET_LOWLANDS',action([expr('setvar[mm_budget_active,0]'),expr('setvar[mm_budget_fallback,1]'),expr('setvar[ml_run_mode,1]'),expr('setvar[ml_zone,180]'),expr('setvar[ml_use_maps,0]'),expr('setvar[ml_status,`Task camps finished; continuous Lowlands hunting, maps off`]'),'SetState {T180_Default}']))
 # A reconnect during return/turn-in must finish that return before dispatching a new camp.
 branches=''
 for z in (180,225,275):
  branches+='\tIF: Expr {getvar[ml_area_'+str(z)+']==1 && getpvar[mm_cap_pending_'+str(z)+']==1}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+v+'\n' for v in [expr(f'setvar[ml_zone,{z}]'),expr('setvar[ml_use_maps,0]'),'SetState {T'+str(z)+'_Default}'])
 at=states['DISPATCH'].index('\tIF: Expr {getvar[ml_hunt_only_180]')
 states['DISPATCH']=states['DISPATCH'][:at]+branches+states['DISPATCH'][at:]
 # Keep explicitly selected hunt-only areas ahead of the automatic fallback.
 at=states['DISPATCH'].index('\tIF: Expr {getpvar[mm_fallback_180]!=0}')
 states['DISPATCH']=states['DISPATCH'][:at]+'\tIF: Expr {'+enabled+'}\n\t\tDO: SetState {BUDGET_LOWLANDS}\n'+states['DISPATCH'][at:]
 # Expire between actions only: a possibly-issued inventory move must settle first.
 deadline='getpvar[`mm_deadline_`+getvar[ml_zone]]'
 condition=f'{enabled} && {active} && getvar[mm_cap_return]!=1 && getvar[pt_turnin]!=1 && getvar[ml_hunt_only]!=1 && {deadline}>0 && getunixtime[]>={deadline} && getvar[tm_cache_pending]==0 && getcharvital_current[1]>0 && maraerecoverybusy[]==0'
 for n,b in list(states.items()):
  if n in ('Default','DISPATCH','HOLD','STOP_CLEANUP','MISSION_ERROR','MAP_FALLBACK_180') or n.endswith('_Default') or n.startswith(('AREA_','CONTINUOUS_','BUDGET_')) or 'DEATH' in n or 'BUDGET' in n:continue
  pos=b.index('\tIF:')
  states[n]=b[:pos]+'\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Expr {'+condition+'}\n\t\tDO: SetState {BUDGET_EXPIRED}\n'+b[pos:]

 # Fallback requires every selected task area to be resolved, never just fall-through.
 resolved=' && '.join(f'(getvar[ml_area_{z}]!=1 || getvar[ml_hunt_only_{z}]==1 || getpvar[mm_area_{z}]==1 || getpvar[mm_attempted_{z}]==1 || (getpvar[mm_camp_limit]==1 && getpvar[mm_timed_{z}]==1))' for z in (180,225,275))
 for condition in ('getpvar[mm_camp_limit]==1','getpvar[mm_fallback_180]!=0'):
  states['DISPATCH']=states['DISPATCH'].replace('IF: Expr {'+condition+'}', 'IF: Expr {('+resolved+') && '+condition+'}')
