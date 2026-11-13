from pathlib import Path
import sys,struct,zlib
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
from inspect_swf import Reader,ABC,swf
from avm2_listing import blocks
def u30(n):
 out=bytearray()
 while n>127:out.append((n&127)|128);n>>=7
 out.append(n);return bytes(out)
def bridge(input,output,classname='MiaoMiaoBaseInterface',method='getPresent'):
 name,prefix,abc=next(x for x in blocks(input) if x[0].endswith('/'+classname))
 r=Reader(abc.data);r.take(4)
 for _ in range(2):
  for _ in range(r.var()-1):r.var()
 r.take(max(0,r.var()-1)*8)
 for _ in range(r.var()-1):r.take(r.var())
 for _ in range(r.var()-1):r.u8();r.var()
 for _ in range(r.var()-1):
  for _ in range(r.var()):r.var()
 count_at=r.pos;count=r.var();count_end=r.pos
 for _ in range(count-1):ABC.multiname(r)
 pool_end=r.pos
 public=next(i for i,(kind,s) in enumerate(abc.namespaces) if kind==0x16 and abc.strings[s]=='')
 original=next(i for i in range(1,len(abc.multinames)) if abc.name(i).split('::')[-1]==method)
 method_name=abc.multinames[original][2]
 for _ in range(r.var()):
  nargs=r.var();r.var();[r.var() for _ in range(nargs)];r.var();flags=r.u8()
  if flags&8:
   for _ in range(r.var()):r.var();r.u8()
  if flags&128:[r.var() for _ in range(nargs)]
 for _ in range(r.var()):r.var();[r.var() for _ in range(r.var()*2)]
 found=None
 for _ in range(r.var()):
  cls=r.var();r.var();flags=r.u8()
  if flags&8:r.var()
  [r.var() for _ in range(r.var())];r.var()
  trait_at=r.pos;traits=r.var();trait_count_end=r.pos
  target=None
  for _ in range(traits):
   nm=r.var();fl=r.u8();kind=fl&15;r.var();value=r.var()
   if kind in (0,6) and r.var():r.u8()
   if fl&64:[r.var() for _ in range(r.var())]
   if nm==original and kind==1:target=value
  if abc.name(cls).endswith(classname):
   assert target is not None
   found=(trait_at,trait_count_end,traits,r.pos,target)
 assert found
 start,end,n,append,target=found
 patches=[(count_at,count_end,u30(count+1)),(pool_end,pool_end,b'\x07'+u30(public)+u30(method_name)),(start,end,u30(n+1)),(append,append,u30(count)+b'\x01'+u30(0)+u30(target))]
 data=abc.data
 for start,end,replacement in sorted(patches,reverse=True):data=data[:start]+replacement+data[end:]
 checked=ABC(data).init();assert checked.bodies==abc.bodies
 source=input.read_bytes();raw=zlib.decompress(source[8:]) if source[:3]==b'CWS' else source[8:]
 body=bytearray();header_bits=raw[0]>>3;offset=(5+header_bits*4+7)//8+4;body.extend(raw[:offset])
 for tag,payload in swf(input)[-1]:
  if tag==82 and payload==prefix+abc.data:payload=prefix+data
  length=len(payload);body.extend(struct.pack('<H',(tag<<6)|min(length,63)))
  if length>=63:body.extend(struct.pack('<I',length))
  body.extend(payload)
 output.write_bytes(b'CWS'+source[3:4]+struct.pack('<I',len(body)+8)+zlib.compress(body))
 print('Added public alias; all original reward method bytecode preserved.')
if __name__=='__main__':
 source=Path(sys.argv[1]);output=Path(sys.argv[2])
 bridge(source,output)
 for cls in ('GameBoxActivity','YaoYaoLeActivity3'):bridge(output,output,cls,'getGameBoxPresent')
