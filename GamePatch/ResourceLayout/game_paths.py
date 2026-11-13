"""Layout v2 selectors. Selection never rewrites definitions or claim history."""
import os,json

def layout_version(root):
    marker=os.path.join(root,'schema','layout.json')
    if not os.path.exists(marker):return 1
    with open(marker,encoding='utf-8-sig') as source:data=json.load(source)
    version=data.get('layoutVersion')
    if version not in (1,2):raise ValueError('不支持的文件布局版本，请保留现有文件。')
    return version

def within(root,relative):
    base=os.path.realpath(os.path.abspath(root));path=os.path.abspath(os.path.join(base,*relative.split('/')))
    if not os.path.normcase(os.path.realpath(path)).startswith(os.path.normcase(base+os.sep)):raise ValueError('文件路径超出游戏目录。')
    return path

def select(root,new,old):
    current,legacy=within(root,new),within(root,old)
    if layout_version(root)==2:
        if not os.path.isfile(current):raise ValueError('新版数据文件缺失：'+new+'；请恢复原文件，不要重建领取记录。')
        return current
    if os.path.isfile(current) and os.path.isfile(legacy):
        with open(current,'rb') as source:a=source.read()
        with open(legacy,'rb') as source:b=source.read()
        if a!=b:raise ValueError('发现两份不同的数据：'+new+' 和 '+old+'；迁移尚未完成，请先核对。')
    return current if os.path.isfile(current) else legacy

def gift_state(root):return select(root,'saves/gifts/local_gifts.json','tools/local_gifts.json')
def gift_catalog(root):return select(root,'schema/local_gift_catalog.json','tools/local_gift_catalog.json')
