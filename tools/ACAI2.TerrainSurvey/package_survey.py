"""Package the Windows-published companion with the survey; never include engine DLLs or player files."""
from pathlib import Path
import argparse,zipfile,hashlib,shutil,json
p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--maps',type=Path);p.add_argument('--plugin',type=Path,required=True);a=p.parse_args()
repo=Path(__file__).resolve().parents[2];assets=repo/'metas/terrain-survey'
a.out.mkdir(parents=True,exist_ok=True)
stage=a.out/'Terrain Access Survey v0.4.2'
if stage.exists():shutil.rmtree(stage)
shutil.copytree(assets,stage)
assert (a.plugin/'ACAI2.Rynth.Terrain.Plugin.dll').is_file(), 'A Windows-published companion is required'
assert 'RYNTH_PLUGIN_MANIFEST'.encode('utf-16le') in (a.plugin/'ACAI2.Rynth.Terrain.Plugin.dll').read_bytes(), 'Publish the companion on Windows before packaging'
assert not any(a.plugin.glob('RynthCore.Plugin.Rynth*.dll')), 'Never bundle engine/plugin replacements'
shutil.copytree(a.plugin,stage/'Plugin')
notices=stage/'Notices';notices.mkdir()
shutil.copy2(repo/'external/RynthCore/LICENSE',notices/'RynthCore-LICENSE.txt')
shutil.copy2(repo/'THIRD-PARTY-NOTICES.md',notices/'ACAI2-NOTICES.md')
shutil.copy2(repo/'src/ACAI2.Rynth.Terrain.Plugin/Native/ORIGIN.txt',notices/'RynthSuite-API-ORIGIN.txt')
if a.maps:
 for ext in ['png','pdf']:shutil.copy2(a.maps/f'terrain-access-survey.{ext}',stage/f'Terrain survey map.{ext}')
files={str(f.relative_to(stage)):hashlib.sha256(f.read_bytes()).hexdigest()for f in sorted(stage.rglob('*'))if f.is_file()}
(stage/'SHA256.json').write_text(json.dumps(files,indent=2))
def archive(folder,name,source=False):
 dest=a.out/name
 with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED,compresslevel=9)as z:
  for f in sorted(folder.rglob('*')):
   if f.is_file():
    assert f.suffix.lower()not in ['.tile','.exe']
    if source: assert f.suffix.lower()!='.dll'
    z.write(f,f.relative_to(folder.parent))
 with zipfile.ZipFile(dest)as z:assert z.testzip()is None
 print(dest,hashlib.sha256(dest.read_bytes()).hexdigest())
archive(stage,'Terrain-Access-Survey-v0.4.2.zip')
source=a.out/'Terrain Survey v0.4.2 Sources'
if source.exists():shutil.rmtree(source)
for part in ['tools/ACAI2.TerrainSurvey','tests/ACAI2.TerrainSurvey.Tests','metas/terrain-survey','src/ACAI2.Rynth.Terrain.Plugin','tests/ACAI2.TerrainMesh.Tests']:
 for f in (repo/part).rglob('*'):
  if f.is_file()and not {'bin','obj','__pycache__'}&set(f.parts):
   dest=source/f.relative_to(repo);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest)
archive(source,'Terrain-Access-Survey-Sources-v0.4.2.zip',source=True)
