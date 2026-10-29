$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$baseline = Join-Path $workspace '_inspect/CrusaderDE-Native-Baseline'
if ($env:SHCDE_NATIVE_BASELINE_ROOT) { $baseline = $env:SHCDE_NATIVE_BASELINE_ROOT }
$current = Get-Content -LiteralPath (Join-Path $baseline 'CURRENT.json') -Raw | ConvertFrom-Json
$vanilla = Join-Path (Join-Path $baseline $current.semanticDirectory) 'resources/xaml/Assets/GUI/XAML/IngameUIScreens.xaml'
$output = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'SpectatorDock'
& dotnet msbuild (Join-Path $PSScriptRoot 'DockLayoutTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Dock layout harness compilation failed.' }
# Read the real page roster without constructing the game HUD or executing game code.
$game = 'E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx/core/Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'))
try {
    $ctor = $assembly.MainModule.GetType('CrusaderDE.HUD_Main').Methods | Where-Object Name -eq '.ctor'
    $il = $ctor.Body.Instructions
    $names = @{}; $table = $null
    for ($i=0; $i -lt $il.Count; $i++) {
        if ($il[$i].OpCode.Name -eq 'stfld' -and $il[$i].Operand.Name -eq 'BuildIconLists') {
            if ($il[$i-2].OpCode.Name -ne 'ldtoken') { throw 'Native page initializer no longer has a readable array contract.' }
            $table = $il[$i-2].Operand.Resolve().InitialValue
        }
        if ($il[$i].OpCode.Name -eq 'ldfld' -and $il[$i].Operand.Name -eq 'buildButtons' -and $il[$i+1].OpCode.Name.StartsWith('ldc.i4')) {
            $indexOp = $il[$i+1]
            $index = if ($indexOp.Operand -ne $null) { [int]$indexOp.Operand } else { [int]$indexOp.OpCode.Name.Substring(7) }
            if ($il[$i+3].OpCode.Name -ne 'ldstr') { throw 'Native button-name initializer changed.' }
            $names[$index] = [string]$il[$i+3].Operand
        }
    }
    if ($table.Length -ne 23*17*4) { throw 'Unexpected native build-page table dimensions.' }
    $rows = foreach ($page in 0..22) {
        foreach ($slot in 0..16) {
            $index = [BitConverter]::ToInt32($table,4*($page*17+$slot))
            if ($index -le 0) { break }
            if (-not $names.ContainsKey($index)) { throw "Unmapped native build button $index" }
            "$page`t$($names[$index])"
        }
    }
    # Alternate Lord artwork uses the same page commands but can have taller sprite bounds.
    foreach ($index in 583..587) { if (-not $names.ContainsKey($index)) { throw 'Missing alternate native building sprite.' }; $rows += "23`t$($names[$index])" }
    # Resolve dynamic, player-coloured sprite bindings from their actual resource assignments.
    $spriteIL = ($assembly.MainModule.GetType('CrusaderDE.MainViewModel').Methods | Where-Object Name -eq 'UpdateUITroopSprites').Body.Instructions
    for ($i=3; $i -lt $spriteIL.Count; $i++) {
        if ($spriteIL[$i].Operand -is [Mono.Cecil.MethodReference] -and $spriteIL[$i].Operand.Name.StartsWith('set_UI')) {
            if ($spriteIL[$i-3].OpCode.Name -ne 'ldstr') { throw 'Dynamic troop sprite assignment contract changed.' }
            $rows += "@binding`t$($spriteIL[$i].Operand.Name.Substring(4))`t$($spriteIL[$i-3].Operand)"
        }
    }
    $roster = Join-Path $output 'native-pages.tsv'
    [IO.File]::WriteAllLines($roster, $rows, [Text.UTF8Encoding]::new($false))
} finally { $assembly.Dispose() }
$gui = Join-Path $game 'BepInEx/plugins/000shcdese/Override/Assets/GUI'
& (Join-Path $output 'DockLayoutTests.exe') $vanilla (Join-Path $PSScriptRoot '../Patches/Assets/GUI/XAML/IngameUIScreens.xaml') (Join-Path $gui 'XAMLResources/HUD_Main.xaml') (Join-Path $gui 'Sprites/UI-MasterAtlas.xaml') $roster
if ($LASTEXITCODE -ne 0) { throw 'Dock patch integration failed.' }
