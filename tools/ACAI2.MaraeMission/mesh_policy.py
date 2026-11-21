"""Opt-in replacement of timed forward clue runs with bounded mesh-planned steps."""
import re
def apply(states,state,action,expr):
 far='iif[getpvar[mm_mesh_far]>0,max[8,min[120,getpvar[mm_mesh_far]]],24]'
 close='iif[getpvar[mm_mesh_close]>0,max[4,min[120,getpvar[mm_mesh_close],'+far+']],8]'
 for n,b in list(states.items()):
  if not n.startswith(('FAR_','CLOSE_')):continue
  heading=re.search(r'Chat \{/ub face (\d+)\}',b);length=re.search(r'setvar\[tm_run_length,(\d+)\]',b)
  assert heading and length,n
  pos=b.index('\tIF:\tAlways')
  states[n]=b[:pos]+'\tIF: Expr {getpvar[ml_mesh_maps]==1}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+v+'\n' for v in [expr('setvar[tm_mesh_heading,'+heading[1]+']'),expr('setvar[tm_mesh_step,'+(far if length[1]=='6' else close)+']'),'SetState {MAP_MESH_PREP}'])+b[pos:]
 b=states['STUCK'];pos=re.search(r'\tIF:\s*Always',b).start()
 states['STUCK']=b[:pos]+'\tIF: Expr {getpvar[ml_mesh_maps]==1}\n\t\tDO: DoAll\n\t\t\t\t'+expr('echo[`Mesh map search could not make progress; no raw backward motion was issued.`]')+'\n\t\t\t\tSetState {MISSION_ERROR}\n'+b[pos:]
 states['MAP_LOCK']=states['MAP_LOCK'].replace('DoExpr {setpvar[tm_dig_active,0]}',expr('setvar[tm_mesh_failures,0]')+'\n\t\t\t\tDoExpr {setpvar[tm_dig_active,0]}',1)
 state('MAP_MESH_PREP',action(['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {False}','SetOpt {EnableBuffing} {False}','SetOpt {EnableLooting} {False}',expr('setvar[tm_walk_generation,getvar[tm_walk_generation]+1]'),expr('setvar[tm_walk_ack,0]'),expr('setvar[ml_status,`Planning a mesh path in the map clue direction`]'),'SetState {MAP_MESH_WAIT}']))
 state('MAP_MESH_WAIT','\tIF: Expr {getvar[tm_walk_ack]==-1}\n\t\tDO: SetState {MAP_MESH_FAILED}\n\tIF: Expr {getvar[tm_walk_ack]==1 && strlen[getplayercoordinates[]]>0 && coordinatedistanceflat[getplayercoordinates[],getvar[tm_walk_target]]<=1}\n\t\tDO: DoAll\n\t\t\t\tSetOpt {EnableNav} {False}\n\t\t\t\tChat {/ub clearmotion}\n\t\t\t\t'+expr('setvar[tm_mesh_failures,0]')+'\n\t\t\t\tSetState {MAP_RESUME}\n\tIF: SecsInStateGE 45\n\t\tDO: SetState {MAP_MESH_FAILED}')
 state('MAP_MESH_FAILED',action(['SetOpt {EnableNav} {False}','Chat {/ub clearmotion}',expr('maraenavrestore[]'),expr('setvar[tm_mesh_failures,getvar[tm_mesh_failures]+1]'),expr('setvar[ml_status,`Mesh map step failed: `+getvar[tm_mesh_error]]'),'SetState {MAP_MESH_RETRY}']))
 state('MAP_MESH_RETRY','\tIF: Expr {getvar[tm_mesh_failures]>=3}\n\t\tDO: DoAll\n\t\t\t\t'+expr('echo[`Map mesh path unavailable after three tries. Check RynthNav/mesh coverage, or turn off Mesh map steps.`]')+'\n\t\t\t\tSetState {MISSION_ERROR}\n\tIF: SecsInStateGE 2\n\t\tDO: DoAll\n\t\t\t\t'+expr('setvar[tm_mesh_step,max[4,getvar[tm_mesh_step]/2]]')+'\n\t\t\t\tSetState {MAP_MESH_PREP}')
 for n in ('MAP_MESH_PREP','MAP_MESH_WAIT','MAP_MESH_FAILED','MAP_MESH_RETRY'):
  header='STATE: {'+n+'} ~~ {\n'
  states[n]=states[n].replace(header,header+'\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Expr {getcharvital_current[1]*100<=getcharvital_buffedmax[1]*75}\n\t\tDO: SetState {MAP_COMBAT_RESUME}\n',1)
 for n in ('MAP_RETURN','MAP_COMBAT_RESUME'):
  # Restore the exact hunt route before handing off or rejoining normal combat.
  if n=='MAP_RETURN':
   states[n]=re.sub(r'DO: SetState \{([^}]+)\}',lambda m:'DO: DoAll\n\t\t\t\t'+expr('maraenavrestore[]')+'\n\t\t\t\tSetState {'+m[1]+'}',states[n])
  else:states[n]=states[n].replace('DoExpr {',expr('maraenavrestore[]')+'\n\t\t\t\tDoExpr {',1)
