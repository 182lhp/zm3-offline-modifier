from support import SOURCE,seed_rules
from pathlib import Path
import os,sys,zlib,subprocess,threading,time,json,functools,urllib.request,shutil,hashlib
from resource_versions import build_id,launch_command
from http.server import ThreadingHTTPServer,SimpleHTTPRequestHandler
H=Path(os.environ['ZM3_BUILD_OUTPUT']);W=SOURCE;F=H/'service-fixture';R=Path(os.environ['ZM3_GAME_ROOT'])
shutil.copytree(R/'game',F/'game',dirs_exist_ok=True);shutil.copytree(R/'icons',F/'icons',dirs_exist_ok=True);shutil.copytree(R/'schema',F/'schema',dirs_exist_ok=True)
for name in ['slot0.json','vip.json']:shutil.copy2(R/'saves'/name,F/'saves'/name)
for name in ['local_gifts.py','storage_tx.py','runtime_state.py','runtime_integration.py','game_paths.py','resource_versions.py','game_rules.py','rules_stamp.py']:shutil.copy2(H/name,F/'tools'/name)
from game_paths import gift_state,gift_catalog
shutil.copy2(gift_state(str(R)),F/'tools/local_gifts.json');shutil.copy2(gift_catalog(str(R)),F/'tools/local_gift_catalog.json')
(F/'saves/gifts/local_gifts.json').write_bytes((F/'tools/local_gifts.json').read_bytes());(F/'tools/local_gifts.json').unlink()
(F/'schema/local_gift_catalog.json').write_bytes((F/'tools/local_gift_catalog.json').read_bytes());(F/'tools/local_gift_catalog.json').unlink()
(F/'schema/layout.json').write_text('{"layoutVersion":2}');seed_rules(F)
shutil.copy2(R/'tools/game_log.html',F/'tools/game_log.html')
def ports(source,target):
 raw=source.read_bytes();body=zlib.decompress(raw[8:]) if raw[:3]==b'CWS' else raw[8:]
 target.write_bytes(b'CWS'+raw[3:8]+zlib.compress(body.replace(b'43999',b'44049')))
ports(H/'gift-identity.swf',F/'game/zmxy3_game.swf');ports(R/'game/launcher.swf',F/'game/launcher.swf')
entries=[]
for directory in ['game','icons']:
 for file in (F/directory).rglob('*'):
  if file.is_file():entries.append(dict(path=file.relative_to(F).as_posix(),size=file.stat().st_size,sha256=hashlib.sha256(file.read_bytes()).hexdigest()))
(F/'schema/resource_manifest.json').write_text(json.dumps(dict(format=1,version='2.33.0-test',buildId=build_id('2.33.0-test',entries),files=entries)),encoding='utf-8')
(H/'game-claim.json').unlink(missing_ok=True)
class Handler(SimpleHTTPRequestHandler):
 def do_GET(self):
  if self.path=='/crossdomain.xml':body=b'<cross-domain-policy><allow-access-from domain="*"/></cross-domain-policy>'
  elif self.path=='/fixture/saves/slot0.json':body=(F/'saves/slot0.json').read_bytes()
  else:self.send_error(404);return
  self.send_response(200);self.end_headers();self.wfile.write(body)
 def do_POST(self):
  value=json.loads(self.rfile.read(int(self.headers['Content-Length'])));(H/'game-claim.json').write_text(json.dumps(value,ensure_ascii=False),encoding='utf-8');self.send_response(200);self.end_headers();self.wfile.write(b'{}')
 def log_message(self,*a):pass
http=ThreadingHTTPServer(('127.0.0.1',44029),Handler);threading.Thread(target=http.serve_forever,daemon=True).start()
env=dict(os.environ);env.update(NO_PROXY='*',no_proxy='*',HTTP_PROXY='',HTTPS_PROXY='',ALL_PROXY='',RUST_LOG='warn,avm_trace=info')
startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
op=urllib.request.build_opener(urllib.request.ProxyHandler({}))
with (H/'game-backend.log').open('wb') as out,(H/'game-runtime.log').open('wb') as runtime:
 backend=subprocess.Popen([str(F/'tools/zm3server.exe')],cwd=F,env=env,stdout=out,stderr=out,startupinfo=startup);game=None
 try:
  for n in range(30):
   try:status=json.load(op.open('http://127.0.0.1:44049/runtime/status',timeout=2));break
   except OSError:time.sleep(.2)
  else:raise AssertionError('Frozen service did not start')
  game=subprocess.Popen(launch_command(str(F),[str(F/'runtime/ruffle.exe'),'--no-gui','--width','940','--height','590','--save-directory',str(F/'rundata/game/SharedObjects'),'--config',str(F/'rundata/game/config'),'--cache-directory',str(F/'rundata/game/cache'),'http://127.0.0.1:44049/launcher.swf']),cwd=F,env=env,stdout=runtime,stderr=runtime,startupinfo=startup)
  for n in range(95):
   if (H/'game-claim.json').exists():break
   if game.poll() is not None:raise AssertionError('Isolated game exited early')
   time.sleep(.5)
  assert (H/'game-claim.json').exists(),'Game claim did not complete'
  result=json.loads((H/'game-claim.json').read_text(encoding='utf-8'));assert result['ok'] and result['panelOpen'] and not result['busy'] and not result['uncertain'],{k:v for k,v in result.items() if k!='save'}
  state=json.loads((F/'saves/gifts/local_gifts.json').read_text(encoding='utf-8-sig'));identity=result['evidence']['newId'];claims=[r for r in state['claims'] if r['code']=='ZM3-566' and r['saveId']==identity];assert len(claims)==1
  req=urllib.request.Request('http://127.0.0.1:44049/local-gifts/redeem',data=json.dumps(dict(token=claims[0]['request'],save={})).encode(),headers={'Content-Type':'application/json'})
  assert json.load(op.open(req))['alreadyCompleted']
  report=dict(status='PASS',frozenBackendWithRealGame=True,originalPetGift=True,identityPreserved=True,newGameSave=True,receiptAndPetSavedTogether=True,retryNoDuplicate=True,receiptKeptOpen=True,versionedRealGame=True,migratedLayout=True)
  (H/'game-qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report),flush=True)
 finally:
  if game and game.poll() is None:game.terminate();game.wait(timeout=8)
  if backend.poll() is None:subprocess.run(['taskkill','/PID',str(backend.pid),'/T','/F'],startupinfo=startup,capture_output=True)
  backend.wait(timeout=8);http.shutdown()
