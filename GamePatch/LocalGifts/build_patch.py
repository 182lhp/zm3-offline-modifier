from pathlib import Path
import argparse,hashlib,json,subprocess,sys,zlib
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE.parent))
from inspect_swf import swf
from avm2_listing import blocks
parser=argparse.ArgumentParser(description='Build the local redemption UI against its recorded pre-update game resource.')
parser.add_argument('--input',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--ffdec',type=Path,required=True);parser.add_argument('--java',default='java')
args=parser.parse_args();meta=json.loads((HERE/'patch.json').read_text(encoding='utf-8'))
assert args.input.resolve()!=args.output.resolve()
assert hashlib.sha256(args.input.read_bytes()).hexdigest()==meta['inputSha256']
subprocess.run([args.java,'-Xmx512m','-jar',str(args.ffdec),'-replace',str(args.input),str(args.output),'export.SelectPLace',str(HERE/'SelectPLace.as')],check=True)
raw=args.output.read_bytes()
if raw[:3]==b'FWS':raw=b'CWS'+raw[3:8]+zlib.compress(raw[8:])
args.output.write_bytes(raw)
old=swf(args.input)[-1];new=swf(args.output)[-1]
assert len(old)==len(new)==1308
assert [i for i,(a,b) in enumerate(zip(old,new)) if a!=b]==[76]
oldabc={n:c.data for n,_,c in blocks(args.input)};newabc={n:c.data for n,_,c in blocks(args.output)}
assert [n for n in oldabc if oldabc[n]!=newabc[n]]==meta['changedClasses']
assert hashlib.sha256(raw).hexdigest()==meta['outputSha256']
print('PASS: only SelectPLace changed; release hash matches.')
