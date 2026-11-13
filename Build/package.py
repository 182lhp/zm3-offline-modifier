"""Prepare a release from validated outputs. Never includes mutable game data."""
from pathlib import Path
import json,hashlib,shutil,zipfile

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def info(path):return dict(size=path.stat().st_size,sha256=sha(path))
def write(path,data):path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
def prepare(source,root,out,config,source_fingerprint):
    import sys
    sys.path.insert(0,str(source/'Build/runtime'));from resource_versions import build_id
    stage=out/'stage';version=config['version']
    for path in (source/'Build/runtime').glob('*'):
        if path.is_file() and path.suffix in ('.py','.html'):write(stage/'tools'/path.name,path.read_bytes())
    write(stage/'schema/game_rules.json',(source/'Rules/generated.json').read_bytes())
    write(stage/'修改器/启动修改器.exe',(out/'publish/启动修改器.exe').read_bytes())
    captures=Path((out/'latest-captures.txt').read_text(encoding='utf-8'));write(stage/'修改器/修改器-预览.png',(captures/'save-online.png').read_bytes())
    note=(source/'Build/user-note.md').read_text(encoding='utf-8')
    for name in ['使用说明.md','资料/修改器使用手册.md','资料/版本与文件说明.md']:
        text=(root/name).read_text(encoding='utf-8-sig')
        if note.strip() not in text:text=text.rstrip()+'\n\n'+note
        write(stage/name,text.encode('utf-8'))
    files={}
    for directory in ['game','icons','runtime','schema']:
        for path in (root/directory).rglob('*'):
            if path.is_file():
                rel=path.relative_to(root).as_posix()
                if rel not in ('schema/resource_manifest.json','schema/ref_from_game.xml'):files[rel]=path
    for rel in ['启动游戏.exe','tools/zm3server.exe']:files[rel]=root/rel
    for path in stage.rglob('*'):
        if path.is_file():
            rel=path.relative_to(stage).as_posix()
            if rel.startswith(('tools/','schema/')) or rel=='修改器/启动修改器.exe':files[rel]=path
    files.pop('schema/resource_manifest.json',None)
    rows=[dict(path=rel,kind=rel.split('/')[0],**info(path)) for rel,path in sorted(files.items())]
    manifest=dict(format=1,version=version,buildId=build_id(version,rows),rulesSha256=sha(source/'Rules/generated.json'),files=rows)
    write(stage/'schema/resource_manifest.json',json.dumps(manifest,ensure_ascii=False,indent=2).encode('utf-8'))
    references=[]
    for path in sorted((source/'GamePatch/GiftGallery/art').glob('*')):
        if path.is_file():
            paired=root/'icons/activity-gifts'/path.name
            if path.read_bytes()!=paired.read_bytes():raise ValueError('Source image differs from paired installation: '+path.name)
            references.append(dict(sourcePath=path.relative_to(source).as_posix(),gamePath=paired.relative_to(root).as_posix(),**info(path)))
    if len(references)!=132:raise ValueError('Unexpected source resource reference set')
    write(source/'资源引用.json',json.dumps(dict(format=1,version=version,buildId=manifest['buildId'],files=references),ensure_ascii=False,indent=2).encode('utf-8'))
    excluded={r['sourcePath'] for r in references};archive=stage/'修改器/修改器-源码.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as output:
        for path in sorted(source.rglob('*')):
            relative=path.relative_to(source)
            if path.is_file() and not set(relative.parts)&{'bin','obj','.git','__pycache__'} and relative.as_posix() not in excluded:output.write(path,relative.as_posix())
    before={};updated={}
    for path in stage.rglob('*'):
        if path.is_file():
            rel=path.relative_to(stage).as_posix();updated[rel]=info(path);original=root/rel;before[rel]=info(original) if original.exists() else None
            if original.exists():write(out/'before'/rel,original.read_bytes())
    plan=dict(root=str(root),version=version,sourceFingerprint=source_fingerprint,buildId=manifest['buildId'],rulesSha256=manifest['rulesSha256'],resources=len(rows),before=before,updated=updated)
    write(out/'install-plan.json',json.dumps(plan,ensure_ascii=False,indent=2).encode('utf-8'))
    return dict(resources=len(rows),buildId=manifest['buildId'],rulesSha256=manifest['rulesSha256'],sourceZipBytes=archive.stat().st_size,externalSourceImages=len(references),programFiles=len(updated))
