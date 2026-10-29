param([ValidateSet(0,1)][int]$DevDiag=0, [ValidateSet(0,1)][int]$NoInstall=0)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$game='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
if(-not $NoInstall -and (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue)){throw 'Close the game first.'}
$sdk=$env:SHCDE_API_SHARED_DIR
if(-not $sdk){$sdk=Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory $sdk
if($LASTEXITCODE){throw 'APIShared package validation failed.'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -ModDirectory 'Testmods/LordOverviewTest' -ConsumerOnly
if($LASTEXITCODE){throw 'Runtime boundary checks failed.'}
& dotnet run --project (Join-Path $PSScriptRoot 'tests/Tests.csproj') -c Release
if($LASTEXITCODE){throw 'Controller tests failed.'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests/Test-Presets.ps1')
if($LASTEXITCODE){throw 'Mod-specific preset tests failed.'}
$msbuild='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$diagnosticProperty=if($DevDiag){'/p:SerpDevDiagnostics=true'}else{'/p:SerpDevDiagnostics=false'}
& $msbuild (Join-Path $PSScriptRoot 'LordOverviewTest.csproj') /t:Rebuild /p:Configuration=Release "/p:ApiSharedDir=$sdk" $diagnosticProperty /v:minimal
if($LASTEXITCODE){throw 'Compilation failed.'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests/Test-Appearance.ps1') -AssemblyPath (Join-Path $PSScriptRoot 'BepInEx/plugins/LordOverviewTest_Serp/LordOverviewTest.dll') -GameDirectory $game
if($LASTEXITCODE){throw 'Appearance template validation failed.'}

if($NoInstall){Write-Host "LordOverviewTest built without installation (DevDiag=$DevDiag)."; exit 0}
$source=Join-Path $PSScriptRoot 'BepInEx/plugins/LordOverviewTest_Serp'
$destination=Join-Path $game 'BepInEx/plugins/LordOverviewTest_Serp'
[void](New-Item -ItemType Directory -Path $destination -Force)
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $destination -Recurse -Force
Write-Host 'LordOverviewTest built and installed.'
