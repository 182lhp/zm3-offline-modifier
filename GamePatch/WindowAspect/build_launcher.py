"""Repack only the starter member. Requires a full Python 3.8 executable."""
from pathlib import Path
import argparse,struct,zlib,tempfile,shutil,subprocess,json,hashlib
MAGIC=b'MEI\x0c\x0b\x0a\x0b\x0e'
def archive(data):
 cookie=data.rindex(MAGIC);magic,length,offset,size,version,library=struct.unpack_from('!8sIIII64s',data,cookie)
 start=cookie+88-length;at=start+offset;entries=[]
 while at<start+offset+size:
  length,pos,size,plain,compressed,kind=struct.unpack_from('!IIIIBc',data,at);name_raw=data[at+18:at+length];name=name_raw.split(b'\0')[0].decode()
  entries.append((name,name_raw,data[start+pos:start+pos+size],plain,compressed,kind));at+=length
 return data[:start],entries,version,library
def repack(base,entries,version,library,starter):
 chunks=[];toc=[];position=0
 for name,raw,packed,plain,compressed,kind in entries:
  if name=='starter':plain=len(starter);packed=zlib.compress(starter,9) if compressed else starter
  chunks.append(packed);toc.append(struct.pack('!IIIIBc',18+len(raw),position,len(packed),plain,compressed,kind)+raw);position+=len(packed)
 table=b''.join(toc)
 return base+b''.join(chunks)+table+struct.pack('!8sIIII64s',MAGIC,position+len(table)+88,position,len(table),version,library)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--input',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--python38',required=True);a=p.parse_args()
 original=a.input.read_bytes();base,entries,version,library=archive(original);e=next(e for e in entries if e[0]=='starter')
 with tempfile.TemporaryDirectory() as folder:
  folder=Path(folder);(folder/'starter.marshal').write_bytes(zlib.decompress(e[2]) if e[4] else e[2]);shutil.copy2(Path(__file__).with_name('build_launcher38.py'),folder/'build_launcher38.py')
  subprocess.run([a.python38,str(folder/'build_launcher38.py')],cwd=folder,check=True);result=repack(base,entries,version,library,(folder/'starter-patched.marshal').read_bytes())
 after=archive(result);assert after[0]==base and all(x==y for x,y in zip(entries,after[1]) if x[0]!='starter')
 a.output.write_bytes(result);print(json.dumps({'sha256':hashlib.sha256(result).hexdigest(),'otherMembersPreserved':True,'iconPreserved':True}))
