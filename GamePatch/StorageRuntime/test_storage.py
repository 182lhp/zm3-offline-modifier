import unittest,tempfile,pathlib,threading,json,hashlib,urllib.request,urllib.error,functools,copy,os
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
import storage_tx as tx,local_gifts as gifts,runtime_integration
from runtime_state import RuntimeState,WindowsProcesses,context

class FakeProcesses:
 def __init__(self):self.items=[];self.codes={};self.closed=[]
 def list(self):return [dict(x) for x in self.items]
 def exited(self,item):return self.codes.get(item['pid'])
 def close(self,item):self.closed.append(item['pid'])

class StorageTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.temp.name);self.s=tx.StorageCoordinator(str(self.root));self.write('saves/slot0.json',{'value':1});self.write('saves/vip.json',{'value':2});self.write('tools/local_gifts.json',{'version':1,'revision':0,'codes':[],'claims':[]})
 def tearDown(self):self.temp.cleanup()
 def write(self,name,value):tx.atomic(str(self.root/name),tx.encode(value))
 def read(self,name):return json.loads((self.root/name).read_bytes())
 def item(self,name,value):return dict(path=name,before=(self.root/name).read_bytes().hex(),after=tx.encode(value).decode())
 def commit(self,items):
  with self.s.mutex,tx.StoreLock(self.s.lockfile):return self.s.commit(items,'test','correlation-control')
 def test_conflict_in_second_file_changes_neither(self):
  a=self.item('saves/slot0.json',{'value':10});b=self.item('saves/vip.json',{'value':20});self.write('saves/vip.json',{'outside':True})
  with self.assertRaises(tx.StorageError):self.commit([a,b])
  self.assertEqual(self.read('saves/slot0.json'),{'value':1});self.assertFalse(pathlib.Path(self.s.journal).exists())
 def test_interruption_rolls_forward_with_claim_history(self):
  state=self.read('tools/local_gifts.json');state['claims'].append({'receipt':'preserved'})
  items=[self.item('saves/slot0.json',{'value':10}),self.item('saves/vip.json',{'value':20}),self.item('tools/local_gifts.json',state)]
  original=self.s.writer
  def fail(path,data):
   if path==str(self.root/'saves/vip.json'):raise OSError('Interrupted process')
   original(path,data)
  self.s.writer=fail
  with self.assertRaises(OSError):self.commit(items)
  self.s.writer=original
  with tx.StoreLock(self.s.lockfile):self.s.recover();self.assertFalse(self.s.recover())
  self.assertEqual(self.read('saves/vip.json'),{'value':20});self.assertEqual(len(self.read('tools/local_gifts.json')['claims']),1)
 def test_legacy_journal_is_recovered(self):
  state=self.read('tools/local_gifts.json');state['revision']=4
  tx.atomic(self.s.journal,tx.encode(dict(files=[self.item('saves/slot0.json',{'value':9})],state=state)))
  with tx.StoreLock(self.s.lockfile):self.s.recover()
  self.assertEqual(self.read('tools/local_gifts.json')['revision'],4)
 def test_recovery_preflights_all_files(self):
  items=[self.item('saves/slot0.json',{'value':10}),self.item('saves/vip.json',{'value':20})]
  tx.atomic(self.s.journal,tx.encode(dict(version=2,id='control',files=items)));self.write('saves/vip.json',{'outside':True})
  with self.assertRaises(tx.StorageError):self.s.recover()
  self.assertEqual(self.read('saves/slot0.json'),{'value':1})
 def test_path_escape_and_bad_payload_are_rejected(self):
  for name,after in [('../outside.json','{}'),('saves/slot0.json','[]'),('saves/slot0.json','{')]:
   with self.assertRaises(tx.StorageError):self.commit([dict(path=name,before=None,after=after)])
 def test_no_change_has_no_journal_or_backup(self):
  self.commit([self.item('saves/slot0.json',{'value':1})]);self.assertFalse((self.root/'saves/backups').exists())
 def test_parallel_editors_only_one_expected_snapshot_commits(self):
  before=(self.root/'saves/slot0.json').read_bytes();results=[];gate=threading.Barrier(3)
  def worker(number):
   gate.wait()
   try:self.commit([dict(path='saves/slot0.json',before=before.hex(),after=tx.encode({'value':number}).decode())]);results.append('ok')
   except tx.StorageError:results.append('conflict')
  ts=[threading.Thread(target=worker,args=(n,)) for n in (10,20)]
  for t in ts:t.start()
  gate.wait()
  for t in ts:t.join()
  self.assertCountEqual(results,['ok','conflict'])

class RuntimeTests(unittest.TestCase):
 def test_owned_process_birth_exit_and_pid_reuse(self):
  with tempfile.TemporaryDirectory() as root:
   p=FakeProcesses();s=RuntimeState(root,p,False);self.assertFalse(s.status()['gameRunning'])
   p.items=[dict(pid=10,created=100,startedAt=1,handle=1)];a=s.status();first=a['games'][0]['sessionId'];self.assertTrue(a['gameRunning'])
   p.codes[10]=7;p.items=[];a=s.status();self.assertFalse(a['gameRunning']);self.assertEqual(a['lastGameExit']['exitCode'],7)
   p.codes.clear();p.items=[dict(pid=10,created=200,startedAt=2,handle=2)];self.assertNotEqual(s.status()['games'][0]['sessionId'],first);s.close()
 def test_probe_failure_is_unknown(self):
  with tempfile.TemporaryDirectory() as root:
   p=FakeProcesses();p.list=lambda:(_ for _ in ()).throw(OSError('Denied'));s=RuntimeState(root,p,False)
   self.assertIsNone(s.status()['gameRunning']);s.close()
  import ctypes,types
  native=WindowsProcesses.__new__(WindowsProcesses);native.c=ctypes;native.w=None;native.kernel=types.SimpleNamespace(CreateToolhelp32Snapshot=lambda *_:ctypes.c_void_p(-1).value)
  with self.assertRaises(OSError):native.list()

