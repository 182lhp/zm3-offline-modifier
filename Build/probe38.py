"""Execute exact launcher integrity functions; never execute the launcher main."""
import os,json,marshal,types,hashlib
inputs=json.load(open('probe-inputs.json',encoding='utf-8'));found={}
def walk(value):
    if isinstance(value,types.CodeType):
        found[value.co_name]=value
        for child in value.co_consts:walk(child)
    elif isinstance(value,bytes) and len(value)>300 and value[0]&127==ord('c'):walk(marshal.loads(value))
walk(marshal.load(open('starter.marshal','rb')))
namespace=dict(__builtins__=__builtins__,os=os,hashlib=hashlib,PKG=inputs['root'])
for name in ['sha256','sha512','_manifest_spec','verify_manifest','integrity_check']:namespace[name]=types.FunctionType(found[name],namespace)
def forbid(*args,**kwargs):raise AssertionError('Unexpected integrity recovery prompt')
namespace.update(ask_yes_no=forbid,msgbox=forbid,restore_from_backup=forbid)
count,bad=namespace['verify_manifest']();assert count==281 and not bad,(count,len(bad));assert namespace['integrity_check']() is True
negative=inputs['negative'];os.makedirs(os.path.join(negative,'game'),exist_ok=True);sample=os.path.join(negative,'game','control.bin');raw=b'release-integrity-control'
open(sample,'wb').write(raw);open(os.path.join(negative,'manifest.sha256'),'wb').write((hashlib.sha256(raw).hexdigest().upper()+'  game/control.bin\r\n').encode('ascii'))
namespace['PKG']=negative;assert namespace['verify_manifest']()==(1,[]);assert namespace['integrity_check']() is True
open(sample,'ab').write(b'corrupt');assert len(namespace['verify_manifest']()[1])==1
json.dump(dict(status='PASS',rows=count,mismatches=0,noRecoveryPrompt=True,corruptionDetected=True),open(inputs['report'],'w'),indent=2)
print('PASS: actual launcher integrity, 281 rows, no mismatch; corruption control detected.')
