import marshal,sys
sys.path.insert(0,'../manifest-case-fix/lib')
import json
Code=type(compile('','','exec'));counts={'adapter':0,'template':0}
source='''
def Popen(self, command, *args, **kwargs):
    game = isinstance(command, (tuple, list)) and command and _same_path(command[0], RUFFLE)
    if game:
        command = list(command)
        if '-p' in command:
            command[command.index('-p') + 1] = 'high'
        else:
            command[1:1] = ['-p', 'high']
        command[1:1] = ['--scale', 'show-all', '--force-scale', '--align', 'center', '--force-align', '--letterbox', 'on']
        environment = dict(kwargs.get('env') or os.environ)
        environment['RUST_LOG'] = 'error,ruffle_desktop::gui::controller=info'
        kwargs['env'] = environment
    process = subprocess.Popen(command, *args, **kwargs)
    if game:
        helper = os.path.join(PKG, 'runtime', 'window-helper.exe')
        try:
            if not os.path.isfile(helper):
                raise RuntimeError('window-helper.exe is missing')
            control = subprocess.Popen([helper, str(process.pid), '--attach', os.path.join(PKG, 'logs', 'window.log')], creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0x08000000))
            result = control.wait(timeout=16)
            _log('window: proportional client 940:590; helper status=' + str(result))
        except Exception as error:
            _log('window.aspect.error: ' + str(error))
    return process
'''
replacement=next(c for c in compile(source,'game-window.py','exec').co_consts if isinstance(c,Code))
template='''; 造梦西游3 单机版设置
; 缩放=1：默认940x590；0.9：846x531；1.2：1128x708；0为屏幕92%自动尺寸。
; 拖动边框时宽高按940:590联动，画面等比显示；最大化使用保持比例的最大窗口。
缩放=1
标题=
玩家名字=
批次={BATCH}
'''
def transform(v):
 if isinstance(v,Code):
  if v.co_name=='Popen':counts['adapter']+=1;return replacement
  return v.replace(co_consts=tuple(transform(x) for x in v.co_consts))
 if isinstance(v,bytes) and len(v)>300 and v[0]&127==ord('c'):
  try:old=marshal.loads(v)
  except (ValueError,TypeError,EOFError):return v
  if isinstance(old,Code):return marshal.dumps(transform(old))
 if isinstance(v,tuple):return tuple(transform(x) for x in v)
 if isinstance(v,str) and '缩放=' in v and '标题=' in v and '\n' in v:counts['template']+=1;return template
 return v
old=marshal.load(open('starter.marshal','rb'));new=transform(old)
assert counts['adapter']==1 and counts['template']>=1,counts
def verify(a,b):
 if isinstance(a,Code):
  if a.co_name=='Popen':
   assert a.co_argcount==b.co_argcount and a.co_freevars==b.co_freevars
  else:
   assert a.co_code==b.co_code and a.co_names==b.co_names and a.co_varnames==b.co_varnames
   assert len(a.co_consts)==len(b.co_consts)
   for x,y in zip(a.co_consts,b.co_consts):verify(x,y)
 elif isinstance(a,bytes) and len(a)>300 and a[0]&127==ord('c'):
  try:c=marshal.loads(a)
  except (ValueError,TypeError,EOFError):assert a==b;return
  if isinstance(c,Code):verify(c,marshal.loads(b))
  else:assert a==b
 elif isinstance(a,tuple):
  assert len(a)==len(b)
  for x,y in zip(a,b):verify(x,y)
 elif not (isinstance(a,str) and '缩放=' in a and '标题=' in a and '\n' in a):assert a==b
verify(old,new)
marshal.dump(new,open('starter-patched.marshal','wb'))
class Proc:
 def __init__(self,pid):self.pid=pid
 def wait(self,timeout=None):return 0
class Subprocess:
 def __init__(self):self.commands=[]
 def Popen(self,command,*args,**kwargs):self.commands.append(list(command));return Proc(1000+len(self.commands))
class Path:
 def join(self,*parts):return '/'.join(parts)
 def isfile(self,path):return True
class OS:
 environ={};path=Path()
subprocess=Subprocess();logs=[]
env={'__builtins__':__builtins__,'subprocess':subprocess,'os':OS(),'PKG':'fixture','RUFFLE':'ruffle.exe','_same_path':lambda a,b:a==b,'_log':logs.append}
fn=type(lambda:0)(replacement,env);process=fn(None,['ruffle.exe','game.swf'])
command=subprocess.commands[0]
assert command[command.index('--scale')+1]=='show-all' and command[command.index('--letterbox')+1]=='on'
assert subprocess.commands[1][0].endswith('window-helper.exe') and subprocess.commands[1][1]==str(process.pid)
assert logs[-1].endswith('status=0')
fn(None,['backend.exe']);assert len(subprocess.commands)==3
report={'status':'PASS','changes':counts,'proportionalScale':True,'nativeWindowHelperStarted':True,'backendLaunchUnchanged':True,'command':command,'helperCommand':subprocess.commands[1]}
json.dump(report,open('launcher-qa.json','w',encoding='utf-8'),ensure_ascii=False,indent=2);print(json.dumps(report))
