"""Per-area continuous hunting, independent of quests and treasure-map selection."""
def apply(states,navs,state,action,expr):
 # Reuse real hunt-loop geometry; add a terminal acknowledgment for repeating/rotating laps.
 for z in (180,225):
  lines=list(navs[f'{z}_-_Hunt_Loop']);last=lines[-5:]
  assert last[0]=='0',last
  lines[1]='4';lines[2]=str(int(lines[2])+1)
  lines+=['4',last[1],last[2],last[3],'0','/ra mexec setvar[pt_travel_end,1]']
  navs[f'{z}_Continuous_Hunt']=lines
 for z in (180,225,275):
  p=f'T{z}_'
  states['Default']=states['Default'].replace(expr(f'setvar[ml_maps_{z},getpvar[ml_maps_{z}]]'),expr(f'setvar[ml_maps_{z},getpvar[ml_maps_{z}]]')+'\n\t\t\t\t'+expr(f'setvar[ml_hunt_only_{z},getpvar[ml_hunt_only_{z}]]'),1)
  states[p+'Default']=states[p+'Default'].replace('SetOpt {StopMacroOnDeath} {False}',expr(f'setvar[ml_hunt_only,getpvar[ml_hunt_only_{z}]]')+'\n\t\t\t\tSetOpt {StopMacroOnDeath} {False}',1)
  for name in (p+'CAMP_SETTLE',p+'HUNT'):
   states[name]=states[name].replace('getvar[ml_run_mode]==2','getvar[ml_run_mode]==2 || getvar[ml_hunt_only]==1')
  # Quest dispatch ignores continuous areas; a completed quest flag must not exclude them.
  states['DISPATCH']=states['DISPATCH'].replace(f'getpvar[mm_area_{z}]!=1 &&',f'getvar[ml_hunt_only_{z}]!=1 && getpvar[mm_area_{z}]!=1 &&',1)
  for name in (p+'CAMP_SETTLE',p+'HUNT'):
   states[name]=states[name].replace('getvar[ml_run_mode]==2}', 'getvar[ml_run_mode]==2 || getvar[ml_hunt_only]==1}')
  # Preserve native travel, corpse recovery and normal loot-profile use.
  original=states[p+'FARM_LOAD']
  route=f'{z}_Continuous_Hunt' if z!=275 else '275_-_Treasure_Circuit'
  body=action(['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}',expr('setvar[pt_travel_end,0]'),expr('setvar[pt_region,1]'),expr('setvar[ml_status,`Hunting the area circuit; tasks ignored, maps off`]'),f'EmbedNav {route} {{once}}','SetState {'+p+'HUNT_RESUME}'])
  state(p+'CONTINUOUS_LOAD','\tIF: Death\n\t\tDO: SetState {'+p+'DEATH_RECOVERY}\n'+body)
  first=original.index('\tIF: Always')
  states[p+'FARM_LOAD']=original[:first]+'\tIF: Expr {getvar[ml_hunt_only]==1 && getvar[ml_use_maps]!=1}\n\t\tDO: SetState {'+p+'CONTINUOUS_LOAD}\n'+original[first:]
  states[p+'FARM_RUN']=states[p+'FARM_RUN'].replace('getitemcountininventorybyname[Treasure Map]','getitemcountininventorybyname[`Treasure Map`]')
  # Route end means another lap or another selected continuous area, never task completion.
  states[p+'FARM_RUN']=states[p+'FARM_RUN'].replace('\tIF: Expr {getvar[pt_travel_end]==1}', '\tIF: Expr {getvar[pt_travel_end]==1 && getvar[ml_hunt_only]==1}\n\t\tDO: SetState {CONTINUOUS_LAP_DONE}\n\tIF: Expr {getvar[pt_travel_end]==1}',1)
  states[p+'FARM_RUN']=states[p+'FARM_RUN'].replace('getvar[tm_map_id]!=0 && (', 'getvar[ml_use_maps]==1 && getvar[tm_map_id]!=0 && (').replace('getitemcountininventorybyname[Treasure Map]','getitemcountininventorybyname[`Treasure Map`]')
 # Finite task areas retain their existing order; then all selected continuous areas cycle.
 branches=''
 for wrap in (False,True):
  for z in (180,225,275):
   cond=f'getvar[ml_area_{z}]==1 && getvar[ml_hunt_only_{z}]==1'
   if not wrap:cond+=f' && getpvar[mm_hunt_cursor]<{z}'
   branches+='\tIF: Expr {'+cond+'}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+v+'\n' for v in [expr(f'setvar[ml_zone,{z}]'),expr(f'setvar[ml_use_maps,getvar[ml_maps_{z}]]'),expr('setvar[ml_status,`Traveling to the next continuous hunting area`]'),'SetState {T'+str(z)+'_Default}'])
 pos=states['DISPATCH'].index('\tIF: Expr {getpvar[mm_fallback_180]!=0}')
 states['DISPATCH']=states['DISPATCH'][:pos]+branches+states['DISPATCH'][pos:]
 count='+'.join(f'(getvar[ml_area_{z}]==1 && getvar[ml_hunt_only_{z}]==1)' for z in (180,225,275))
 body='\tIF: Expr {'+count+'>1}\n\t\tDO: DoAll\n\t\t\t\t'+expr('setpvar[mm_hunt_cursor,getvar[ml_zone]]')+'\n\t\t\t\tSetState {DISPATCH}\n'
 for z in (180,225,275):body+='\tIF: Expr {getvar[ml_zone]=='+str(z)+'}\n\t\tDO: SetState {T'+str(z)+'_FARM_LOAD}\n'
 state('CONTINUOUS_LAP_DONE','\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n'+body)
