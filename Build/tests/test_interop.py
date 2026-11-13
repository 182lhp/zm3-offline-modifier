from support import SOURCE,seed_rules
from pathlib import Path
import os
import json,subprocess,time
import storage_tx as tx
H=Path(os.environ['ZM3_BUILD_OUTPUT']);C=Path((H/'latest-captures.txt').read_text(encoding='utf-8'));R=C/'interop-csharp'
storage=tx.StorageCoordinator(str(R))
with tx.StoreLock(storage.lockfile):storage.recover()
assert json.loads((R/'saves/slot0.json').read_bytes())['value']==100
assert json.loads((R/'saves/vip.json').read_bytes())['value']==200
F=H/'interop-python';(F/'saves').mkdir(parents=True,exist_ok=True);s=tx.StorageCoordinator(str(F))
for name,value in [('slot0',1),('vip',2)]:tx.atomic(str(F/'saves'/(name+'.json')),tx.encode({'value':value}))
rows=[]
for name,value in [('slot0',111),('vip',222)]:rows.append(dict(path='saves/'+name+'.json',before=(F/'saves'/(name+'.json')).read_bytes().hex(),after=tx.encode({'value':value,'未知字段':'保留'}).decode('utf-8')))
tx.atomic(s.journal,tx.encode(dict(version=2,id='python-interop-control',origin='game',files=rows)))
tx.atomic(str(F/'saves/slot0.json'),rows[0]['after'].encode('utf-8'))
report=H/'interop-csharp-result.txt';report.unlink(missing_ok=True);Path(str(report)+'.waiting').unlink(missing_ok=True)
startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
with tx.StoreLock(s.lockfile):
 process=subprocess.Popen([str(H/'qa/启动修改器.exe'),'--qa-recover',str(F),str(report)],startupinfo=startup)
 end=time.monotonic()+8
 while time.monotonic()<end and not Path(str(report)+'.waiting').exists():time.sleep(.05)
 assert Path(str(report)+'.waiting').exists(),'WPF recovery process failed to start'
 time.sleep(.2);assert not report.exists(),'C# did not respect Python cross-process lock'
assert process.wait(timeout=12)==0 and report.read_text(encoding='utf-8-sig')=='PASS'
assert json.loads((F/'saves/slot0.json').read_bytes())=={'value':111,'未知字段':'保留'}
assert json.loads((F/'saves/vip.json').read_bytes())=={'value':222,'未知字段':'保留'}
result=dict(status='PASS',csharpRecoversPython=True,pythonRecoversCsharp=True,sharedCrossProcessLock=True,unicodeAndUnknownFieldsPreserved=True)
(H/'interop-qa.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
