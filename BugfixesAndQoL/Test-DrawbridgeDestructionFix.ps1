[CmdletBinding()]
param([switch]$RunTests)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$view = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLViewModel.cs'))
$main = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLRuntime.cs'))
$runtime = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\DrawbridgeDestructionFix\FixRuntime.cs'))
foreach ($required in @('private bool enableDrawbridgeDestructionFix = true;', "[SyncHostOnly]`r`n        public bool EnableDrawbridgeDestructionFix", 'EnableDrawbridgeDestructionFix = true;', 'SetSetting(ref enableDrawbridgeDestructionFix, value, nameof(EnableDrawbridgeDestructionFix))')) {
    if (-not $view.Contains($required)) { throw "Missing host/default/reset/preset contract: $required" }
}
foreach ($required in @('private static DrawbridgeDestructionFix.FixRuntime processDrawbridgeDestructionFixRuntime;', 'drawbridgeDestructionFixInitializationAttempted = true;', 'processDrawbridgeDestructionFixRuntime.Install(context, settings.EnableMod, settings.EnableDrawbridgeDestructionFix);', 'processDrawbridgeDestructionFixRuntime?.SetEnabled(settings.EnableMod, settings.EnableDrawbridgeDestructionFix);')) {
    if (-not $main.Contains($required)) { throw "Missing permanent integration: $required" }
}
foreach ($required in @('if (!published)', 'NativeContract.IsEnabled(enableMod, enableFix)', 'Volatile.Write(ref *(int*)state', 'Volatile.Read(ref confirmed) != 0', 'DRAWBRIDGE_FIX_CONFIRMED', 'DisplacedByteCount != NativeContract.SpanLength')) {
    if (-not $runtime.Contains($required)) { throw "Missing native/activation/minimal-diagnostic contract: $required" }
}
if ($runtime -match 'OnDestroy|OnDisable|OnApplicationQuit|CodePatch|\.Disable\(|\.Undo\(|Snapshot\[|DAMAGE_PRE|NEXT_TICK|MAP_END') { throw 'Unsafe teardown or verbose test diagnostics returned.' }
$plugin = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLPlugin.cs'))
if (-not $plugin.Contains('[BepInIncompatibility("DrawbridgeDestructionFixTest_Serp")]')) { throw 'Old testmod overlap guard missing.' }
[xml]$xaml = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'))
$rows = @($xaml.SelectNodes("//*[local-name()='CheckBox']") | Where-Object { $_.IsChecked -eq '{Binding EnableDrawbridgeDestructionFix, Mode=TwoWay}' })
if ($rows.Count -ne 1 -or $rows[0].GetAttribute('ToolTipService.ShowDuration') -ne '60000' -or -not $rows[0].GetAttribute('ToolTip')) { throw 'Missing/invalid checkbox tooltip.' }
if (-not @($rows[0].ParentNode.ParentNode.Attributes | Where-Object { $_.LocalName.EndsWith('.SectionKey') -and $_.Value -eq 'bugfixes.section.host-fixes-gameplay' }).Count) { throw 'Drawbridge checkbox is outside host gameplay fixes.' }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Locales') -File -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in @('EnableDrawbridgeDestructionFix','EnableDrawbridgeDestructionFixHelp')) {
        if ([regex]::Matches($text, "(?m)^BugfixesAndQoL\.$key=").Count -ne 1) { throw "Missing/duplicate locale: $($file.Name)/$key" }
    }
}
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll'))
try {
    $building = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.GameBuilding')[0]
    foreach ($name in @('r_AliveState','r_BuildingType','r_GlobalId','r_CurrentHealth','r_TilePositionXBegin','r_TilePositionYBegin')) {
        if (@($building.Fields | Where-Object { $_.Name -eq $name -and $_.IsPublic }).Count -ne 1) { throw "Installed field is not public: $name" }
    }
    $api = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.API.GameBuildingManagerAPI')[0]
    foreach ($name in @('TryGetBuildingById','IsValidId')) {
        if (-not @($api.Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic }).Count) { throw "Installed public building API missing: $name" }
    }
} finally { $assembly.Dispose() }
$targets = @('src\DrawbridgeDestructionFix\FixRuntime.cs','src\DrawbridgeDestructionFix\NativeContract.cs','src\BugfixesAndQoLRuntime.cs','src\BugfixesAndQoLViewModel.cs','src\BugfixesAndQoLPlugin.cs','BugfixesAndQoL.csproj','Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml','Test-DrawbridgeDestructionFix.ps1')
foreach ($relative in $targets) { if ([IO.File]::ReadAllText((Join-Path $PSScriptRoot $relative)) -match '(?<!\r)\n|\r(?!\n)') { throw "Invalid CRLF: $relative" } }
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot 'tests\DrawbridgeDestructionFix.Tests\audit_native.py') $workspace
if ($LASTEXITCODE -ne 0) { throw 'Native static audit failed.' }
Write-Host 'PASS: drawbridge native/public field/host UI/default/reset/activation/minimal diagnostic contracts.'
if ($RunTests) {
    $testRoot = Join-Path $PSScriptRoot 'tests\DrawbridgeDestructionFix.Tests'
    & 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' (Join-Path $testRoot 'DrawbridgeDestructionFix.Tests.csproj') /p:Configuration=Debug /nologo /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Drawbridge native fixture build failed.' }
    & (Join-Path $testRoot 'bin\DrawbridgeDestructionFix.Tests.exe') (Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Drawbridge native/activation machine tests failed.' }
}
