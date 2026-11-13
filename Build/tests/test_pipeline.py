from support import SOURCE,seed_rules
import unittest,tempfile,pathlib,json,unittest.mock as mock
from release import Pipeline
from package import info

class PipelineGatesTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.out=pathlib.Path(self.temp.name);(self.out/'stage').mkdir();self.file=self.out/'stage/program.bin';self.file.write_bytes(b'verified program');(self.out/'test-report.json').write_text('{"status":"PASS"}')
  plan=dict(sourceFingerprint='source',updated={'program.bin':info(self.file)})
  (self.out/'install-plan.json').write_text(json.dumps(plan));self.pipeline=Pipeline.__new__(Pipeline);self.pipeline.out=self.out;self.pipeline.root=self.out/'game';self.pipeline.config={};self.pipeline.report=dict(inputFingerprint='inputs',evidence={'test-report.json':info(self.out/'test-report.json')})
  self.source=mock.patch('release.source_fingerprint',return_value='source');self.source.start();self.inputs=mock.patch('release.input_fingerprint',return_value='inputs');self.inputs.start()
 def tearDown(self):self.source.stop();self.inputs.stop();self.temp.cleanup()
 def test_verified_artifacts_and_evidence_resume(self):self.assertEqual(self.pipeline.check_stage()['sourceFingerprint'],'source')
 def test_changed_program_is_rejected(self):
  self.file.write_bytes(b'changed')
  with self.assertRaisesRegex(ValueError,'artifact changed'):self.pipeline.check_stage()
 def test_unexpected_stage_file_is_rejected(self):
  (self.out/'stage/unexpected.bin').write_bytes(b'x')
  with self.assertRaisesRegex(ValueError,'artifact changed'):self.pipeline.check_stage()
 def test_changed_source_is_rejected(self):
  with mock.patch('release.source_fingerprint',return_value='new-source'):
   with self.assertRaisesRegex(ValueError,'Source changed'):self.pipeline.check_stage()
 def test_changed_paired_input_is_rejected(self):
  with mock.patch('release.input_fingerprint',return_value='new-inputs'):
   with self.assertRaisesRegex(ValueError,'Paired inputs changed'):self.pipeline.check_stage()
 def test_changed_evidence_is_rejected(self):
  (self.out/'test-report.json').write_text('{"status":"FAKE"}')
  with self.assertRaisesRegex(ValueError,'evidence'):self.pipeline.check_stage()
 def test_missing_evidence_is_rejected(self):
  (self.out/'test-report.json').unlink()
  with self.assertRaisesRegex(ValueError,'evidence'):self.pipeline.check_stage()

if __name__=='__main__':unittest.main()
