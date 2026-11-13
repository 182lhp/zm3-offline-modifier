"""Local-only gift definitions, redemption transactions, and HTTP adapter (Python 3.8)."""
import json,os,re,time,threading

class GiftError(Exception):
    pass

def clone(value):
    return json.loads(json.dumps(value,ensure_ascii=False))

def day():
    return time.strftime('%Y-%m-%d',time.gmtime(time.time()+8*3600))

def encode(value):
    return json.dumps(value,ensure_ascii=False,separators=(',',':')).encode('utf-8')

def atomic(path,data):
    os.makedirs(os.path.dirname(path),exist_ok=True)
    temporary=path+'.'+os.urandom(8).hex()+'.tmp'
    try:
        with open(temporary,'xb') as out:
            out.write(data);out.flush();os.fsync(out.fileno())
        os.replace(temporary,path)
    finally:
        if os.path.exists(temporary):os.remove(temporary)

class StoreLock:
    def __init__(self,path):self.path=path;self.file=None
    def __enter__(self):
        import msvcrt
        os.makedirs(os.path.dirname(self.path),exist_ok=True)
        self.file=open(self.path,'a+b')
        if os.path.getsize(self.path)==0:self.file.write(b'0');self.file.flush()
        end=time.monotonic()+4
        while True:
            try:
                self.file.seek(0);msvcrt.locking(self.file.fileno(),msvcrt.LK_NBLCK,1);return self
            except OSError:
                if time.monotonic()>end:self.file.close();raise GiftError('兑换数据正在使用，请稍后重试。')
                time.sleep(.04)
    def __exit__(self,*args):
        import msvcrt
        self.file.seek(0);msvcrt.locking(self.file.fileno(),msvcrt.LK_UNLCK,1);self.file.close()

class GiftStore:
    BAGS={'bagSaveString','bagdjSaveString','bagszSaveString','bagjnsSaveString','petEquipSaveString'}
    def __init__(self,root):
        self.root=os.path.abspath(root);self.tools=os.path.join(self.root,'tools')
        self.file=os.path.join(self.tools,'local_gifts.json')
        self.journal=os.path.join(self.tools,'local_gifts.transaction.json')
        self.lockfile=os.path.join(self.tools,'local_gifts.lock')
        self.pending={};self.mutex=threading.RLock()
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
        if not os.path.exists(self.journal):return
        with open(self.journal,'r',encoding='utf-8') as source:transaction=json.load(source)
        for item in transaction['files']:
            relative=item['path']
            if not re.fullmatch(r'saves/(slot[0-5]|vip)\.json',relative):raise GiftError('兑换恢复文件路径无效。')
            path=os.path.join(self.root,*relative.split('/'))
            current=b''
            if os.path.exists(path):
                with open(path,'rb') as source:current=source.read()
            before=bytes.fromhex(item['before']);after=item['after'].encode('utf-8')
            if current not in (before,after):raise GiftError('兑换恢复时发现存档被外部更新，请保留备份并联系维护。')
            if current!=after:atomic(path,after)
        atomic(self.file,encode(transaction['state']))
        os.remove(self.journal)
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
        own=[c for c in claims if c['slot']==slot]
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
    def preview(self,code,slot):
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
    @staticmethod
    def count(value):
        if type(value) is not int or not 1<=value<=9999:raise GiftError('奖励数量应为 1–9999。')
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
        limit=15 if petgear else 75 if int(player.get('curLevel',1))>=50 else 50
        stackable=bag=='bagdjSaveString' and reward.get('stackable',False)
        if stackable:
            index=next((i for i,r in enumerate(records) if len(r.split('|'))>22 and r.split('|')[2]==reward['id']),-1)
            if index>=0:
                fields=records[index].split('|');quantity=int(fields[22])+count
                if not 1<=quantity<=9999:raise GiftError(reward['name']+'已有数量将超过9999。')
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
        limit=10 if int(vip.get('allMoney',0))>=10000 else 5
        if len(records)+count>limit:raise GiftError('宠物栏空间不足，请整理后领取。')
        raw=reward.get('record','');fields=raw.split('&',1)[0].split('|')
        if len(fields)<32 or fields[0]!=reward['id'] or not 0<=int(fields[31])<=100:raise GiftError('宠物奖励记录无效。')
        for _ in range(count):
            value=fields[:];value[18]='0'
            profile=reward.get('profile','')
            if profile in ('original-turtle','original-nian'):
                def pick(a,b):return a+int.from_bytes(os.urandom(4),'big')%(b-a+1)
                if profile=='original-turtle':ranges=[(1300,1560),(650,910),(1040,1170),(390,611)];stats=[pick(300,600),pick(100,150),pick(10,25),pick(5,10)]
                else:ranges=[(1300,1950),(130,260),(1040,1560),(130,520)];stats=[pick(500,1000),pick(200,400),pick(30,60),pick(5,10)]
                value[9:13]=[str(pick(a,b)) for a,b in ranges]
                value[15:18]=[str(pick(2,5)),str(pick(2,4)),str(pick(2,4))]
                value[1]=value[2]=str(stats[0]);value[3]=value[4]=str(stats[1]);value[7]=str(stats[2]);value[8]=str(stats[3])
                value[5]='1';value[6]='0';value[19]='';value[24]='0';value[25:32]=['0']*7
            suffix='&'+raw.split('&',1)[1] if '&' in raw else ''
            records.append('|'.join(value)+suffix)
        player['petSave']='}'.join(records)
    def redeem(self,token,snapshot):
        with self.mutex,StoreLock(self.lockfile):
            self.recover();state=self.read()
            receipt=next((c for c in state['claims'] if c.get('request')==token),None)
            if receipt:
                _,data=self.read_slot(receipt['slot']);_,vip=self.read_vip()
                return {'ok':True,'alreadyCompleted':True,'name':receipt['name'],'save':data,'vip':vip,'slot':receipt['slot'],'receipt':receipt['receipt']}
            pending=self.pending.get(token)
            if not pending or time.time()-pending['created']>300:raise GiftError('奖励预览已过期，请重新验证兑换码。')
            slot=pending['slot'];raw,current=self.read_slot(slot)
            if raw!=pending['raw']:raise GiftError('存档已被游戏或修改器更新，请重新验证兑换码。')
            definition=next((c for c in state['codes'] if c['id']==pending['codeId']),None)
            if definition!=pending['definition']:raise GiftError('兑换规则已更新，请重新验证兑换码。')
            self.available(state,definition,slot)
            if not isinstance(snapshot,dict) or snapshot.get('version')!=current.get('version'):raise GiftError('游戏提交的存档版本不一致。')
            updated=clone(snapshot)
            # Preserve extension fields that the original game serializer does not know.
            for key,value in current.items():
                if key not in updated and key not in ('player1_obj','player2_obj'):updated[key]=clone(value)
            for key in ('player1_obj','player2_obj'):
                if isinstance(updated.get(key),dict) and isinstance(current.get(key),dict):
                    for name,value in current[key].items():
                        if name not in updated[key]:updated[key][name]=clone(value)
            players=self.roles(updated,definition.get('target','all'));vipraw,vip=self.read_vip();vip=clone(vip);vip_changed=False
            for reward in definition['rewards']:
                kind=reward['kind']
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
            receipt={'request':token,'receipt':os.urandom(12).hex(),'codeId':definition['id'],'code':definition['code'],'name':definition['name'],'slot':slot,'day':day(),'time':int(time.time())}
            state['claims'].append(receipt);state['revision']=int(state.get('revision',0))+1
            files=[{'path':'saves/slot%d.json'%slot,'before':raw.hex(),'after':encode(updated).decode('utf-8')}]
            if vip_changed:files.append({'path':'saves/vip.json','before':vipraw.hex(),'after':encode(vip).decode('utf-8')})
            backup=os.path.join(self.root,'saves','backups','redeem-slot%d-%s.json'%(slot,receipt['receipt']))
            atomic(backup,raw)
            atomic(self.journal,encode({'files':files,'state':state}))
            self.recover()
            return {'ok':True,'alreadyCompleted':False,'name':definition['name'],'save':updated,'vip':vip,'slot':slot,'receipt':receipt['receipt']}

