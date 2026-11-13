from pathlib import Path
import json,re
from PIL import Image
HERE=Path(__file__).resolve().parent
source=(HERE.parent/'activity-audit/redeem-details/scripts/export/huodong/miaomiaoBase/MiaoMiaoBaseInterface.as').read_text(encoding='utf-8')
def block(text,start):
 depth=0;quote=None;escape=False
 for i in range(start,len(text)):
  c=text[i]
  if quote:
   if escape:escape=False
   elif c=='\\':escape=True
   elif c==quote:quote=None
  elif c in '\"\'':quote=c
  elif c=='{':depth+=1
  elif c=='}':
   depth-=1
   if depth==0:return text[start+1:i]
functions={}
for m in re.finditer(r'function\s+(\w+)\([^)]*\)[^{]*\{',source):functions[m[1]]=block(source,m.end()-1)
dispatch=functions['getPresent'];branches=[]
for case in re.finditer(r'case\s+(\d+)\s*:',dispatch):
 call=re.search(r'this\.(\w+)\(([^)]*)\)',dispatch[case.end():])
 assert call,(case[1],'missing dispatch')
 branches.append({'result':int(case[1]),'function':call[1],'arguments':call[2].strip()})
assert len(branches)==766
titles={2:'首款专属礼包',11:'超值大礼包',22:'羊年新春礼包',27:'手机专属礼包',41:'王子软件礼包',45:'独家礼包第二期',49:'游戏盒百万礼包',53:'独家礼包第三期',57:'涂鸦画板礼包',61:'圈圈暑期礼包',65:'连送礼包',69:'手机助手周年礼包',77:'游戏盒每月签到礼包',83:'手游通六一礼包',87:'端午礼包',91:'新春礼包',95:'微信礼包',99:'新春年兽礼包',103:'微信专属礼包',107:'手游专属礼包',111:'超值大礼包',115:'粉丝福利礼包',119:'小伙伴礼包',123:'周常礼包',127:'手机客户端礼包',131:'国庆礼包',135:'国庆礼包',139:'双旦礼包',143:'手机助手五一礼包',147:'人参果礼包',167:'百万礼包摇摇乐',171:'百万礼包暴击礼包',177:'快乐家园益益礼包',181:'快乐家园奇异礼包',185:'相伴十年礼包',189:'系列联动礼包',193:'2015年新春礼包',197:'羊年礼包',201:'唐僧系统礼包',205:'PK礼包',209:'欢乐无穷礼包',213:'嘉年华礼包',217:'手游前传礼包',221:'万圣节礼包',225:'元宵礼包',229:'九周年礼包',244:'欢喜迎新年礼包',248:'暑期礼包',253:'小游戏大众礼包',257:'主站端午礼包',261:'签到礼包',265:'主站五一礼包',269:'公会礼包',273:'暑假福利礼包',277:'桌面签到礼包',281:'游戏盒百万礼包',285:'独家礼包第八期',289:'独家礼包第九期',293:'国庆礼包',297:'微信礼包',301:'中秋节礼包',305:'开学礼包',309:'夏日游泳礼包',313:'超值福利礼包',317:'微信五一礼包',321:'爆笑五一礼包',325:'龙族大礼包',329:'独家礼包第十七期',333:'微信元宵礼包',337:'海绵宝宝礼包',341:'游府海量礼包',345:'微信爆笑礼包',349:'哆啦A梦礼包',353:'微信钓鱼礼包',357:'双十一礼包',361:'万圣节礼包',365:'微信超级龙礼包',369:'小黄人礼包',373:'微信游内涵礼包',377:'沙僧表情礼包',381:'捕捉精灵礼包',385:'夏日礼包',389:'情怀礼包',393:'小游戏超值礼包',397:'精彩暑期礼包',401:'游戏盒六一礼包',405:'造梦工作室礼包',409:'造梦工作室龙年礼包',413:'奥特曼礼包',417:'端午节微信礼包',421:'跨年狂欢礼包',425:'比巴卜玩乐礼包',429:'小游戏超值礼包',433:'充值福利礼包',441:'微信礼包',445:'藏经阁礼包',449:'星斗群仙礼包',467:'七周年礼包',471:'微信礼包',475:'中秋节礼包',479:'十周年礼包',483:'猴年大礼包',487:'十周年主题礼包',492:'十周年礼包',495:'礼包第68期',499:'作品书礼包',503:'劳动节礼包',547:'云游戏礼包',551:'游戏盒新春礼包',555:'四周年礼包',559:'游泳礼包',563:'小游戏七周年礼包',592:'绘画活动礼包',596:'十四周年礼包',600:'十二周年礼包',604:'12周年礼包',608:'微信礼包',615:'中秋礼包',619:'快乐家园礼包',623:'造梦大礼包',627:'圣诞节礼包',631:'百万礼包第四期',635:'游哒盒专属礼包',639:'城市嘉年华礼包',643:'明星宠物礼包',649:'万圣节礼包',653:'十周年主题礼包',657:'一卡通礼包'}
posters=[]
for p in sorted((HERE/'images').glob('*'),key=lambda p:int(p.stem)):
 if p.suffix.lower() not in ('.png','.jpg'):continue
 im=Image.open(p)
 if im.width<500 or not 390<=im.height<=465 or int(p.stem) in (507,433):continue
 id=int(p.stem);out=HERE/'stage/icons/activity-gifts'/f'{id}.png';im.convert('RGBA').save(out)
 posters.append({'id':id,'name':titles.get(id,'历史礼包 '+str(id)),'codes':[]})
