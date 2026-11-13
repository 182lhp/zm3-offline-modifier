"""Owned Ruffle process lifetime and correlation context; never terminates a process."""
import os,time,json,threading,logging,re
from storage_tx import StoreLock,atomic,encode
context=threading.local()

class CorrelationFilter(logging.Filter):
    def __init__(self,service):super().__init__();self.service=service
    def filter(self,record):
        record.serviceId=self.service.service_id;record.gameId=getattr(context,'game','-')
        record.requestId=getattr(context,'request','-');record.component=getattr(context,'component','service')
        return True

class WindowsProcesses:
    def __init__(self,root):
        import ctypes as c
        class w:
            DWORD=c.c_uint32;BOOL=c.c_int;HANDLE=c.c_void_p;LPWSTR=c.c_wchar_p;LONG=c.c_int32;WCHAR=c.c_wchar
            class FILETIME(c.Structure):_fields_=[('dwLowDateTime',c.c_uint32),('dwHighDateTime',c.c_uint32)]
        self.c=c;self.w=w;self.kernel=c.WinDLL('kernel32',use_last_error=True)
        self.expected=os.path.normcase(os.path.realpath(os.path.join(root,'runtime','ruffle.exe')))
        class Entry(c.Structure):
            _fields_=[('dwSize',w.DWORD),('cntUsage',w.DWORD),('th32ProcessID',w.DWORD),('th32DefaultHeapID',c.c_size_t),('th32ModuleID',w.DWORD),('cntThreads',w.DWORD),('th32ParentProcessID',w.DWORD),('pcPriClassBase',w.LONG),('dwFlags',w.DWORD),('szExeFile',w.WCHAR*260)]
        self.Entry=Entry
        k=self.kernel
        k.CreateToolhelp32Snapshot.argtypes=[w.DWORD,w.DWORD];k.CreateToolhelp32Snapshot.restype=w.HANDLE
        k.Process32FirstW.argtypes=[w.HANDLE,c.POINTER(Entry)];k.Process32NextW.argtypes=[w.HANDLE,c.POINTER(Entry)]
        k.OpenProcess.argtypes=[w.DWORD,w.BOOL,w.DWORD];k.OpenProcess.restype=w.HANDLE
        k.QueryFullProcessImageNameW.argtypes=[w.HANDLE,w.DWORD,w.LPWSTR,c.POINTER(w.DWORD)]
        k.GetProcessTimes.argtypes=[w.HANDLE,c.POINTER(w.FILETIME),c.POINTER(w.FILETIME),c.POINTER(w.FILETIME),c.POINTER(w.FILETIME)]
        k.GetExitCodeProcess.argtypes=[w.HANDLE,c.POINTER(w.DWORD)];k.CloseHandle.argtypes=[w.HANDLE]
        k.WaitForSingleObject.argtypes=[w.HANDLE,w.DWORD]
    def list(self):
        c,w,k=self.c,self.w,self.kernel;result=[];snapshot=k.CreateToolhelp32Snapshot(2,0)
        if snapshot in (None,c.c_void_p(-1).value):raise OSError('Cannot inspect process table')
        try:
            entry=self.Entry();entry.dwSize=c.sizeof(entry);more=k.Process32FirstW(snapshot,c.byref(entry))
            while more:
                if entry.szExeFile.lower()=='ruffle.exe':
                    handle=k.OpenProcess(0x100000|0x1000,False,entry.th32ProcessID)
                    if not handle and c.get_last_error()==5:
                        for previous in result:self.close(previous)
                        raise PermissionError('Cannot verify a Ruffle process owner')
                    if handle:
                        text=c.create_unicode_buffer(32768);size=w.DWORD(len(text));times=[w.FILETIME() for _ in range(4)]
                        valid=k.QueryFullProcessImageNameW(handle,0,text,c.byref(size)) and os.path.normcase(os.path.realpath(text.value))==self.expected and k.GetProcessTimes(handle,*[c.byref(t) for t in times])
                        if valid:
                            created=(times[0].dwHighDateTime<<32)|times[0].dwLowDateTime
                            result.append(dict(pid=int(entry.th32ProcessID),created=created,startedAt=(created/10000000)-11644473600,handle=handle))
                        else:k.CloseHandle(handle)
                more=k.Process32NextW(snapshot,c.byref(entry))
        finally:k.CloseHandle(snapshot)
        return result
    def exited(self,item):
        code=self.w.DWORD()
        if self.kernel.WaitForSingleObject(item['handle'],0)!=0:return None
        return int(code.value) if self.kernel.GetExitCodeProcess(item['handle'],self.c.byref(code)) else -1
    def close(self,item):self.kernel.CloseHandle(item['handle'])

