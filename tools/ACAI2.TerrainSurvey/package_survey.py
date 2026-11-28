"""Create the playable text/Lua package; never bundle DLLs, player files or mesh tiles."""
from pathlib import Path
import argparse,zipfile,hashlib,shutil,json
p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--maps',type=Path);a=p.parse_args()
repo=Path(__file__).resolve().parents[2];assets=repo/'metas/terrain-survey'
a.out.mkdir(parents=True,exist_ok=True)
stage=a.out/'Terrain Access Survey v0.3.1'
if stage.exists():shutil.rmtree(stage)
shutil.copytree(assets,stage)
if a.maps:
 for ext in ['png','pdf']:shutil.copy2(a.maps/f'terrain-access-survey.{ext}',stage/f'Terrain survey map.{ext}')
files={str(f.relative_to(stage)):hashlib.sha256(f.read_bytes()).hexdigest()for f in sorted(stage.rglob('*'))if f.is_file()}
(stage/'SHA256.json').write_text(json.dumps(files,indent=2))
def archive(folder,name):
 dest=a.out/name
 with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED,compresslevel=9)as z:
  for f in sorted(folder.rglob('*')):
   if f.is_file():
    assert f.suffix.lower()not in ['.dll','.tile','.exe']
    z.write(f,f.relative_to(folder.parent))
 with zipfile.ZipFile(dest)as z:assert z.testzip()is None
 print(dest,hashlib.sha256(dest.read_bytes()).hexdigest())
archive(stage,'Terrain-Access-Survey-v0.3.1.zip')
source=a.out/'Terrain Survey v0.3.1 Sources'
if source.exists():shutil.rmtree(source)
for part in ['tools/ACAI2.TerrainSurvey','tests/ACAI2.TerrainSurvey.Tests','metas/terrain-survey']:
 for f in (repo/part).rglob('*'):
  if f.is_file()and not {'bin','obj','__pycache__'}&set(f.parts):
   dest=source/f.relative_to(repo);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest)
archive(source,'Terrain-Access-Survey-Sources-v0.3.1.zip')
