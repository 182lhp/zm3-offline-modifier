from support import SOURCE,seed_rules
from pathlib import Path
import os
import shutil,subprocess,json,hashlib
from resource_versions import build_id
import datetime
H=Path(os.environ['ZM3_BUILD_OUTPUT']);R=Path(os.environ['ZM3_GAME_ROOT']);F=H/'modifier-fixture';C=H/('captures-'+datetime.datetime.now().strftime('%H%M%S'))
(H/'latest-captures.txt').write_text(str(C),encoding='utf-8')
for folder in ['game','icons','schema','saves','资料','tools']:shutil.copytree(R/folder,F/folder,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.lock','*.transaction.json','__pycache__'))
shutil.copy2(R/'使用说明.md',F/'使用说明.md')
(F/'tools/local_gifts.json').unlink(missing_ok=True)
(F/'saves/gifts/local_gifts.json').unlink(missing_ok=True)
(F/'schema/layout.json').unlink(missing_ok=True)
for name in ['appearance.json','catalog-view.json','navigation.json']:(F/name).unlink(missing_ok=True)
(C/'qa.txt').unlink(missing_ok=True)
shutil.copy2(SOURCE/'Build/tests/fixtures/slot0.json',F/'saves/slot0.json');seed_rules(F)
RF=F/'resource-qa';(RF/'game').mkdir(parents=True,exist_ok=True);(RF/'schema').mkdir(exist_ok=True)
(RF/'game/probe.swf').write_bytes(b'known release resource')
rows=[dict(path='game/probe.swf',size=22,sha256=hashlib.sha256(b'known release resource').hexdigest())]
rows[0]['size']=len(b'known release resource')
(RF/'schema/resource_manifest.json').write_text(json.dumps(dict(format=1,version='2.33.0',buildId=build_id('2.33.0',rows),files=rows)),encoding='utf-8')
startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
result=subprocess.run([str(H/'qa/启动修改器.exe'),'--qa',str(F),str(C)],startupinfo=startup,capture_output=True,text=True,timeout=180)
print('QA exit',result.returncode)
if (C/'qa.txt').exists():print((C/'qa.txt').read_text(encoding='utf-8-sig'))
print(result.stdout,result.stderr);raise SystemExit(result.returncode)
