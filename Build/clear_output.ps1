param([Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$Name)
$ErrorActionPreference='Stop'
$Output=[IO.Path]::GetFullPath($Output)
if($Name -notin @('modifier-fixture','service-fixture','source-fixture','source-rebuilt','install-fixture','stage','before')){throw 'Unmanaged output subtree'}
$marker=Get-Content -LiteralPath (Join-Path $Output '.release-owner.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($marker.product -ne 'zm3-release-output' -or $marker.output -ne $Output){throw 'Output ownership marker mismatch'}
$target=[IO.Path]::GetFullPath((Join-Path $Output $Name))
if(-not $target.StartsWith($Output+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup escaped output root'}
if(Test-Path -LiteralPath $target){
 $at=$target
 while($at){if((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Cleanup path contains a reparse point'};$parent=Split-Path $at;if($parent -eq $at){break};$at=$parent}
 if(@(Get-ChildItem -LiteralPath $target -Force -Recurse|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Cleanup subtree contains linked content'}
 Remove-Item -LiteralPath $target -Recurse -Force
}
