"""Extract the proven treasure workflow; isolate it from all Marae task/camp policy."""
from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'metas/marae-lassel-mission/MetaFiles/Marae Lassel - Embedded.af'
OUT=ROOT/'metas/maphunt/MetaFiles/MapHunt.af'
allstates={}
for b in re.split(r'(?=^STATE:)',SOURCE.read_text(),flags=re.M):
 m=re.match(r'STATE: \{([^}]+)\}',b)
 if not m:continue
 name=m[1]
 if name.startswith(('T180','T225','T275','AREA','BUDGET','CONTINUOUS','FAIL_FROM')) or name in ('Default','DISPATCH','STOP_CLEANUP','HOLD','RECOVERY_RETRY_AREAS','MAP_FALLBACK_180'):continue
 chunks=re.split(r'(?=^\tIF:)',b,flags=re.M)
 b=''.join(c.split('NAVDATA:')[0] for c in chunks if not ('Your task is complete' in c and 'setpvar[mm_' in c) and 'mm_camp_limit' not in c)
 if not b.rstrip().endswith('~~ }'):b=b.rstrip()+'\n~~ }\n\n'
 b=re.sub(r'SetState \{FAIL_FROM_[^}]+\}','SetState {MISSION_ERROR}',b)
 # Persistent keys are isolated from the installed three-zone mission. Native helper runtime keys stay intact.
 b=re.sub(r'((?:get|set)pvar\[)([a-zA-Z][a-zA-Z0-9_]*)',lambda m:m[1]+'mh_'+m[2],b)
 b=b.replace('getpvar[mh_ml_mesh_maps]','getpvar[meshnav]')
 b=b.replace('getvar[ml_status]','getvar[mh_status]').replace('setvar[ml_status,','setvar[mh_status,')
 b=re.sub(r'180 Lowlands KT|Plateau Treasure|Treasure Hunter','MapHunt',b)
 b=b.replace('cache collection timed out; currency pickup is not confirmed. Holding for review.','cache collection timed out; resuming hunting with a map backoff.')
 # Respect each user's combat range instead of carrying the old mage/area overrides.
 b=re.sub(r'^\s*SetOpt \{AttackDistance\}[^\n]*\n','\n',b,flags=re.M)
 allstates[name]=b
states=allstates
E=lambda x:'DoExpr {'+x+'}'
def actions(lines):return '\tIF: Always\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in lines)
def state(n,b):states[n]='STATE: {'+n+'} ~~ {\n'+b+'\n~~ }\n\n'
def rule(c,a):return '\tIF: '+c+'\n\t\tDO: '+a+'\n'
ready='getcharvital_current[1]>0 && maraerecoverybusy[]==0'
death=rule('Death','SetState {MAP_DEATH}')
restore=E('if[getpvar[mh_pt_cache_pickup_held]==1,rasetsetting[`LootChestsMacroOff`,getpvar[mh_pt_cache_old_pickup]]]')
base=['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}','SetOpt {EnableLooting} {True}']
state('Default',rule('Expr {maraemissionapi[]<6}','SetState {SUPPORT_REQUIRED}')+rule('Expr {getpvar[mh_mm_pickup_unresolved]==1}','SetState {INVENTORY_REVIEW}')+actions([
 restore,E('setpvar[mh_pt_cache_pickup_held,0]'),E('maraenavrestore[]'),E('setvar[mh_schema,1]'),
 E('if[testpvar[meshnav]==0,setpvar[meshnav,0]]'),E('if[testpvar[maphunt_close]==0,setpvar[maphunt_close,30]]'),E('if[testpvar[maphunt_far]==0,setpvar[maphunt_far,130]]'),
 E('setvar[pt_skipped,listcreate[]]'),E('setvar[tm_map_id,0]'),E('setvar[tm_resume_needed,0]'),E('setvar[tm_map_gate_until,getunixtime[]+5]'),E('setpvar[mh_tm_dig_active,0]'),E('setvar[tm_cache_pending,0]'),E('setvar[tm_cache_anchor,0]'),E('setvar[tm_walk_generation,0]'),E('setvar[mh_walk_token,0]'),
 'SetOpt {StopMacroOnDeath} {False}','SetOpt {DeathRecallMode} {0}','SetOpt {LootOwnCorpse} {True}','SetOpt {TravelToOwnCorpse} {True}','SetOpt {KeepHuntingAfterDeath} {False}',*base,'SetState {HUNT_LOAD}']))
