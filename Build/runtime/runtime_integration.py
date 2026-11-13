"""HTTP compatibility adapter: one storage coordinator and owned-process status."""
import os,json,re,time,hashlib,threading,logging
from storage_tx import StoreLock,StorageError,encode
from runtime_state import RuntimeState,CorrelationFilter,context
from resource_versions import ResourceIndex

def attach(handler,store,provider=None,start_monitor=True):
    runtime=RuntimeState(store.root,provider,start_monitor);runtime.log=store.log;store.runtime=runtime
    store.game_versions={}
    resources=ResourceIndex(store.root,store.log);store.resources=resources
    for output in store.log.handlers:
        for previous in list(output.filters):
            if isinstance(previous,CorrelationFilter):output.removeFilter(previous)
        output.addFilter(CorrelationFilter(runtime))
        output.setFormatter(logging.Formatter('%(asctime)s %(levelname)s service=%(serviceId)s game=%(gameId)s req=%(requestId)s component=%(component)s %(message)s'))
    store.log.info('service.started pid=%s storageProtocol=2 diagnostics=6 rules=%s',os.getpid(),store.rules.status()['sha256'])
    if runtime.problem:store.log.error('runtime.monitor.unavailable reason=%s',runtime.problem)
    runtime.refresh()
    old_get,old_post,old_headers=handler.do_GET,handler.do_POST,handler.end_headers
    def response(self,value,status=200):self._send(encode(value),'application/json; charset=utf-8',status)
    def read_request(self):
        length=int(self.headers.get('Content-Length','0'))
        if not 0<length<=4*1024*1024:raise StorageError('请求长度无效。')
        data=json.loads(self.rfile.read(length).decode('utf-8-sig'))
        if not isinstance(data,dict):raise StorageError('请求必须为JSON对象。')
        return data
    def relative(self,path):
        if path=='/vip':return 'saves/vip.json'
        match=re.search(r'(?:\?|&)slot=([0-5])(?:&|$)',self.path)
        if not match:raise StorageError('请选择0–5号存档。')
        return 'saves/slot%s.json'%match.group(1)
    def current_hash(relative):
        raw=store.storage.read(store.storage.path(relative))
        return hashlib.sha256(raw).hexdigest() if raw is not None else None
    def headers(self):
        self.send_header('X-Zm3-Request-Id',getattr(context,'request','-'))
        self.send_header('X-Zm3-Service-Session',runtime.service_id)
        self.send_header('X-Zm3-Game-Session',getattr(context,'game','-'))
        return old_headers(self)
    def post(self):
        runtime.begin_request(self);path=self.path.split('?',1)[0];began=time.monotonic()
        try:
            if path=='/resources/verify':response(self,resources.verify());return
            if path=='/storage/notify':
                data=read_request(self);files=data.get('files')
                if not isinstance(files,list) or not 1<=len(files)<=2:raise StorageError('保存确认文件无效。')
                seen=set()
                with store.mutex,StoreLock(store.lockfile):
                    store.recover()
                    for item in files:
                        name=item.get('path','')
                        if not re.fullmatch(r'saves/(?:slot[0-5]|vip)\.json',name) or name in seen:raise StorageError('保存确认路径无效。')
                        seen.add(name)
                        if not re.fullmatch(r'[a-fA-F0-9]{64}',item.get('sha256','')) or current_hash(name)!=item['sha256'].lower():raise StorageError('文件已在保存后被更新，请刷新后核对。')
                status=runtime.status();status.update(saved=True,needsReload=status['gameRunning'] is True and any(n!='saves/vip.json' for n in seen),vipAwaitPolling=status['gameRunning'] is True and 'saves/vip.json' in seen)
                store.log.info('storage.notified files=%s gameRunning=%s needsReload=%s elapsedMs=%d',','.join(sorted(seen)),status['gameRunning'],status['needsReload'],(time.monotonic()-began)*1000)
                response(self,status);return
            if path in ('/save','/vip'):
                name=relative(self,path);previous=store.game_versions.get(name);data=read_request(self)
                with store.mutex,StoreLock(store.lockfile):
                    store.recover();before=store.storage.read(store.storage.path(name))
                    expected=self.headers.get('If-Match')
                    if expected and (before is None or hashlib.sha256(before).hexdigest()!=expected.strip('"').lower()):raise StorageError('存档版本冲突，请重新读取存档。')
                    if path=='/save':data=store.prepare_save(int(name[10]),data)
                    after=encode(data)
                    # Legacy game requests have no If-Match header. Remember the last
                    # snapshot served to this game; outside edits require an actual read.
                    if getattr(context,'component','game')=='game' and previous is not None and before is not None and previous!=hashlib.sha256(before).hexdigest():
                        current=json.loads(before.decode('utf-8-sig'));new_game=path=='/save' and data.get('_localSaveId')!=current.get('_localSaveId')
                        if data!=current and not new_game:raise StorageError('存档已被修改器更新，请回到选档界面重新读取后继续保存。')
                    identifier=store.storage.commit([dict(path=name,before=before.hex() if before is not None else None,after=after.decode('utf-8'))],'game',getattr(context,'request',None))
                    store.game_versions[name]=hashlib.sha256(after).hexdigest()
                store.log.info('save.write path=%s transaction=%s elapsedMs=%d',path,identifier,(time.monotonic()-began)*1000)
                self._send(b'vip saved' if path=='/vip' else b'saved slot','text/plain; charset=utf-8',200);return
            return old_post(self)
        except (StorageError,ValueError,TypeError,KeyError) as error:
            store.log.warning('http.rejected path=%s elapsedMs=%d error=%s',path,(time.monotonic()-began)*1000,str(error));response(self,dict(ok=False,error=str(error)),409)
        except Exception:
            store.log.exception('http.failed path=%s elapsedMs=%d',path,(time.monotonic()-began)*1000);response(self,dict(ok=False,error='保存服务发生异常，事务已保留，请查看游戏日志。'),500)
        finally:context.request='-';context.game='-';context.component='service'
    def get(self):
        runtime.begin_request(self);path=self.path.split('?',1)[0]
        try:
            if path=='/runtime/status':
                status=runtime.status();status['resources']=resources.status();status['rules']=store.rules.status();response(self,status);return
            if path=='/rules/status':response(self,store.rules.status());return
            if path=='/resources/status':response(self,resources.status());return
            if resources.serve(self):return
            if path in ('/save','/vip'):
                name=relative(self,path)
                with store.mutex,StoreLock(store.lockfile):
                    store.recover();result=old_get(self);store.game_versions[name]=current_hash(name);return result
            return old_get(self)
        except Exception:
            store.log.exception('http.get.failed path=%s',path);raise
        finally:context.request='-';context.game='-';context.component='service'
    handler.do_POST=post;handler.do_GET=get;handler.end_headers=headers
    return runtime
