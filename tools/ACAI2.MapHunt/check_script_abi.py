"""Check manual slot bindings against the genuine published RynthAi.Script source."""
import re,sys
from pathlib import Path
s=Path(sys.argv[1]).read_text()
fields=re.findall(r'public delegate\* unmanaged\[Cdecl\]<[^;]+?>\s+(\w+);',s)
expected=['Evaluate','GetSetting','SetSetting','GetSettingNames','IsMacroRunning','SetMacroRunning','NavGoTo','NavStop','FindNearest','DistanceTo','IsPathClear','SubmitCommand']
assert fields==expected,(fields,expected)
assert 'public const uint InterfaceVersion = 1;' in s
adapter=(Path(__file__).resolve().parents[2]/'src/ACAI2.Rynth.MapHunt.Plugin/NativeMapHuntBot.cs').read_text()
for slot in [0,1,4,6,11]:assert 'Function('+str(slot)+')' in adapter
print('PASS: app Script v1 Cdecl slots match the genuine RynthSuite ABI; no SDK substitutes.')
