"""Build read-only illustrations from the verified native reward samples.

These examples never authorize claims. Random/role-dependent rewards are replaced
with the native in-memory preview before the claim button becomes available.
"""
from pathlib import Path
import json
HERE=Path(__file__).resolve().parent
catalog=json.loads((HERE/'catalog.json').read_text(encoding='utf-8'))
samples=json.loads((HERE/'all-reward-qa.json').read_text(encoding='utf-8'))
assert len(samples)==766 and all(row['ok'] and row['rewards'] for row in samples)
examples={row['code'].replace('ZM3-',''):row['rewards'] for row in samples}
def item(code,count=1):return {'kind':'item','id':code,'name':code,'count':count}
examples['GBX']=[item('hehua'),item('wptlz'),item('wpllz'),item('wphlz'),item('wpqhs1',3),item('wplvdyl',10)]
examples['YYL']=[item('_4399ptsjwj'),item('tlzsp'),item('xns1'),item('gjs1'),{'kind':'resource','name':'世界传送次数','count':2}]
assert len(examples)==768
catalog['rewardExamples']=examples
(HERE/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared 768 read-only reward illustrations; actual claims still require native preview.')
