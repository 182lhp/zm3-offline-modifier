param([string]$Compiler='gcc',[string]$OutputDirectory=$PSScriptRoot)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $Compiler -O2 -shared -static-libgcc -o (Join-Path $OutputDirectory 'window-aspect.dll') (Join-Path $PSScriptRoot 'window_aspect.c') -lcomctl32 -luser32
if ($LASTEXITCODE -ne 0) { throw 'Window aspect DLL build failed' }
& $Compiler -O2 -municode -mwindows -static-libgcc -o (Join-Path $OutputDirectory 'window-helper.exe') (Join-Path $PSScriptRoot 'window_helper.c') -lshell32 -luser32
if ($LASTEXITCODE -ne 0) { throw 'Window helper build failed' }
