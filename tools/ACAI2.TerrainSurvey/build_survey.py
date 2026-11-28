from pathlib import Path
import json,re,math,argparse
parser=argparse.ArgumentParser();parser.add_argument('--data',type=Path,required=True);parser.add_argument('--out',type=Path,required=True);args=parser.parse_args()
root=args.data;pack=args.out;(pack/'MetaFiles').mkdir(parents=True,exist_ok=True);(pack/'NavProfiles').mkdir(exist_ok=True)
src=Path(__file__).resolve().parents[2].joinpath('metas/marae-lassel-stock/MetaFiles/Marae Lassel - Embedded.af').read_text()
navs={};states={};report=[]
def E(x):return 'DoExpr {'+x+'}'
def A(a):return '\tIF: Always\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in a)
def R(c,a):return '\tIF: '+c+'\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in a)
def S(n,b):states[n]=b
D=R('Expr {getvar[te_logger_schema]!=3 || getvar[te_logger_active]!=1 || getunixtime[]-getvar[te_logger_heartbeat]>20}',['SetState {LOGGER_LOST}'])+R('Death',['SetState {RECOVERY_WAIT}'])+R('Expr {getvar[te_recovery_busy]==1}',['SetState {RECOVERY_WAIT}'])
pos={275:'45.2079477958333|-75.0632305458333|0.3833541875',180:'43.5416666666667|-85.4083333333333|0.0833541625',225:'53.5283333458333|-79.802500025|0.3000208542'}
def coord(p):return f"{p['north']:.12f}|{p['east']:.12f}|{p['z']/240:.12f}"
def nav(name,points,label=None):
 pts=[]
 for p in points:pts.append(['0',str(p['east']),str(p['north']),str(p['z']/240),'0'])
 navs[name]=['uTank2 NAV 1.2','4',str(len(pts))]+[v for p in pts for v in p]
S('Default',D+R('Expr {ragetsetting[FollowNavMin]>0.75 || ragetsetting[NavResumeNearest]!=0}',[E('echo[`Terrain survey needs FollowNavMin=0.5 and NavResumeNearest=0 before starting. See READ FIRST.`]'),'SetState {FINISH}'])+A([E('setvar[te_stage,0]'),E('setvar[te_probe,1]'),E('setvar[te_attempt,0]'),E('setvar[te_entry_hub,1]'),E('setvar[te_recall_attempts,0]'),E('echo[`Terrain Access Survey: fresh mesh-planned cliff approaches, no hunt loops. Probe results are provisional.`]'),'SetState {RECALL}']))
S('RESUME',D+A([E('setvar[te_return_reason,`resume_current_point`]'),E('setvar[te_entry_hub,1]'),E('setvar[te_recall_attempts,0]'),'SetState {RECALL}']))
S('RECALL',D+R('Expr {getvar[te_can_travel]==1}',['SetOpt {EnableNav} {False}',E('setvar[te_transport_generation,getvar[te_transport_generation]+1]'),E('setvar[te_transport_timeout,0]'),E('setvar[te_recall_attempts,getvar[te_recall_attempts]+1]'),E('setvar[te_settle,getunixtime[]+5]'),E('echo[`[TerrainSurvey] Hub recall: `+getvar[te_return_reason]+`; last movement result: `+getvar[te_reason]]'),'Chat {/ls}','SetState {HUB_WAIT}']))
S('HUB_WAIT',D+R('Expr {getvar[te_location_stable]==1 && getcharvital_current[1]>0 && strlen[getplayercoordinates[]]>0 && coordinatedistanceflat[getplayercoordinates[],`'+pos[275]+'`]<15 && abs[coordinategetz[getplayercoordinates[]]*240-92.005]<8 && getunixtime[]>getvar[te_settle]}',['SetOpt {EnableNav} {False}','SetState {DISPATCH}'])+R('Expr {getvar[te_transport_timeout]==1}',['SetState {RECALL_CHECK}']))
S('RECALL_CHECK',D+R('Expr {getvar[te_recall_attempts]<2}',['SetState {RECALL}'])+A([E('echo[`Terrain survey: /ls arrival unconfirmed after two attempts. No wrong-area walking was started.`]'),'SetState {FINISH}']))
S('DISPATCH',D+R('Expr {getvar[te_stage]==0}',['SetState {SELECT_275}'])+R('Expr {getvar[te_stage]==1}',['SetState {PORTAL_180}'])+R('Expr {getvar[te_stage]==2}',['SetState {PORTAL_225}'])+A(['SetState {FINISH}']))
for z in [180,225]:
 name=f'Hub_to_{z}';m=re.search(r'NAVDATA: '+name+r' (\d+) ~~ \{\n(.*?)\n~~ \}',src,re.S);navs[name]=m[2].splitlines()
 S(f'PORTAL_{z}',D+A([E('setvar[te_transport_generation,getvar[te_transport_generation]+1]'),E('setvar[te_transport_timeout,0]'),f'EmbedNav {name} {{once}}','SetOpt {EnableNav} {True}',f'SetState {{LAND_{z}}}']))
 S(f'LAND_{z}',D+R('Expr {getvar[te_location_stable]==1 && getcharvital_current[1]>0 && strlen[getplayercoordinates[]]>0 && coordinatedistanceflat[getplayercoordinates[],`'+pos[z]+'`]<15 && abs[coordinategetz[getplayercoordinates[]]*240-'+('20.005' if z==180 else '72.005')+']<8}',['SetOpt {EnableNav} {False}',f'SetState {{SELECT_{z}}}'])+R('Expr {getvar[te_transport_timeout]==1}',[E(f'echo[`Terrain survey: {z} portal arrival unconfirmed; sector remains untested.`]'),'SetState {NEXT_SECTOR}']))