class RuntimeState:
    def __init__(self,root,provider=None,start=True):
        self.root=os.path.abspath(root);self.service_id=os.urandom(16).hex();self.started=time.time()
        self.lock=threading.RLock();self.games={};self.last_exit=None;self.problem=None;self.stop_event=threading.Event();self.log=None
        try:self.provider=provider or WindowsProcesses(root)
        except Exception as error:self.provider=None;self.problem=type(error).__name__+':'+str(getattr(error,'name',''))
        self.err_offset=self.err_size();self.last_error_at=0
        if start:
            self.thread=threading.Thread(target=self.watch,name='owned-game-watch',daemon=True);self.thread.start()
    def err_size(self):
        try:return os.path.getsize(os.path.join(self.root,'logs','runtime.err.log'))
        except OSError:return 0
    def refresh(self):
        if self.provider is None:return
        with self.lock:
            try:
                discovered=self.provider.list();self.problem=None
                for item in discovered:
                    key='%s:%s'%(item['pid'],item['created'])
                    if key in self.games:self.provider.close(item);continue
                    item.update(sessionId=os.urandom(16).hex(),errStart=self.err_offset);self.games[key]=item
                    if self.log:
                        old=(getattr(context,'game','-'),getattr(context,'component','service'));context.game=item['sessionId'];context.component='engine'
                        try:self.log.info('game.started pid=%s startedAt=%s',item['pid'],item['startedAt'])
                        finally:context.game,context.component=old
                for key,item in list(self.games.items()):
                    code=self.provider.exited(item)
                    if code is None:continue
                    self.last_exit=dict(sessionId=item['sessionId'],pid=item['pid'],exitCode=code,exitedAt=time.time())
                    if self.log:
                        old=(getattr(context,'game','-'),getattr(context,'component','service'));context.game=item['sessionId'];context.component='engine'
                        try:
                            self.log.log(logging.ERROR if code else logging.INFO,'game.exited pid=%s exitCode=%s',item['pid'],code)
                            size=self.err_size()
                            if code and size>item['errStart']:
                                with open(os.path.join(self.root,'logs','runtime.err.log'),'rb') as source:
                                    source.seek(max(item['errStart'],size-8192));tail=source.read(8192).decode('utf-8',errors='replace')
                                self.log.error('game.stderr %s',json.dumps(tail,ensure_ascii=False))
                        finally:context.game,context.component=old
                    self.provider.close(item);del self.games[key]
                self.err_offset=self.err_size()
            except Exception as error:
                changed=self.problem!=type(error).__name__;self.problem=type(error).__name__
                if self.log and (changed or time.monotonic()-self.last_error_at>60):
                    self.log.exception('runtime.monitor.failed');self.last_error_at=time.monotonic()
    def watch(self):
        while not self.stop_event.wait(2):self.refresh()
    def status(self):
        self.refresh()
        with self.lock:
            games=[{k:v for k,v in item.items() if k in ('pid','sessionId','startedAt')} for item in self.games.values()]
            return dict(ok=True,storageProtocol=2,diagnostics=6,root=self.root,saves=os.path.join(self.root,'saves'),serviceSession=self.service_id,servicePid=os.getpid(),serviceStartedAt=self.started,gameRunning=bool(games) if self.problem is None else None,games=games,lastGameExit=self.last_exit,monitorError=self.problem)
    def game_id(self):
        with self.lock:return next(iter(self.games.values()))['sessionId'] if len(self.games)==1 else '-'
    def begin_request(self,handler):
        request=handler.headers.get('X-Zm3-Request-Id','')
        context.request=request if re.fullmatch(r'[A-Za-z0-9_-]{8,64}',request) else os.urandom(16).hex()
        context.game=self.game_id();context.component='modifier' if handler.headers.get('X-Zm3-Client')=='modifier' else 'game'
        return context.request
    def close(self):
        self.stop_event.set()
        with self.lock:
            for item in self.games.values():self.provider.close(item)
            self.games.clear()
