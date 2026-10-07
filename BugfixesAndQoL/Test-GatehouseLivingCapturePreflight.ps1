[CmdletBinding()]
param([switch]$RunTests)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source = Join-Path $PSScriptRoot 'src\GatehouseLivingCapture'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extender = Join-Path $game 'BepInEx\plugins\000shcdese'
$runtime = [IO.File]::ReadAllText((Join-Path $source 'CaptureRuntime.cs'))
$main = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLRuntime.cs'))
$view = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLViewModel.cs'))
foreach ($required in @('private readonly CaptureCallback rootedCallback;', 'OwnsHooks = true', 'published = true;', 'if (!published) transaction?.Dispose();', 'Volatile.Write(ref active, published && CaptureDecision.IsEnabled(enableMod, enableFix) ? 1 : 0);', 'GATEHOUSE_LIVING_CAPTURE_CONFIRMED', 'context->R11 = originalBoolean;')) {
    if (-not $runtime.Contains($required)) { throw "Missing capture contract: $required" }
}
if ($runtime -match '\.(?:Enable|Disable|Undo)\s*\(|VirtualProtect|CodePatch\.Write|Marshal\.Write|OnDestroy|OnDisable|StartCoroutine|FIRST_EXCLUSION|filteredLogged' -or
    ([regex]::Matches($runtime,'\.Dispose\s*\(')).Count -ne 1) { throw 'Capture runtime mutation/teardown/diagnostic regression' }
foreach ($required in @('private static GatehouseLivingCapture.CaptureRuntime processGatehouseLivingCaptureRuntime;', 'processGatehouseLivingCaptureRuntime.Install(context, settings.EnableMod, settings.EnableGatehouseLivingCaptureFix);', 'processGatehouseLivingCaptureRuntime?.SetEnabled(settings.EnableMod, settings.EnableGatehouseLivingCaptureFix);', 'gatehouseLivingCaptureInitializationAttempted = true;')) {
    if (-not $main.Contains($required)) { throw "Missing main runtime integration: $required" }
}
foreach ($required in @('private bool enableGatehouseLivingCaptureFix = true;', "[SyncHostOnly]`r`n        public bool EnableGatehouseLivingCaptureFix", 'EnableGatehouseLivingCaptureFix = true;', 'SetSetting(ref enableGatehouseLivingCaptureFix, value, nameof(EnableGatehouseLivingCaptureFix))')) {
    if (-not $view.Contains($required)) { throw "Missing host setting contract: $required" }
}
$xamlPath = Join-Path $PSScriptRoot 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'
[xml]$xaml = [IO.File]::ReadAllText($xamlPath)
$row = @($xaml.SelectNodes("//*[local-name()='CheckBox']") | Where-Object { $_.IsChecked -eq '{Binding EnableGatehouseLivingCaptureFix, Mode=TwoWay}' })
if ($row.Count -ne 1) { throw 'Expected exactly one capture checkbox' }
$section = $row[0].ParentNode.ParentNode
if (-not @($section.Attributes | Where-Object { $_.LocalName.EndsWith('.SectionKey') -and $_.Value -eq 'bugfixes.section.host-fixes-gameplay' }).Count) { throw 'Capture checkbox is outside host Fixes/gameplay' }
foreach ($file in Get-ChildItem (Join-Path $PSScriptRoot 'Locales') -File -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in @('EnableGatehouseLivingCaptureFix','EnableGatehouseLivingCaptureFixHelp')) {
        if (([regex]::Matches($text,"(?m)^BugfixesAndQoL\.$key=")).Count -ne 1) { throw "Missing/duplicate locale key: $($file.Name)/$key" }
    }
}
# No Assembly-CSharp member is introduced. Validate the actually installed public SE/API contracts.
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$api = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\APIShared_Serp\APIShared.dll'))
try {
    $access = @($api.MainModule.Types | Where-Object FullName -eq 'APIShared.UnitAccess')[0]
    foreach ($parameter in @('SHCDESE.Interop.GameUnit&','SHCDESE.Interop.GameUnit*')) {
        if (@($access.Methods | Where-Object { $_.Name -eq 'IsReallyAlive' -and $_.IsPublic -and $_.IsStatic -and $_.ReturnType.FullName -eq 'System.Boolean' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq $parameter }).Count -ne 1) { throw "Installed APIShared lacks IsReallyAlive($parameter)" }
    }
} finally { $api.Dispose() }
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $extender 'SHCDESE.dll'))
try {
    $unit = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.GameUnit')[0]
    foreach ($name in @('r_AliveState','N0000019A','r_CurrentHealth','r_ControllableForPlayerId')) {
        $field = @($unit.Fields | Where-Object Name -ceq $name)
        if ($field.Count -ne 1 -or -not $field[0].IsPublic) { throw "Private/missing installed GameUnit.$name" }
    }
} finally { $assembly.Dispose() }
$files = @('BugfixesAndQoL.csproj','Test-GatehouseLivingCapturePreflight.ps1','build.bat','src\BugfixesAndQoLRuntime.cs','src\BugfixesAndQoLViewModel.cs','Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml') | ForEach-Object { Get-Item (Join-Path $PSScriptRoot $_) }
$files += @(Get-ChildItem $source -File)
$files += @(Get-ChildItem (Join-Path $PSScriptRoot 'tests\GatehouseLivingCapture.Tests') -File)
$files += @(Get-ChildItem (Join-Path $PSScriptRoot 'Locales') -File -Filter '*.txt')
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Invalid CRLF: $($file.FullName)" }
}
Write-Host 'PASS: gatehouse host setting/UI/locales, installed API visibility, permanent roots/rollback, minimal logging and CRLF.'
if ($RunTests) {
    $testRoot = Join-Path $PSScriptRoot 'tests\GatehouseLivingCapture.Tests'
    $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $testRoot 'GatehouseLivingCapture.Tests.csproj') /t:Rebuild /p:Configuration=Debug /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Gatehouse test compilation failed' }
    & (Join-Path $testRoot 'bin\GatehouseLivingCapture.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Gatehouse native/life/activation machine tests failed' }
}