for z in [275,180,225]:
 route=json.loads((root/f'route-{z}.json').read_text());hub=json.loads((root/f'hub-{z}-approaches.json').read_text());legs=route['legs']
 original=root/f'Terrain_{z}_Cliff_Approaches.nav';(pack/'NavProfiles'/original.name).write_text(original.read_text())
 selects=''
 for i,leg in enumerate(legs,1):selects+=R('Expr {getvar[te_probe]=='+str(i)+'}',[f'SetState {{PROBE_{z}_{i}}}'])
 selects+=R('Expr {getvar[te_entry_hub]==1}',['SetState {NEXT_SECTOR}'])+A([f'SetState {{RETURN_{z}}}']);S(f'SELECT_{z}',D+selects)
 for i,leg in enumerate(legs,1):
  ident=f'{z}_{i:02d}';approach=leg['approach'];direct=next(x for x in hub if math.hypot(x['approach']['north']-approach['north'],x['approach']['east']-approach['east'])<1e-7)
  # A retry must approach the final section differently, not repeat the same edge forever.
  def signature(pts):return [(round(p['east']*240/5),round(p['north']*240/5)) for p in pts[-4:]]
  def behind(pts,distance=50):
   left=distance
   for a,b in zip(reversed(pts[:-1]),reversed(pts[1:])):
    length=math.hypot(a['north']-b['north'],a['east']-b['east'])*240
    if length>=left and length>0:return {'north':b['north']+(a['north']-b['north'])*left/length,'east':b['east']+(a['east']-b['east'])*left/length}
    left-=length
   return pts[0]
  a,b=behind(leg['corners']),behind(direct['corners'])
  ax,ay=a['east']-approach['east'],a['north']-approach['north'];bx,by=b['east']-approach['east'],b['north']-approach['north']
  denom=math.hypot(ax,ay)*math.hypot(bx,by)
  angle=math.degrees(math.acos(max(-1,min(1,(ax*bx+ay*by)/denom)))) if denom else 0
  different=i>1 and math.hypot(a['east']-b['east'],a['north']-b['north'])*240>=20 and angle>=45
  nav('Leg_'+ident,leg['corners'],ident);nav('Hub_'+ident,direct['corners'],ident)
  reset=[E('setvar[te_movement_fail,0]'),E('setvar[te_reason,``]'),E('setvar[te_entry_hub,0]'),E('setvar[te_travel_seconds,0]'),E('setvar[te_trace,listcreate[]]'),E('setvar[te_sample_at,getunixtime[]+6]'),E('setvar[te_last_pos,getplayercoordinates[]]'),E('setvar[te_last_progress,getunixtime[]]'),'SetOpt {EnableNav} {True}',f'SetState {{WAIT_{ident}}}']
  S(f'PROBE_{z}_{i}',D+R('Expr {getvar[te_entry_hub]==1}',[f'EmbedNav Hub_{ident} {{once}}']+reset)+A([f'EmbedNav Leg_{ident} {{once}}']+reset))
  done=['SetOpt {EnableNav} {False}',E('setvar[te_report,1]'),E('setvar[te_commit,0]'),E('setvar[te_seq,getvar[te_seq]+1]'),E('setvar[te_report_coords,getplayercoordinates[]]'),f'SetState {{COMMIT_REACHED_{ident}}}']
  S('COMMIT_REACHED_'+ident,D+R('Expr {getvar[te_commit]==getvar[te_seq]}',[E('setvar[te_probe,getvar[te_probe]+1]'),E('setvar[te_attempt,0]'),f'SetState {{SELECT_{z}}}']))
  body=D+'\tIF: All\n\t\t\tExpr {getvar[te_location_stable]==1 && strlen[getplayercoordinates[]]>0 && coordinatedistanceflat[getplayercoordinates[],`'+coord(approach)+'`]<3 && abs[coordinategetz[getplayercoordinates[]]*240-'+str(approach['z'])+']<4}\n\t\tDO: DoAll\n'+''.join('\t\t\t\t'+x+'\n' for x in done)
  body+=R('Expr {getvar[te_movement_fail]==1}',[f'SetState {{FAILED_{ident}}}'])
  S('WAIT_'+ident,body)
  failure=[]
  retry_actions=[E('setvar[te_return_reason,`retry_different_hub_approach`]'),E('setvar[te_attempt,1]'),E('setvar[te_entry_hub,1]'),E('setvar[te_recall_attempts,0]'),'SetOpt {EnableNav} {False}','SetState {RECALL}']
  skip_actions=[E('setvar[te_return_reason,`defer_failed_approach`]'),'SetOpt {EnableNav} {False}',E('setvar[te_probe,getvar[te_probe]+1]'),E('setvar[te_attempt,0]'),E('setvar[te_entry_hub,1]'),E('setvar[te_recall_attempts,0]'),('SetState {NEXT_SECTOR}' if i==len(legs) else 'SetState {RECALL}')]
  S('FAILED_'+ident,D+A(['SetOpt {EnableNav} {False}',E('setvar[te_report,'+('if[getvar[te_attempt]==0,2,3]' if different else '3')+']'),E('setvar[te_commit,0]'),E('setvar[te_seq,getvar[te_seq]+1]'),E('setvar[te_report_coords,getplayercoordinates[]]'),f'SetState {{COMMIT_FAILED_{ident}}}']))
  S('COMMIT_FAILED_'+ident,D+R('Expr {getvar[te_commit]==getvar[te_seq] && getvar[te_report]==2}',retry_actions)+R('Expr {getvar[te_commit]==getvar[te_seq] && getvar[te_report]==3}',skip_actions))
  report.append(dict(id=ident,zone=z,distinct_hub_retry=different,approach=approach,boundaryA=leg['boundaryA'],boundaryB=leg['boundaryB'],opposite=leg['opposite'],heightDifference=leg['heightDifference'],route_length=leg['length'],status='mesh_predicted_not_live_verified'))
 nav('Return_'+str(z),route['returnCorners'])
 S('RETURN_'+str(z),D+A([f'EmbedNav Return_{z} {{once}}','SetOpt {EnableNav} {True}',f'SetState {{RETURN_WAIT_{z}}}']))
 S('RETURN_WAIT_'+str(z),D+R('Expr {getvar[te_location_stable]==1 && strlen[getplayercoordinates[]]>0 && coordinatedistanceflat[getplayercoordinates[],`'+pos[z]+'`]<5}',['SetState {NEXT_SECTOR}'])+R('SecsInStateGE 120',[E(f'echo[`Terrain survey: {z} local return did not finish; using the lifestone hub for the next sector.`]'),'SetState {NEXT_SECTOR}']))
