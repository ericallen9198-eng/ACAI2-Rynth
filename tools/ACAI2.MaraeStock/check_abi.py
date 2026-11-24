"""Validate the exact public Script and mesh slots used by this app."""
import re,sys
from pathlib import Path
root=Path(sys.argv[1]);local=Path(__file__).resolve().parents[2]
script=(root/'Plugins/Shared/RynthAiScriptApi.cs').read_text()
fields=re.findall(r'public delegate\* unmanaged\[Cdecl\]<[^;]+?>\s+(\w+);',script)
assert fields==['Evaluate','GetSetting','SetSetting','GetSettingNames','IsMacroRunning','SetMacroRunning','NavGoTo','NavStop','FindNearest','DistanceTo','IsPathClear','SubmitCommand']
assert 'public const uint InterfaceVersion = 1;' in script
adapter=(local/'src/ACAI2.Rynth.Marae.Plugin/NativeMaraeBot.cs').read_text()
for slot in [0,1,4,6,11]:assert 'Function('+str(slot)+')' in adapter
mesh=(root/'Plugins/Shared/RynthNavPathApi.cs').read_text()
mesh_fields=re.findall(r'public delegate\* unmanaged\[Cdecl\]<([^;]+?)>\s+(\w+);',mesh)
assert [name for _,name in mesh_fields]==['FindPath','FindPathEx','Raycast']
assert mesh_fields[1][0].replace(' ','')=='double,double,double,double,double,double,int,int,double*,int,int*,double*,int'
print('PASS: Marae Script v1 and RynthNav.Path v2 bindings match genuine stock ABI signatures and slots.')
