"""Insert three small save-identity hooks; preserve the existing serializers."""
from pathlib import Path
import sys,struct,zlib
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from inspect_swf import Reader,ABC,swf
from avm2_listing import blocks,insns,class_methods,BRANCH

def u30(n):
 out=bytearray()
 while n>127:out.append((n&127)|128);n>>=7
 out.append(n);return bytes(out)

def instrument(data,cls):
 abc=ABC(data).init();r=Reader(data);r.take(4)
 for _ in range(2):
  for _ in range(r.var()-1):r.var()
 r.take(max(0,r.var()-1)*8)
 str_at=r.pos;str_count=r.var();str_end=r.pos
 for _ in range(str_count-1):r.take(r.var())
 str_append=r.pos
 for _ in range(r.var()-1):r.u8();r.var()
 for _ in range(r.var()-1):
  for _ in range(r.var()):r.var()
 mn_at=r.pos;mn_count=r.var();mn_end=r.pos
 for _ in range(mn_count-1):ABC.multiname(r)
 mn_append=r.pos
 names=['SelectPLace','attachSaveIdentity','useSaveIdentity'] if cls=='MemoryClass' else ['SelectPLace','beginNewSave']
 assert not any(x in abc.strings for x in names[1:]),'Identity hooks already present'
 export_ns=next(i for i,(k,s) in enumerate(abc.namespaces) if k==0x16 and abc.strings[s]=='export')
 public=next(i for i,(k,s) in enumerate(abc.namespaces) if k==0x16 and abc.strings[s]=='')
 strings=b''.join(u30(len(n.encode()))+n.encode() for n in names)
 mn=b''.join(b'\x07'+u30(export_ns if i==0 else public)+u30(str_count+i) for i in range(len(names)))
 edits=[(str_at,str_end,u30(str_count+len(names))),(str_append,str_append,strings),(mn_at,mn_end,u30(mn_count+len(names))),(mn_append,mn_append,mn)]
 changed={}
 methods=dict(class_methods(abc,cls))
 for name in (['setStorage','storageValue'] if cls=='MemoryClass' else ['newGameClick']):
  index=methods[name];code=abc.bodies[index];rows=list(insns(abc,index))
  assert all(not op.startswith('op') or opcode in (0xaf,0x87) for _,_,opcode,op,_ in rows)
  assert not any(op=='lookupswitch' for _,_,_,op,_ in rows)
  if name=='setStorage':
   found=[x for x in rows if x[3]=='setproperty' and x[4].endswith('::mystorage')];assert len(found)==1
   at=found[0][1];private=Reader(code[found[0][0]+1:]).var()
   hook=b'\x60'+u30(mn_count)+b'\xd0\x66'+u30(private)+b'\x4f'+u30(mn_count+1)+u30(1)
  elif name=='storageValue':
   at=next(x[0] for x in rows if x[3]=='returnvoid');hook=b'\x60'+u30(mn_count)+b'\xd2\x4f'+u30(mn_count+2)+u30(1)
  else:
   found=[x for x in rows if x[3] in ('callproperty','callpropvoid') and x[4]=='newGameClick (0)'];assert len(found)==1
   at=found[0][1];hook=b'\x60'+u30(mn_count)+b'\x4f'+u30(mn_count+1)+u30(0)
  delta=len(hook);patched=bytearray(code[:at]+hook+code[at:])
  def move(pos):return pos+(delta if pos>=at else 0)
  for start,end,opcode,op,args in rows:
   if opcode in BRANCH:
    target=end+int.from_bytes(code[start+1:end],'little',signed=True)
    assert target!=at,'Insertion lies on a branch target'
    rel=move(target)-move(end)
    patched[move(start)+1:move(end)]=rel.to_bytes(3,'little',signed=True)
  begin,end=abc.body_ranges[index];encoded=u30(len(code));assert data[begin-len(encoded):begin]==encoded
  assert Reader(data[end:]).var()==0,'Unexpected exception table in hook target'
  edits.append((begin-len(encoded),end,u30(len(patched))+patched));changed[index]=(code,bytes(patched))
 result=data
 for start,end,replacement in sorted(edits,reverse=True):result=result[:start]+replacement+result[end:]
 checked=ABC(result).init()
 assert len(abc.bodies)==len(checked.bodies)
 assert all(checked.bodies[i]==v for i,v in abc.bodies.items() if i not in changed)
 return result

def patch(source,output):
 source=Path(source);output=Path(output);replacements={}
 for name,prefix,abc in blocks(source):
  cls=name.rsplit('/',1)[-1]
  if cls in ('MemoryClass','GameMenu'):replacements[prefix+abc.data]=prefix+instrument(abc.data,cls)
 assert len(replacements)==2
 original=source.read_bytes();raw=zlib.decompress(original[8:]) if original[:3]==b'CWS' else original[8:]
 end=(5+(raw[0]>>3)*4+7)//8+4;body=bytearray(raw[:end])
 for tag,payload in swf(source)[-1]:
  if tag==82 and payload in replacements:payload=replacements[payload]
  length=len(payload);body.extend(struct.pack('<H',(tag<<6)|min(length,63)))
  if length>=63:body.extend(struct.pack('<I',length))
  body.extend(payload)
 output.write_bytes(b'CWS'+original[3:4]+struct.pack('<I',len(body)+8)+zlib.compress(body))
 print('Inserted save creation, load and serialization hooks; all other method bodies preserved.')
if __name__=='__main__':patch(sys.argv[1],sys.argv[2])