S('NEXT_SECTOR',D+A(['SetOpt {EnableNav} {False}',E('setvar[te_return_reason,`next_sector`]'),E('setvar[te_stage,getvar[te_stage]+1]'),E('setvar[te_probe,1]'),E('setvar[te_attempt,0]'),E('setvar[te_entry_hub,1]'),E('setvar[te_recall_attempts,0]'),'SetState {NEXT_CHECK}']))
S('NEXT_CHECK',R('Expr {getvar[te_stage]>=3}',['SetState {FINISH}'])+A(['SetState {RECALL}']))
# Native corpse recovery keeps the macro running; no survey route is active here.
S('RECOVERY_WAIT',R('Expr {getvar[te_logger_schema]!=3 || getvar[te_logger_active]!=1 || getunixtime[]-getvar[te_logger_heartbeat]>20}',['SetState {LOGGER_LOST}'])+A(['SetOpt {EnableNav} {False}']))
S('LOGGER_LOST',R('Expr {getpvar[te_user_stop]==1}',['Chat {/ra stop}','SetState {RECORDER_STOPPED}'])+A(['SetOpt {EnableNav} {False}',E('echo[`Terrain survey: recorder heartbeat missing; pausing movement and trying automatic recorder recovery.`]'),'SetState {RECORDER_WINDOW}']))
S('RECORDER_WINDOW',R('Expr {getunixtime[]-getpvar[te_recorder_window]>=1800}',[E('setpvar[te_recorder_window,getunixtime[]]'),E('setpvar[te_recorder_restarts,0]'),'SetState {RECORDER_RETRY}'])+A(['SetState {RECORDER_RETRY}']))
S('RECORDER_RETRY',R('Expr {getpvar[te_recorder_restarts]>=3}',['SetState {RECORDER_FAILED}'])+A(['SetOpt {EnableNav} {False}',E('setpvar[te_recorder_restarts,getpvar[te_recorder_restarts]+1]'),E('setvar[te_recorder_retry_at,getunixtime[]+60]'),'Chat {/lua start TerrainSurvey}','SetState {RECORDER_AWAIT}']))
S('RECORDER_AWAIT',R('Expr {getunixtime[]>=getvar[te_recorder_retry_at]}',['SetState {RECORDER_WINDOW}']))
S('RECORDER_FAILED',A(['SetOpt {EnableNav} {False}',E('echo[`Terrain survey: three recorder recovery attempts in 30 minutes were exhausted. Progress retained; check recorder/disk errors and use /terrainsurvey resume.`]'),'Chat {/ra stop}','SetState {RECORDER_STOPPED}']))
S('RECORDER_STOPPED','\tIF: Expr {0==1}\n\t\tDO: SetState {RECORDER_STOPPED}\n')
S('FINISH',A(['SetOpt {EnableNav} {False}',E('setvar[te_finish,1]'),'SetState {FINISH_COMMIT}']))
S('FINISH_COMMIT',D+R('Expr {getvar[te_finish_ack]==1}',['Chat {/ra stop}','SetState {FINISHED}']))
S('FINISHED','\tIF: Expr {0==1}\n\t\tDO: SetState {FINISHED}\n')
af='\n'.join('STATE: {'+n+'} ~~ {\n'+b+'~~ }\n' for n,b in states.items())
for n,a in navs.items():af+='\nNAVDATA: '+n+' '+str(len(a))+' ~~ {\n'+'\n'.join(a)+'\n~~ }\n'
(pack/'MetaFiles/Terrain Access Survey.af').write_text(af)
(pack/'probe-index.json').write_text(json.dumps(dict(schema=1,source='RynthNav NavData release 2026102103; new Detour mesh paths, not hunt loops',probes=report),indent=2))
print('Built terrain-only controller:',len(states),'states;',len(navs),'embedded navs;',len(report),'cliff targets;',sum(p['distinct_hub_retry'] for p in report),'distinct hub retries.')
