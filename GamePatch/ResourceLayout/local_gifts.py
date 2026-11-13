from game_rules import RuleBook
from game_paths import gift_state,gift_catalog
"""Local-only gift definitions, redemption transactions, and HTTP adapter (Python 3.8)."""
import json,os,re,time,threading,logging,hashlib,io
from storage_tx import StorageCoordinator, StorageError as GiftError, StoreLock, atomic

class GameLogHandler(logging.Handler):
    def __init__(self,path):
        super().__init__();self.path=path
    def emit(self,record):
        try:
            with StoreLock(os.path.join(os.path.dirname(self.path),'game-events.lock'),timeout=.2):
                if os.path.exists(self.path) and os.path.getsize(self.path)>=2*1024*1024:
                    for n in (3,2,1):
                        source=self.path+('.%d'%(n-1) if n>1 else '')
                        if os.path.exists(source):os.replace(source,self.path+'.%d'%n)
                with open(self.path,'a',encoding='utf-8') as out:out.write(self.format(record)+'\n');out.flush()
        except (OSError,GiftError):self.handleError(record)

def game_logger(root):
    name='zm3.runtime.'+os.path.normcase(os.path.abspath(root))
    log=logging.getLogger(name)
    if not log.handlers:
        os.makedirs(os.path.join(root,'logs'),exist_ok=True)
        handler=GameLogHandler(os.path.join(root,'logs','game-events.log'))
        handler.setFormatter(logging.Formatter('%(asctime)s %(levelname)s %(message)s'))
        log.addHandler(handler);log.setLevel(logging.INFO);log.propagate=False
    return log

def clone(value):
    return json.loads(json.dumps(value,ensure_ascii=False))

def day():
    return time.strftime('%Y-%m-%d',time.gmtime(time.time()+8*3600))

def encode(value):
    return json.dumps(value,ensure_ascii=False,separators=(',',':')).encode('utf-8')

