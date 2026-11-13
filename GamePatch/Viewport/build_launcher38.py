"""Run under bundled Python 3.8; preserve opcodes and every unrelated constant."""
import marshal,sys
sys.path.insert(0,'../manifest-case-fix/lib')
import json
Code=type(compile('','','exec'));counts={'scale':0,'template':0}
template='''; 造梦西游3 单机版设置
; 修改后重新双击「启动游戏.exe」生效。
; 缩放=1：940x590，默认较小窗口；0.9：846x531；1.2：1128x708。
; 缩放=0：自动适配屏幕的92%，过大尺寸会自动限制在屏幕内。
; 拖动或最大化时，画面随窗口宽高同时铺满显示。
缩放=1

; 标题留空时只显示游戏图标。
标题=
; 背包玩家昵称，留空显示本地玩家。
玩家名字=
批次={BATCH}
'''
def transform(v,context=''):
 if isinstance(v,Code):
  return v.replace(co_consts=tuple(transform(x,v.co_name) for x in v.co_consts))
 if isinstance(v,bytes) and len(v)>300 and v[0]&127==ord('c'):
  try:old=marshal.loads(v)
  except (ValueError,TypeError,EOFError):return v
  if isinstance(old,Code):return marshal.dumps(transform(old))
 if isinstance(v,tuple):return tuple(transform(x,context) for x in v)
 if isinstance(v,str):
  if context=='Popen':
   if v=='show-all':counts['scale']+=1;return 'exact-fit'
   if v=='on':return 'off'
   if 'window: centered show-all' in v:return 'window: exact-fit; forced scale; letterbox off'
  if '缩放=0' in v and '标题=' in v and '\n' in v:counts['template']+=1;return template
 return v
original=marshal.load(open('starter.marshal','rb'));patched=transform(original)
assert counts['scale']==1 and counts['template']>=1,counts
marshal.dump(patched,open('starter-patched.marshal','wb'))
functions={}
def walk(v):
 if isinstance(v,Code):
  functions[v.co_name]=v
  for c in v.co_consts:walk(c)
 elif isinstance(v,bytes) and len(v)>300 and v[0]&127==ord('c'):
  try:walk(marshal.loads(v))
  except (ValueError,TypeError,EOFError):pass
walk(patched)
class OS:
 environ={}
class Captures:
 def __init__(self):self.commands=[]
 def Popen(self,command,*a,**kw):self.commands.append(list(command));return 1
captures=Captures();logs=[]
env={'__builtins__':__builtins__,'subprocess':captures,'RUFFLE':'ruffle.exe','_same_path':lambda a,b:a==b,'os':OS,'_log':logs.append}
popen=type(lambda:0)(functions['Popen'],env)
popen(None,['ruffle.exe','game.swf'])
cmd=captures.commands[-1]
assert cmd[cmd.index('--scale')+1]=='exact-fit' and cmd[cmd.index('--letterbox')+1]=='off' and '--force-scale' in cmd
class Dummy:pass
env.update(STAGE_W=940,STAGE_H=590,user32=Dummy(),ctypes=Dummy())
pick=type(lambda:0)(functions['pick_size'],env)
sizes=[]
for area in [(1920,1040),(1366,728),(1280,680)]:
 env['work_area']=lambda area=area:area
 size=pick({'缩放':'1'});assert size[0]<=area[0]-32 and size[1]<=area[1]-64
 sizes.append({'area':area,'initial':size})
assert sizes[0]['initial']==(940,590)
result={'status':'PASS','constantChanges':counts,'command':cmd,'sizes':sizes,'opcodesPreserved':True}
json.dump(result,open('launcher-qa.json','w',encoding='utf-8'),ensure_ascii=False,indent=2);print(json.dumps(result))
