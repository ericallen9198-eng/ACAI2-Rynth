"""Build the mission from Soot's supplied beta and verified player portal observations."""
from pathlib import Path
import re,json
root=Path(__file__).resolve().parents[2];src=root/'metas/marae-lassel-beta';out=root/'metas/marae-lassel-mission'
s=(src/'MetaFiles/Marae Lassel.af').read_text()
parts=re.split(r'(?=^STATE:)',s,flags=re.M);states={re.search(r'STATE: \{([^}]+)\}',b)[1]:b for b in parts if b.strip()}
def state(name,body): states[name]='STATE: {'+name+'} ~~ {\n'+body.strip('\n')+'\n~~ }\n\n'
def action(lines):return '\tIF: Always\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+v+'\n' for v in lines)
def expr(x):return 'DoExpr {'+x+'}'
restore=expr('if[getpvar[pt_cache_pickup_held]==1,rasetsetting[`LootChestsMacroOff`,getpvar[pt_cache_old_pickup]]]')
# Preserve verified quest completion separately for each zone; never reset it on a reconnect.
keys={}
for zone in (180,225,275):
 keys[zone]=sorted(set(re.findall(r'pt_done_(\w+)', ''.join(b for n,b in states.items() if n.startswith('T'+str(zone)+'_')))))
 for n,b in list(states.items()):
  if not n.startswith('T'+str(zone)+'_'):continue
  b=re.sub(r'getvar\[pt_done_(\w+)\]',lambda m:f'getpvar[mm_{zone}_done_{m[1]}]',b)
  b=re.sub(r'setvar\[pt_done_(\w+),([^\]]+)\]',lambda m:f'setpvar[mm_{zone}_done_{m[1]},'+(f'getpvar[mm_{zone}_done_{m[1]}]' if m[2]=='0' and n.endswith('_Default') else m[2])+']',b)
  states[n]=b
 # Startup preferences and one recovery owner: Rynth's native corpse recovery.
 n=f'T{zone}_Default';b=states[n];pos=b.index('SetOpt {StopMacroOnDeath} {False}')
 b=b[:pos]+b[pos:].replace('SetOpt {StopMacroOnDeath} {False}', '\n\t\t\t\t'.join(['SetOpt {StopMacroOnDeath} {False}','SetOpt {DeathRecallMode} {0}','SetOpt {LootOwnCorpse} {True}','SetOpt {TravelToOwnCorpse} {True}','SetOpt {KeepHuntingAfterDeath} {False}']),1);states[n]=b
# Global encounter may see Plateau tasks: bind by current zone in shared code too.
for n,b in list(states.items()):
 if n.startswith('T'):continue
 b=re.sub(r'getvar\[pt_done_(\w+)\]',lambda m:f'getpvar[mm_275_done_{m[1]}]',b)
 b=re.sub(r'setvar\[pt_done_(\w+),1\]',lambda m:f'setpvar[mm_275_done_{m[1]},1]',b)
 states[n]=b
# Real server names from the user's verified task replies; tolerate singular/plural completion text.
for n,b in list(states.items()):
 b=b.replace('Olthoi Hive Mutilator','Olthoi Hive Guardian').replace('Olthoi\\ Hive\\ Mutilator','Olthoi\\ Hive\\ Guardian').replace('olthoihivemutilator','olthoihiveguardian')
 b=b.replace('Virindi Observer','Virindi Sentry').replace('Virindi\\ Observer','Virindi\\ Sentry').replace('virindiobserver','virindisentry')
 b=b.replace('Sentrys!', 'Sentr(?:y|ies)!').replace('Guardians!', 'Guardians?!')
 states[n]=b
# Whole-area completion persists only after the beta's confirmed turn-in/all-unavailable branch.
state('AREA_DONE',action([expr('setpvar[`mm_area_`+getvar[ml_zone],1]'),expr('setvar[ml_done,getvar[ml_done]+1]'),'SetState {DISPATCH}']))
state('AREA_COOLDOWN',action([expr('setpvar[`mm_area_`+getvar[ml_zone],1]'),expr('setvar[ml_cooldown,getvar[ml_cooldown]+1]'),'SetState {DISPATCH}']))
for z in (180,225,275):
 states['DISPATCH']=states['DISPATCH'].replace(f'getvar[ml_zone]<{z} && getvar[ml_area_{z}]==1',f'getpvar[mm_area_{z}]!=1 && getvar[ml_area_{z}]==1')
