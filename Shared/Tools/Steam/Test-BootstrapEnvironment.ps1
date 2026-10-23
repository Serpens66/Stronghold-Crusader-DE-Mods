$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'BootstrapEnvironment'
$savedGame=$env:SHCDE_GAME_DIR; $savedExtender=$env:SHCDESE_EXTENDER_DIR; $savedCompiler=$env:SHCDE_MSBUILD
function Write-EnvironmentFixture([string]$Path,[string]$Text) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))
}
try {
    $api=Join-Path $fixture 'APIShared'
    $helper=Join-Path $api 'tools/BuildProof.ps1'
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $helper))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../../APIShared/tools/BuildProof.ps1') -Destination $helper
    $game=Join-Path $fixture 'Game with spaces'
    foreach($relative in @('BepInEx/core/BepInEx.dll','BepInEx/plugins/000shcdese/SHCDESE.dll','Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll','Stronghold Crusader Definitive Edition_Data/Managed/UnityEngine.dll','Stronghold Crusader Definitive Edition_Data/Managed/UnityEngine.CoreModule.dll')) { Write-EnvironmentFixture (Join-Path $game $relative) 'reference fixture' }
    Write-EnvironmentFixture (Join-Path $api '.local/build-proof.json') (@{SchemaVersion=1;GameDir=$game;ExtenderDir=(Join-Path $game 'BepInEx/plugins/000shcdese')}|ConvertTo-Json)
    $env:SHCDE_GAME_DIR='';$env:SHCDESE_EXTENDER_DIR='';$env:SHCDE_MSBUILD=''
    Initialize-WorkshopBootstrapEnvironment $fixture
    if($env:SHCDE_GAME_DIR -cne $game){throw 'Game directory was not restored from build evidence.'}
    & powershell.exe -NoProfile -Command 'if (-not $env:SHCDE_GAME_DIR -or -not (Test-Path -LiteralPath (Join-Path $env:SHCDE_GAME_DIR "BepInEx/core/BepInEx.dll"))) { exit 1 }'
    if($LASTEXITCODE){throw 'Fresh PowerShell child did not inherit the bootstrap game directory.'}
    $env:SHCDE_GAME_DIR=Join-Path $fixture 'invalid explicit game'
    $rejected=$false;try{Initialize-WorkshopBootstrapEnvironment $fixture}catch{$rejected=$_.Exception.Message -match 'Assembly-CSharp'}
    if(-not$rejected){throw 'Invalid explicit environment must not fall back silently.'}
    $env:SHCDE_GAME_DIR=$game
    Remove-Item -LiteralPath (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/UnityEngine.dll')
    $rejected=$false;try{Initialize-WorkshopBootstrapEnvironment $fixture}catch{$rejected=$_.Exception.Message -match 'bootstrap reference is missing'}
    if(-not$rejected){throw 'Missing bootstrap reference was not rejected early.'}
    Write-Host 'PASS: bootstrap environment from saved paths, fresh child inheritance, explicit invalid paths and missing references.'
} finally {
    $env:SHCDE_GAME_DIR=$savedGame;$env:SHCDESE_EXTENDER_DIR=$savedExtender;$env:SHCDE_MSBUILD=$savedCompiler
    if([IO.Path]::GetFullPath($fixture).StartsWith([IO.Path]::GetTempPath(),[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue}
}
