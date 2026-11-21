"""Try local Unearthed Cache object IDs fairly; bind pickup only after an open acknowledgment."""

def apply(states,state,action,expr):
 states['CACHE_SEARCH']=states['CACHE_SEARCH'].replace('max[1,wobjectgetintprop[wobjectfindbyid[getvar[tm_cache_pending]],12]]','iif[wobjectgetintprop[wobjectfindbyid[getvar[tm_cache_pending]],12]>1,wobjectgetintprop[wobjectfindbyid[getvar[tm_cache_pending]],12],1]')
 states['CACHE_PICKUP']=states['CACHE_PICKUP'].replace('actiontrymove[wobjectfindbyid[getvar[tm_cache_pending]],wobjectgetplayer[],0,getvar[tm_cache_stackmode]]','maraecachepickup[wobjectfindbyid[getvar[tm_cache_pending]]]')
 states['CACHE_SETTLE']=states['CACHE_SETTLE'].replace('!(', '(').replace(') ||', ')==0 ||',1)
 typed='wobjectgetname[wobjectgetopencontainer[]]==`Unearthed Cache`'
 # If entering an already active encounter, don't exclude its already spawned reward.
 states['CACHE_PREP']=states['CACHE_PREP'].replace('DoExpr {setvar[tm_cache_id,0]}','\n\t\t\t\t'.join([expr('setvar[tm_cache_id,0]'),expr('if[getvar[tm_cache_anchor]==0,setvar[tm_cache_anchor,getplayercoordinates[]]]'),expr('setvar[tm_cache_tried,listcreate[]]'),expr('setvar[tm_cache_rejected,listcreate[]]'),expr('setvar[tm_cache_probe_id,0]'),expr('setvar[tm_cache_probe_until,0]'),expr('setvar[tm_cache_candidates,listcreate[]]')]))
 b=states['CACHE_SEARCH'];start=b.index('\tIF: All\n\t\t\tSecsInStateGE 5');b=b[:start]
 # Keep the proven exact-name/open-container path; no coordinate/WCID prerequisite.
 b=b.replace('DoExpr {setvar[tm_cache_id,wobjectgetid[wobjectgetopencontainer[]]]}',expr('setvar[tm_cache_id,wobjectgetid[wobjectgetopencontainer[]]]')+'\n\t\t\t\t'+expr('setvar[tm_cache_bound_at,getunixtime[]]')+'\n\t\t\t\t'+expr('setvar[tm_cache_probe_id,0]'))
 # All cache use is idempotent container opening. Inventory moves stay on the existing ack path.
 b+='\tIF: Expr {getvar[tm_cache_probe_id]!=0 && getunixtime[]>=getvar[tm_cache_probe_until]}\n\t\tDO: DoAll\n\t\t\t\tDoExpr {setvar[tm_cache_probe_id,0]}\n\t\t\t\tSetState {CACHE_SEARCH}\n'
 b+='\tIF: Expr {getvar[tm_cache_id]!=0 && wobjectgetid[wobjectgetopencontainer[]]!=getvar[tm_cache_id] && getvar[tm_cache_probe_id]==0}\n\t\tDO: SetState {CACHE_REOPEN_BOUND}\n'
 b+='\tIF: All\n\t\t\tSecsInStateGE 2\n\t\t\tExpr {getvar[tm_cache_id]==0 && getvar[tm_cache_probe_id]==0 && getunixtime[]>=getvar[tm_cache_retry_at]}\n\t\tDO: SetState {CACHE_SCAN}\n~~ }\n\n';states['CACHE_SEARCH']=b
 # A late corpse/direct payout needs no cache to exist; real inventory increase is still required.
 b=states['CACHE_SEARCH'];pos=b.index('\tIF: Expr {'+typed)
 b=b[:pos]+'\tIF: Expr {getpvar[tm_dig_active]==1 && getvar[tm_cache_id]==0 && getvar[tm_cache_pending]==0 && getinventorycountbytemplatetype[1004101]>getvar[tm_doubloon_before]}\n\t\tDO: SetState {CACHE_FINISH}\n'+b[pos:];states['CACHE_SEARCH']=b
 # A previously opened cache can close without yielding currency. Release that
 # provisional binding after a short grace period, never during/after an item move.
 b=states['CACHE_SEARCH']
 b=b.replace(typed+' && getvar[tm_cache_id]==0}',typed+' && getvar[tm_cache_id]==0 && listcontains[getvar[tm_cache_rejected],wobjectgetid[wobjectgetopencontainer[]]]==0}')
 pos=b.index('\tIF: Expr {'+typed)
 empty='wobjectgetid[wobjectgetopencontainer[]]!=getvar[tm_cache_id] || listcount[listfilter[wobjectfindallbycontainer[wobjectgetopencontainer[]],`wobjectgetintprop[$1,218103808]==1004101 || wobjectgetobjectclass[$1]==15`]]==0'
 guard='getvar[tm_cache_id]!=0 && getvar[tm_cache_pending]==0 && getvar[tm_cache_pickups]==0 && getunixtime[]>=getvar[tm_cache_bound_at]+8 && (getvar[tm_cache_probe_id]==0 || getunixtime[]>=getvar[tm_cache_probe_until])'
 b=b[:pos]+'\tIF: Expr {'+guard+' && ('+empty+')}\n\t\tDO: SetState {CACHE_RELEASE_EMPTY}\n'+b[pos:]
 states['CACHE_SEARCH']=b
 state('CACHE_RELEASE_EMPTY',action([expr('listadd[getvar[tm_cache_tried],getvar[tm_cache_id]]'),expr('listadd[getvar[tm_cache_rejected],getvar[tm_cache_id]]'),expr('setvar[tm_cache_id,0]'),expr('setvar[tm_cache_probe_id,0]'),expr('setvar[ml_status,`Cache yielded no currency; trying another local cache`]'),'SetState {CACHE_SCAN}']))
 candidates='listfilter[wobjectfindalllandscapebynamerx[`^Unearthed Cache$`],`listcontains[getvar[tm_cache_tried],wobjectgetid[$1]]==0 && listcontains[getvar[tm_cache_rejected],wobjectgetid[$1]]==0`]'
 state('CACHE_SCAN',action([expr('setvar[tm_cache_candidates,'+candidates+']'),'SetState {CACHE_CHOOSE}']))
 state('CACHE_CHOOSE','\tIF: Expr {listcount[getvar[tm_cache_candidates]]>0}\n\t\tDO: SetState {CACHE_TRY_NEXT}\n'+action([expr('setvar[tm_cache_tried,listcreate[]]'),expr('setvar[tm_cache_rejected,listcreate[]]'),expr('setvar[tm_cache_retry_at,getunixtime[]+8]'),expr('setvar[ml_status,`Waiting for another local cache candidate`]'),'SetState {CACHE_SEARCH}']))
 state('CACHE_TRY_NEXT',action([expr('setvar[tm_cache_probe_id,wobjectgetid[listgetitem[getvar[tm_cache_candidates],0]]]'),expr('listadd[getvar[tm_cache_tried],getvar[tm_cache_probe_id]]'),expr('setvar[tm_cache_probe_until,getunixtime[]+8]'),expr('setvar[ml_status,`Trying the next local Unearthed Cache`]'),expr('actiontryuseitem[wobjectfindbyid[getvar[tm_cache_probe_id]]]'),'SetState {CACHE_SEARCH}']))
 state('CACHE_REOPEN_BOUND',action([expr('setvar[tm_cache_probe_id,getvar[tm_cache_id]]'),expr('setvar[tm_cache_probe_until,getunixtime[]+8]'),expr('actiontryuseitem[wobjectfindbyid[getvar[tm_cache_id]]]'),'SetState {CACHE_SEARCH}']))
 # No re-use of a destructive reward action: still only one outstanding acknowledged item move.
 for name in ('CACHE_FINISH',):
  states[name]=states[name].replace('DoExpr {setvar[tm_cache_deadline,0]}',expr('setvar[tm_cache_probe_id,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_anchor,0]')+'\n\t\t\t\t'+expr('setvar[tm_cache_preexisting,listcreate[]]')+'\n\t\t\t\tDoExpr {setvar[tm_cache_deadline,0]}',1)
 for name in ('CACHE_SCAN','CACHE_CHOOSE','CACHE_TRY_NEXT','CACHE_REOPEN_BOUND','CACHE_RELEASE_EMPTY'):
  header='STATE: {'+name+'} ~~ {\n'
  states[name]=states[name].replace(header,header+'\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Expr {getunixtime[]>=getvar[tm_cache_deadline]}\n\t\tDO: SetState {MISSION_ERROR}\n',1)