# Every area starts from its known camp, or /ls to the verified Plateau hub.
blocks={180:'14B50000',225:'1BC20000',275:'21B70000'}
for z in (180,225,275):
 p=f'T{z}_';death='\tIF: Death\n\t\tDO: SetState {'+p+'DEATH_RECOVERY}\n'
 state(p+'START_LOCATION',death+'\tIF: All\n\t\t\tBlockE '+blocks[z]+'\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: SetState {'+p+'CAMP_SETTLE}\n\tIF: Always\n\t\tDO: SetState {'+p+'GET_TO}')
 state(p+'GET_TO',death+action([restore,expr('setpvar[pt_cache_pickup_held,0]'),'Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}','SetOpt {EnableLooting} {True}',expr('setvar[ml_status,`Returning to the Plateau lifestone hub`]'),'SetState {'+p+'LS_DISPATCH}']))
 state(p+'LS_DISPATCH',death+'\tIF: All\n\t\t\tBlockE 21B70000\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: SetState {'+p+'HUB_READY}\n\tIF: Expr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: DoAll\n\t\t\t\tChat {/ls}\n\t\t\t\tSetState {'+p+'LS_WAIT}')
 state(p+'LS_WAIT',death+'\tIF: IntoPortal\n\t\tDO: SetState {'+p+'LS_WAIT}\n\tIF: ExitPortal\n\t\tDO: SetState {'+p+'LS_WAIT}\n\tIF: All\n\t\t\tSecsInStateGE 5\n\t\t\tBlockE 21B70000\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: SetState {'+p+'HUB_READY}\n\tIF: SecsInStateGE 120\n\t\tDO: SetState {'+p+'ROUTE_HOLD}')
 if z==275:state(p+'HUB_READY',action(['SetState {'+p+'CAMP_SETTLE}']))
 else:state(p+'HUB_READY',action([expr(f'setvar[ml_status,`Taking the {z} camp portal`]'),'EmbedNav Hub_to_'+str(z)+' {once}','SetOpt {EnableNav} {True}','SetState {'+p+'GET_TO_WAIT}']))
 state(p+'GET_TO_WAIT',death+'\tIF: All\n\t\t\tBlockE '+blocks[z]+'\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: DoAll\n\t\t\t\tSetOpt {EnableNav} {False}\n\t\t\t\tSetState {'+p+'CAMP_SETTLE}\n\tIF: SecsInStateGE 180\n\t\tDO: SetState {'+p+'ROUTE_HOLD}')
 # Remove hard second-death stop. Never issue a recall while corpse recovery owns the character.
 state(p+'DEATH_RECOVERY',action([restore,expr('setpvar[pt_cache_pickup_held,0]'),'Chat {/ub clearmotion}','SetOpt {EnableNav} {False}','SetOpt {EnableLooting} {True}','SetOpt {EnableCombat} {True}','SetOpt {EnableBuffing} {True}',expr('setpvar[tm_dig_active,0]'),expr('setvar[tm_map_id,0]'),expr('setvar[tm_cache_pending,0]'),expr('setvar[tm_cache_deadline,0]'),expr('setvar[tm_resume_needed,0]'),expr('setvar[pt_region,0]'),expr('setvar[pt_recover_hunt,0]'),expr('setvar[pt_recallwest,0]'),expr('setvar[ml_status,`Waiting for respawn and native corpse recovery`]'),'SetState {'+p+'DEATH_RETURN_WAIT}']))
 state(p+'DEATH_RETURN_WAIT',death+'\tIF: All\n\t\t\tSecsInStateGE 6\n\t\t\tExpr {getcharvital_current[1]>0 && maraerecoverybusy[]==0}\n\t\tDO: SetState {'+p+'START_LOCATION}')
# Each nav action now uses data inside this AF, not an installed filename.
navs={}
for path in (src/'NavProfiles').glob('*.nav'):
 if path.name in ('180 KT - Get to.nav','225 KT - Get to.nav','275 - Get to.nav'):continue
 navs[path.stem.replace(' ','_')]=path.read_text().splitlines()
for n,b in list(states.items()):
 b=re.sub(r'Chat \{/ra nav load ([^}]+)\}',lambda m:'EmbedNav '+m[1].replace(' ','_')+' {once}',b)
 states[n]=b
# Both portals share the actual standing coordinate supplied by the user.
ns=183*.8+156.236633/240-101.95;ew=33*.8+113.813011/240-101.95;zz=92.934402/240
for z,name in ((180,'Southern Hamlet Hunters (Rec Lvl 180)'),(225,"Northern Hunters' Camp (Rec Lvl 225)")):
 navs['Hub_to_'+str(z)]=['uTank2 NAV 1.2','4','2','0',str(ew),str(ns),str(zz),'0','6',str(ew),str(ns),str(zz),'0',name,'14','False',str(ew),str(ns),str(zz)]
