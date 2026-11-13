param(
 [string]$GameRoot='D:\Games\ZM3',
 [string]$Output=(Join-Path $PSScriptRoot 'bin/release-output'),
 [switch]$Install,
 [switch]$Resume
)
$ErrorActionPreference='Stop'
$arguments=@((Join-Path $PSScriptRoot 'Build/release.py'),'--game-root',$GameRoot,'--output',$Output)
if($Install){$arguments+='--install'}
if($Resume){$arguments+='--resume'}
& python @arguments
if($LASTEXITCODE -ne 0){throw "发布流程未完成，退出码 $LASTEXITCODE；详见输出目录 release-report.json。"}