def attach(handler,root):
    store=GiftStore(root)
    # Recover before the first game read; a restart must not expose half a gift.
    with store.mutex,StoreLock(store.lockfile):store.recover()
    original_get=handler.do_GET;original_post=handler.do_POST
    def response(self,data,status=200):self._send(encode(data),'application/json; charset=utf-8',status)
    def post(self):
        path=self.path.split('?',1)[0]
        if path in ('/local-gifts/preview','/local-gifts/redeem'):
            data={}
            try:
                length=int(self.headers.get('Content-Length','0'))
                if not 0<length<=4*1024*1024:raise GiftError('请求长度无效。')
                data=json.loads(self.rfile.read(length).decode('utf-8-sig'))
                if not isinstance(data,dict):raise GiftError('请求格式无效。')
                if path.endswith('/preview'):result=store.preview(data.get('code'),data.get('slot'))
                else:result=store.redeem(data.get('token'),data.get('save'))
                response(self,result)
            except (GiftError,ValueError,KeyError,TypeError,OSError) as error:
                unresolved=os.path.exists(store.journal)
                if path.endswith('/redeem'):
                    try:unresolved=unresolved or any(c.get('request')==data.get('token') for c in store.read()['claims'])
                    except Exception:unresolved=True
                response(self,{'ok':False,'error':str(error),'unresolved':unresolved})
            return
        # Serialize ordinary game/modifier saves with redemption commits and
        # complete interrupted transactions before accepting new writes.
        if path in ('/save','/vip'):
            with store.mutex,StoreLock(store.lockfile):store.recover();return original_post(self)
        return original_post(self)
    def get(self):
        if self.path.split('?',1)[0]=='/local-gifts/status':
            try:
                with store.mutex,StoreLock(store.lockfile):
                    store.recover();state=store.read()
                    response(self,{'ok':True,'version':1,'root':store.root,'configuredCodes':len(state['codes'])})
            except (GiftError,OSError,ValueError) as error:response(self,{'ok':False,'error':str(error)},400)
            return
        if self.path.split('?',1)[0] in ('/save','/vip'):
            with store.mutex,StoreLock(store.lockfile):store.recover();return original_get(self)
        return original_get(self)
    handler.do_POST=post;handler.do_GET=get
    return store
