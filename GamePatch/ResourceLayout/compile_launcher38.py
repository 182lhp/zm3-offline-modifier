import os,sys,marshal,types
sys.path.insert(0,os.path.abspath('../manifest-case-fix/lib'))
import json
Code=types.CodeType
source='\ndef Popen(self, command, *args, **kwargs):\n    game = isinstance(command, (tuple, list)) and command and _same_path(command[0], RUFFLE)\n    if game:\n        import sys\n        tools = os.path.join(PKG, "tools")\n        if tools not in sys.path:sys.path.insert(0, tools)\n        from resource_versions import launch_command\n        command = launch_command(PKG, command)\n        if \'-p\' in command:\n            command[command.index(\'-p\') + 1] = \'high\'\n        else:\n            command[1:1] = [\'-p\', \'high\']\n        command[1:1] = [\'--scale\', \'show-all\', \'--force-scale\', \'--align\', \'center\', \'--force-align\', \'--letterbox\', \'on\']\n        environment = dict(kwargs.get(\'env\') or os.environ)\n        environment[\'RUST_LOG\'] = \'error,ruffle_desktop::gui::controller=info\'\n        kwargs[\'env\'] = environment\n    process = subprocess.Popen(command, *args, **kwargs)\n    if game:\n        helper = os.path.join(PKG, \'runtime\', \'window-helper.exe\')\n        try:\n            if not os.path.isfile(helper):\n                raise RuntimeError(\'window-helper.exe is missing\')\n            control = subprocess.Popen([helper, str(process.pid), \'--attach\', os.path.join(PKG, \'logs\', \'window.log\')], creationflags=getattr(subprocess, \'CREATE_NO_WINDOW\', 0x08000000))\n            result = control.wait(timeout=16)\n            _log(\'window: proportional client 940:590; helper status=\' + str(result))\n        except Exception as error:\n            _log(\'window.aspect.error: \' + str(error))\n    return process\n'
replacement=next(c for c in compile(source,'resource-launch.py','exec').co_consts if isinstance(c,Code))
count=[0]
def transform(v):
 if isinstance(v,Code):
  if v.co_name=='Popen':count[0]+=1;return replacement
  return v.replace(co_consts=tuple(transform(x) for x in v.co_consts))
 if isinstance(v,bytes) and len(v)>300 and v[0]&127==ord('c'):
  try:c=marshal.loads(v)
  except (ValueError,TypeError,EOFError):return v
  if isinstance(c,Code):return marshal.dumps(transform(c))
 if isinstance(v,tuple):return tuple(transform(x) for x in v)
 return v
old=marshal.load(open('starter.marshal','rb'));new=transform(old);assert count[0]==1,count
unchanged=[0]
def verify(a,b):
 if isinstance(a,Code):
  if a.co_name=='Popen':assert a.co_argcount==b.co_argcount and a.co_freevars==b.co_freevars;return
  unchanged[0]+=1
  assert a.co_code==b.co_code and a.co_names==b.co_names and a.co_varnames==b.co_varnames and len(a.co_consts)==len(b.co_consts)
  for x,y in zip(a.co_consts,b.co_consts):verify(x,y)
 elif isinstance(a,bytes) and len(a)>300 and a[0]&127==ord('c'):
  try:c=marshal.loads(a)
  except (ValueError,TypeError,EOFError):assert a==b;return
  if isinstance(c,Code):verify(c,marshal.loads(b))
  else:assert a==b
 elif isinstance(a,tuple):
  assert len(a)==len(b)
  for x,y in zip(a,b):verify(x,y)
 else:assert a==b
verify(old,new);marshal.dump(new,open('starter-patched.marshal','wb'))
json.dump(dict(status='PASS',adapterChanges=count[0],otherMethodsUnchanged=unchanged[0]),open('launcher-build.json','w'),indent=2)
print('Resource launcher adapter compiled; other methods preserved:',unchanged[0])

# Exercise the actual patched code object with a fake process launcher.
resource_versions=types.ModuleType('resource_versions');resource_versions.launch_command=lambda root,command:command
sys.modules['resource_versions']=resource_versions
calls=[]
class Proc:
 pid=777
 def wait(self,timeout=None):return 0
class Sub:
 def Popen(self,command,*args,**kwargs):calls.append((command,kwargs));return Proc()
original=resource_versions.launch_command
exists=os.path.isfile;os.path.isfile=lambda path:True
resource_versions.launch_command=lambda root,command:list(command)+['resource-version-probe']
try:
 logs=[];env=dict(__builtins__=__builtins__,subprocess=Sub(),os=os,PKG=os.getcwd(),RUFFLE='ruffle.exe',_same_path=lambda a,b:a==b,_log=logs.append)
 fn=types.FunctionType(replacement,env)
 fn(None,['ruffle.exe','--scale','show-all','--letterbox','on','--width','940','--height','590','http://127.0.0.1:43999/launcher.swf'])
 assert calls[0][0][-1]=='resource-version-probe' and calls[0][0][calls[0][0].index('-p')+1]=='high'
 assert calls[1][0][0].endswith('window-helper.exe') and calls[1][0][1]=='777'
 fn(None,['backend.exe']);assert calls[2][0]==['backend.exe']
 report=dict(status='PASS',adapterChanges=count[0],otherMethodsUnchanged=unchanged[0],qualityAndAspectPreserved=True,nativeHelperPreserved=True,backendLaunchUnchanged=True)
 json.dump(report,open('launcher-build.json','w'),indent=2)
finally:resource_versions.launch_command=original;os.path.isfile=exists
