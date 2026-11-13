"""One release entry: generate, build, test, package, rehearse, install and verify."""
from pathlib import Path
import argparse,datetime,hashlib,json,marshal,os,shutil,struct,subprocess,sys,time,zipfile,zlib
from generate_rules import generate
from archive_io import archive
from package import prepare,info,sha,write
from build_fixture import build as build_fixture

SOURCE=Path(__file__).resolve().parent.parent;BUILD=SOURCE/'Build'
IGNORED={'bin','obj','.git','__pycache__'}
def source_fingerprint():
    h=hashlib.sha256()
    for path in sorted(SOURCE.rglob('*')):
        rel=path.relative_to(SOURCE)
        if not path.is_file() or set(rel.parts)&IGNORED or rel.as_posix()=='资源引用.json' or rel.as_posix().startswith('GamePatch/GiftGallery/art/'):continue
        h.update(rel.as_posix().encode('utf-8')+b'\0'+hashlib.sha256(path.read_bytes()).digest())
    return h.hexdigest()
def input_fingerprint(config,root):
    rows={name:sha(root/name) for name in config['baseline']}
    return hashlib.sha256(json.dumps(rows,sort_keys=True).encode()).hexdigest()
def require_paired_inputs(config,root):
    for name,expected in config['baseline'].items():
        if sha(root/name)!=expected:raise ValueError('Paired binary changed; update its reviewed input declaration: '+name)
    for name,expected in config['protectedPatchSources'].items():
        if sha(SOURCE/name)!=expected:raise ValueError('Patch source changed without a corresponding rebuilt binary: '+name)
    rules=json.loads((SOURCE/'Rules/definitions.json').read_text(encoding='utf-8'))
    for name,expected in rules['inputs'].items():
        path=root/'schema'/name
        if path.exists() and sha(path)!=expected:raise ValueError('Extracted schema differs from the reviewed rule source: '+name)
    for name in ['manifest.sha256','manifest.sha512']:
        raw=(root/name).read_bytes();lines=raw.decode('ascii').splitlines()
        if len(lines)!=281 or raw.count(b'\r\n')!=281 or any(row.split()[0]!=row.split()[0].upper() for row in lines):raise ValueError('Legacy manifest casing/encoding/line endings are invalid')

