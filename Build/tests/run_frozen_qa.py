from support import SOURCE,seed_rules
from pathlib import Path
import shutil,sys,zlib,struct,subprocess,time,urllib.request,urllib.error,json,hashlib,os
H=Path(os.environ['ZM3_BUILD_OUTPUT']);R=Path(os.environ['ZM3_GAME_ROOT']);F=H/'service-fixture'
from archive_io import archive
base,entries,version,library=archive((H/'zm3server.exe').read_bytes());updated=(H/'server-test.marshal').read_bytes();chunks=[];toc=[];position=0
for name,rawname,packed,plain,compressed,kind in entries:
 if name=='server':plain=len(updated);packed=zlib.compress(updated,9) if compressed else updated
 chunks.append(packed);toc.append(struct.pack('!IIIIBc',18+len(rawname),position,len(packed),plain,compressed,kind)+rawname);position+=len(packed)
table=b''.join(toc);data=base+b''.join(chunks)+table+struct.pack('!8sIIII64s',b'MEI\x0c\x0b\x0a\x0b\x0e',position+len(table)+88,position,len(table),version,library)
for folder in ['tools','saves','game','runtime','logs']:(F/folder).mkdir(parents=True,exist_ok=True)
(F/'tools/zm3server.exe').write_bytes(data)
for name in ['local_gifts.py','storage_tx.py','runtime_state.py','runtime_integration.py','game_paths.py','resource_versions.py','game_rules.py','rules_stamp.py']:shutil.copy2(H/name,F/'tools'/name)
for name in ['slot0.json','slot1.json','slot2.json','vip.json']:
 if (R/'saves'/name).exists():shutil.copy2(R/'saves'/name,F/'saves'/name)
from game_paths import gift_state,gift_catalog
shutil.copy2(gift_state(str(R)),F/'tools/local_gifts.json');shutil.copy2(gift_catalog(str(R)),F/'tools/local_gift_catalog.json')
(F/'schema').mkdir(exist_ok=True);(F/'saves/gifts').mkdir(exist_ok=True)
(F/'saves/gifts/local_gifts.json').write_bytes((F/'tools/local_gifts.json').read_bytes());(F/'tools/local_gifts.json').unlink()
(F/'schema/local_gift_catalog.json').write_bytes((F/'tools/local_gift_catalog.json').read_bytes());(F/'tools/local_gift_catalog.json').unlink()
(F/'schema/layout.json').write_text('{"layoutVersion":2}');seed_rules(F)
shutil.copy2(R/'game/launcher.swf',F/'game/launcher.swf');shutil.copy2(R/'runtime/ruffle.exe',F/'runtime/ruffle.exe')
# A blank, valid SWF tests real process ownership/lifetime without loading the game.
bits='10000'+''.join(format(x,'016b') for x in [0,18800,0,11800]);bits+='0'*((-len(bits))%8)
body=int(bits,2).to_bytes(len(bits)//8,'big')+struct.pack('<HH',30*256,1)+struct.pack('<HBBB',9*64+3,30,40,50)+struct.pack('<HH',64,0)
(F/'game/blank.swf').write_bytes(b'FWS'+bytes([9])+struct.pack('<I',8+len(body))+body)
protected={str(p.relative_to(F)):hashlib.sha256(p.read_bytes()).hexdigest() for p in list((F/'saves').glob('*.json'))+[F/'saves/gifts/local_gifts.json']}
startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
env=dict(os.environ);env.update(NO_PROXY='*',no_proxy='*',HTTP_PROXY='',HTTPS_PROXY='',ALL_PROXY='')
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}));url='http://127.0.0.1:44049'
def get(route):return json.load(opener.open(url+route,timeout=4))
def wait(predicate,seconds=12):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  try:
   value=predicate()
   if value:return value
  except (OSError,ValueError):pass
  time.sleep(.2)
 raise AssertionError('Timed out waiting for isolated service/process status')
with (H/'frozen-backend.log').open('wb') as log,(H/'frozen-ruffle.log').open('wb') as glog:
 server=subprocess.Popen([str(F/'tools/zm3server.exe')],cwd=F,env=env,startupinfo=startup,stdout=log,stderr=log)
 game=None
 try:
  initial=wait(lambda:get('/runtime/status'));assert initial['root'].lower()==str(F).lower() and initial['gameRunning'] is False and initial['monitorError'] is None,initial
  game=subprocess.Popen([str(F/'runtime/ruffle.exe'),'--no-gui','--width','470','--height','295','--save-directory',str(F/'rundata/SharedObjects'),'--config',str(F/'rundata/config'),'--cache-directory',str(F/'rundata/cache'),str(F/'game/blank.swf')],cwd=F,env=env,startupinfo=startup,stdout=glog,stderr=glog)
  online=wait(lambda:(lambda x:x if x['gameRunning'] else None)(get('/runtime/status')));assert online['games'][0]['pid']==game.pid
  game_session=online['games'][0]['sessionId']
  game.terminate();game.wait(timeout=8);game=None
  stopped=wait(lambda:(lambda x:x if x['gameRunning'] is False and x['lastGameExit'] else None)(get('/runtime/status')))
  assert stopped['lastGameExit']['sessionId']==game_session and stopped['lastGameExit']['exitCode'] is not None
  after={name:hashlib.sha256((F/name).read_bytes()).hexdigest() for name in protected};assert after==protected
  text=(F/'logs/game-events.log').read_text(encoding='utf-8');assert 'game.started' in text and 'game.exited' in text and 'service='+initial['serviceSession'] in text
  report=dict(status='PASS',frozenBackend=True,nativeProcessInspection=True,serviceOnlyNotGame=True,ownedGameDetected=True,exitCodeCaptured=True,serviceStayedOnline=True,protectedFilesUnchanged=len(protected),gameSession=game_session)
  (H/'frozen-qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report),flush=True)
 finally:
  if game and game.poll() is None:game.terminate();game.wait(timeout=8)
  if server.poll() is None:subprocess.run(['taskkill','/PID',str(server.pid),'/T','/F'],startupinfo=startup,capture_output=True)
  server.wait(timeout=8)
