"""Adapt the tested mission to public stock APIs. Never patches the RynthAi DLL."""
from pathlib import Path
import re,json,math
root=Path(__file__).resolve().parents[2]
src=root/'metas/marae-lassel-mission'; out=root/'metas/marae-lassel-stock'
af=(src/'MetaFiles/Marae Lassel - Embedded.af').read_text()
af=af.replace('maraerecoverybusy[]','getvar[mm_stock_recovery_busy]')
af=af.replace('maraenavrestore[]','setvar[mm_stock_restore_generation,getvar[mm_stock_restore_generation]+1]')
af=af.replace('setvar[tm_cache_sent,maraecachepickup[wobjectfindbyid[getvar[tm_cache_pending]]]]','listcreate[setvar[tm_cache_sent,-1],setvar[mm_stock_pickup_generation,getvar[mm_stock_pickup_generation]+1]]')
af=af.replace('setpvar[mm_pickup_unresolved,0]','listcreate[setpvar[mm_pickup_unresolved,0],setpvar[mm_stock_pickup_issued,0]]')
af=af.replace('setvar[tm_walk_generation,getvar[tm_walk_generation]+1]','listcreate[setvar[tm_walk_generation,getvar[tm_walk_generation]+1],setvar[mm_stock_walk_generation,getvar[mm_stock_walk_generation]+1]]')
af=af.replace('DoExpr {setvar[mm_schema,1]}','DoExpr {setvar[mm_schema,1]}\n\t\t\t\tDoExpr {setvar[mm_stock_schema,1]}')
# Enough time for ten hunters with three eight-second attempts each, plus indoor travel.
af=re.sub(r'(STATE: \{T\d+_(?:NPC_TALK_WAIT|TURNIN_WAIT)\} ~~ \{\n)(.*?)(?=\n~~ })',lambda m:m[1]+m[2].replace('SecsInStateGE 180','SecsInStateGE 420'),af,flags=re.S)
# A missing/unloaded bridge must not turn into an endless NPC navigation hold.
af=af.replace('STATE: {Default} ~~ {','STATE: {Default} ~~ {\n\tIF: Expr {getvar[mm_stock_ready]!=1 || getunixtime[]-getvar[mm_stock_heartbeat]>5}\n\t\tDO: DoAll\n\t\t\t\tDoExpr {echo[`Marae: enable ACAI2.Marae in RynthCore Plugins, then Resume run.`]}\n\t\t\t\tSetState {HOLD}',1)
trailers={0:0,2:1,3:1,4:1,5:2,6:6,7:6,8:0,9:3}; hunters=[]
def transform(m):
 name=m[1]; lines=m[3].splitlines(); assert lines[0]=='uTank2 NAV 1.2'
 count=int(lines[2]); index=3; points=[]
 for _ in range(count):
  t=int(lines[index]); size=5+trailers[t]; points.append(lines[index:index+size]);index+=size
 assert all(x.startswith('#rynth-talk:') for x in lines[index:]),(name,index,len(lines))
 result=[]; previous=None
 for p in points:
  if int(p[0])!=7:
   result.append(p)
   if int(p[0]) in (0,8):previous=p
   continue
  npc=p[5]; n,e,z=float(p[2]),float(p[1]),float(p[3]); ident=len(hunters)+1
  zone=int(name[:3]); hunters.append(dict(Id=ident,Name=npc,Zone=zone,NS=n,EW=e,Z=z))
  # Approach from the preceding walk leg, keeping clear of the NPC's center.
  dn=(float(previous[2])-n) if previous else -1
  de=(float(previous[1])-e) if previous else 0
  length=math.hypot(dn,de)
  if length<1e-9:dn,de,length=-1,0,1
  n+=dn/length*2.5/240;e+=de/length*2.5/240
  def chat(text):return ['4',str(e),str(n),str(z),'0',text]
  result.append(chat('/ra mexec listcreate[if[getvar[mm_stock_npc_held]!=1,setvar[mm_stock_npc_old_min,ragetsetting[FollowNavMin]]],setvar[mm_stock_npc_held,1],rasetsetting[FollowNavMin,0.5]]'))
  result.append(['0',str(e),str(n),str(z),'0'])
  result.append(chat(f'/ra mexec listcreate[vtsetsetting[EnableNav,0],setvar[mm_stock_npc_request,{ident}],setvar[mm_stock_npc_generation,getvar[mm_stock_npc_generation]+1]]'))
 content=['uTank2 NAV 1.2',lines[1],str(len(result))]+[x for p in result for x in p]
 return 'NAVDATA: '+name+' '+str(len(content))+' ~~ {\n'+'\n'.join(content)+'\n~~ }'
af=re.sub(r'NAVDATA: (\S+) (\d+) ~~ \{\n(.*?)\n~~ }',transform,af,flags=re.S)
assert len(hunters)==27
lua_source=(src/'Scripts/MaraeLassel/index.lua').read_text()
for h in hunters:
 block=re.search(r'\['+str(h['Zone'])+r'\] = \{(.*?)\},',lua_source)[1]
 pairs=dict((name,key) for key,name in re.findall(r'\["([^"]+)"\]="([^"]+)"',block))
 h['Key']=pairs[h['Name']]

assert not re.search(r'\bmarae\w*\[',af,re.I)
(out/'MetaFiles').mkdir(parents=True,exist_ok=True);(out/'MetaFiles/Marae Lassel - Embedded.af').write_text(af)
(root/'src/ACAI2.Rynth.Marae.Plugin/hunters.json').write_text(json.dumps(hunters,indent=2)+'\n')
print('Stock mission: 27 explicit confirmed hunter requests; original 20 routes preserved; no custom expressions.')
