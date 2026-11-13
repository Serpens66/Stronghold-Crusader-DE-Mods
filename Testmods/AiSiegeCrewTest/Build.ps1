[CmdletBinding()]
param([string]$SdkRoot,[Parameter(ValueFromRemainingArguments)][string[]]$DriverArguments)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $SdkRoot){$SdkRoot=Join-Path $workspace 'APIShared'}
$SdkRoot=[IO.Path]::GetFullPath($SdkRoot)
. (Join-Path $SdkRoot 'tools/BuildProof.ps1')
$null=Assert-ApiBuildProof $SdkRoot
$diag=@($DriverArguments | Where-Object {$_ -ieq '/devdiag'}).Count -gt 0
if($diag -ne ($env:SHCDE_DEVDIAG -eq '1')){throw 'Use build.bat /devdiag for diagnostics.'}
$msbuild='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $PSScriptRoot 'AiSiegeCrewTest.csproj') /t:Rebuild /p:Configuration=Release "/p:ApiSharedDir=$SdkRoot/BepInEx/plugins/APIShared_Serp" "/p:SerpDevDiagnostics=$($diag.ToString().ToLowerInvariant())" /nologo /verbosity:minimal
if($LASTEXITCODE -ne 0){throw 'AI crew testmod build failed.'}
Write-Host ('Built testmod only; installation skipped. DEVDIAG='+$diag)
