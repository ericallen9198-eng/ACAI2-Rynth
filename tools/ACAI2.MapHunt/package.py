"""Assemble only the distributable meta/helper and the unchanged reviewed support binary."""
from pathlib import Path
import sys,hashlib,json,zipfile,shutil
root=Path(__file__).resolve().parents[2]
support=Path(sys.argv[1]);target=Path(sys.argv[2]);target.mkdir(parents=True,exist_ok=True)
expected='9b29173de8a04cba9ec6be3b86006f60923eef75de0949b947e2e6ea4804d9ed'
assert hashlib.sha256(support.read_bytes()).hexdigest()==expected,'Unreviewed support DLL'
pkg=target/'MapHunt-v0.1.0';shutil.copytree(root/'metas/maphunt',pkg,dirs_exist_ok=True)
(pkg/'Compatibility').mkdir(exist_ok=True);shutil.copy2(support,pkg/'Compatibility'/support.name)
files=[]
for rel in ['MetaFiles/MapHunt.af','Scripts/MapHunt/index.lua','Compatibility/RynthCore.Plugin.RynthAi.dll']:
 files.append({'Source':rel.replace('/','\\'),'Sha256':hashlib.sha256((pkg/rel).read_bytes()).hexdigest()})
(pkg/'checksums.json').write_text(json.dumps({'Files':files},indent=2)+'\n')
(pkg/'Compatibility/RynthSuite-LICENSE.txt').write_text(Path(sys.argv[3]).read_text())
zipname=target/'ACAI2-MapHunt-v0.1.0.zip'
with zipfile.ZipFile(zipname,'w',zipfile.ZIP_DEFLATED) as z:
 for f in sorted(pkg.rglob('*')):
  if f.is_file():z.write(f,f.relative_to(pkg))
zipname.with_suffix('.zip.sha256').write_text(hashlib.sha256(zipname.read_bytes()).hexdigest()+'  '+zipname.name+'\n')
print(zipname)
