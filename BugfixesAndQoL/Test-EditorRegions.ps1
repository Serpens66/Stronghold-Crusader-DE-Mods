[CmdletBinding()]
param([switch]$RunTests)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extender = Join-Path $game 'BepInEx/plugins/000shcdese'
if ($env:SHCDESE_EXTENDER_DIR) { $extender = $env:SHCDESE_EXTENDER_DIR }
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx/core/Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'))
try {
    $editor = @($assembly.MainModule.Types | Where-Object Name -eq 'EditorDirector')[0]
    $before = @($editor.Methods | Where-Object Name -eq 'preDLLCallActions')
    if ($before.Count -ne 1 -or -not $before[0].IsPublic -or $before[0].IsStatic -or
        ($before[0].Parameters.ParameterType.FullName -join ',') -cne 'System.Int32&,System.Int32&') {
        throw 'Actual public EditorDirector.preDLLCallActions signature differs.'
    }
    $engine = @($assembly.MainModule.Types | Where-Object Name -eq 'EngineInterface')[0]
    $gate = @($engine.Fields | Where-Object Name -eq 'threadLock')
    if ($gate.Count -ne 1 -or -not $gate[0].IsStatic -or $gate[0].FieldType.FullName -ne 'System.Object') {
        throw 'Actual reflected engine lock contract differs.'
    }
    $run = @($engine.Methods | Where-Object Name -eq 'run')[0]
    $calls = @($run.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt') })
    $names = @($calls | ForEach-Object { $_.Operand.DeclaringType.FullName + '.' + $_.Operand.Name })
    $anchors = @('System.Threading.Monitor.Enter','EditorDirector.preDLLCallActions','EngineInterface.DLL_RunTick','System.Threading.Monitor.Exit')
    $last = -1
    foreach ($name in $anchors) {
        if (@($names | Where-Object { $_ -ceq $name }).Count -ne 1) { throw "Missing or repeated engine anchor: $name" }
        $index = [Array]::IndexOf($names, $name)
        if ($index -le $last) { throw 'Editor preparation/native run lock ordering differs.' }
        $last = $index
    }
} finally { $assembly.Dispose() }
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $extender 'SHCDESE.dll'))
try {
    $api = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.API.GamePathingManagerAPI')[0]
    foreach ($name in @('RequestComponentRebuild','GetComponentGeneration','GetPathfindingContextView')) {
        if (@($api.Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic -and $_.Parameters.Count -eq 0 }).Count -ne 1) {
            throw "Missing actual public path API: $name"
        }
    }
    $view = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.GamePathfindingContextView')[0]
    foreach ($name in @('get_Address','get_ComponentGridDirty','get_ComponentGeneration')) {
        if (@($view.Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic }).Count -ne 1) { throw "Missing actual public context member: $name" }
    }
    $events = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.EventAPI.MapEditorR3EventHooks')[0]
    foreach ($name in @('OnSetTileType','OnSetTileTypeToNone','OnSetTileHeightToPreset','OnSetTileHeightToNumber','OnRaiseTileHeight','OnEvenTileHeight','OnDelete','OnVegetationPlace')) {
        if (@($events.Fields | Where-Object { $_.Name -eq $name -and $_.IsPublic -and $_.IsStatic }).Count -ne 1) { throw "Missing actual edit event: $name" }
    }
} finally { $assembly.Dispose() }
Write-Host 'PASS: actual public editor/path/event members and locked pre-cursor engine boundary.'
if (-not $RunTests) { return }
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'editor-regions' -Identity 'Production scheduling and private canonical PCL execution'
$project = Join-Path $PSScriptRoot 'tests/EditorRegions.Tests/EditorRegions.Tests.csproj'
& dotnet run --project $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Editor region production scheduling tests failed.' }
$contract = Join-Path $fixture 'native-contract.json'
& dotnet run --project $project --configuration Release --no-build -- --native-contract $contract
if ($LASTEXITCODE -ne 0) { throw 'Compiled editor native contract export failed.' }
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot 'tests/EditorRegions.Tests/native_fixture.py') $game $contract
if ($LASTEXITCODE -ne 0) { throw 'Private canonical editor region fixture failed.' }
