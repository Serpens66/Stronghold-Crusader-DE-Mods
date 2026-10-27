[CmdletBinding()]
param([switch]$RunTests, [switch]$UpdateAuditEvidence)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extender = Join-Path $game 'BepInEx/plugins/000shcdese'
if ($env:SHCDESE_EXTENDER_DIR) { $extender = $env:SHCDESE_EXTENDER_DIR }
# Read the existing trusted metadata reader without changing download-zone metadata on game files.
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx/core/Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'))
try {
    $engine = @($assembly.MainModule.Types | Where-Object FullName -eq 'EngineInterface')[0]
    $gate = @($engine.Fields | Where-Object Name -eq 'threadLock')
    if ($gate.Count -ne 1 -or -not $gate[0].IsStatic -or $gate[0].FieldType.FullName -ne 'System.Object') {
        throw 'The reflected Vanilla engine gate is unavailable; no alternate lock is safe.'
    }
} finally { $assembly.Dispose() }
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $extender 'SHCDESE.dll'))
try {
    $api = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.API.GameTileManagerAPI')[0]
    foreach ($name in @('GetTileManager','GetLogicLayer','GetLogic2Layer','GetStructureLayer','GetCurrentMapSize')) {
        if (@($api.Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic -and $_.Parameters.Count -eq 0 }).Count -ne 1) {
            throw "Missing actual public tile API: $name"
        }
    }
    $building = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.GameBuilding')[0]
    if (@($building.Fields | Where-Object { $_.Name -eq 'r_BuildingType' -and $_.IsPublic }).Count -ne 1) {
        throw 'Actual building type field is not public.'
    }
} finally { $assembly.Dispose() }
Write-Host 'PASS: actual Vanilla lock and public Extender member contracts.'
if (-not $RunTests) { return }
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'moat-terrain' -Identity 'Production native stubs, lifecycle, codec and current machine audit'
$testRoot = Join-Path $PSScriptRoot 'tests/MoatTerrain.Tests'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $testRoot 'MoatTerrain.Tests.csproj') /nologo /v:minimal /p:Configuration=Release /p:UseSharedCompilation=false "/p:ExtenderDir=$extender"
if ($LASTEXITCODE -ne 0) { throw 'Moat terrain fixture compilation failed.' }
$exe = Join-Path $testRoot 'bin/MoatTerrain.Tests.exe'
$contract = Join-Path $fixture 'compiled-sites.tsv'
& $exe --audit-out $contract
if ($LASTEXITCODE -ne 0) { throw 'Compiled native contract export failed.' }
# Incoming-xref/instruction facts are fixed for one native image. The full audit reads the
# untracked knowledge database; build copies never contain it. They instead require the
# compiled site contract to equal the committed audited evidence for the installed hash.
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
$audit = Join-Path $testRoot 'audit_native.py'
$evidence = Join-Path $testRoot 'native-audit.tsv'
$baseline = Join-Path $workspace '_inspect/CrusaderDE-Native-Baseline'
$current = Get-Content -LiteralPath (Join-Path $baseline 'CURRENT.json') -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $baseline $current.databaseManifest) -Raw | ConvertFrom-Json
$database = Join-Path $baseline $manifest.database.path
if ($UpdateAuditEvidence) {
    if (-not (Test-Path -LiteralPath $database)) { throw "Audit evidence update needs the knowledge database: $database" }
    & $python -I $audit $workspace $contract --evidence-out $evidence
    if ($LASTEXITCODE -ne 0) { throw 'Current native audit failed.' }
} else {
    if (Test-Path -LiteralPath $database) {
        & $python -I $audit $workspace $contract
        if ($LASTEXITCODE -ne 0) { throw 'Current native audit failed.' }
    }
    $native = @($manifest.binaries | Where-Object role -eq 'current-native')[0]
    # .NET directly: Windows PowerShell 5 in the build driver lacks Get-FileHash here.
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $installed = -join ($sha.ComputeHash([IO.File]::ReadAllBytes($native.sourcePath)) | ForEach-Object { $_.ToString('X2') }) }
    finally { $sha.Dispose() }
    $expected = @("# CrusaderDE $installed") + @(Get-Content -LiteralPath $contract)
    $recorded = if (Test-Path -LiteralPath $evidence) { @(Get-Content -LiteralPath $evidence) } else { @() }
    if ($installed -ne $current.currentNativeHash -or
        (Compare-Object -ReferenceObject $recorded -DifferenceObject $expected -SyncWindow 0)) {
        throw 'Moat terrain sites or native image differ from the audited evidence; run Test-MoatTerrain.ps1 -RunTests -UpdateAuditEvidence in a workspace with the knowledge database.'
    }
    Write-Host "PASS: compiled moat terrain sites match the audited native evidence ($($recorded.Count - 1) sites)."
}
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Executable production moat terrain fixtures failed.' }
