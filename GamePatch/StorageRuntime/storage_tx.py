"""Shared on-disk transaction protocol, compatible with the WPF editor (Python 3.8)."""
import os,json,re,time,threading
from game_paths import gift_state,within

class StorageError(Exception):pass

def encode(value):return json.dumps(value,ensure_ascii=False,separators=(',',':')).encode('utf-8')

def atomic(path,data):
    os.makedirs(os.path.dirname(path),exist_ok=True)
    temporary=path+'.'+os.urandom(8).hex()+'.tmp'
    try:
        with open(temporary,'xb') as out:out.write(data);out.flush();os.fsync(out.fileno())
        os.replace(temporary,path)
    finally:
        if os.path.exists(temporary):os.remove(temporary)

class StoreLock:
    def __init__(self,path,timeout=4):self.path=path;self.timeout=timeout;self.file=None
    def __enter__(self):
        import msvcrt
        os.makedirs(os.path.dirname(self.path),exist_ok=True)
        self.file=open(self.path,'a+b')
        if os.path.getsize(self.path)==0:self.file.write(b'0');self.file.flush()
        end=time.monotonic()+self.timeout
        while True:
            try:self.file.seek(0);msvcrt.locking(self.file.fileno(),msvcrt.LK_NBLCK,1);return self
            except OSError:
                if time.monotonic()>end:self.file.close();raise StorageError('存档正在使用，请稍后重试。')
                time.sleep(.04)
    def __exit__(self,*args):
        import msvcrt
        self.file.seek(0);msvcrt.locking(self.file.fileno(),msvcrt.LK_UNLCK,1);self.file.close()

class StorageCoordinator:
    def __init__(self,root,log=None,writer=None):
        self.root=os.path.abspath(root);self.log=log;self.writer=writer or atomic
        self.journal=os.path.join(self.root,'tools','local_gifts.transaction.json')
        self.lockfile=os.path.join(self.root,'tools','local_gifts.lock');self.mutex=threading.RLock()
    def path(self,relative):
        if not re.fullmatch(r'(?:saves/(?:slot[0-5]|vip)\.json|(?:tools|saves/gifts)/local_gifts\.json)',relative):raise StorageError('事务文件路径无效。')
        path=gift_state(self.root) if relative in ('tools/local_gifts.json','saves/gifts/local_gifts.json') else within(self.root,relative)
        if not os.path.normcase(path).startswith(os.path.normcase(self.root+os.sep)):raise StorageError('事务文件超出游戏目录。')
        return path
    @staticmethod
    def read(path):
        if not os.path.exists(path):return None
        with open(path,'rb') as source:return source.read()
    def plan(self,items):
        if not isinstance(items,list) or not 1<=len(items)<=8:raise StorageError('事务文件数量无效。')
        result=[];seen=set()
        for item in items:
            if not isinstance(item,dict):raise StorageError('事务记录无效。')
            relative=item.get('path','');path=self.path(relative)
            if os.path.normcase(path) in seen:raise StorageError('事务文件重复。')
            seen.add(os.path.normcase(path))
            before=item.get('before');after=item.get('after')
            if before is not None and (not isinstance(before,str) or len(before)>16*1024*1024 or not re.fullmatch(r'(?:[0-9a-fA-F]{2})*',before)):raise StorageError('事务原始数据无效。')
            if not isinstance(after,str) or len(after)>8*1024*1024:raise StorageError('事务写入数据无效。')
            try:
                if not isinstance(json.loads(after.lstrip('\ufeff')),dict):raise ValueError()
            except (ValueError,TypeError):raise StorageError('事务写入内容必须为JSON对象。')
            result.append((relative,path,bytes.fromhex(before) if before is not None else None,after.encode('utf-8')))
        return result
    def recover(self,replay=True):
        """Caller owns both the process mutex and the shared file lock."""
        if not os.path.exists(self.journal):return False
        with open(self.journal,encoding='utf-8-sig') as source:transaction=json.load(source)
        version=transaction.get('version',1)
        if version==2:items=transaction.get('files')
        elif version==1:
            items=list(transaction.get('files',[]))
            for item in items:
                if item.get('before')=='' and self.read(self.path(item.get('path',''))) is None:item['before']=None
            state=transaction.get('state')
            if not isinstance(state,dict):raise StorageError('旧事务缺少领取记录。')
            # Legacy receipts did not record the previous state bytes. Preserve their
            # established roll-forward behavior while using the same recovery engine.
            current=self.read(self.path('tools/local_gifts.json'))
            items.append(dict(path='tools/local_gifts.json',before=current.hex() if current is not None else None,after=encode(state).decode('utf-8')))
        else:raise StorageError('不支持的事务版本，请保留事务文件。')
        plan=self.plan(items)
        # Validate EVERY file before changing ANY file, including replay after a crash.
        for relative,path,before,after in plan:
            if self.read(path) not in (before,after):raise StorageError('事务恢复发现外部修改：'+relative+'；请保留备份并刷新。')
        for relative,path,before,after in plan:
            if self.read(path)!=after:self.writer(path,after)
        os.remove(self.journal)
        if self.log:self.log.info('%s transaction=%s origin=%s files=%d','storage.recovered' if replay else 'storage.committed',transaction.get('id','legacy'),transaction.get('origin','gift-legacy'),len(plan))
        return True
    def commit(self,items,origin,request_id=None,backups=None):
        """Caller owns the common lock; completed journal is the commit decision."""
        self.recover();plan=self.plan(items);identifier=request_id or os.urandom(16).hex()
        for relative,path,before,after in plan:
            if self.read(path)!=before:raise StorageError('磁盘文件已被游戏或修改器更新，请刷新后重新编辑。')
        changed=[(relative,path,before,after) for relative,path,before,after in plan if before!=after]
        if not changed:return identifier
        for relative,path,before,after in changed:
            if before is None:continue
            backup=(backups or {}).get(relative)
            if backup is None:backup='saves/backups/%s-%s-%s.json'%(origin,os.path.basename(path)[:-5],identifier)
            if not re.fullmatch(r'saves/(?:backups|\.gm-backups)/[A-Za-z0-9_.-]+\.json',backup):raise StorageError('备份路径无效。')
            self.writer(os.path.join(self.root,*backup.split('/')),before)
        transaction=dict(version=2,id=identifier,origin=origin,files=[dict(path=r,before=b.hex() if b is not None else None,after=a.decode('utf-8')) for r,p,b,a in changed])
        self.writer(self.journal,encode(transaction))
        if self.log:self.log.info('storage.prepared transaction=%s origin=%s files=%d',identifier,origin,len(changed))
        self.recover(False)
        return identifier
