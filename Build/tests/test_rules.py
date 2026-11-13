from support import SOURCE,seed_rules
import unittest,tempfile,json,hashlib,pathlib,shutil
from game_rules import RuleBook
from rules_stamp import EXPECTED_SHA256
from generate_rules import compile_rules,generate

class RulesTests(unittest.TestCase):
 def setUp(self):self.temp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.temp.name);seed_rules(self.root);self.book=RuleBook(str(self.root))
 def tearDown(self):self.temp.cleanup()
 def test_generator_is_deterministic_and_outputs_are_current(self):
  self.assertEqual(compile_rules(SOURCE),compile_rules(SOURCE));self.assertEqual(generate(SOURCE,True)['sha256'],EXPECTED_SHA256)
 def test_pinned_extracted_references_preserved(self):
  for section,name in [('official','official_zmxy3.json'),('catalog','library_final.json'),('equipmentStrength','equip_strength.json'),('petEquipmentStrength','peteq_strength.json')]:self.assertEqual(self.book.data[section],json.loads((SOURCE/'Rules/Inputs'/name).read_text(encoding='utf-8-sig')))
 def test_only_missing_explicit_spirits_supplemented(self):
  original=json.loads((SOURCE/'Rules/Inputs/ref_all.json').read_text(encoding='utf-8-sig'));actual=self.book.data['references']
  self.assertEqual(actual['equip'],original['equip']);self.assertTrue(all(actual['petequip'][k]==v for k,v in original['petequip'].items()));self.assertEqual(len(actual['petequip'])-len(original['petequip']),12)
 def test_backend_uses_shared_capacity_boundaries(self):
  self.assertEqual([self.book.bag_capacity(i) for i in (1,49,50,90)],[50,50,75,75]);self.assertEqual([self.book.pet_capacity(i) for i in (0,9999,10000,50000)],[5,5,10,10]);self.assertEqual(self.book.rules['inventory']['petGear'],15)
 def test_no_rule_load_rewrites_user_data(self):
  path=self.root/'saves/gifts/local_gifts.json';path.parent.mkdir(parents=True);raw=b'{"claims":[{"receipt":"keep"}],"custom":"keep"}';path.write_bytes(raw);RuleBook(str(self.root));self.assertEqual(path.read_bytes(),raw)
 def test_corrupt_registry_fails_closed(self):
  path=self.root/'schema/game_rules.json';path.write_bytes(path.read_bytes()+b' ')
  with self.assertRaises(ValueError):RuleBook(str(self.root))
 def test_missing_registry_does_not_reset_claim_history(self):
  (self.root/'schema/game_rules.json').unlink()
  with self.assertRaises(OSError):RuleBook(str(self.root))
  self.assertFalse((self.root/'saves').exists())
 def test_changed_extracted_input_requires_source_revision(self):
  source=self.root/'source';shutil.copytree(SOURCE/'Rules',source/'Rules');p=source/'Rules/Inputs/ref_all.json';p.write_bytes(p.read_bytes()+b' ')
  with self.assertRaisesRegex(ValueError,'without a reviewed source revision'):compile_rules(source)
 def test_non_increasing_thresholds_rejected(self):
  source=self.root/'source';shutil.copytree(SOURCE/'Rules',source/'Rules');p=source/'Rules/definitions.json';data=json.loads(p.read_text(encoding='utf-8'));data['rules']['vip']['thresholds'][2]=0;p.write_text(json.dumps(data),encoding='utf-8')
  with self.assertRaisesRegex(ValueError,'thresholds'):compile_rules(source)
 def test_original_reward_ranges_and_acquisition_preserved(self):
  rules=self.book.rules;self.assertEqual(rules['giftProfiles']['original-turtle']['qualities'][3],[390,611]);self.assertEqual(rules['pet']['spiritUltimates']['neat']['skill'],'cnaoyi');self.assertEqual(len(rules['vip']['gifts']),7);self.assertEqual(len(rules['buff']['kinds']),7)

if __name__=='__main__':unittest.main()
