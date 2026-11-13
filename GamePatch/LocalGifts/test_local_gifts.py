import json,unittest,tempfile,copy,pathlib,unittest.mock as mock,threading
import local_gifts as gifts
class RedemptionTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.temp.name);(self.root/'saves').mkdir();(self.root/'tools').mkdir()
  self.store=gifts.GiftStore(str(self.root));self.save={'version':38.7,'extension':'preserve','player1_obj':{'roleid':1,'curLevel':1,'lhValue':100,'bagdjSaveString':'','petSave':''}}
  self.vip={'allMoney':50000,'lastMoney':20,'usedMoney':0};self.write('saves/vip.json',self.vip)
  for n in range(6):self.write('saves/slot%d.json'%n,self.save)
  fields=['0']*28;fields[2]='wpcsd';fields[22]='1'
  self.item={'kind':'item','id':'wpcsd','name':'长生丹','count':2,'record':'|'.join(fields),'bag':'bagdjSaveString','stackable':True}
  pet=['0']*32;pet[0]='turtle1';pet[18]='1'
  self.pet={'kind':'pet','id':'turtle1','name':'龟布','count':1,'record':'|'.join(pet),'profile':'original-turtle'}
  self.code={'id':'test-id','code':'TEST-2026','name':'测试礼包','enabled':True,'rule':'once','target':'all','perSaveLimit':1,'totalLimit':0,'expires':'','rewards':[self.item,{'kind':'soul','name':'灵魂','count':50},{'kind':'points','name':'点券','count':100}]}
  self.state={'version':1,'revision':0,'codes':[self.code],'claims':[]};self.flush()
 def tearDown(self):self.temp.cleanup()
 def write(self,p,v):gifts.atomic(str(self.root/p),gifts.encode(v))
 def read(self,p):return json.loads((self.root/p).read_bytes())
 def flush(self):self.write('tools/local_gifts.json',self.state)
 def preview(self,slot=0):return self.store.preview('test-2026',slot)['token']
 def redeem(self,slot=0,snapshot=None):return self.store.redeem(self.preview(slot),snapshot or copy.deepcopy(self.save))
 def error(self,fn):
  with self.assertRaises(gifts.GiftError):fn()
 def test_reward_unsaved_progress_unknown_fields_and_idempotency(self):
  s=copy.deepcopy(self.save);s.pop('extension');s['player1_obj']['lhValue']=400;s['player1_obj']['curExp']=123
  t=self.preview();r=self.store.redeem(t,s);self.assertEqual(r['save']['player1_obj']['lhValue'],450);self.assertEqual(r['save']['extension'],'preserve');self.assertEqual(r['save']['player1_obj']['curExp'],123)
  self.assertEqual(r['vip']['lastMoney'],120);self.assertEqual(self.store.redeem(t,s)['receipt'],r['receipt']);self.assertEqual(len(self.read('tools/local_gifts.json')['claims']),1)
  self.assertTrue(gifts.GiftStore(str(self.root)).redeem(t,{})['alreadyCompleted']);self.error(lambda:self.preview())
 def test_full_bag_atomic_no_points_no_claim(self):
  self.save['player1_obj']['bagdjSaveString']='}'.join(self.item['record'].replace('wpcsd','other') for _ in range(50));self.write('saves/slot0.json',self.save)
  before=(self.root/'saves/slot0.json').read_bytes();self.error(lambda:self.redeem());self.assertEqual(before,(self.root/'saves/slot0.json').read_bytes());self.assertEqual(self.read('saves/vip.json'),self.vip);self.assertEqual(self.read('tools/local_gifts.json')['claims'],[])
 def test_two_roles_rollback_when_second_full(self):
  self.save['player2_obj']=copy.deepcopy(self.save['player1_obj']);self.save['player2_obj']['roleid']=2;self.save['player2_obj']['bagdjSaveString']='}'.join(self.item['record'].replace('wpcsd','other') for _ in range(50));self.write('saves/slot0.json',self.save);self.error(lambda:self.redeem());self.assertEqual(self.read('saves/slot0.json'),self.save)
 def test_points_once_for_two_roles(self):
  self.save['player2_obj']=copy.deepcopy(self.save['player1_obj']);self.save['player2_obj']['roleid']=2;self.write('saves/slot0.json',self.save);r=self.redeem();self.assertEqual(r['vip']['lastMoney'],120);self.assertEqual(r['save']['player2_obj']['lhValue'],150)
 def test_rule_changes_disabled_expiry_limits(self):
  for change in ({'enabled':False},{'expires':'2000-01-01'},{'totalLimit':1}):
   c=copy.deepcopy(self.code);c.update(change);self.state['codes']=[c]
   self.state['claims']=[{'codeId':c['id'],'slot':1,'day':gifts.day()}] if 'totalLimit' in change else [];self.flush();self.error(lambda:self.preview())
  self.state['codes']=[self.code];self.state['claims']=[];self.flush();t=self.preview();self.code['description']='changed';self.flush();self.error(lambda:self.store.redeem(t,self.save))
 def test_daily_and_per_save(self):
  self.code['rule']='daily';self.flush();self.redeem();self.error(lambda:self.preview());self.redeem(1)
  self.state=self.read('tools/local_gifts.json');self.state['claims'][0]['day']='2000-01-01';self.flush();self.redeem()
 def test_limited_count(self):
  self.code.update(rule='limited',perSaveLimit=2);self.flush();self.redeem();self.redeem();self.error(lambda:self.preview())
 def test_conflict_and_expired_preview(self):
  t=self.preview();self.write('saves/slot0.json',dict(self.save,external=True));self.error(lambda:self.store.redeem(t,self.save))
  t=self.preview();self.store.pending[t]['created']-=301;self.error(lambda:self.store.redeem(t,self.save))
 def test_stack_overflow(self):
  fields=self.item['record'].split('|');fields[22]='9998';self.save['player1_obj']['bagdjSaveString']='|'.join(fields);self.write('saves/slot0.json',self.save);self.error(lambda:self.redeem())
 def test_original_pet_and_capacity(self):
  self.code['rewards']=[self.pet];self.flush();r=self.redeem();f=r['save']['player1_obj']['petSave'].split('|');self.assertEqual(f[18],'0');self.assertEqual(f[5],'1');self.assertTrue(1300<=int(f[9])<=1560);self.assertEqual(f[31],'0')
 def test_interrupted_commit_recovery(self):
  t=self.preview();original=gifts.atomic
  def interrupt(path,data):
   if path==self.store.file:raise OSError('simulated power interruption')
   original(path,data)
  with mock.patch.object(gifts,'atomic',side_effect=interrupt):
   with self.assertRaises(OSError):self.store.redeem(t,self.save)
  self.assertTrue(pathlib.Path(self.store.journal).exists());store=gifts.GiftStore(str(self.root));r=store.redeem(t,self.save);self.assertTrue(r['alreadyCompleted']);self.assertEqual(len(store.read()['claims']),1);self.assertEqual(r['vip']['lastMoney'],120)
 def test_parallel_requests_grant_once(self):
  t=self.preview();results=[]
  threads=[threading.Thread(target=lambda:results.append(self.store.redeem(t,copy.deepcopy(self.save)))) for _ in range(6)]
  for thread in threads:thread.start()
  for thread in threads:thread.join()
  self.assertEqual(len(results),6);self.assertEqual(len(self.store.read()['claims']),1);self.assertEqual(self.read('saves/vip.json')['lastMoney'],120)
if __name__=='__main__':unittest.main(verbosity=2)
