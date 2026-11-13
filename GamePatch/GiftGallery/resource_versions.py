"""Read-only release index, HTTP validators and isolated Ruffle cache (Python 3.8)."""
import os,json,re,hashlib
from urllib.parse import urlsplit,urlunsplit,parse_qsl,urlencode,unquote
from game_paths import within

def digest(data):return hashlib.sha256(data).hexdigest()
def build_id(version,files):
    rows=[version]+['%s\t%d\t%s'%(r['path'],r['size'],r['sha256']) for r in sorted(files,key=lambda r:r['path'])]
    return digest(('\n'.join(rows)+'\n').encode('utf-8'))

class ResourceIndex:
    def __init__(self,root,log=None):
        self.root=os.path.abspath(root);self.log=log;self.rows={};self.version='未登记';self.build='';self.problem=None
        try:
            with open(within(root,'schema/resource_manifest.json'),encoding='utf-8-sig') as source:data=json.load(source)
            if data.get('format')!=1 or not isinstance(data.get('files'),list):raise ValueError('资源清单格式无效')
            for row in data['files']:
                name=row['path']
                if not re.fullmatch(r'(?:game|icons|runtime|schema|tools|修改器)/[^\x00]+|启动游戏\.exe',name) or '\\' in name or '..' in name.split('/') or name in self.rows:raise ValueError('资源清单路径无效')
                if name.startswith(('saves/','rundata/','logs/')) or name in ('tools/local_gifts.json','schema/ref_from_game.xml'):raise ValueError('资源清单包含用户数据')
                within(root,name)
                if not re.fullmatch('[a-f0-9]{64}',row['sha256']) or not isinstance(row['size'],int) or row['size']<0:raise ValueError('资源摘要无效')
                self.rows[name]=row
            self.version=data['version'];self.build=build_id(self.version,data['files'])
            if self.build!=data.get('buildId'):raise ValueError('资源清单版本指纹不一致')
        except (OSError,ValueError,KeyError,TypeError) as error:
            self.rows={};self.build='';self.problem=str(error)
            if log:log.warning('resources.index.unavailable error=%s',self.problem)
    def status(self):return dict(version=self.version,buildId=self.build,files=len(self.rows),error=self.problem)
    def verify(self):
        errors=[]
        for name,row in sorted(self.rows.items()):
            try:
                with open(within(self.root,name),'rb') as source:body=source.read()
                if len(body)!=row['size'] or digest(body)!=row['sha256']:errors.append(dict(path=name,error='内容与发布版本不同'))
            except (OSError,ValueError):errors.append(dict(path=name,error='文件缺失或路径无效'))
        result=self.status();result.update(ok=not self.problem and not errors,checked=len(self.rows),errors=errors)
        if self.log:self.log.info('resources.verified build=%s checked=%d errors=%d',self.build,len(self.rows),len(errors))
        return result
    def route(self,url):
        path=unquote(urlsplit(url).path)
        if re.fullmatch(r'/[A-Za-z0-9_.-]+\.(?:swf|html)',path):return 'game'+path
        art=re.fullmatch(r'/local-gifts/art/(hall|[0-9]{1,4})\.png',path)
        if art:return 'icons/activity-gifts/'+art.group(1)+'.png'
        icon=re.fullmatch(r'/local-gifts/icon/(items|pets)/([A-Za-z0-9_-]{1,100})\.png',path)
        if icon:
            group,code=icon.groups();override='icons/activity-gifts/'+code+'.png'
            if group=='pets' and code in ('nian1','nian2','nian3') and os.path.isfile(within(self.root,override)):return override
            with open(within(self.root,'schema/zmxy3_icons.json'),encoding='utf-8-sig') as source:mapping=json.load(source)
            name=os.path.basename(mapping.get(group,{}).get(code,''))
            if not name:return ''
            return 'icons/'+group+'/'+name
        return None
    def serve(self,handler):
        try:name=self.route(handler.path)
        except (OSError,ValueError,KeyError):name=''
        if name is None:return False
        status=200;body=b'';content='application/octet-stream';etag=None;cache='no-store'
        try:
            if not name:raise FileNotFoundError()
            with open(within(self.root,name),'rb') as source:body=source.read()
            actual=digest(body);etag='"'+actual+'"';row=self.rows.get(name)
            query=dict(parse_qsl(urlsplit(handler.path).query));version=query.get('zm3v');expected=query.get('zm3h')
            matched=row is not None and actual==row['sha256'] and len(body)==row['size']
            if version or expected:
                if not self.build or version!=self.build or expected!=actual or not matched:
                    status=409;body='资源版本已更新或校验失败，请保存游戏后重新启动。'.encode('utf-8');etag=None;content='text/plain; charset=utf-8'
                    if self.log:self.log.warning('resources.version.conflict path=%s',name)
                else:cache='public, max-age=31536000, immutable'
            elif matched:cache='no-cache, must-revalidate'
            elif self.log:self.log.warning('resources.content.unregistered path=%s',name)
            if status==200:
                content='application/x-shockwave-flash' if name.endswith('.swf') else 'text/html; charset=utf-8' if name.endswith('.html') else 'image/png'
                if matched and handler.headers.get('If-None-Match')==etag:status=304;body=b''
        except (OSError,ValueError):status=404;body=b''
        handler.send_response(status);handler.send_header('Content-Type',content);handler.send_header('Cache-Control',cache)
        if status!=304:handler.send_header('Content-Length',str(len(body)))
        if etag:handler.send_header('ETag',etag)
        if self.build:handler.send_header('X-Zm3-Resource-Version',self.build)
        handler.end_headers()
        if body:handler.wfile.write(body)
        return True

def launch_command(root,command):
    """Keep all viewport/save/config arguments, isolate only disposable HTTP cache."""
    index=ResourceIndex(root)
    if index.problem:raise RuntimeError('资源版本清单不可用：'+index.problem)
    command=list(command);found=False
    for position,value in enumerate(command):
        if not isinstance(value,str):continue
        url=urlsplit(value)
        if url.scheme in ('http','https') and url.hostname in ('127.0.0.1','localhost') and url.path=='/launcher.swf':
            with open(within(root,'game/launcher.swf'),'rb') as source:actual=digest(source.read())
            row=index.rows.get('game/launcher.swf')
            if not row or actual!=row['sha256']:raise RuntimeError('入口资源与发布版本不同，请校验资源。')
            query=[(k,v) for k,v in parse_qsl(url.query,keep_blank_values=True) if k not in ('zm3v','zm3h')]
            query.extend([('zm3v',index.build),('zm3h',actual)])
            command[position]=urlunsplit((url.scheme,url.netloc,url.path,urlencode(query),url.fragment));found=True
    if not found:raise RuntimeError('未找到本地游戏入口，已停止启动以免使用过期缓存。')
    cache=within(root,'rundata/cache/'+index.build)
    os.makedirs(cache,exist_ok=True)
    if '--cache-directory' in command:
        position=command.index('--cache-directory')
        if position+1>=len(command):raise RuntimeError('缓存参数缺失。')
        command[position+1]=cache
    else:command[1:1]=['--cache-directory',cache]
    return command
