"""Shared generated rule registry. Does not read or migrate any user data."""
import os,json,hashlib
from game_paths import within
from rules_stamp import EXPECTED_SHA256

class RuleBook:
    def __init__(self,root):
        with open(within(root,'schema/game_rules.json'),'rb') as source:raw=source.read()
        if hashlib.sha256(raw).hexdigest()!=EXPECTED_SHA256:raise ValueError('规则数据与后台版本不一致，请安装同一次构建的文件。')
        self.data=json.loads(raw.decode('utf-8'));self.rules=self.data['rules']
        if self.data['format']!=1:raise ValueError('不支持的规则数据版本。')
    def status(self):return dict(revision=self.data['revision'],sha256=EXPECTED_SHA256,sourceSha256=self.data['sourceSha256'])
    def bag_capacity(self,level):
        rules=self.rules['inventory'];return rules['expandedBag'] if level>=rules['expandLevel'] else rules['bag']
    def pet_capacity(self,recharge):
        rules=self.rules['inventory'];return rules['expandedPets'] if recharge>=self.rules['vip']['thresholds'][rules['petsVipLevel']] else rules['pets']