state('SUPPORT_REQUIRED',actions(['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}',E('echo[`MapHunt needs the documented compatibility support (maraemissionapi >= 6). No map action was issued.`]'),'SetState {HOLD}']))
state('HUNT_LOAD',death+actions(['Chat {/ub clearmotion}',E('maraenavrestore[]'),'Chat {/ra nav load maphunt.nav}',E('setvar[mh_route_ack,0]'),E('setvar[mh_status,`Loading maphunt.nav`]'),'SetState {HUNT_VERIFY}']))
state('HUNT_VERIFY',death+rule('Expr {getvar[mh_route_ack]==1}','SetState {HUNT_READY}')+rule('SecsInStateGE 10','SetState {NAV_REQUIRED}'))
state('NAV_REQUIRED',actions(['SetOpt {EnableNav} {False}',E('setvar[mh_status,`Missing, ambiguous or empty maphunt.nav. Put your route in RynthAi NavProfiles, then Resume.`]'),E('echo[getvar[mh_status]]'),'SetState {HOLD}']))
state('HUNT_READY',death+actions([*base,'SetOpt {EnableNav} {True}',E('setvar[mh_status,`Hunting maphunt.nav; watching the pack for maps`]'),'SetState {HUNT}']))
state('HUNT',death+rule('NavEmpty','SetState {HUNT_LOAD}')+rule('Expr {getvar[tm_resume_needed]==1 && (coordinatedistanceflat[getplayercoordinates[],getvar[tm_resume_pos]]>=30 || getunixtime[]>=getvar[tm_map_gate_until]+60)}',E('setvar[tm_resume_needed,0]'))+rule('Expr {'+ready+' && getunixtime[]>=getvar[tm_map_gate_until] && getvar[tm_resume_needed]==0 && getcharvital_current[1]*100>=getcharvital_buffedmax[1]*85 && getvar[tm_map_id]!=0}','SetState {MAP_RESUME}')+rule('Expr {'+ready+' && getunixtime[]>=getvar[tm_map_gate_until] && getvar[tm_resume_needed]==0 && getcharvital_current[1]*100>=getcharvital_buffedmax[1]*85 && getitemcountininventorybyname[`Treasure Map`]>0}','SetState {MAP_LOCK}'))
# Preserve the exact original route/index before any temporary mesh/approach route.
states['MAP_LOCK']=states['MAP_LOCK'].replace(E('setvar[tm_cache_anchor,0]'),E('maraenavsave[]')+'\n\t\t\t\t'+E('setvar[tm_cache_anchor,0]'),1)
state('MAP_RETURN',death+actions(['Chat {/ub clearmotion}',E('maraenavrestore[]'),*base,'SetOpt {EnableNav} {True}',E('setvar[mh_status,`Resuming maphunt.nav`]'),'SetState {HUNT}']))
# Configurable measured steps for both modes. No speed/run-stat assumptions.
for name in list(states):
 if not name.startswith(('FAR_','CLOSE_')):continue
 heading=re.search(r'Chat \{/ub face (\d+)\}',states[name])[1]
 close=name.startswith('CLOSE_');key,default,lo,hi=('maphunt_close',30,10,60) if close else ('maphunt_far',130,60,180)
 stride=f'iif[getpvar[{key}]<{lo},{lo},iif[getpvar[{key}]>{hi},{hi},getpvar[{key}]]]'
 state(name,death+actions([E('setvar[tm_mesh_heading,'+heading+']'),E('setvar[tm_mesh_step,'+stride+']'),E('setvar[tm_step_origin,getplayercoordinates[]]'),'SetState {CLUE_STEP}']))
state('CLUE_STEP',death+rule('Expr {getpvar[meshnav]==1}','SetState {MAP_MESH_PREP}')+actions(['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {False}','SetOpt {EnableBuffing} {False}','SetOpt {EnableLooting} {False}',E('setvar[mh_status,`Walking a measured clue step without mesh planning`]'),'SetState {DIRECT_PREP}']))
# Lua sets bearing once; the meta stops on real displacement, not elapsed run time.
state('DIRECT_PREP',death+actions([E('setvar[tm_walk_generation,getvar[tm_walk_generation]+1]'),E('setvar[mh_walk_token,getvar[mh_walk_token]+1]'),E('setvar[tm_walk_ack,0]'),'SetState {DIRECT_WAIT}']))
state('DIRECT_WAIT',death+rule('Expr {getcharvital_current[1]*100<=getcharvital_buffedmax[1]*75}','SetState {MAP_COMBAT_RESUME}')+rule('Expr {coordinatedistanceflat[getplayercoordinates[],getvar[tm_step_origin]]>=getvar[tm_mesh_step]}','SetState {STEP_DONE}')+rule('Expr {getvar[tm_walk_ack]==-1}','SetState {MISSION_ERROR}')+rule('SecsInStateGE 45','SetState {MISSION_ERROR}'))
state('STEP_DONE',death+actions(['Chat {/ub clearmotion}','SetOpt {EnableNav} {False}',E('setvar[tm_mesh_failures,0]'),'SetState {MAP_RESUME}']))
# Make generation identity monotonic across cache prep, which intentionally clears its local generation.
for name in ('MAP_MESH_PREP','CACHE_RETURN','CACHE_APPROACH','DIGSITE_RETURN'):
 states[name]=states[name].replace(E('setvar[tm_walk_generation,getvar[tm_walk_generation]+1]'),E('setvar[tm_walk_generation,getvar[tm_walk_generation]+1]')+'\n\t\t\t\t'+E('setvar[mh_walk_token,getvar[mh_walk_token]+1]'))