class Pipeline:
    def __init__(self,args):
        self.args=args;self.root=args.game_root.resolve();self.out=args.output.resolve();self.out.mkdir(parents=True,exist_ok=True)
        # Generated output must never be inside the installed game directory.
        if self.out==self.root or self.out.is_relative_to(self.root):raise ValueError('Release output cannot be inside the game installation')
        marker=self.out/'.release-owner.json'
        if marker.exists():
            owner=json.loads(marker.read_text(encoding='utf-8'))
            if owner.get('product')!='zm3-release-output' or owner.get('output')!=str(self.out):raise ValueError('Output directory belongs to another task')
        else:
            if any((self.out/name).exists() for name in ['stage','install-fixture','source-fixture','service-fixture','modifier-fixture']):raise ValueError('Refusing to adopt an existing unowned output tree; choose a fresh output')
            marker.write_text(json.dumps(dict(product='zm3-release-output',output=str(self.out))),encoding='utf-8')
        self.config=json.loads((BUILD/'release.json').read_text(encoding='utf-8'));self.env=dict(os.environ)
        self.env.update(DOTNET_CLI_HOME=str(self.out/'dotnet-home'),DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_CLI_TELEMETRY_OPTOUT='1',ZM3_BUILD_OUTPUT=str(self.out),ZM3_GAME_ROOT=str(self.root),PYTHONIOENCODING='utf-8',NO_PROXY='*',no_proxy='*')
        self.report=dict(format=1,version=self.config['version'],status='running',steps={},startedAt=datetime.datetime.now(datetime.timezone.utc).isoformat())
        self.active=None
    def save_report(self):
        target=self.out/'release-report.json';temporary=target.with_suffix('.tmp');temporary.write_text(json.dumps(self.report,ensure_ascii=False,indent=2),encoding='utf-8');os.replace(temporary,target)
    def step(self,name,action):
        self.active=name;print('['+name+']',flush=True);start=time.monotonic();result=action();self.report['steps'][name]=dict(status='PASS',seconds=round(time.monotonic()-start,2),result=result);self.save_report();return result
    def run(self,command,log,cwd=None,timeout=240,allow_failure=False):
        startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
        with (self.out/log).open('wb') as output:r=subprocess.run([str(x) for x in command],cwd=cwd or SOURCE,env=self.env,startupinfo=startup,stdout=output,stderr=subprocess.STDOUT,timeout=timeout)
        if r.returncode and not allow_failure:
            tail=(self.out/log).read_text(encoding='utf-8',errors='replace').splitlines()[-12:];raise RuntimeError('Command failed ('+log+'):\n'+'\n'.join(tail))
        return r.returncode
    def powershell(self,script,arguments,log,allow_failure=False):return self.run(['pwsh','-NoProfile','-File',script,*arguments],log,timeout=180,allow_failure=allow_failure)
    def generate(self):
        self.run([sys.executable,SOURCE/'恢复资源.py','--game-root',self.root],'restore-source-inputs.log')
        result=generate(SOURCE)
        for file in (BUILD/'runtime').glob('*'):
            if file.is_file() and file.suffix in ('.py','.html'):
                write(self.out/file.name,file.read_bytes())
                for compatibility in ['StorageRuntime','GiftGallery','ResourceLayout']:write(SOURCE/'GamePatch'/compatibility/file.name,file.read_bytes())
        self.report.update(sourceFingerprint=source_fingerprint(),inputFingerprint=input_fingerprint(self.config,self.root),rules=result);return result
    def clear_outputs(self):
        for name in ['modifier-fixture','service-fixture','source-fixture','source-rebuilt','install-fixture','stage','before']:
            self.powershell(BUILD/'clear_output.ps1',['-Output',str(self.out),'-Name',name],'clear-'+name+'.log')
    def evidence(self):
        names=['unit-tests.log','interop-qa.json','frozen-qa.json','game-qa.json','rehearsal-result.json','rehearsal-integrity.json','install-plan.json','build-TRACE.log','source-resources.log','latest-captures.txt']
        captures=Path((self.out/'latest-captures.txt').read_text(encoding='utf-8'))
        names.extend(str((captures/name).relative_to(self.out)) for name in ['qa.txt','rules-qa.json'])
        return {name:info(self.out/name) for name in names}
    def dotnet(self,configuration,output,project=None,restore=False):
        project=project or SOURCE/'GmStudio.csproj'
        if restore:self.run(['dotnet','restore',project,'-r','win-x64','-p:SelfContained=false','-p:PublishSingleFile=false','-p:EnableRuntimePackDownload=false','--ignore-failed-sources'],'restore-'+configuration+'.log')
        self.run(['dotnet','publish',project,'-c','Release','-r','win-x64','--self-contained','false','-p:PublishSingleFile=true','-p:DefineConstants='+configuration,'-o',output,'--no-restore'],'build-'+configuration+'.log')
    def unit_tests(self):
        self.run([sys.executable,'-m','unittest','test_local_gifts','test_storage','test_layout_resources','test_rules','test_pipeline'],'unit-tests.log',BUILD/'tests')
        text=(self.out/'unit-tests.log').read_text(encoding='utf-8');
        if 'Ran 76 tests' not in text or not text.rstrip().endswith('OK'):raise ValueError('Unit test report is incomplete')
        return dict(passed=76)
    def modifier_qa(self):
        self.dotnet('QA',self.out/'qa',restore=True)
        self.run([sys.executable,BUILD/'tests/run_modifier_qa.py'],'modifier-qa.log',timeout=240)
        captures=Path((self.out/'latest-captures.txt').read_text(encoding='utf-8'))
        if not (captures/'qa.txt').read_text(encoding='utf-8-sig').startswith('PASS'):raise ValueError('Modifier regression did not complete')
        rules=json.loads((captures/'rules-qa.json').read_text(encoding='utf-8'))
        if rules['sha256']!=self.report['rules']['sha256']:raise ValueError('C# and Python rule fingerprints differ')
        return dict(fullRegression=True,rulesFingerprintMatches=True)
    def compiler(self):
        # Reuse the paired frozen interpreter; no downloaded build dependency.
        raw=(self.root/'tools/zm3server.exe').read_bytes();base,entries,version,library=archive(raw);runtime=self.out/'compiler38';runtime.mkdir(exist_ok=True)
        def unpack(entry):return zlib.decompress(entry[2]) if entry[4] else entry[2]
        for entry in entries:
            name=entry[0]
            if name=='base_library.zip' or name.lower().endswith(('.dll','.pyd')):
                target=runtime/name
                if not target.resolve().is_relative_to(runtime.resolve()):raise ValueError('Invalid archive entry')
                write(target,unpack(entry))
        pyz=unpack(next(e for e in entries if e[0]=='PYZ-00.pyz'));table=dict(marshal.loads(pyz[struct.unpack('!I',pyz[8:12])[0]:]))
        for name,(package,start,size) in table.items():
            if not all(part.isidentifier() for part in name.split('.')):raise ValueError('Invalid module name')
            target=runtime.joinpath(*name.split('.'))/ '__init__.pyc' if package else runtime.joinpath(*name.split('.')).with_suffix('.pyc')
            write(target,pyz[4:8]+b'\0'*12+zlib.decompress(pyz[start:start+size]))
        write(self.out/'server.marshal',unpack(next(e for e in entries if e[0]=='server')))
        write(self.out/'zm3server.exe',raw)
        self.powershell(BUILD/'compile38.ps1',['-SourcePath',str(BUILD/'compile_test38.py'),'-WorkingDirectory',str(self.out),'-RuntimeDirectory',str(runtime)],'compile-test-host.log')
        build_fixture(self.root/'game/zmxy3_game.swf',BUILD/'tests/fixtures/gift-identity.delta',self.out/'gift-identity.swf')
        return dict(pairedInterpreter='3.8',testOnlyMovieReconstructed=True)
    def integrations(self):
        for script,log in [('test_interop.py','interop.log'),('run_frozen_qa.py','frozen.log'),('run_game_qa.py','game.log')]:self.run([sys.executable,BUILD/'tests'/script],log,timeout=180)
        for name in ['interop-qa.json','frozen-qa.json','game-qa.json']:
            if json.loads((self.out/name).read_text(encoding='utf-8'))['status']!='PASS':raise ValueError('Incomplete integration test: '+name)
        return dict(crossLanguageRecovery=True,frozenService=True,realGameAndGift=True)
    def package(self):
        self.dotnet('TRACE',self.out/'publish')
        return prepare(SOURCE,self.root,self.out,self.config,self.report['sourceFingerprint'])
    def source_rebuild(self):
        folder=self.out/'source-fixture';folder.mkdir(exist_ok=True)
        with zipfile.ZipFile(self.out/'stage/修改器/修改器-源码.zip') as archive:
            for name in archive.namelist():
                if not (folder/name).resolve().is_relative_to(folder):raise ValueError('Source archive path escaped output')
            archive.extractall(folder)
        # Pre-existing output resources may be revalidated; never overwrite differing source images.
        self.run([sys.executable,folder/'恢复资源.py','--game-root',self.root],'source-resources.log')
        self.dotnet('TRACE',self.out/'source-rebuilt',folder/'GmStudio.csproj',restore=True)
        return dict(restoredImages=132,recompiledFromPublishedZip=True)
    def rehearse(self):
        fixture=self.out/'install-fixture'
        if fixture.exists():raise ValueError('Installation rehearsal output already exists; use a fresh output directory')
        shutil.copytree(self.root,fixture,ignore=shutil.ignore_patterns('*.lock','*.transaction.json','__pycache__'))
        write(fixture/'tools/local_gifts.lock',b'0')
        args=['-Output',str(self.out),'-GameRoot',str(self.root),'-Rehearsal']
        code=self.powershell(BUILD/'install.ps1',args+['-FaultAfter','4'],'rollback-control.log',True)
        if code==0 or 'Simulated installer interruption' not in (self.out/'rollback-control.log').read_text(encoding='utf-8-sig',errors='replace'):raise ValueError('Failure injection did not execute')
        plan=json.loads((self.out/'install-plan.json').read_text(encoding='utf-8'))
        for name,before in plan['before'].items():
            target=fixture/name
            if before is None:
                if target.exists():raise ValueError('Rollback left a new program file: '+name)
            elif info(target)!=before:raise ValueError('Rollback failed to restore a program: '+name)
        self.powershell(BUILD/'install.ps1',args,'rehearsal.log')
        sys.path.insert(0,str(BUILD/'runtime'));from resource_versions import ResourceIndex
        result=ResourceIndex(str(fixture)).verify()
        if not result['ok']:raise ValueError('Rehearsed resource verification failed: '+str(result['errors'][:3]))
        self.verify_launcher(fixture,'rehearsal-integrity.json')
        return dict(interruptionRolledBack=True,resourcesVerified=result['checked'],legacyIntegrityVerified=True)
    def verify_launcher(self,root,name):
        starter=next(e for e in archive((root/'启动游戏.exe').read_bytes())[1] if e[0]=='starter');write(self.out/'starter.marshal',zlib.decompress(starter[2]) if starter[4] else starter[2])
        write(self.out/'probe-inputs.json',json.dumps(dict(root=str(root),negative=str(self.out/'negative-integrity-fixture'),report=str(self.out/name)),ensure_ascii=False).encode('utf-8'))
        self.powershell(BUILD/'compile38.ps1',['-SourcePath',str(BUILD/'probe38.py'),'-WorkingDirectory',str(self.out),'-RuntimeDirectory',str(self.out/'compiler38')],name+'.log')
        report=json.loads((self.out/name).read_text(encoding='utf-8'))
        if report['status']!='PASS':raise ValueError('Actual launcher integrity verification failed')
        return report
    def check_stage(self):
        plan=json.loads((self.out/'install-plan.json').read_text(encoding='utf-8'))
        if plan['sourceFingerprint']!=source_fingerprint():raise ValueError('Source changed since verification; rebuild before installation')
        if self.report['inputFingerprint']!=input_fingerprint(self.config,self.root):raise ValueError('Paired inputs changed since testing')
        for name,expected in self.report.get('evidence',{}).items():
            path=self.out/name
            if not path.exists() or info(path)!=expected:raise ValueError('Test evidence changed or is missing: '+name)
        files={p.relative_to(self.out/'stage').as_posix():info(p) for p in (self.out/'stage').rglob('*') if p.is_file()}
        if files!=plan['updated']:raise ValueError('A release artifact changed since verification')
        return plan
    def install(self):
        self.check_stage();self.powershell(BUILD/'install.ps1',['-Output',str(self.out),'-GameRoot',str(self.root)],'installation.log')
        result=json.loads((self.out/'result.json').read_text(encoding='utf-8-sig'));self.verify_launcher(self.root,'launcher-verification.json');return result
    def execute(self):
        require_paired_inputs(self.config,self.root)
        if self.args.resume:
            self.report=json.loads((self.out/'release-report.json').read_text(encoding='utf-8'));self.active='resume-validation'
            required=['generate','unit-tests','modifier-qa','compiler','integration-tests','package','source-rebuild','rehearsal']
            if self.report.get('status') not in ('validated','installed') or not self.report.get('evidence') or any(self.report['steps'].get(s,{}).get('status')!='PASS' for s in required):raise ValueError('No complete verified build is available to resume')
            self.check_stage()
            if self.args.install and self.report['status']!='installed':self.step('installation',self.install);self.report['status']='installed'
        else:
            self.clear_outputs();self.step('generate',self.generate);self.step('unit-tests',self.unit_tests);self.step('modifier-qa',self.modifier_qa);self.step('compiler',self.compiler);self.step('integration-tests',self.integrations);self.step('package',self.package);self.step('source-rebuild',self.source_rebuild);self.step('rehearsal',self.rehearse)
            self.report['evidence']=self.evidence();self.check_stage();self.report['status']='validated';self.save_report()
            if self.args.install:self.step('installation',self.install);self.report['status']='installed'
        self.report['completedAt']=datetime.datetime.now(datetime.timezone.utc).isoformat();self.save_report();print(json.dumps(dict(status=self.report['status'],version=self.config['version'],output=str(self.out),rules=self.report['rules']),ensure_ascii=False),flush=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--game-root',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--install',action='store_true');parser.add_argument('--resume',action='store_true');args=parser.parse_args();pipeline=Pipeline(args)
    import msvcrt
    with (pipeline.out/'release.lock').open('a+b') as lock:
        if lock.tell()==0:lock.write(b'0');lock.flush()
        lock.seek(0)
        try:msvcrt.locking(lock.fileno(),msvcrt.LK_NBLCK,1)
        except OSError:raise RuntimeError('Another release process owns this output directory')
        try:pipeline.execute()
        except Exception as error:
            pipeline.report.update(status='failed',failedStep=pipeline.active,error=str(error));pipeline.save_report();print(str(error),file=sys.stderr);raise SystemExit(1)
        finally:lock.seek(0);msvcrt.locking(lock.fileno(),msvcrt.LK_UNLCK,1)
if __name__=='__main__':main()
