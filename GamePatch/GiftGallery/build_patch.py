from pathlib import Path
import argparse,hashlib,json,subprocess,sys,zlib
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE));sys.path.insert(0,str(HERE.parent))
from bridge_patch import bridge
from avm2_listing import blocks
from inspect_swf import swf
from identity_patch import patch as patch_identity
p=argparse.ArgumentParser();p.add_argument('--input',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--ffdec',type=Path,required=True);p.add_argument('--java',default='java');a=p.parse_args()
meta=json.loads((HERE/'patch.json').read_text(encoding='utf-8'))
assert a.input.resolve()!=a.output.resolve()
assert hashlib.sha256(a.input.read_bytes()).hexdigest()==meta['inputSha256']
temporary=a.output.with_suffix('.ui.swf')
subprocess.run([a.java,'-XX:+UseSerialGC','-XX:ReservedCodeCacheSize=32m','-Xss512k','-Xms32m','-Xmx320m','-jar',str(a.ffdec),'-replace',str(a.input),str(temporary),'export.SelectPLace',str(HERE/'SelectPLace.as')],check=True)
if meta.get('aliasesInInput'):
 raw=temporary.read_bytes();a.output.write_bytes(b'CWS'+raw[3:8]+zlib.compress(raw[8:]) if raw[:3]==b'FWS' else raw)
else:
 bridge(temporary,a.output)
 for cls in ('GameBoxActivity','YaoYaoLeActivity3'):bridge(a.output,a.output,cls,'getGameBoxPresent')
 for entry in json.loads((HERE/'native-aliases.json').read_text(encoding='utf-8')):
  if entry['className'] in ('GameBoxActivity','YaoYaoLeActivity3') and entry['method']=='getGameBoxPresent':continue
  bridge(a.output,a.output,entry['className'],entry['method'])
if meta.get('identityHooks') and not meta.get('identityInInput'):patch_identity(a.output,a.output)
old={n:c for n,_,c in blocks(a.input)};new={n:c for n,_,c in blocks(a.output)}
changed={n for n in old if old[n].data!=new[n].data}
assert changed==set(meta['changedClasses'])
for name in changed:
 if name=='export/SelectPLace':continue
 from avm2_listing import class_methods
 allowed=meta.get('identityMethods',{}).get(name,[])
 actual=[label for label,index in class_methods(old[name],name.rsplit('/',1)[-1]) if old[name].bodies[index]!=new[name].bodies[index]]
 assert sorted(actual)==sorted(allowed),(name,actual)

assert len(swf(a.input)[-1])==len(swf(a.output)[-1])==1308
assert hashlib.sha256(a.output.read_bytes()).hexdigest()==meta['outputSha256']
temporary.unlink()
print('PASS: release matches, all original reward method bytecode unchanged.')
