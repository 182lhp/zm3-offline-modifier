"""Append stage-bound clamping to the two original tooltip placement methods."""
from pathlib import Path
import sys,struct,zlib,json,hashlib
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
from inspect_swf import ABC,Reader,swf
from avm2_listing import blocks,class_methods

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
 append_s=r.pos
 for _ in range(r.var()-1):r.u8();r.var()
 for _ in range(r.var()-1):
  for _ in range(r.var()):r.var()
 ma=r.pos;mc=r.var();me=r.pos
 for _ in range(mc-1):ABC.multiname(r)
 append_m=r.pos
 public=next(i for i,(kind,s) in enumerate(a.namespaces) if kind==0x16 and a.strings[s]=='')
 names=['Math','min','max','stage','getBounds','width','height','x','y','scaleX','scaleY','parent','localToGlobal','globalToLocal']
 assert 'globalToLocal' not in a.strings,'Tooltip patch is already present'
 ids={name:mc+i for i,name in enumerate(names)}
 tip=next(i for i in range(1,len(a.multinames)) if a.name(i).endswith('::mianban'))
 point=next(i for i in range(1,len(a.multinames)) if a.name(i)=='flash.geom::Point')
 method=dict(class_methods(a,cls))['showattribute'];code=a.bodies[method]
 begin,end=a.body_ranges[method];encoded=u30(len(code));header=begin-len(encoded)-5
 old=list(data[header:header+5]);assert old[0]==method and all(x<128 for x in old)
 assert data[header+5:begin]==encoded and code[-1:]==b'\x47' and Reader(data[end:]).var()==0
 rect,origin,factor=old[2],old[2]+1,old[2]+2
 out=bytearray()
 def emit(op,arg=None):out.append(op);out.extend(u30(arg) if arg is not None else b'')
 def load(i):emit(0x62,i)
 def store(i):emit(0x63,i)
 def obj():emit(0x60,tip)
 def get(n):emit(0x66,ids[n])
 def setp(n):emit(0x61,ids[n])
 def math():emit(0x60,ids['Math'])
 def call(n,nargs):emit(0x46,ids[n]);out.extend(u30(nargs))
 def num(n):emit(0x25,n)
 def bounds():obj();obj();get('stage');call('getBounds',1);store(rect)
 bounds()
 math();num(1);num(932);load(rect);get('width');emit(0xa3);num(582);load(rect);get('height');emit(0xa3);call('min',3);store(factor)
 for n in ['scaleX','scaleY']:
  obj();obj();get(n);load(factor);emit(0xa2);setp(n)
 bounds()
 obj();emit(0x5d,point);num(0);num(0);emit(0x4a,point);out.extend(u30(2));call('localToGlobal',1);store(origin)
 for axis,size,edge in [('x','width',936),('y','height',586)]:
  load(origin);load(origin);get(axis)
  math();num(4);math();load(rect);get(axis);num(edge);load(rect);get(size);emit(0xa1);call('min',2);call('max',2)
  load(rect);get(axis);emit(0xa1);emit(0xa0);setp(axis)
 obj();get('parent');load(origin);call('globalToLocal',1);store(origin)
 for axis in ['x','y']:obj();load(origin);get(axis);setp(axis)
 patched=code[:-1]+out+b'\x47'
 edits=[(sa,se,u30(sc+len(names))),(append_s,append_s,b''.join(u30(len(n))+n.encode() for n in names)),
        (ma,me,u30(mc+len(names))),(append_m,append_m,b''.join(b'\x07'+u30(public)+u30(sc+i) for i in range(len(names)))),
        (header,end,u30(method)+u30(max(old[1],10))+u30(old[2]+3)+u30(old[3])+u30(old[4])+u30(len(patched))+patched)]
 result=data
 for start,end,replacement in sorted(edits,reverse=True):result=result[:start]+replacement+result[end:]
 b=ABC(result).init()
 assert all(b.bodies[i]==v for i,v in a.bodies.items() if i!=method)
 assert b.bodies[method].startswith(code[:-1])
 return result

def patch(source,target):
 source=Path(source);target=Path(target);changes={}
 for name,prefix,a in blocks(source):
  if name in ['export/pack/ShowObj','export/pet/PetShowObj']:changes[prefix+a.data]=prefix+instrument(a.data,name.split('/')[-1])
 assert len(changes)==2
 original=source.read_bytes();raw=zlib.decompress(original[8:]) if original[:3]==b'CWS' else original[8:]
 offset=(5+(raw[0]>>3)*4+7)//8+4;body=bytearray(raw[:offset])
 for tag,payload in swf(source)[-1]:
  if tag==82 and payload in changes:payload=changes[payload]
  length=len(payload);body.extend(struct.pack('<H',(tag<<6)|min(length,63)))
  if length>=63:body.extend(struct.pack('<I',length))
  body.extend(payload)
 target.write_bytes(b'CWS'+original[3:4]+struct.pack('<I',len(body)+8)+zlib.compress(body))
 a={n:c for n,_,c in blocks(source)};b={n:c for n,_,c in blocks(target)}
 changed=[n for n in a if a[n].data!=b[n].data]
 assert set(changed)=={'export/pack/ShowObj','export/pet/PetShowObj'}
 assert len(swf(source)[-1])==len(swf(target)[-1])
 print(json.dumps({'changedClasses':changed,'allOtherMethodBodiesPreserved':True,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()}))
if __name__=='__main__':patch(sys.argv[1],sys.argv[2])