# Carry forward the already-tested acknowledged optional-currency collector.
v3=(root/'metas/plateau-treasure-verified-v3/275 Plateau Treasure - Verified.af').read_text()
v3states={re.search(r'STATE: \{([^}]+)\}',b)[1]:b for b in re.split(r'(?=^STATE:)',v3,flags=re.M) if b.strip()}
for name in ('CACHE_PREP','CACHE_SEARCH','CACHE_PICKUP','CACHE_CONFIRM','CACHE_SETTLE'):
 states[name]=v3states[name].replace('{DEATH_RECOVER}','{MAP_DEATH}').replace('{ERROR_HOLD}','{MISSION_ERROR}')
for n in ('ENCOUNTER',):
 b=states[n].replace('SetState {CACHE_SEARCH}','SetState {CACHE_PREP}')
 # Ordinary XP is not proof that an encounter is over.
 b=b.replace('|You\'ve earned [0-9,]+ experience','')
 states[n]=b
states['CACHE_FINISH']=states['CACHE_FINISH'].replace('DoExpr {setpvar[tm_dig_active,0]}',restore+'\n\t\t\t\t'+expr('setpvar[pt_cache_pickup_held,0]')+'\n\t\t\t\tDoExpr {setpvar[tm_dig_active,0]}')
state('MISSION_ERROR',action([restore,expr('setpvar[pt_cache_pickup_held,0]'),expr('setvar[ml_outcome,`Needs attention`]'),expr('setvar[ml_status,`An action was not confirmed. The run is paused without crediting success.`]'),'SetState {STOP_CLEANUP}']))
states['STOP_CLEANUP']=states['STOP_CLEANUP'].replace('Chat {/ub clearmotion}',restore+'\n\t\t\t\t'+expr('setpvar[pt_cache_pickup_held,0]')+'\n\t\t\t\tChat {/ub clearmotion}')
# Prevent the panel from accepting an older/empty same-name meta as this mission.
states['Default']=states['Default'].replace('SetOpt {EnableNav} {False}',expr('setvar[mm_schema,1]')+'\n\t\t\t\tSetOpt {EnableNav} {False}',1)
# Task completions can arrive during a treasure fight, not just the hunting state.
completion_rules=[]
for zone in (180,225,275):
 for rule in re.split(r'(?=^\tIF:)',states[f'T{zone}_HUNT'],flags=re.M):
  if 'Your task is complete!' not in rule or 'setpvar[mm_' not in rule:continue
  rule=rule.split('~~ }')[0]
  assert rule.startswith('\tIF: All'),rule
  completion_rules.append(rule.replace('\tIF: All\n','\tIF: All\n\t\t\tExpr {getvar[ml_zone]=='+str(zone)+'}\n',1))
for name,body in list(states.items()):
 if name in ('Default','DISPATCH','AREA_DONE','AREA_COOLDOWN','STOP_CLEANUP','HOLD','MISSION_ERROR'):continue
 chunks=re.split(r'(?=^\tIF:)',body,flags=re.M)
 chunks=[c for c in chunks if not ('Your task is complete!' in c and 'setpvar[mm_' in c)]
 body=''.join(chunks)
 # Keep a closing state delimiter if a removed completion rule held it.
 if not body.rstrip().endswith('~~ }'):body=body.rstrip()+'\n~~ }\n\n'
 first=body.find('\tIF:')
 selected_rules=[r for r in completion_rules if not name.startswith('T') or ('getvar[ml_zone]=='+name[1:4]+'}') in r]
 body=body[:first]+''.join(selected_rules)+body[first:]
 states[name]=body


text=''.join(states.values())
# No optional raw portal expression is required. Native readiness is in the compatibility patch.
text=text.replace('!isportaling[]','maraerecoverybusy[]==0').replace('isportaling[]==0','maraerecoverybusy[]==0')
assert '/ra nav load' not in text
for name,lines in navs.items():text+='NAVDATA: '+name+' '+str(len(lines))+' ~~ {\n'+'\n'.join(lines)+'\n~~ }\n\n'
(out/'MetaFiles/Marae Lassel - Embedded.af').write_text(text)
(out/'checkpoint-keys.json').write_text(json.dumps([f'mm_area_{z}' for z in (180,225,275)]+[f'mm_{z}_done_{k}' for z in keys for k in keys[z]],indent=2)+'\n')
print(f'Built mission with {len(states)} states, {len(navs)} embedded routes and {sum(map(len,keys.values()))} durable task flags.')
