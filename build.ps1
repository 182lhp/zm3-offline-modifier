param([string]$Output=(Join-Path $PSScriptRoot 'bin/public-build'))
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot 'bin/dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$resolvedOutput=[IO.Path]::GetFullPath($Output)
$sourceRoot=[IO.Path]::GetFullPath($PSScriptRoot)
if($resolvedOutput -eq $sourceRoot){throw 'Output must not be the source root.'}
& python (Join-Path $PSScriptRoot 'Build/check_publication.py')
if($LASTEXITCODE -ne 0){throw 'Public source privacy check failed.'}
& python (Join-Path $PSScriptRoot 'Build/generate_rules.py') --check
if($LASTEXITCODE -ne 0){throw 'Generated rules are out of date.'}
Push-Location (Join-Path $PSScriptRoot 'Build/tests')
try {
    & python -m unittest test_local_gifts test_storage test_layout_resources test_rules test_pipeline
    if($LASTEXITCODE -ne 0){throw 'Unit tests failed.'}
} finally {Pop-Location}
& dotnet restore (Join-Path $PSScriptRoot 'GmStudio.csproj') -r win-x64 -p:SelfContained=false -p:PublishSingleFile=false -p:EnableRuntimePackDownload=false --ignore-failed-sources
if($LASTEXITCODE -ne 0){throw 'Restore failed. Install .NET 8 SDK and the Windows desktop targeting pack.'}
& dotnet publish (Join-Path $PSScriptRoot 'GmStudio.csproj') -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DefineConstants=TRACE --no-restore -o $resolvedOutput
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
Write-Host ('Built: '+(Join-Path $resolvedOutput '启动修改器.exe'))
