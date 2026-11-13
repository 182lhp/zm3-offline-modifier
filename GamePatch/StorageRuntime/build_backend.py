from pathlib import Path
import sys,zlib,struct,marshal,json,hashlib
H=Path(__file__).resolve().parent;R=Path(r'D:\Games\ZM3')
from archive_io import archive
MAGIC=b'MEI\x0c\x0b\x0a\x0b\x0e'
base,entries,version,library=archive((R/'tools/zm3server.exe').read_bytes())
launcher=archive((R/'启动游戏.exe').read_bytes())[1]
def raw(entry):return zlib.decompress(entry[2]) if entry[4] else entry[2]
def pyz(es):
    data=raw(next(e for e in es if e[0]=='PYZ-00.pyz'));return data,dict(marshal.loads(data[struct.unpack('!I',data[8:12])[0]:]))
data,table=pyz(entries);other,source=pyz(launcher)
added=[name for name in source if name=='ctypes' or name.startswith('ctypes.') if name not in table]
assert added==['ctypes','ctypes._endian'],added
payload=bytearray(data[:12]);toc=[]
for name,entry in list(table.items())+[(name,source[name]) for name in added]:
    origin=data if name in table else other;kind,start,size=entry;position=len(payload);payload.extend(origin[start:start+size]);toc.append((name,(kind,position,size)))
struct.pack_into('!I',payload,8,len(payload));payload.extend(marshal.dumps(toc,4))
new=[]
for entry in entries:
    if entry[0]=='PYZ-00.pyz':
        name,rawname,packed,plain,compressed,kind=entry;plain=len(payload);packed=zlib.compress(bytes(payload),9) if compressed else bytes(payload);entry=(name,rawname,packed,plain,compressed,kind)
    new.append(entry)
for name in ['_ctypes.pyd','libffi-7.dll']:
    assert not any(e[0]==name for e in entries)
    new.append(next(e for e in launcher if e[0]==name))
chunks=[];toc=[];cursor=0
for name,rawname,packed,plain,compressed,kind in new:
    chunks.append(packed);toc.append(struct.pack('!IIIIBc',18+len(rawname),cursor,len(packed),plain,compressed,kind)+rawname);cursor+=len(packed)
tablebytes=b''.join(toc);output=base+b''.join(chunks)+tablebytes+struct.pack('!8sIIII64s',MAGIC,cursor+len(tablebytes)+88,cursor,len(tablebytes),version,library)
(H/'zm3server.exe').write_bytes(output)
check=archive(output)
assert check[0]==base
assert len(check[1])==len(entries)+2
for before,after in zip(entries,check[1]):
    if before[0]!='PYZ-00.pyz':assert before==after
data2,table2=pyz(check[1])
for name,(kind,pos,size) in table.items():
    k,p,s=table2[name];assert kind==k and data[pos:pos+size]==data2[p:p+s]
report={'status':'PASS','existingModulesUnchanged':len(table),'addedModules':added,'addedNativeLibraries':['_ctypes.pyd','libffi-7.dll'],'originalServerWrapperUnchanged':True,'sha256':hashlib.sha256(output).hexdigest()}
(H/'backend-build.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