for p in posters:
 if p['id']==2:p['name']='雀巢笨nana礼包'
# Keep the two original entrance posters and the custom-code card.
for id,file,name in [(519,'519.png','积分神秘礼包'),(524,'524.png','游戏盒签到礼包')]:
 Image.open(HERE/'images'/file).save(HERE/'stage/icons/activity-gifts'/f'{id}.png');posters.insert(0,{'id':id,'name':name,'codes':[]})
codes=[]
for row in branches:
 body=functions[row['function']];arg=row['arguments'];chosen=body
 if arg.isdigit():
  switch=re.search(r'switch\(\s*\w+\s*(?:-\s*(\d+))?\s*\)',body)
  if switch:
   case=int(arg)-int(switch[1] or 0);m=re.search(r'case\s+'+str(case)+r'\s*:',body[switch.end():])
   if m:
    start=switch.end()+m.end();end=re.search(r'\n\s*(?:case\s+\d+\s*:|default\s*:)',body[start:]);chosen=body[start:start+end.start()] if end else body[start:]
 toasts=re.findall(r'gc\.ts\.setTxt\("([^"\n]+)',chosen)
 description=next((t for t in toasts if any(x in t for x in ('领取','获得','礼包','赠送'))),toasts[0] if toasts else '原版礼包奖励')
 description=re.sub(r'<[^>]*>','',description).replace('成功领取','').replace('成功获得','').replace('成功获得了','').strip(' ，:：')
 name=description[:48] or '原版礼包奖励'
 rid=row['result'];item_ids=re.findall(r'(?:findByName|getSomeOneEquipNumberByName)\("([^"\n]+)"|putQhsInBackPack\([^,]+,"([^"\n]+)"',chosen)
 first=next((a or b for a,b in item_ids),'')
 codes.append({'id':'legacy-'+str(rid),'code':'ZM3-'+str(rid),'name':name,'description':description,'enabled':True,'rule':'once','perSaveLimit':1,'totalLimit':0,'expires':'','target':'all','legacyFunction':row['function'],'legacyArguments':arg,'rewards':[{'kind':'legacy','id':str(rid),'name':name,'count':1,'icon':first}]})
def preferred(poster,code):
 fn=code['legacyFunction'].lower();name=poster['name']
 if poster['id']==519:return fn=='getshenmipresent'
 if poster['id'] in (77,524,261,277):return 'sign' in fn or 'qiandao' in fn
 words={'端午':['duanwu','dragonboat'],'中秋':['zhongqiu','midautumn','doublefestival'],'新春':['newyear','nian','newyears'],'国庆':['national','guoqing'],'周年':['year','zhounian','zaompresent'],'暑':['summer','xiari'],'万圣':['halloween','wansheng'],'微信':['weixin'],'六一':['children','child'],'五一':['wuyi','labor'],'年兽':['nianshou'],'人参果':['renshen','ginseng'],'七夕':['valentine','qixi'],'圣诞':['christmas'],'手机':['clientphone','phone'],'小黄人':['minion','biba'],'年礼包':['newyear']}
 if poster['id']==127:return fn=='getclientphonepresent'
 if poster['id'] in (193,197,99):return fn=='getnianshoupresent'
 return any(key in name and any(w in fn for w in options) for key,options in words.items())
for key,name,description in [('GBX','游戏盒新手礼包','慈心妙莲、土灵珠×1、雷灵珠×1、火灵珠×1、一级强化石×3、龙女的眼泪×10'),('YYL','游戏盒签到基础礼包','4399手机玩家头衔、土灵珠碎片、一级仙女石、一级攻击石各1，世界传送次数×2')]:
 codes.append({'id':'legacy-'+key,'code':'ZM3-'+key,'name':name,'description':description,'enabled':True,'rule':'once','perSaveLimit':1,'totalLimit':0,'expires':'','target':'all','legacyFunction':key,'legacyArguments':'','rewards':[{'kind':'legacy','id':key,'name':name,'count':1,'icon':'_4399ptsjwj' if key=='YYL' else 'hehua'}]})
for p in posters:
 p['codes']=[c['id'] for c in codes if preferred(p,c)]
 if p['id'] in (77,524):p['codes'].insert(0,'legacy-YYL')
 if p['id'] in (615,301,475):p['codes']=['legacy-2846']+[v for v in p['codes'] if v!='legacy-2846']
 if p['id']==2:p['codes']=[c['id'] for c in codes if any(word in c['description'] for word in ('朱雀','神秘商人装','神秘商人时装','至尊礼包'))]
posters.append({'id':0,'name':'全部礼包与自定义','codes':[c['id'] for c in codes]})
order=[519,524,127,193,615]
posters.sort(key=lambda p:(0,order.index(p['id'])) if p['id'] in order else (1,p['id'] if p['id'] else 9999))
catalog={'version':1,'posters':posters,'codes':codes}
(HERE/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'posters':len(posters),'rewardCodes':len(codes),'uniqueRewardIds':len({c['id'] for c in codes})}))
