from pathlib import Path
import sys,struct,zlib,json,hashlib
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
from inspect_swf import ABC,Reader,swf
from avm2_listing import blocks,class_methods,insns,BRANCH
def u30(n):
 b=bytearray()
 while n>127:b.append((n&127)|128);n>>=7
 b.append(n);return bytes(b)
def patch(source,output):
 source=Path(source);output=Path(output);n,prefix,a=next(x for x in blocks(source) if x[0]=='export/MapMenu')
 methods=dict(class_methods(a,'MapMenu'));r=Reader(a.data);r.take(4)
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
 public=next(i for i,(k,s) in enumerate(a.namespaces) if k==0x16 and a.strings[s]=='')
 names=['mouseEnabled','mouseChildren','stop'];ids={x:mc+i for i,x in enumerate(names)}
 rz=next(i for i in range(1,len(a.multinames)) if a.name(i)=='rzmc')
 visible=next(i for i in range(1,len(a.multinames)) if a.name(i)=='visible')
 edits=[(sa,se,u30(sc+3)),(sappend,sappend,b''.join(u30(len(n))+n.encode() for n in names)),(ma,me,u30(mc+3)),(mappend,mappend,b''.join(b'\x07'+u30(public)+u30(sc+i) for i in range(3)))]
 changed={}
 for method in ['added','removed','rzClick']:
  index=methods[method];old=a.bodies[index];rows=list(insns(a,index));patched=bytearray(old)
  if method in ['added','removed']:
   positions=[i for i,x in enumerate(rows) if x[3]=='getlex' and x[4]=='rzmc'];assert positions
   if method=='added':
    start=rows[positions[0]][0];finish=next(x[0] for x in rows if x[0]>start and x[3]=='getlex' and x[4].endswith('::gc') and x[0]>rows[positions[-1]][0])
    for s,e,op,label,arg in rows:
     if op in BRANCH:assert not start<int(arg)<finish
    hook=b''.join(b'\x60'+u30(rz)+b'\x27\x61'+u30(prop) for prop in [visible,ids['mouseEnabled'],ids['mouseChildren']])+b'\x60'+u30(rz)+b'\x4f'+u30(ids['stop'])+b'\x00'
    assert len(hook)<=finish-start;patched[start:finish]=hook+b'\x02'*(finish-start-len(hook))
   else:
    assert len(positions)==1;start=rows[positions[0]][0]
    finish=next(x[1] for x in rows if x[0]>start and x[3] in ['callproperty','callpropvoid'] and x[4]=='removeEventListener (2)')
    if rows[next(i for i,x in enumerate(rows) if x[1]==finish)+1][3]=='pop':finish+=1
    patched[start:finish]=b'\x02'*(finish-start)
  else:patched=bytearray(b'\xd0\x30\x47')
  start,end=a.body_ranges[index];encoded=u30(len(old));assert a.data[start-len(encoded):start]==encoded and Reader(a.data[end:]).var()==0
  edits.append((start-len(encoded),end,u30(len(patched))+patched));changed[index]=bytes(patched)
 data=a.data
 for start,end,value in sorted(edits,reverse=True):data=data[:start]+value+data[end:]
 b=ABC(data).init();assert all(b.bodies[i]==body for i,body in a.bodies.items() if i not in changed)
 for method in ['added','removed','rzClick']:
  assert not any('Auth4399MainView' in row[4] or 'hasGetAuth4399Present' in row[4] or 'rzClick' in row[4] for row in insns(b,methods[method]))
 raw=source.read_bytes();body=zlib.decompress(raw[8:]) if raw[:3]==b'CWS' else raw[8:];offset=(5+(body[0]>>3)*4+7)//8+4;result=bytearray(body[:offset])
 for tag,payload in swf(source)[-1]:
  if tag==82 and payload==prefix+a.data:payload=prefix+data
  length=len(payload);result.extend(struct.pack('<H',(tag<<6)|min(length,63)))
  if length>=63:result.extend(struct.pack('<I',length))
  result.extend(payload)
 output.write_bytes(b'CWS'+raw[3:4]+struct.pack('<I',len(result)+8)+zlib.compress(result))
 old={n:c for n,_,c in blocks(source)};new={n:c for n,_,c in blocks(output)}
 assert [n for n in old if old[n].data!=new[n].data]==['export/MapMenu']
 print(json.dumps({'methods':['added','removed','rzClick'],'otherMethodBodiesPreserved':True,'sha256':hashlib.sha256(output.read_bytes()).hexdigest()}))
if __name__=='__main__':patch(sys.argv[1],sys.argv[2])