class GiftStore:
    BAGS={'bagSaveString','bagdjSaveString','bagszSaveString','bagjnsSaveString','petEquipSaveString'}
    def __init__(self,root):
        self.root=os.path.abspath(root);self.rules=RuleBook(self.root);self.tools=os.path.join(self.root,'tools')
        self.file=gift_state(self.root)
        self.journal=os.path.join(self.tools,'local_gifts.transaction.json')
        self.lockfile=os.path.join(self.tools,'local_gifts.lock')
        self.pending={};self.mutex=threading.RLock();self.log=game_logger(self.root)
        self.storage=StorageCoordinator(self.root,self.log,lambda path,data:atomic(path,data));self.mutex=self.storage.mutex

    @staticmethod
    def save_identity(data,slot):
        value=data.get('_localSaveId') if isinstance(data,dict) else None
        if isinstance(value,str) and re.fullmatch(r'[0-9a-f]{32}',value):return value
        # Older saves had no identity. Use the first hero as conservative legacy
        # evidence: switching heroes proves a new game; leveling/adding player 2 does not.
        role=(data.get('player1_obj') or {}).get('roleid',0)
        return hashlib.sha256(('legacy-save-v1:%d:%s'%(slot,role)).encode()).hexdigest()[:32]

    def legacy_claim_identity(self,claim):
        if claim.get('saveId'):return claim['saveId']
        slot=claim['slot'];receipt=claim.get('receipt','')
        if isinstance(receipt,str) and re.fullmatch(r'[0-9a-f]{24}',receipt):
            path=os.path.join(self.root,'saves','backups','redeem-slot%d-%s.json'%(slot,receipt))
            if os.path.exists(path):
                with open(path,encoding='utf-8-sig') as source:return self.save_identity(json.load(source),slot)
        try:return self.save_identity(self.read_slot(slot)[1],slot)
        except GiftError:return self.save_identity({},slot)

    def migrate_identities(self):
        """Attach metadata using the existing recoverable transaction protocol."""
        self.recover();state=self.read();files=[];changed=False
        for claim in state['claims']:
            if not claim.get('saveId'):claim['saveId']=self.legacy_claim_identity(claim);changed=True
        for slot in range(6):
            if not os.path.exists(self.slot_path(slot)):continue
            raw,data=self.read_slot(slot);identity=self.save_identity(data,slot)
            if data.get('_localSaveId')==identity:continue
            data['_localSaveId']=identity
            backup=os.path.join(self.root,'saves','backups','identity-slot%d-%s.json'%(slot,os.urandom(8).hex()))
            atomic(backup,raw);files.append(dict(path='saves/slot%d.json'%slot,before=raw.hex(),after=encode(data).decode('utf-8')))
        if files or changed:
            with open(self.file,'rb') as source:history=source.read()
            atomic(os.path.join(self.root,'saves','backups','identity-gift-history-%s.json'%os.urandom(8).hex()),history)
            state['revision']=int(state.get('revision',0))+1
            atomic(self.journal,encode(dict(files=files,state=state)));self.recover()
            self.log.info('save.identity.migrated slots=%s claims=%s',len(files),len(state['claims']))

    def prepare_save(self,slot,data):
        if not isinstance(data,dict):raise GiftError('存档格式无效。')
        try:current=self.read_slot(slot)[1]
        except GiftError:current={}
        # Old game windows serialize no extension fields; preserve their identity.
        # New windows send a new identity only after the explicit new-game action.
        identity=data.get('_localSaveId')
        if not isinstance(identity,str) or not re.fullmatch(r'[0-9a-f]{32}',identity):identity=self.save_identity(current or data,slot)
        old=self.save_identity(current,slot);data['_localSaveId']=identity
        self.log.info('save.identity slot=%s identity=%s newGame=%s',slot,identity,identity!=old)
        return data

    def resolve(self,token):
        """Fence a lost request before allowing a retry or closing its UI."""
        if not isinstance(token,str) or not re.fullmatch(r'[0-9a-f]{48}',token):
            raise GiftError('兑换请求编号无效。')
        with self.mutex,StoreLock(self.lockfile):
            self.recover();state=self.read()
            receipt=next((c for c in state['claims'] if c.get('request')==token),None)
            if receipt:
                _,save=self.read_slot(receipt['slot']);_,vip=self.read_vip()
                if self.save_identity(save,receipt['slot'])!=self.legacy_claim_identity(receipt):
                    return dict(ok=True,completed=False,cancelled=True,otherSave=True)
                self.log.info('gift.resolve committed slot=%s codeId=%s',receipt['slot'],receipt['codeId'])
                return dict(ok=True,completed=True,alreadyCompleted=True,save=save,vip=vip,slot=receipt['slot'],name=receipt['name'],receipt=receipt['receipt'])
            pending=self.pending.pop(token,None)
            self.log.info('gift.resolve cancelled slot=%s codeId=%s',(pending or {}).get('slot'),(pending or {}).get('codeId'))
            return dict(ok=True,completed=False,cancelled=True)
    def catalog(self):
        path=gift_catalog(self.root)
        if not os.path.exists(path):return {'posters':[],'codes':[]}
        with open(path,encoding='utf-8-sig') as source:return json.load(source)
    def menu(self):
        with self.mutex,StoreLock(self.lockfile):
            self.recover();state=self.read();catalog=self.catalog()
            petnames=self.rules.data['official']['petNames']
            itemnames={r['fill']:r['name'] for r in self.rules.data['catalog']['items']}
            codes=[]
            for definition in state['codes']:
                row={k:definition.get(k) for k in ('id','code','name','description','enabled')}
                examples=[]
                for reward in definition.get('rewards',[]):
                    kind=reward.get('kind');code=str(reward.get('id',''))
                    if kind=='legacy':examples.extend(clone(catalog.get('rewardExamples',{}).get(code,[])))
                    else:examples.append({k:reward.get(k) for k in ('kind','id','name','count')})
                for reward in examples:
                    code=str(reward.get('id',''))
                    if reward.get('kind')=='item':reward['name']=itemnames.get(code,reward.get('name') or code)
                    elif reward.get('kind')=='pet':reward['name']=petnames.get(code,reward.get('name') or code)
                row['examples']=examples;codes.append(row)
            return {'ok':True,'posters':catalog['posters'],'petNames':petnames,'itemNames':itemnames,'nativeRewards':catalog.get('nativeRewards',{}),'codes':codes}
    def slot_path(self,slot):
        if type(slot) is not int or not 0<=slot<=5:raise GiftError('请选择 0–5 号角色存档。')
        return os.path.join(self.root,'saves','slot%d.json'%slot)
    def read(self):
        if not os.path.exists(self.file):return {'version':1,'revision':0,'codes':[],'claims':[]}
        with open(self.file,'r',encoding='utf-8-sig') as source:data=json.load(source)
        if data.get('version')!=1 or not isinstance(data.get('codes'),list) or not isinstance(data.get('claims'),list):raise GiftError('本地兑换配置格式无效。')
        return data
    def read_slot(self,slot):
        try:
            with open(self.slot_path(slot),'rb') as source:raw=source.read()
            data=json.loads(raw.decode('utf-8-sig'))
        except (OSError,ValueError):raise GiftError('当前存档不存在或无法读取，请先正常保存游戏。')
        return raw,data
    def read_vip(self):
        path=os.path.join(self.root,'saves','vip.json')
        try:
            with open(path,'rb') as source:raw=source.read()
            value=json.loads(raw.decode('utf-8-sig'))
        except (OSError,ValueError):raw=b'';value={'allMoney':0,'lastMoney':0,'usedMoney':0}
        return raw,value
    def recover(self):
        return self.storage.recover()
    @staticmethod
    def normalize(code):
        result=str(code or '').strip().upper()
        if not re.fullmatch(r'[A-Z0-9_-]{4,64}',result):raise GiftError('兑换码应为 4–64 位字母、数字、横线或下划线。')
        return result
    def available(self,state,definition,slot):
        if not definition.get('enabled',True):raise GiftError('这个兑换码已停用。')
        expiry=definition.get('expires','')
        if expiry and (not re.fullmatch(r'\d{4}-\d{2}-\d{2}',expiry) or day()>expiry):raise GiftError('兑换码已到期。')
        claims=[c for c in state['claims'] if c['codeId']==definition['id']]
        total=int(definition.get('totalLimit',0))
        if total and len(claims)>=total:raise GiftError('兑换码总次数已用完。')
        identity=self.save_identity(self.read_slot(slot)[1],slot)
        own=[c for c in claims if self.legacy_claim_identity(c)==identity]
        rule=definition.get('rule','once')
        if rule=='once' and own:raise GiftError('这个存档已经领取过该礼包。')
        if rule=='daily' and any(c['day']==day() for c in own):raise GiftError('这个存档今天已经领取过该礼包。')
        limit=int(definition.get('perSaveLimit',0))
        if rule=='limited' and limit and len(own)>=limit:raise GiftError('这个存档的领取次数已用完。')
        return total-len(claims) if total else 0
    @staticmethod
    def roles(snapshot,target):
        keys=['player1_obj','player2_obj'] if target=='all' else [target+'_obj']
        roles=[snapshot[k] for k in keys if isinstance(snapshot.get(k),dict) and snapshot[k].get('roleid') in (1,2,3,4)]
        if not roles:raise GiftError('目标存档没有已创建的角色。')
        return roles
    @staticmethod
    def activity(definition):
        ids={r.get('id') for r in definition.get('rewards',[]) if isinstance(r,dict)}
        if 'ptsmsrsz' in ids:return 1
        if '_4399ptsjwj' in ids:return 5
        if 'nian1' in ids:return 3
        if 'turtle1' in ids and 'p_cygsjl' in ids:return 2
        if 'yxsmsrsz' in ids or 'ghyb' in ids:return 4
        return 0
    def preview(self,code,slot,activity=None):
        with self.mutex,StoreLock(self.lockfile):
            self.recover();state=self.read();code=self.normalize(code)
            definition=next((c for c in state['codes'] if self.normalize(c['code'])==code),None)
            if definition is None:raise GiftError('兑换码不存在，请检查输入。')
            remaining=self.available(state,definition,slot);raw,snapshot=self.read_slot(slot)
            self.roles(snapshot,definition.get('target','all'))
            token=os.urandom(24).hex()
            self.pending={k:v for k,v in self.pending.items() if time.time()-v['created']<300}
            self.pending[token]={'created':time.time(),'slot':slot,'raw':raw,'codeId':definition['id'],'definition':clone(definition),'revision':state['revision']}
            return {'ok':True,'token':token,'name':definition['name'],'description':definition.get('description',''),'target':definition.get('target','all'),'rule':definition.get('rule','once'),'perSaveLimit':definition.get('perSaveLimit',1),'remaining':remaining,'unlimited':not definition.get('totalLimit',0),'expires':definition.get('expires',''),'rewards':[{'kind':r['kind'],'id':r.get('id',''),'name':r['name'],'count':r['count']} for r in definition['rewards']]}
    def count(self,value):
        if type(value) is not int or not 1<=value<=self.rules.rules['inventory']['stack']:raise GiftError('奖励数量应为 1–9999。')
        return value
    @staticmethod
    def amount(value):
        if type(value) is not int or not 1<=value<=2147483647:raise GiftError('奖励资源数值超出范围。')
        return value
    def grant_item(self,player,reward):
        bag=reward.get('bag');record=reward.get('record','');count=self.count(reward['count'])
        if bag not in self.BAGS or not isinstance(record,str) or '}' in record or len(record)>4096:raise GiftError('物品奖励数据无效，请重新保存兑换码配置。')
        parts=record.split('|');petgear=bag=='petEquipSaveString'
        if len(parts)<(13 if petgear else 23) or parts[0 if petgear else 2]!=reward['id']:raise GiftError('奖励物品编号与记录不一致。')
        records=[s for s in player.get(bag,'').split('}') if s]
        if reward.get('fashionDate') and len(parts)>27:parts[27]=day()
        limit=self.rules.rules['inventory']['petGear'] if petgear else self.rules.bag_capacity(int(player.get('curLevel',1)))
        stackable=bag=='bagdjSaveString' and reward.get('stackable',False)
        if stackable:
            index=next((i for i,r in enumerate(records) if len(r.split('|'))>22 and r.split('|')[2]==reward['id']),-1)
            if index>=0:
                fields=records[index].split('|');quantity=int(fields[22])+count
                if not 1<=quantity<=self.rules.rules['inventory']['stack']:raise GiftError(reward['name']+'已有数量将超过9999。')
                fields[22]=str(quantity);records[index]='|'.join(fields)
            else:
                if len(records)>=limit:raise GiftError('道具栏已满，请整理后领取。')
                parts[22]=str(count);records.append('|'.join(parts))
        else:
            if len(records)+count>limit:raise GiftError('奖励所需背包空间不足，请整理后领取。')
            if not petgear:parts[22]='1'
            records.extend(['|'.join(parts)]*count)
        player[bag]='}'.join(records)
    def grant_pet(self,player,reward,vip):
        count=self.count(reward['count']);records=[s for s in player.get('petSave','').split('}') if s]
        limit=self.rules.pet_capacity(int(vip.get('allMoney',0)))
        if len(records)+count>limit:raise GiftError('宠物栏空间不足，请整理后领取。')
        raw=reward.get('record','');fields=raw.split('&',1)[0].split('|')
        if len(fields)<32 or fields[0]!=reward['id'] or not 0<=int(fields[31])<=self.rules.rules['pet']['potentialMax']:raise GiftError('宠物奖励记录无效。')
        for _ in range(count):
            value=fields[:];value[18]='0'
            profile=reward.get('profile','')
            if profile in ('original-turtle','original-nian'):
                def pick(a,b):return a+int.from_bytes(os.urandom(4),'big')%(b-a+1)
                preset=self.rules.rules['giftProfiles'][profile];ranges=preset['qualities'];stats=[pick(a,b) for a,b in preset['stats']]
                value[9:13]=[str(pick(a,b)) for a,b in ranges]
                value[15:18]=[str(pick(a,b)) for a,b in self.rules.rules['giftProfiles']['abilities']]
                value[1]=value[2]=str(stats[0]);value[3]=value[4]=str(stats[1]);value[7]=str(stats[2]);value[8]=str(stats[3])
                value[5]='1';value[6]='0';value[19]='';value[24]='0';value[25:32]=['0']*7
            suffix='&'+raw.split('&',1)[1] if '&' in raw else ''
            records.append('|'.join(value)+suffix)
        player['petSave']='}'.join(records)
    def redeem(self,token,snapshot,base=None):
        with self.mutex,StoreLock(self.lockfile):
            self.recover();state=self.read()
            receipt=next((c for c in state['claims'] if c.get('request')==token),None)
            if receipt:
                _,data=self.read_slot(receipt['slot']);_,vip=self.read_vip()
                if self.save_identity(data,receipt['slot'])!=self.legacy_claim_identity(receipt):raise GiftError('这次领取已保存到原存档；当前槽位已换成其他存档，请重新选择礼包。')
                return {'ok':True,'alreadyCompleted':True,'name':receipt['name'],'save':data,'vip':vip,'slot':receipt['slot'],'receipt':receipt['receipt']}
            pending=self.pending.get(token)
            if not pending or time.time()-pending['created']>300:raise GiftError('奖励预览已过期，请重新验证兑换码。')
            slot=pending['slot'];raw,current=self.read_slot(slot)
            if raw!=pending['raw']:raise GiftError('存档已被游戏或修改器更新，请重新验证兑换码。')
            definition=next((c for c in state['codes'] if c['id']==pending['codeId']),None)
            if definition!=pending['definition']:raise GiftError('兑换规则已更新，请重新验证兑换码。')
            self.available(state,definition,slot)
            if not isinstance(snapshot,dict) or snapshot.get('version')!=current.get('version'):raise GiftError('游戏提交的存档版本不一致。')
            identity=self.save_identity(current,slot)
            if snapshot.get('_localSaveId') and snapshot['_localSaveId']!=identity:raise GiftError('当前游戏与该槽位不是同一份存档，请先保存新存档或重新读档。')
            updated=clone(snapshot)
            updated['_localSaveId']=identity
            # Preserve extension fields that the original game serializer does not know.
            for key,value in current.items():
                if key not in updated and key not in ('player1_obj','player2_obj'):updated[key]=clone(value)
            for key in ('player1_obj','player2_obj'):
                if isinstance(updated.get(key),dict) and isinstance(current.get(key),dict):
                    for name,value in current[key].items():
                        if name not in updated[key]:updated[key][name]=clone(value)
            players=self.roles(updated,definition.get('target','all'));vipraw,vip=self.read_vip();vip=clone(vip);vip_changed=False
            legacy=[r for r in definition['rewards'] if r['kind']=='legacy']
            if legacy:
                if len(legacy)!=1 or len(definition['rewards'])!=1:raise GiftError('原版礼包不能混入其他奖励。')
                if not isinstance(base,dict) or base.get('version')!=current.get('version'):raise GiftError('缺少原版奖励预览，请重新查看奖励。')
                for key in ('player1_obj','player2_obj'):
                    before=base.get(key);after=updated.get(key)
                    if not isinstance(before,dict) or not isinstance(after,dict):continue
                    if definition.get('target','all')!='all' and key!=definition['target']+'_obj':updated[key]=clone(before);continue
                    limit=self.rules.bag_capacity(int(before.get('curLevel',1)))
                    for bag in self.BAGS|{'petSave'}:
                        cap=self.rules.pet_capacity(int(vip.get('allMoney',0))) if bag=='petSave' else self.rules.rules['inventory']['petGear'] if bag=='petEquipSaveString' else limit
                        old=[r for r in before.get(bag,'').split('}') if r];new=[r for r in after.get(bag,'').split('}') if r]
                        if len(new)>len(old) and len(new)>cap:raise GiftError('礼包所需背包或宠物空间不足，请整理后领取。')
                        if bag=='bagdjSaveString':
                            oldqty={r.split('|')[2]:int(r.split('|')[22]) for r in old if len(r.split('|'))>22}
                            for row in new:
                                f=row.split('|')
                                if len(f)>22 and int(f[22])>max(self.rules.rules['inventory']['stack'],oldqty.get(f[2],0)):raise GiftError('礼包物品数量超过9999，请整理后领取。')
                        if bag=='petSave' and len(new)>len(old):
                            for i in range(len(old),len(new)):
                                petparts=new[i].split('&',1);fields=petparts[0].split('|')
                                if len(fields)<32:raise GiftError('原版宠物记录不完整，未消耗领取次数。')
                                fields[18]='0';fields[31]=str(max(0,min(self.rules.rules['pet']['potentialMax'],int(fields[31]))))
                                new[i]='|'.join(fields)+('&'+petparts[1] if len(petparts)>1 else '')
                            after['petSave']='}'.join(new)
            for reward in definition['rewards']:
                kind=reward['kind']
                if kind=='legacy':continue
                if kind=='points':
                    amount=self.amount(reward['count']);balance=int(vip.get('lastMoney',0))+amount
                    if not 0<=balance<=2147483647:raise GiftError('点券余额将超过游戏上限。')
                    vip['lastMoney']=balance;vip_changed=True
                else:
                    for player in players:
                        if kind=='item':self.grant_item(player,reward)
                        elif kind=='pet':self.grant_pet(player,reward,vip)
                        elif kind=='soul':
                            value=int(player.get('lhValue',0))+self.amount(reward['count'])
                            if not 0<=value<=2147483647:raise GiftError('灵魂数值将超过游戏上限。')
                            player['lhValue']=value
                        else:raise GiftError('不支持的奖励类型。')
            receipt={'request':token,'receipt':os.urandom(12).hex(),'codeId':definition['id'],'code':definition['code'],'name':definition['name'],'slot':slot,'saveId':identity,'day':day(),'time':int(time.time())}
            state['claims'].append(receipt);state['revision']=int(state.get('revision',0))+1
            files=[{'path':'saves/slot%d.json'%slot,'before':raw.hex(),'after':encode(updated).decode('utf-8')}]
            if vip_changed:files.append({'path':'saves/vip.json','before':vipraw.hex() if os.path.exists(os.path.join(self.root,'saves','vip.json')) else None,'after':encode(vip).decode('utf-8')})
            backup=os.path.join(self.root,'saves','backups','redeem-slot%d-%s.json'%(slot,receipt['receipt']))
            oldstate=self.storage.read(self.file)
            files.append(dict(path='tools/local_gifts.json',before=oldstate.hex() if oldstate is not None else None,after=encode(state).decode('utf-8')))
            self.storage.commit(files,'gift',backups={'saves/slot%d.json'%slot:os.path.relpath(backup,self.root).replace(os.sep,'/')})
            if hasattr(self,'game_versions'):
                for item in files:
                    if item['path'].startswith('saves/'):self.game_versions[item['path']]=hashlib.sha256(item['after'].encode('utf-8')).hexdigest()
            return {'ok':True,'alreadyCompleted':False,'name':definition['name'],'save':updated,'vip':vip,'slot':slot,'receipt':receipt['receipt']}

