[CmdletBinding()]
param([Parameter(ValueFromRemainingArguments)][string[]]$DriverArguments)
$ErrorActionPreference='Stop'
function Invoke-LoggedNative([string]$Executable,[string[]]$Arguments,[string]$Log,[string]$Label)
{
    # Windows PowerShell turns native stderr into ErrorRecords even for successful programs.
    # Preserve its complete output, then decide success from the actual process exit status.
    $previous=$ErrorActionPreference
    try{$ErrorActionPreference='Continue'; & $Executable @Arguments *> $Log; $code=$LASTEXITCODE}
    finally{$ErrorActionPreference=$previous}
    if($code -ne 0){Get-Content -LiteralPath $Log -Tail 25;throw "$Label failed (exit $code); see $Log"}
}
$modRoot=$PSScriptRoot
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $modRoot '../..'))
$controllerRoot=if($env:DESYNC_WORKSPACE_ROOT){[IO.Path]::GetFullPath($env:DESYNC_WORKSPACE_ROOT)}else{$sourceRoot}
. (Join-Path $controllerRoot 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $controllerRoot 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
$runRoot=New-ShortWorkDirectory -Root $controllerRoot -Kind a -Name desync-build -Identity 'No-install DesyncTest validation and package'
$testRoot=New-ShortWorkDirectory -Root $controllerRoot -Kind t -Name desync -Identity 'Production core and native machine fixtures'
$gameDir=if($env:SHCDE_GAME_DIR){$env:SHCDE_GAME_DIR}else{'E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition'}
$extenderDir=if($env:SHCDESE_EXTENDER_DIR){$env:SHCDESE_EXTENDER_DIR}else{Join-Path $gameDir 'BepInEx/plugins/000shcdese'}
$apiDir=if($env:SHCDE_API_SHARED_DIR){$env:SHCDE_API_SHARED_DIR}else{Join-Path $sourceRoot 'APIShared/BepInEx/plugins/APIShared_Serp'}
$python='D:/CDesktopLink/Portable/Python/WinPy64/python/python.exe'
$msbuild=if($env:SHCDE_MSBUILD){$env:SHCDE_MSBUILD}else{'C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe'}
$auditFile=Join-Path $runRoot 'native-audit.json'
$references=@((Join-Path $extenderDir 'SHCDESE.dll'),(Join-Path $extenderDir 'RedBird.Backends.NativeX64.dll'),(Join-Path $extenderDir 'RedBird.X64.dll'),(Join-Path $extenderDir 'Iced.dll'),(Join-Path $apiDir 'APIShared.dll'),(Join-Path $apiDir 'APIShared.Content.dll'))
$referenceHashes=@{};foreach($file in $references){$referenceHashes[$file]=Get-ApiRepositoryHash $file}
Write-Host "DesyncTest validation: $runRoot; no installation step exists in this driver."
& $python (Join-Path $modRoot 'tools/Audit-Native.py') --workspace $controllerRoot --source-root $sourceRoot --game $gameDir --output $auditFile
if($LASTEXITCODE -ne 0){throw 'Native contract audit failed.'}
Invoke-LoggedNative 'powershell.exe' @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $sourceRoot 'Shared/Tools/Validation/Test-SharedBoundaries.ps1'),'-Workspace',$sourceRoot,'-ModDirectory','Testmods/DesyncTest','-ConsumerOnly') (Join-Path $runRoot 'preflight.log') 'Scoped preflight'
Invoke-LoggedNative 'dotnet' @('run','--project',(Join-Path $modRoot 'tests/DesyncTest.Tests.csproj'),'-c','Release',"/p:ApiSharedDir=$apiDir",'--',$testRoot,$auditFile) (Join-Path $runRoot 'tests.log') 'Production core/native tests'
Get-Content -LiteralPath (Join-Path $runRoot 'tests.log') -Tail 1
$oldFixtureRoot=$env:DESYNC_TEST_ROOT
try{$env:DESYNC_TEST_ROOT=$testRoot;Invoke-LoggedNative $python @((Join-Path $modRoot 'tests/test_analyzer.py')) (Join-Path $runRoot 'analyzer-tests.log') 'Offline analyzer tests'}
finally{$env:DESYNC_TEST_ROOT=$oldFixtureRoot}
Invoke-LoggedNative $msbuild @((Join-Path $modRoot 'DesyncTest.csproj'),'/p:Configuration=Release',"/p:GameDir=$gameDir","/p:ExtenderDir=$extenderDir","/p:ApiSharedDir=$apiDir",'/v:minimal','/nologo') (Join-Path $runRoot 'compile.log') 'Runtime compilation'
foreach($file in $references){if((Get-ApiRepositoryHash $file) -ne $referenceHashes[$file]){throw "Referenced dependency changed during build: $file"}}
$package=Join-Path $modRoot 'BepInEx/plugins/DesyncTest_Serp'
Copy-Item -LiteralPath (Join-Path $modRoot 'info.json'),(Join-Path $modRoot 'USAGE.md'),(Join-Path $modRoot 'DesyncTest_Serp.cfg.example') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $modRoot 'tools/Analyze-Desync.py') -Destination $package -Force
$proof=[ordered]@{Version='0.1.0';CreatedUtc=[DateTime]::UtcNow.ToString('o');NoInstall=$true;RuntimeAcceptance='pending';Evidence=$runRoot;NativeAudit=$auditFile;ReferencesUnchanged=$referenceHashes;Files=@(Get-ChildItem -LiteralPath $package -File | Where-Object Name -ne 'build-proof.json' | ForEach-Object{[ordered]@{Name=$_.Name;SHA256=(Get-ApiRepositoryHash $_.FullName)}})}
[IO.File]::WriteAllText((Join-Path $package 'build-proof.json'),(($proof|ConvertTo-Json -Depth 6)+"`r`n"),[Text.UTF8Encoding]::new($false))
$output=if($env:DESYNC_OUTPUT_ROOT){[IO.Path]::GetFullPath($env:DESYNC_OUTPUT_ROOT)}else{$package}
$expected=[IO.Path]::GetFullPath((Join-Path $controllerRoot 'Testmods/DesyncTest/BepInEx/plugins/DesyncTest_Serp'))
if($output -ine $expected){throw 'Output must stay inside the explicit workspace package directory.'}
if($output -ine [IO.Path]::GetFullPath($package))
{
    [void][IO.Directory]::CreateDirectory($output)
    foreach($file in Get-ChildItem -LiteralPath $package -File){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $output $file.Name) -Force}
}
Write-Host "PASS: built package: $output; NOT INSTALLED; actual host/client and performance acceptance pending."
