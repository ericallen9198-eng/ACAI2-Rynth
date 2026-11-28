"""Range-download selected official NavData tiles; verify ZIP-entry CRC and size."""
from pathlib import Path
import urllib.request,struct,zlib,binascii,concurrent.futures,argparse
p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args()
url='https://aelrynth.com/downloads/rynth/releases/2026102103/RynthNav-NavData.zip';size=1572337084
regions=[(0x13,0x16,0xb4,0xb7),(0x1a,0x1d,0xc0,0xc4),(0x20,0x23,0xb5,0xba)]
def get(lo,hi):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'Range':f'bytes={lo}-{hi}'}),timeout=45)as r:
  assert r.status==206;return r.read()
tail=get(size-65536,size-1);i=tail.rfind(b'PK\x05\x06');e=struct.unpack_from('<4s4H2IH',tail,i);cdsize,cdpos=e[5:7]
if cdpos==0xffffffff:
 i=tail.rfind(b'PK\x06\x07');off=struct.unpack_from('<4sIQI',tail,i)[2];cdsize,cdpos=struct.unpack_from('<QQ',get(off,off+55),40)
cd=get(cdpos,cdpos+cdsize-1);i=0;items=[]
while i<len(cd):
 v=struct.unpack_from('<4s6H3I5H2I',cd,i);assert v[0]==b'PK\x01\x02';nl,el,cl=v[10:13];name=cd[i+46:i+46+nl].decode();i+=46+nl+el+cl
 if not name.endswith('.tile'):continue
 try:lb=int(Path(name).stem.split('_')[-1],16);lb=lb>>16 if lb>65535 else lb
 except ValueError:continue
 if any(x0<=lb>>8<=x1 and y0<=lb&255<=y1 for x0,x1,y0,y1 in regions):items.append((name,v[4],v[7],v[8],v[9],v[16]))
a.out.mkdir(parents=True,exist_ok=True)
def fetch(item):
 name,method,crc,csize,usize,off=item;dst=a.out/Path(name).name
 if dst.exists():
  raw=dst.read_bytes()
  if len(raw)==usize and binascii.crc32(raw)&0xffffffff==crc:return
 h=get(off,off+29);nl,el=struct.unpack_from('<HH',h,26);b=get(off+30+nl+el,off+30+nl+el+csize-1);raw=zlib.decompress(b,-15)if method==8 else b
 assert len(raw)==usize and binascii.crc32(raw)&0xffffffff==crc;dst.write_bytes(raw)
with concurrent.futures.ThreadPoolExecutor(6)as pool:list(pool.map(fetch,items))
print('Fetched/CRC verified',len(items),'camp-region tiles. Full archive hash was not verified.')