class HttpTests(StorageTests):
 def setUp(self):
  super().setUp();self.fake=FakeProcesses();store=gifts.GiftStore(str(self.root));self.store=store
  root=self.root
  class Handler(BaseHTTPRequestHandler):
   def log_message(self,*args):pass
   def _send(self,body,mime,status):self.send_response(status);self.send_header('Content-Type',mime);self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
   def do_POST(self):self._send(b'fallback','text/plain',404)
   def do_GET(self):
    path=self.path.split('?')[0];file=root/('saves/vip.json' if path=='/vip' else 'saves/slot0.json')
    self._send(file.read_bytes(),'application/json',200)
  self.runtime=runtime_integration.attach(Handler,store,self.fake,False)
  self.http=ThreadingHTTPServer(('127.0.0.1',0),Handler);self.thread=threading.Thread(target=self.http.serve_forever,daemon=True);self.thread.start();self.base='http://127.0.0.1:%d'%self.http.server_port
  self.opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
 def tearDown(self):self.http.shutdown();self.http.server_close();self.runtime.close();super().tearDown()
 def request(self,path,value=None,identifier='request-control-123'):
  headers={'X-Zm3-Request-Id':identifier}
  if value is not None:headers['Content-Type']='application/json'
  req=urllib.request.Request(self.base+path,data=tx.encode(value) if value is not None else None,headers=headers)
  return self.opener.open(req,timeout=4)
 def test_notification_is_read_only_and_running_is_not_service_alive(self):
  path=self.root/'saves/slot0.json';before=path.read_bytes();body={'files':[{'path':'saves/slot0.json','sha256':hashlib.sha256(before).hexdigest()}]}
  with self.request('/storage/notify',body) as response:
   data=json.load(response);self.assertFalse(data['gameRunning']);self.assertEqual(response.headers['X-Zm3-Request-Id'],'request-control-123')
  self.assertEqual(path.read_bytes(),before)
  self.fake.items=[dict(pid=10,created=100,startedAt=1,handle=1)]
  with self.request('/storage/notify',body) as response:self.assertTrue(json.load(response)['needsReload'])
  text=(self.root/'logs/game-events.log').read_text(encoding='utf-8');self.assertIn('req=request-control-123',text);self.assertNotIn(before.decode(),text)
 def test_bad_notify_hash_is_rejected_without_write(self):
  before=(self.root/'saves/slot0.json').read_bytes()
  with self.assertRaises(urllib.error.HTTPError) as error:self.request('/storage/notify',{'files':[{'path':'saves/slot0.json','sha256':'0'*64}]})
  self.assertEqual(error.exception.code,409);self.assertEqual((self.root/'saves/slot0.json').read_bytes(),before)
 def test_editor_change_requires_game_reload(self):
  self.write('saves/slot0.json',{'_localSaveId':'a'*32,'value':1})
  self.request('/save?slot=0').close();self.write('saves/slot0.json',{'_localSaveId':'a'*32,'value':10})
  with self.assertRaises(urllib.error.HTTPError):self.request('/save?slot=0',{'_localSaveId':'a'*32,'value':2})
  self.assertEqual(self.read('saves/slot0.json')['value'],10)
  self.request('/save?slot=0').close();self.request('/save?slot=0',{'_localSaveId':'a'*32,'value':11}).close();self.assertEqual(self.read('saves/slot0.json')['value'],11)
 def test_new_game_identity_can_overwrite_old_slot(self):
  self.write('saves/slot0.json',{'_localSaveId':'a'*32,'value':1});self.request('/save?slot=0').close()
  self.write('saves/slot0.json',{'_localSaveId':'a'*32,'value':10});self.request('/save?slot=0',{'_localSaveId':'b'*32,'value':0}).close()
  self.assertEqual(self.read('saves/slot0.json')['_localSaveId'],'b'*32)
 def test_save_already_waiting_does_not_overwrite_a_new_commit(self):
  self.write('saves/slot0.json',{'_localSaveId':'a'*32,'value':1});self.request('/save?slot=0').close()
  lock=threading.RLock();blocked=threading.Event()
  class Gate:
   def __enter__(self):blocked.set();lock.acquire()
   def __exit__(self,*a):lock.release()
  self.store.mutex=Gate();result=[]
  def writer():
   try:self.request('/save?slot=0',{'_localSaveId':'a'*32,'value':2}).close();result.append('overwritten')
   except urllib.error.HTTPError as e:result.append(e.code)
  with self.store.mutex:
   blocked.clear();worker=threading.Thread(target=writer);worker.start();self.assertTrue(blocked.wait(3))
   self.write('saves/slot0.json',{'_localSaveId':'a'*32,'value':10})
   self.store.game_versions['saves/slot0.json']=hashlib.sha256((self.root/'saves/slot0.json').read_bytes()).hexdigest()
  worker.join(4);self.assertEqual(result,[409]);self.assertEqual(self.read('saves/slot0.json')['value'],10)

if __name__=='__main__':unittest.main(verbosity=2)
