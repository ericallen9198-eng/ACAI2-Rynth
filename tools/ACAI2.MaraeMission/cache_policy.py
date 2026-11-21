"""Try local Unearthed Cache object IDs fairly; bind pickup only after an open acknowledgment."""

def apply(states,state,action,expr):
 typed='wobjectgettemplatetype[wobjectgetopencontainer[]]==1004150'
 for name in ('CACHE_SEARCH','CACHE_PICKUP','CACHE_CONFIRM','CACHE_SETTLE'):
  states[name]=states[name].replace('wobjectgetname[wobjectgetopencontainer[]]==`Unearthed Cache`',typed)
 # Exclude caches that existed before the first dig, rather than guessing an owner from a name.
 for n in list(states):
  if n.endswith('_Default') or n=='MAP_LOCK':
   # MAP_LOCK has no deadline reset in the beta: anchor directly to its map-id reset.
   marker='DoExpr {setvar[tm_map_id,' if n=='MAP_LOCK' else 'DoExpr {setvar[tm_cache_deadline,0]}'
   at=states[n].index(marker)
   states[n]=states[n][:at]+expr('setvar[tm_cache_anchor,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_preexisting,listcreate[]]')+'\n\t\t\t\t'+states[n][at:]
 if 'DIG_FIRST' in states:
  states['DIG_FIRST']=states['DIG_FIRST'].replace('DoExpr {setpvar[tm_dig_active,1]}',expr('if[getvar[tm_cache_anchor]==0,setvar[tm_cache_preexisting,wobjectfindalllandscapebytemplatetype[1004150]]]')+'\n\t\t\t\t'+expr('if[getvar[tm_cache_anchor]==0,setvar[tm_cache_anchor,getplayercoordinates[]]]')+'\n\t\t\t\tDoExpr {setpvar[tm_dig_active,1]}',1)
 # If entering an already active encounter, don't exclude its already spawned reward.
 states['CACHE_PREP']=states['CACHE_PREP'].replace('DoExpr {setvar[tm_cache_id,0]}','\n\t\t\t\t'.join([expr('setvar[tm_cache_id,0]'),expr('if[getvar[tm_cache_anchor]==0,setvar[tm_cache_anchor,getplayercoordinates[]]]'),expr('setvar[tm_cache_tried,listcreate[]]'),expr('setvar[tm_cache_probe_id,0]'),expr('setvar[tm_cache_probe_until,0]'),expr('setvar[tm_cache_candidates,listcreate[]]')]))
 b=states['CACHE_SEARCH'];start=b.index('\tIF: All\n\t\t\tSecsInStateGE 5');b=b[:start]
 # An old already-open cache is not proof of a reward from this encounter.
 b=b.replace(typed+' && getvar[tm_cache_id]==0',typed+' && getvar[tm_cache_id]==0 && !listcontains[getvar[tm_cache_preexisting],wobjectgetid[wobjectgetopencontainer[]]] && strlen[wobjectgetphysicscoordinates[wobjectgetopencontainer[]]]>0 && coordinatedistanceflat[wobjectgetphysicscoordinates[wobjectgetopencontainer[]],getvar[tm_cache_anchor]]<=80')
 b=b.replace('DoExpr {setvar[tm_cache_id,wobjectgetid[wobjectgetopencontainer[]]]}',expr('setvar[tm_cache_id,wobjectgetid[wobjectgetopencontainer[]]]')+'\n\t\t\t\t'+expr('setvar[tm_cache_probe_id,0]'))
 # All cache use is idempotent container opening. Inventory moves stay on the existing ack path.
 b+='\tIF: Expr {getvar[tm_cache_probe_id]!=0 && getunixtime[]>=getvar[tm_cache_probe_until]}\n\t\tDO: DoAll\n\t\t\t\tDoExpr {setvar[tm_cache_probe_id,0]}\n\t\t\t\tSetState {CACHE_SEARCH}\n'
 b+='\tIF: Expr {getvar[tm_cache_id]!=0 && wobjectgetid[wobjectgetopencontainer[]]!=getvar[tm_cache_id] && getvar[tm_cache_probe_id]==0}\n\t\tDO: SetState {CACHE_REOPEN_BOUND}\n'
 b+='\tIF: All\n\t\t\tSecsInStateGE 2\n\t\t\tExpr {getvar[tm_cache_id]==0 && getvar[tm_cache_probe_id]==0 && getunixtime[]>=getvar[tm_cache_retry_at]}\n\t\tDO: SetState {CACHE_SCAN}\n~~ }\n\n';states['CACHE_SEARCH']=b
 # A late corpse/direct payout needs no cache to exist; real inventory increase is still required.
 b=states['CACHE_SEARCH'];pos=b.index('\tIF: Expr {'+typed)
 b=b[:pos]+'\tIF: Expr {getpvar[tm_dig_active]==1 && getvar[tm_cache_id]==0 && getvar[tm_cache_pending]==0 && getinventorycountbytemplatetype[1004101]>getvar[tm_doubloon_before]}\n\t\tDO: SetState {CACHE_FINISH}\n'+b[pos:];states['CACHE_SEARCH']=b
 candidates='listfilter[wobjectfindalllandscapebytemplatetype[1004150],`strlen[wobjectgetphysicscoordinates[$1]]>0 && coordinatedistanceflat[wobjectgetphysicscoordinates[$1],getvar[tm_cache_anchor]]<=80 && !listcontains[getvar[tm_cache_preexisting],wobjectgetid[$1]] && !listcontains[getvar[tm_cache_tried],wobjectgetid[$1]]`]'
 state('CACHE_SCAN',action([expr('setvar[tm_cache_candidates,'+candidates+']'),'SetState {CACHE_CHOOSE}']))
 state('CACHE_CHOOSE','\tIF: Expr {listcount[getvar[tm_cache_candidates]]>0}\n\t\tDO: SetState {CACHE_TRY_NEXT}\n'+action([expr('setvar[tm_cache_tried,listcreate[]]'),expr('setvar[tm_cache_retry_at,getunixtime[]+8]'),expr('setvar[ml_status,`Waiting for another local cache candidate`]'),'SetState {CACHE_SEARCH}']))
 state('CACHE_TRY_NEXT',action([expr('setvar[tm_cache_probe_id,wobjectgetid[listgetitem[getvar[tm_cache_candidates],0]]]'),expr('listadd[getvar[tm_cache_tried],getvar[tm_cache_probe_id]]'),expr('setvar[tm_cache_probe_until,getunixtime[]+8]'),expr('setvar[ml_status,`Trying the next local Unearthed Cache`]'),expr('actiontryuseitem[wobjectfindbyid[getvar[tm_cache_probe_id]]]'),'SetState {CACHE_SEARCH}']))
 state('CACHE_REOPEN_BOUND',action([expr('setvar[tm_cache_probe_id,getvar[tm_cache_id]]'),expr('setvar[tm_cache_probe_until,getunixtime[]+8]'),expr('actiontryuseitem[wobjectfindbyid[getvar[tm_cache_id]]]'),'SetState {CACHE_SEARCH}']))
 # No re-use of a destructive reward action: still only one outstanding acknowledged item move.
 for name in ('CACHE_FINISH',):
  states[name]=states[name].replace('DoExpr {setvar[tm_cache_deadline,0]}',expr('setvar[tm_cache_probe_id,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_anchor,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_preexisting,listcreate[]]')+'\n\t\t\t\tDoExpr {setvar[tm_cache_deadline,0]}',1)
 for name in ('CACHE_SCAN','CACHE_CHOOSE','CACHE_TRY_NEXT','CACHE_REOPEN_BOUND'):
  header='STATE: {'+name+'} ~~ {\n'
  states[name]=states[name].replace(header,header+'\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Expr {getunixtime[]>=getvar[tm_cache_deadline]}\n\t\tDO: SetState {MISSION_ERROR}\n',1)
