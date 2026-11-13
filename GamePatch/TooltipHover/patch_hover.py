"""Make the two display-only tooltip containers passive before they are parented."""
from pathlib import Path
import sys,zlib,struct,json,hashlib
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
from inspect_swf import Reader,ABC,swf
from avm2_listing import blocks,class_methods,insns
def u30(n):
 out=bytearray()
 while n>127:out.append((n&127)|128);n>>=7
 out.append(n);return bytes(out)
def instrument(data,cls):
 a=ABC(data).init();r=Reader(data);r.take(4)
 for _ in range(2):
  for _ in range(r.var()-1):r.var()
 r.take(max(0,r.var()-1)*8)
 sa=r.pos;sc=r.var();se=r.pos
 for _ in range(sc-1):r.take(r.var())
 sappend=r.pos
 for _ in range(r.var()-1):r.u8();r.var()
 for _ in range(r.var()-1):
  for _ in range(r.var()):r.var()
 ma=r.pos;mc=r.var();me=r.pos
 for _ in range(mc-1):ABC.multiname(r)
 mappend=r.pos
 public=next(i for i,(kind,s) in enumerate(a.namespaces) if kind==0x16 and a.strings[s]=='')
 names=['mouseEnabled','mouseChildren'];method=dict(class_methods(a,cls))['constructor'];old=a.bodies[method]
 assert not any(row[3]=='setproperty' and row[4] in names for row in insns(a,method)),'Passive tooltip patch is already present'
 assert old[-1:]==b'\x47'
 hook=b''.join(b'\xd0\x27\x61'+u30(mc+i) for i in range(2));new=old[:-1]+hook+b'\x47'
 start,end=a.body_ranges[method];encoded=u30(len(old));assert a.data[start-len(encoded):start]==encoded and Reader(a.data[end:]).var()==0
 edits=[(sa,se,u30(sc+2)),(sappend,sappend,b''.join(u30(len(n))+n.encode() for n in names)),(ma,me,u30(mc+2)),(mappend,mappend,b''.join(b'\x07'+u30(public)+u30(sc+i) for i in range(2))),(start-len(encoded),end,u30(len(new))+new)]
 result=data
 for start,end,replacement in sorted(edits,reverse=True):result=result[:start]+replacement+result[end:]
 b=ABC(result).init();assert all(b.bodies[i]==body for i,body in a.bodies.items() if i!=method)
 assert b.bodies[method].startswith(old[:-1])
 return result
def patch(source,target):
 source=Path(source);target=Path(target);changes={}
 for name,prefix,a in blocks(source):
  if name in ['export/pack/AttributeCon','export/pet/PetEquipAttribute']:changes[prefix+a.data]=prefix+instrument(a.data,name.split('/')[-1])
 assert len(changes)==2
 original=source.read_bytes();raw=zlib.decompress(original[8:]) if original[:3]==b'CWS' else original[8:];offset=(5+(raw[0]>>3)*4+7)//8+4;result=bytearray(raw[:offset])
 for tag,payload in swf(source)[-1]:
  if tag==82 and payload in changes:payload=changes[payload]
  n=len(payload);result.extend(struct.pack('<H',(tag<<6)|min(n,63)))
  if n>=63:result.extend(struct.pack('<I',n))
  result.extend(payload)
 target.write_bytes(b'CWS'+original[3:4]+struct.pack('<I',len(result)+8)+zlib.compress(result))
 old={n:a for n,_,a in blocks(source)};new={n:a for n,_,a in blocks(target)}
 changed=[n for n in old if old[n].data!=new[n].data]
 assert set(changed)=={'export/pack/AttributeCon','export/pet/PetEquipAttribute'}
 assert len(swf(source)[-1])==len(swf(target)[-1])==1308
 print(json.dumps({'changedClasses':changed,'onlyConstructorsChanged':True,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()}))
if __name__=='__main__':patch(sys.argv[1],sys.argv[2])
