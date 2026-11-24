"""Clone the proven cache workflow into an isolated, bounded opportunistic pass."""
import re

def apply(af):
 blocks={m[1]:m[2] for m in re.finditer(r'STATE: \{([^}]+)\} ~~ \{\n(.*?)\n~~ }',af,re.S)}
 clones=[]
 for name,body in blocks.items():
  if not name.startswith('CACHE_'):continue
  if name=='CACHE_FINISH':body='\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Always\n\t\tDO: DoAll\n\t\t\t\tDoExpr {setvar[mm_watch_terminal,1]}\n\t\t\t\tSetState {WATCH_RETURN}'
  else:
   body=re.sub(r'SetState \{CACHE_(\w+)\}',r'SetState {WATCH_CACHE_\1}',body)
   body=re.sub(r'SetState \{(?:FAIL_FROM_\w+|MISSION_ERROR|BUDGET_EXPIRED)\}', 'SetState {WATCH_RETURN}',body)
   # Do not leave a pending possibly issued currency move on ordinary timeout.
   guard='\tIF: Expr {getvar[tm_cache_pending]==0 && (getunixtime[]>=getvar[mm_watch_deadline] || ratargetcount[]>0)}\n\t\tDO: SetState {WATCH_RETURN}\n'
   body=guard+body
   body=body.replace('setpvar[tm_stat_success_search,getpvar[tm_stat_success_search]+1]','setvar[mm_watch_looted,getvar[mm_watch_looted]+1]')
   if name=='CACHE_PREP':body=body.replace('SetOpt {EnableCombat} {False}','SetOpt {EnableCombat} {True}')
   # Never bind a different player's open container or roam through all caches.
   body=body.replace('wobjectgetname[wobjectgetopencontainer[]]==`Unearthed Cache`','wobjectgetid[wobjectgetopencontainer[]]==getvar[mm_watch_id] && wobjectgetname[wobjectgetopencontainer[]]==`Unearthed Cache`')
   body=body.replace('`listcontains[getvar[tm_cache_tried],wobjectgetid[$1]]==0', '`wobjectgetid[$1]==getvar[mm_watch_id] && listcontains[getvar[tm_cache_tried],wobjectgetid[$1]]==0')
   if name in ('CACHE_CHOOSE','CACHE_RELEASE_EMPTY'):
    # No candidate left / opened but empty: leave this pass; watcher schedules any retry.
    body=body.replace('SetState {WATCH_CACHE_SEARCH}','SetState {WATCH_RETURN}').replace('SetState {WATCH_CACHE_SCAN}','SetState {WATCH_RETURN}')
   if name=='CACHE_RELEASE_EMPTY':body=body.replace('DoExpr {setvar[tm_cache_id,0]}','DoExpr {setvar[mm_watch_terminal,1]}\n\t\t\t\tDoExpr {setvar[tm_cache_id,0]}')
   if name=='CACHE_SEARCH':
    body=body.replace('DO: DoAll\n\t\t\t\tDoExpr {setvar[tm_cache_probe_id,0]}\n\t\t\t\tSetState {WATCH_CACHE_SEARCH}', 'DO: SetState {WATCH_RETURN}')
   # Doubloon direct-payout detection belongs to a dig, never an opportunistic probe.
   body=body.replace('getpvar[tm_dig_active]==1 && getvar[tm_cache_id]==0', 'getvar[mm_watch_active]==0 && getpvar[tm_dig_active]==1 && getvar[tm_cache_id]==0')
  clones.append('STATE: {WATCH_'+name+'} ~~ {\n'+body+'\n~~ }\n')
 clones.append('STATE: {WATCH_RETURN} ~~ {\n\tIF: Death\n\t\tDO: SetState {MAP_DEATH}\n\tIF: Always\n\t\tDO: DoExpr {setvar[ml_status,`Nearby cache check finished; restoring hunt route`]}\n~~ }\n')
 # Append rules before the nav data; all ordinary event rules remain unchanged.
 where=af.index('NAVDATA:')
 af=af[:where]+'\n'.join(clones)+'\n'+af[where:]
 af=af.replace('DoExpr {setvar[mm_stock_schema,1]}','DoExpr {setvar[mm_stock_schema,1]}\n\t\t\t\tDoExpr {setvar[mm_watch_active,0]}')
 return af
