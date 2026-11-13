from support import SOURCE,seed_rules
import unittest,tempfile,pathlib,json,hashlib,threading,urllib.request,urllib.error
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
import game_paths as paths,storage_tx as tx,resource_versions as rv

class LayoutTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.tmp.name);self.old=self.root/'tools/local_gifts.json';tx.atomic(str(self.old),b'{"claims":[{"id":"old"}],"unknown":17}')
  self.new=self.root/'saves/gifts/local_gifts.json';self.storage=tx.StorageCoordinator(str(self.root))
 def tearDown(self):self.tmp.cleanup()
 def marker(self):tx.atomic(str(self.root/'schema/layout.json'),b'{"layoutVersion":2}')
 def test_old_install_still_works(self):self.assertEqual(paths.gift_state(str(self.root)),str(self.old))
 def test_byte_preserving_migration(self):
  raw=self.old.read_bytes();tx.atomic(str(self.new),raw);self.marker();self.old.unlink();self.assertEqual(self.new.read_bytes(),raw);self.assertEqual(paths.gift_state(str(self.root)),str(self.new))
 def test_incomplete_conflicting_migration_is_rejected(self):
  tx.atomic(str(self.new),b'{"claims":[]}')
  with self.assertRaises(ValueError):paths.gift_state(str(self.root))
  self.assertEqual(json.loads(self.old.read_bytes())['unknown'],17)
 def test_completed_marker_never_uses_stale_old_history(self):
  tx.atomic(str(self.new),b'{"claims":[1,2]}');self.marker();self.assertEqual(paths.gift_state(str(self.root)),str(self.new))
 def test_missing_migrated_history_does_not_silently_start_empty(self):
  self.marker()
  with self.assertRaises(ValueError):paths.gift_state(str(self.root))
 def test_legacy_journal_targets_moved_file(self):
  tx.atomic(str(self.new),self.old.read_bytes());self.marker();self.old.unlink()
  tx.atomic(self.storage.journal,tx.encode(dict(files=[],state=dict(claims=[{'id':'old'},{'id':'new'}],unknown=17))))
  with tx.StoreLock(self.storage.lockfile):self.storage.recover()
  self.assertEqual(len(json.loads(self.new.read_bytes())['claims']),2);self.assertFalse(self.old.exists())
 def test_aliases_cannot_overwrite_same_file_twice(self):
  tx.atomic(str(self.new),self.old.read_bytes());self.marker()
  rows=[dict(path=p,before=self.new.read_bytes().hex(),after='{}') for p in ['tools/local_gifts.json','saves/gifts/local_gifts.json']]
  with self.assertRaises(tx.StorageError):self.storage.plan(rows)
 def test_old_descriptor_updates_new_file_only(self):
  tx.atomic(str(self.new),self.old.read_bytes());self.marker();old=self.old.read_bytes()
  with tx.StoreLock(self.storage.lockfile):self.storage.commit([dict(path='tools/local_gifts.json',before=old.hex(),after='{"claims":[1,2],"unknown":17}')],'test')
  self.assertEqual(self.old.read_bytes(),old);self.assertEqual(len(json.loads(self.new.read_bytes())['claims']),2)

class ResourceTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.tmp.name)
  self.body=b'not cached across releases';tx.atomic(str(self.root/'game/launcher.swf'),self.body)
  tx.atomic(str(self.root/'game/other.swf'),b'child movie');self.write_manifest();self.index=rv.ResourceIndex(str(self.root))
  index=self.index
  class Handler(BaseHTTPRequestHandler):
   def do_GET(self):
    if not index.serve(self):self.send_error(404)
   def log_message(self,*args):pass
  self.server=ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=self.server.serve_forever,daemon=True).start()
  self.url='http://127.0.0.1:%d'%self.server.server_address[1];self.op=urllib.request.build_opener(urllib.request.ProxyHandler({}))
 def tearDown(self):self.server.shutdown();self.server.server_close();self.tmp.cleanup()
 def write_manifest(self):
  rows=[dict(path='game/'+p.name,size=p.stat().st_size,sha256=rv.digest(p.read_bytes())) for p in (self.root/'game').glob('*.swf')]
  data=dict(format=1,version='2.33.0',files=rows);data['buildId']=rv.build_id(data['version'],rows);tx.atomic(str(self.root/'schema/resource_manifest.json'),tx.encode(data));self.data=data
 def request(self,path,headers=None):return self.op.open(urllib.request.Request(self.url+path,headers=headers or {}))
 def test_unversioned_is_revalidated_and_etag_304(self):
  with self.request('/launcher.swf') as r:self.assertEqual(r.read(),self.body);etag=r.headers['ETag'];self.assertIn('no-cache',r.headers['Cache-Control'])
  with self.assertRaises(urllib.error.HTTPError) as e:self.request('/launcher.swf',{'If-None-Match':etag})
  self.assertEqual(e.exception.code,304)
 def test_matching_version_hash_is_immutable(self):
  with self.request('/launcher.swf?zm3v='+self.index.build+'&zm3h='+rv.digest(self.body)) as r:self.assertIn('immutable',r.headers['Cache-Control']);self.assertEqual(r.read(),self.body)
 def test_stale_version_or_digest_refused(self):
  for q in ['zm3v=old&zm3h='+rv.digest(self.body),'zm3v='+self.index.build+'&zm3h='+'0'*64]:
   with self.assertRaises(urllib.error.HTTPError) as e:self.request('/launcher.swf?'+q)
   self.assertEqual(e.exception.code,409)
 def test_outside_modifications_never_304_or_immutable(self):
  tx.atomic(str(self.root/'game/launcher.swf'),b'new content')
  with self.request('/launcher.swf',{'If-None-Match':'"'+rv.digest(self.body)+'"'}) as r:self.assertEqual(r.status,200);self.assertEqual(r.headers['Cache-Control'],'no-store');self.assertEqual(r.read(),b'new content')
 def test_verify_detects_corrupt_and_missing_and_ignores_saves(self):
  tx.atomic(str(self.root/'saves/slot0.json'),b'{}');self.assertTrue(self.index.verify()['ok'])
  tx.atomic(str(self.root/'game/launcher.swf'),b'corrupt');(self.root/'game/other.swf').unlink();self.assertEqual(len(self.index.verify()['errors']),2)
 def test_path_traversal_is_not_served(self):
  tx.atomic(str(self.root/'saves/slot0.json'),b'secret')
  for p in ['/../saves/slot0.json','/%2e%2e/saves/slot0.json','/local-gifts/art/../../saves/slot0.json']:
   with self.assertRaises(urllib.error.HTTPError) as e:self.request(p)
   self.assertEqual(e.exception.code,404)
 def test_manifest_fingerprint_tamper_is_rejected(self):
  self.data['files'][0]['sha256']='0'*64;tx.atomic(str(self.root/'schema/resource_manifest.json'),tx.encode(self.data));self.assertTrue(rv.ResourceIndex(str(self.root)).problem)
 def test_launch_preserves_save_viewport_and_isolates_cache(self):
  before=['ruffle.exe','--width','940','--height','590','--scale','show-all','--save-directory','keep-save','--config','keep-config','--cache-directory','old-cache',self.url+'/launcher.swf?x=1']
  after=rv.launch_command(str(self.root),before)
  for arg in ['--width','--height','--scale','--save-directory','--config']:self.assertEqual(after[after.index(arg)+1],before[before.index(arg)+1])
  self.assertEqual(before[-1],self.url+'/launcher.swf?x=1');self.assertIn('zm3v='+self.index.build,after[-1]);self.assertIn('x=1',after[-1]);self.assertTrue(after[after.index('--cache-directory')+1].endswith(self.index.build))

if __name__=='__main__':unittest.main()
