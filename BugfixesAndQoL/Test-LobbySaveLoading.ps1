[CmdletBinding()]
param([switch]$RunTests)
$ErrorActionPreference = 'Stop'
$modRoot = $PSScriptRoot
$view = [IO.File]::ReadAllText((Join-Path $modRoot 'src\BugfixesAndQoLViewModel.cs'))
$plugin = [IO.File]::ReadAllText((Join-Path $modRoot 'src\BugfixesAndQoLPlugin.cs'))
$feature = [IO.File]::ReadAllText((Join-Path $modRoot 'src\LobbySaveLoadingFeature.cs'))
if (-not $plugin.Contains('LobbySaveLoadingFeature.Initialize(Logger, Settings);')) { throw 'Missing rooted bootstrap.' }
if ($feature -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(' -or
    $feature -match '\b(?:soloHook|visibilityHook)\s*\.\s*(?:Undo|Dispose|Disable)\s*\(') {
    throw 'Forbidden lifecycle or teardown of a published lobby hook.'
}
[xml]$xaml = [IO.File]::ReadAllText((Join-Path $modRoot 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'))
foreach ($name in @('EnableSoloMultiplayerSaveLoading', 'FixSkirmishLoadButtonVisibility')) {
    $field = [char]::ToLowerInvariant($name[0]) + $name.Substring(1)
    if (-not $view.Contains("private bool $field = true;") -or -not $view.Contains("$name = true;") -or
        $view -notmatch ('\[APIShared\.ModSettings\.PresetLocal\]\s+public bool ' + $name + '\b')) {
        throw "Missing enabled default, reset or local preset classification: $name"
    }
    $controls = @($xaml.SelectNodes("//*[local-name()='CheckBox'][@IsChecked='{Binding $name, Mode=TwoWay}']"))
    if ($controls.Count -ne 1 -or $controls[0].GetAttribute('Content') -ne "{Binding ${name}Text}" -or
        $controls[0].GetAttribute('ToolTip') -ne "{Binding ${name}HelpText}" -or
        $controls[0].GetAttribute('ToolTipService.ShowDuration') -ne '60000') { throw "Invalid setting control: $name" }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $modRoot 'Locales') -Filter '*.txt') {
        $text = [IO.File]::ReadAllText($file.FullName)
        foreach ($key in @($name, ($name + 'Help'))) {
            if ([regex]::Matches($text, '(?m)^BugfixesAndQoL\.' + $key + '=[^\r\n]+$').Count -ne 1) {
                # CRLF lines have a CR before the end-of-line; permit that explicitly.
                if ([regex]::Matches($text, '(?m)^BugfixesAndQoL\.' + $key + '=[^\r\n]+\r?$').Count -ne 1) {
                    throw "Missing or duplicate locale $key in $($file.Name)"
                }
            }
        }
    }
}
foreach ($file in @('src\LobbySaveLoadingFeature.cs','src\LobbySaveLoadingIl.cs','src\LobbySaveLoadingPolicy.cs',
    'tests\LobbySaveLoading.Tests\Program.cs','tests\LobbySaveLoading.Tests\LobbySaveLoading.Tests.csproj','Test-LobbySaveLoading.ps1')) {
    $text = [IO.File]::ReadAllText((Join-Path $modRoot $file))
    if ($text -match '(?<!\r)\n') { throw "Non-CRLF text: $file" }
}
if ($RunTests) {
    $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $modRoot 'tests\LobbySaveLoading.Tests\LobbySaveLoading.Tests.csproj') /p:Configuration=Release /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Lobby save loading test compilation failed.' }
    & $msbuild (Join-Path $modRoot 'tests\LobbySaveLoading.Tests\UnityMonoHost.csproj') /p:Configuration=Release /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Unity Mono test host compilation failed.' }
    & (Join-Path $modRoot 'tests\LobbySaveLoading.Tests\bin\host\UnityMonoHost.exe') 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' (Join-Path $modRoot 'tests\LobbySaveLoading.Tests\bin\LobbySaveLoading.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Lobby save loading behavior/interop/IL tests failed.' }
}
Write-Output 'PASS: lobby save loading settings, localization, tooltips and permanent ownership.'