def attach(handler,root):
    store=GiftStore(root)
    # Recover before the first game read; a restart must not expose half a gift.
    with store.mutex,StoreLock(store.lockfile):store.migrate_identities()
    original_get=handler.do_GET;original_post=handler.do_POST
    def response(self,data,status=200):self._send(encode(data),'application/json; charset=utf-8',status)
    def post(self):
        path=self.path.split('?',1)[0]
        if path=='/game-log':
            try:
                length=int(self.headers.get('Content-Length','0'))
                if not 0<length<=16384:raise GiftError('日志长度无效。')
                data=json.loads(self.rfile.read(length).decode('utf-8-sig'))
                level=logging.ERROR if data.get('level')=='error' else logging.INFO
                # JSON escaping prevents multiline/log injection; never accept save payloads.
                clean={k:str(data[k])[:4000] for k in ('event','phase','slot','activity','message','stack') if k in data}
                store.log.log(level,'client %s',json.dumps(clean,ensure_ascii=False))
                response(self,{'ok':True})
            except Exception:response(self,{'ok':False})
            return
        if path in ('/local-gifts/preview','/local-gifts/redeem','/local-gifts/resolve'):
            data={}
            started=time.monotonic()
            try:
                length=int(self.headers.get('Content-Length','0'))
                if not 0<length<=4*1024*1024:raise GiftError('请求长度无效。')
                data=json.loads(self.rfile.read(length).decode('utf-8-sig'))
                if not isinstance(data,dict):raise GiftError('请求格式无效。')
                store.log.info('request.start action=%s slot=%s bytes=%s',path.rsplit('/',1)[-1],data.get('slot'),length)
                if path.endswith('/preview'):result=store.preview(data.get('code'),data.get('slot'),data.get('activity'))
                elif path.endswith('/resolve'):result=store.resolve(data.get('token'))
                else:result=store.redeem(data.get('token'),data.get('save'),data.get('baseSave'))
                store.log.info('request.ok action=%s slot=%s receipt=%s elapsedMs=%d',path.rsplit('/',1)[-1],result.get('slot'),result.get('receipt'),(time.monotonic()-started)*1000)
                response(self,result)
            except Exception as error:
                if not isinstance(data,dict):data={}
                unresolved=os.path.exists(store.journal)
                if path.endswith('/redeem'):
                    try:unresolved=unresolved or any(c.get('request')==data.get('token') for c in store.read()['claims'])
                    except Exception:unresolved=True
                store.log.log(logging.INFO if isinstance(error,GiftError) else logging.ERROR,'request.failed action=%s slot=%s elapsedMs=%d error=%s',path.rsplit('/',1)[-1],data.get('slot'),(time.monotonic()-started)*1000,str(error),exc_info=not isinstance(error,GiftError))
                response(self,{'ok':False,'error':str(error) if isinstance(error,GiftError) else '兑换服务发生异常，详情已记录到 logs/game-events.log。','unresolved':unresolved})
            return
        try:return original_post(self)
        except Exception:
            store.log.exception('http.post.failed path=%s',path);raise
    def get(self):
        path=self.path.split('?',1)[0]
        if path=='/game-log':
            with open(os.path.join(store.tools,'game_log.html'),'rb') as source:self._send(source.read(),'text/html; charset=utf-8',200)
            return
        if path=='/game-log/tail':
            filename=os.path.join(store.root,'logs','game-events.log')
            lines=[]
            if os.path.exists(filename):
                with open(filename,'rb') as source:
                    source.seek(0,2);size=source.tell();source.seek(max(0,size-128*1024));text=source.read().decode('utf-8',errors='replace');lines=text.splitlines()[-400:]
            response(self,{'ok':True,'lines':lines});return
        if path=='/local-gifts/catalog':
            try:response(self,store.menu())
            except (GiftError,OSError,ValueError) as error:response(self,{'ok':False,'error':str(error)})
            return
        art=re.fullmatch(r'/local-gifts/art/(hall|[0-9]{1,4})\.png',path)
        icon=re.fullmatch(r'/local-gifts/icon/(items|pets)/([A-Za-z0-9_-]{1,100})\.png',path)
        if art or icon:
            try:
                if art:filename=os.path.join(store.root,'icons','activity-gifts',art.group(1)+'.png')
                else:
                    group,code=icon.groups()
                    override=os.path.join(store.root,'icons','activity-gifts',code+'.png')
                    if group=='pets' and code in ('nian1','nian2','nian3') and os.path.exists(override):filename=override
                    else:
                        with open(os.path.join(store.root,'schema','zmxy3_icons.json'),encoding='utf-8-sig') as source:mapping=json.load(source)
                        name=os.path.basename(mapping.get(group,{}).get(code,''))
                        if not name:raise OSError('Unknown icon')
                        filename=os.path.join(store.root,'icons',group,name)
                with open(filename,'rb') as source:body=source.read()
                self._send(body,'image/png',200)
            except (OSError,ValueError,KeyError):self._send(b'','image/png',404)
            return
        if self.path.split('?',1)[0]=='/local-gifts/status':
            try:
                with store.mutex,StoreLock(store.lockfile):
                    store.recover();state=store.read()
                    response(self,{'ok':True,'version':4,'root':store.root,'configuredCodes':len(state['codes']),'diagnostics':'logs/game-events.log','saveIdentity':True})
            except (GiftError,OSError,ValueError) as error:response(self,{'ok':False,'error':str(error)},400)
            return
        if self.path.split('?',1)[0] in ('/save','/vip'):
            with store.mutex,StoreLock(store.lockfile):store.recover();return original_get(self)
        try:return original_get(self)
        except Exception:
            store.log.exception('http.get.failed path=%s',path);raise
    handler.do_POST=post;handler.do_GET=get
    import runtime_integration
    runtime_integration.attach(handler,store)
    return store
