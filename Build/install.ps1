param([Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$GameRoot,[switch]$Rehearsal,[int]$FaultAfter=0)
$ErrorActionPreference='Stop'
$Output=[IO.Path]::GetFullPath($Output);$GameRoot=[IO.Path]::GetFullPath($GameRoot)
$root=if($Rehearsal){Join-Path $Output 'install-fixture'}else{$GameRoot}
$plan=Get-Content -LiteralPath (Join-Path $Output 'install-plan.json') -Raw -Encoding UTF8|ConvertFrom-Json
$allowed=@('tools/local_gifts.py','tools/storage_tx.py','tools/runtime_state.py','tools/runtime_integration.py','tools/game_paths.py','tools/resource_versions.py','tools/game_log.html','tools/game_rules.py','tools/rules_stamp.py','schema/game_rules.json','schema/resource_manifest.json','修改器/启动修改器.exe','修改器/修改器-源码.zip','修改器/修改器-预览.png','使用说明.md','资料/修改器使用手册.md','资料/版本与文件说明.md')
if($plan.root -ne $GameRoot -or @($plan.updated.PSObject.Properties).Count -ne $allowed.Count){throw 'Unexpected release plan'}
if($FaultAfter -and -not $Rehearsal){throw 'Failure injection is restricted to the copied fixture'}
function Child([string]$base,[string]$relative){
 $base=[IO.Path]::GetFullPath($base);$path=[IO.Path]::GetFullPath((Join-Path $base $relative))
 if(-not $path.StartsWith($base+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped its root'}
 $at=$path
 while($at){if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse point refused'};$parent=Split-Path $at;if($parent -eq $at){break};$at=$parent}
 return $path
}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Check([string]$path,$expected){if((Get-Item -LiteralPath $path).Length -ne $expected.size -or (Hash $path).ToLowerInvariant() -cne $expected.sha256){throw "Unexpected file: $path"}}
function AtomicCopy([string]$from,[string]$to){
 [IO.Directory]::CreateDirectory((Split-Path $to))|Out-Null;$temp=$to+'.installing'
 if(Test-Path -LiteralPath $temp){throw 'An installation temporary file already exists'}
 try{[IO.File]::Copy($from,$temp);$stream=[IO.File]::Open($temp,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);try{$stream.Flush($true)}finally{$stream.Dispose()};if(Test-Path -LiteralPath $to){[IO.File]::Replace($temp,$to,[NullString]::Value)}else{[IO.File]::Move($temp,$to)}}finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}}
}
foreach($name in $plan.updated.PSObject.Properties.Name){
 if($name -notin $allowed){throw 'Unexpected update target'}
 $before=$plan.before.PSObject.Properties[$name].Value
 if($null -eq $before){if(Test-Path -LiteralPath (Child $root $name)){throw "New file already exists: $name"}}else{Check (Child $root $name) $before;Check (Child (Join-Path $Output 'before') $name) $before}
 Check (Child (Join-Path $Output 'stage') $name) $plan.updated.PSObject.Properties[$name].Value
}
$modifier=Child $root '修改器/启动修改器.exe';$backend=Child $root 'tools/zm3server.exe'
if(-not $Rehearsal){
 if(@(Get-CimInstance Win32_Process -Filter "Name='启动修改器.exe'"|Where-Object {$_.ExecutablePath -eq $modifier}).Count){throw '新版已经验证完成；修改器仍在运行，请保存并关闭后再安装。尚未替换文件。'}
 $exclusive=[IO.File]::Open($modifier,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$exclusive.Dispose()
}
function OwnedBackend{@(Get-CimInstance Win32_Process -Filter "Name='zm3server.exe'"|Where-Object {$_.ExecutablePath -eq $backend})}
function StopBackend{if(-not $Rehearsal){foreach($p in (OwnedBackend)){Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue};for($i=0;$i -lt 30 -and (OwnedBackend).Count;$i++){Start-Sleep -Milliseconds 100};if((OwnedBackend).Count){throw 'The owned backend did not exit'}}}
function StartBackend{if(-not $Rehearsal){Start-Process -FilePath $backend -WorkingDirectory $root -WindowStyle Hidden -PassThru|Out-Null}}
$restart=if($Rehearsal){$false}else{(OwnedBackend).Count -gt 0}
$protected=@{};$backupDirectory=if($Rehearsal){'rehearsal-protected'}else{'protected-before'}
function Protect([string]$path,[string]$relative){
 $protected[$path]=Hash $path;$copy=Child (Join-Path $Output $backupDirectory) $relative
 [IO.Directory]::CreateDirectory((Split-Path $copy))|Out-Null;Copy-Item -LiteralPath $path -Destination $copy -Force
}
$guard=[IO.File]::Open((Child $root 'tools/local_gifts.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::ReadWrite)
$locked=$false;$stopped=$false;$replaced=[Collections.Generic.List[string]]::new()
try{
 $until=[Environment]::TickCount64+4000
 while(-not $locked){try{$guard.Lock(0,1);$locked=$true}catch{if([Environment]::TickCount64 -ge $until){throw};Start-Sleep -Milliseconds 40}}
 if(Test-Path -LiteralPath (Child $root 'tools/local_gifts.transaction.json')){throw '存在待恢复存档事务；未安装任何文件。'}
 Get-ChildItem -LiteralPath (Child $root 'saves') -File -Recurse|ForEach-Object {Protect $_.FullName ([IO.Path]::GetRelativePath($root,$_.FullName))}
 foreach($name in @('schema/local_gift_catalog.json','设置.ini','启动游戏.exe','启动修改器.lnk','manifest.sha256','manifest.sha512','runtime/ruffle.exe','runtime/runtime.json','runtime/window-helper.exe','runtime/window-aspect.dll','game/zmxy3_game.swf')){Protect (Child $root $name) $name}
 if(-not $Rehearsal){$prefs=Join-Path $env:LOCALAPPDATA 'Zm3GmStudio';if(Test-Path -LiteralPath $prefs){Get-ChildItem -LiteralPath $prefs -File|ForEach-Object {Protect $_.FullName ('preferences/'+$_.Name)}}}
 StopBackend;$stopped=$restart
 foreach($name in $plan.updated.PSObject.Properties.Name){
  AtomicCopy (Child (Join-Path $Output 'stage') $name) (Child $root $name);$replaced.Add($name);Check (Child $root $name) $plan.updated.PSObject.Properties[$name].Value
  if($FaultAfter -and $replaced.Count -eq $FaultAfter){throw 'Simulated installer interruption'}
 }
 foreach($path in $protected.Keys){if((Hash $path) -cne $protected[$path]){throw 'Protected data changed during locked installation'}}
 $guard.Unlock(0,1);$locked=$false
 if(-not $Rehearsal){
  StartBackend;$status=$null
  for($i=0;$i -lt 40;$i++){
   try{$status=Invoke-RestMethod -Uri 'http://127.0.0.1:43999/runtime/status' -NoProxy -TimeoutSec 1;if($status.ok -and $status.root -eq $root -and $status.resources.version -eq $plan.version -and $status.rules.sha256 -ceq $plan.rulesSha256){break}}catch{}
   Start-Sleep -Milliseconds 250
  }
  if(-not $status -or $status.root -ne $root -or $status.resources.version -ne $plan.version -or $status.rules.sha256 -cne $plan.rulesSha256 -or $status.monitorError){throw 'The installed backend failed its version/rule/ownership health check'}
  $check=Invoke-RestMethod -Uri 'http://127.0.0.1:43999/resources/verify' -Method Post -NoProxy -TimeoutSec 30
  if(-not $check.ok -or $check.checked -ne $plan.resources -or $check.buildId -cne $plan.buildId){throw 'Installed release resource verification failed'}
 }
 foreach($path in $protected.Keys){if((Hash $path) -cne $protected[$path]){throw 'Protected data changed after backend restart'}}
 if(@(Get-ChildItem -LiteralPath (Child $root '修改器') -File -Force).Count -ne 3){throw 'The modifier directory must contain exactly three release files'}
}catch{
 $failure=$_
 if($replaced.Count){
  if(-not $locked){StopBackend;$guard.Lock(0,1);$locked=$true}
  if(Test-Path -LiteralPath (Child $root 'tools/local_gifts.transaction.json')){throw 'Interrupted transaction preserved; automatic rollback stopped to protect the current save.'}
  foreach($name in $replaced){$path=Child $root $name;if($null -eq $plan.before.PSObject.Properties[$name].Value){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}else{AtomicCopy (Child (Join-Path $Output 'before') $name) $path}}
 }
 if($locked){$guard.Unlock(0,1);$locked=$false};if($stopped){StartBackend};throw $failure
}finally{if($locked){$guard.Unlock(0,1)};$guard.Dispose()}
$result=[ordered]@{status='installed';version=$plan.version;sourceFingerprint=$plan.sourceFingerprint;rehearsal=[bool]$Rehearsal;programFiles=$replaced.Count;resources=$plan.resources;rulesSha256=$plan.rulesSha256;protectedFilesUnchanged=$protected.Count;claimHistoryPreserved=$true;settingsPreserved=$true;shortcutUnchanged=$true;gameSwfUnchanged=$true;modifierFolderFiles=3;backendRestarted=$restart}
$name=if($Rehearsal){'rehearsal-result.json'}else{'result.json'}
$result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $Output $name) -Encoding UTF8
$result|ConvertTo-Json