# No backward motion, random teleport/re-roll or endless search. Retry mesh at half stride, then hunt.
state('STUCK',death+actions(['SetState {MISSION_ERROR}']))
state('MISSION_ERROR',rule('Expr {getpvar[mh_mm_pickup_unresolved]==1}','SetState {INVENTORY_REVIEW}')+actions(['SetState {RECOVERY_CLEANUP}']))
state('INVENTORY_REVIEW',actions([E('setvar[mh_schema,1]'),restore,E('setpvar[mh_pt_cache_pickup_held,0]'),E('maraenavrestore[]'),'Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}','SetOpt {EnableLooting} {False}',E('setvar[mh_status,`Unconfirmed currency move: inspect inventory before using Review complete. No blind resend.`]'),E('echo[getvar[mh_status]]'),'SetState {HOLD}']))
state('RECOVERY_CLEANUP',actions([restore,E('setpvar[mh_pt_cache_pickup_held,0]'),E('maraenavrestore[]'),*base,E('setpvar[mh_tm_dig_active,0]'),E('setvar[tm_map_id,0]'),E('setvar[tm_resume_needed,0]'),E('setvar[tm_cache_pending,0]'),E('setvar[tm_cache_id,0]'),E('setvar[tm_map_gate_until,getunixtime[]+300]'),E('setvar[mh_status,`Map attempt unconfirmed; hunting continues. Maps retry in five minutes.`]'),E('echo[getvar[mh_status]]'),'SetState {RECOVERY_WAIT}']))
state('RECOVERY_WAIT',death+rule('All\n\t\t\tSecsInStateGE 10\n\t\t\tExpr {'+ready+'}','SetState {MAP_RETURN}'))
state('MAP_DEATH',actions(['Chat {/ub clearmotion}',E('maraenavrestore[]'),restore,E('setpvar[mh_pt_cache_pickup_held,0]'),*base,E('setvar[mh_status,`Waiting for respawn and native corpse recovery`]'),'SetState {DEATH_WAIT}']))
state('DEATH_WAIT',rule('All\n\t\t\tSecsInStateGE 6\n\t\t\tExpr {'+ready+'}','SetState {DEATH_READY}'))
state('DEATH_READY',rule('Expr {getpvar[mh_mm_pickup_unresolved]==1}','SetState {INVENTORY_REVIEW}')+actions([E('setvar[tm_map_id,0]'),E('setvar[tm_resume_needed,0]'),E('setpvar[mh_tm_dig_active,0]'),E('setvar[tm_map_gate_until,getunixtime[]+30]'),'SetState {HUNT_LOAD}']))
state('HOLD','\tIF: Never\n\t\tDO: None')
# Limit skipped maps to the latest 64 IDs (no indefinitely growing unattended list).
states['SKIP_MAP']=states['SKIP_MAP'].replace(E('listadd[getvar[pt_skipped],getvar[tm_map_id]]'),E('if[listcount[getvar[pt_skipped]]>=64,setvar[pt_skipped,listcreate[]]]')+'\n\t\t\t\t'+E('listadd[getvar[pt_skipped],getvar[tm_map_id]]'))
# Consumed maps without event confirmation are not automatically credited as an encounter.
states['DIG_3_RESPONSE']=re.sub(r'\tIF:\s*Expr \{\(listcount\[.*?\n\t\tDO:\s*SetState \{ENCOUNTER_INIT\}\n','',states['DIG_3_RESPONSE'])
# Completion/error wrappers no longer use area-specific dispatch.
state('ENCOUNTER_TIMEOUT',death+actions(['SetState {MISSION_ERROR}']))
# Choose only reachable states so legacy reroll/camp helpers never ship.
reachable=set();pending=['Default']
while pending:
 n=pending.pop()
 if n in reachable:continue
 if n not in states:raise AssertionError('Missing state '+n)
 reachable.add(n);pending+=re.findall(r'SetState \{([^}]+)\}',states[n])
text=states['Default']+''.join(v for k,v in states.items() if k in reachable and k!='Default')
assert not re.search(r'T(?:180|225|275)_|mm_camp_limit|/ls|/recall|EmbedNav|NAVDATA|setmotion Backward',text)
OUT.write_text(text)
print(f'Built MapHunt: {len(reachable)} states, external maphunt.nav, no quest/camp policy.')
