"""Rebuild two ActionScript classes against the recorded input game SWF.

python build_patch.py --input old.swf --output patched.swf --ffdec ffdec.jar
Requires Java 17 and JPEXS FFDec 26.4.0. Never installs or changes save data.
"""
from pathlib import Path
import argparse, hashlib, json, subprocess, sys, zlib

HERE = Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent))
from inspect_swf import swf
from avm2_listing import blocks

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--input',required=True,type=Path)
parser.add_argument('--output',required=True,type=Path)
parser.add_argument('--ffdec',required=True,type=Path)
parser.add_argument('--java',default='java')
args = parser.parse_args()
assert args.input.resolve() != args.output.resolve(), 'Use a separate output file'
plan = json.loads((HERE/'patch.json').read_text(encoding='utf-8'))
assert hashlib.sha256(args.input.read_bytes()).hexdigest() == plan['inputSha256'], 'Unexpected input version'
command = [args.java,'-Xmx512m','-jar',str(args.ffdec),'-replace',str(args.input),str(args.output)]
for name in ['MapMenu','SelectPLace']:
    command.extend(['export.'+name,str(HERE/(name+'.as'))])
subprocess.run(command,check=True)
raw = args.output.read_bytes()
if raw[:3] == b'FWS':
    raw = b'CWS'+raw[3:8]+zlib.compress(raw[8:])
args.output.write_bytes(raw)
old,new = swf(args.input)[-1],swf(args.output)[-1]
assert len(old) == len(new) == 1308
changed = [i for i,(a,b) in enumerate(zip(old,new)) if a != b]
assert changed == [76,278],changed
old_abcs = {n:c.data for n,_,c in blocks(args.input)}
new_abcs = {n:c.data for n,_,c in blocks(args.output)}
assert {n for n in old_abcs if old_abcs[n] != new_abcs[n]} == set(plan['changedClasses'])
assert hashlib.sha256(raw).hexdigest() == plan['outputSha256'], 'Build differs; inspect before installing'
print('PASS: only SelectPLace and MapMenu changed; all other SWF tags unchanged.')
