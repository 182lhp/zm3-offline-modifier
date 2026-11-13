"""Restore verified, deduplicated source inputs from the paired game installation."""
import argparse,json,hashlib,os,shutil
from pathlib import Path

def contained(base,relative):
    base=base.resolve();path=base/relative
    if Path(relative).is_absolute() or '..' in Path(relative).parts or not path.resolve().is_relative_to(base):raise ValueError('资源路径超出指定目录：'+relative)
    if any(p.is_symlink() or getattr(p,'is_junction',lambda:False)() for p in [path,*path.parents] if p!=base and p.is_relative_to(base)):raise ValueError('资源路径经过链接：'+relative)
    return path

def restore(game,source,verify_only=False):
    rows=json.loads((source/'资源引用.json').read_text(encoding='utf-8'))['files'];plan=[]
    for row in rows:
        original=contained(game,row['gamePath']);target=contained(source,row['sourcePath']);data=original.read_bytes()
        if len(data)!=row['size'] or hashlib.sha256(data).hexdigest()!=row['sha256']:raise ValueError('安装目录资源与源码版本不匹配：'+row['gamePath'])
        if target.exists() and target.read_bytes()!=data:raise ValueError('保留已有不同文件，未覆盖：'+row['sourcePath'])
        plan.append((original,target))
    # Preflight the entire set before creating any output file.
    if not verify_only:
        for original,target in plan:
            target.parent.mkdir(parents=True,exist_ok=True)
            if not target.exists():shutil.copyfile(original,target)
    return len(plan)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--game-root',type=Path,required=True);p.add_argument('--source-root',type=Path,default=Path(__file__).resolve().parent);p.add_argument('--verify-only',action='store_true');args=p.parse_args()
    print('Verified source resources:',restore(args.game_root,args.source_root,args.verify_only))
