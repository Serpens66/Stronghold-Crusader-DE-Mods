$ErrorActionPreference = 'Stop'
$policy = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'src\KeepBuildRangeOverride.cs')
$tests = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'tests\KeepBuildRangeTests.cs')
$runtime = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'src\KeepBuildRangeRuntime.cs')
$harness = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'tests\KeepBuildRangeRuntimeHarness.cs')
$redbird = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\RedBird.Core.dll'
Add-Type -Path $redbird
$body = [regex]::Replace(($policy + "`r`n" + $tests + "`r`n" + $runtime + "`r`n" + $harness), '(?m)^using [^;]+;\r?\n', '')
$imports = 'using System; using APIShared; using BepInEx.Logging; using R3; using SHCDESE.API; using RedBird.Core.Memory.Managed;'
Add-Type -TypeDefinition ($imports + "`r`n" + $body) -ReferencedAssemblies $redbird
[ExtraFeatures.KeepBuildRangeTests]::Run()
[ExtraFeatures.KeepBuildRangeRuntimeTests]::Run()
$parentRuntime = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'src\ExtraFeaturesRuntime.cs')
if ($parentRuntime -notmatch '(?s)if \(propertyName == nameof\(ExtraFeaturesViewModel.KeepBuildRange\)\)\s*\{\s*processKeepBuildRangeRuntime\?\.Refresh\(\);\s*return;' -or
    ($policy + $runtime) -match 'threadLock|SendPacket|EngineInterface') {
    throw 'Keep range must use the existing settings flow once, without additional engine locks or transport.'
}
$viewModel = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'src\ExtraFeaturesViewModel.cs')
if ($viewModel -notmatch 'private int keepBuildRange = -1;' -or
    $viewModel -notmatch '\[SyncHostOnly\] public int KeepBuildRange' -or
    $viewModel -notmatch 'SetIntSetting\(ref keepBuildRange, value, -1, 500,' -or
    $viewModel -notmatch 'KeepBuildRange = -1;' -or
    $viewModel -notmatch 'SetIntValueText\(value, parsed => KeepBuildRange = parsed') {
    throw 'Keep range host/default/reset/input contract failed.'
}
[xml]$xaml = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Override\ScriptExtenderUI\ExtraFeaturesSettings.xaml')
$slider = @($xaml.SelectNodes('//*[local-name()="Slider"]') | Where-Object { $_.Value -like '*KeepBuildRange,*' })
if ($slider.Count -ne 1 -or $slider[0].Minimum -ne '-1' -or $slider[0].Maximum -ne '500' -or
    $slider[0].TickFrequency -ne '1' -or $slider[0].SmallChange -ne '1' -or
    $slider[0].LargeChange -ne '1' -or $slider[0].IsSnapToTickEnabled -ne 'True' -or
    $slider[0].GetAttribute('ToolTipService.ShowDuration') -ne '60000' -or
    $null -eq $slider[0].SelectSingleNode('ancestor::*[@IsEnabled="{Binding CanEditHostSettings}"]')) {
    throw 'Keep range slider or host editing gate is invalid.'
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Locales') -Filter '*.txt') {
    $text = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($key in @('SomeSettings.KeepBuildRange','SomeSettings.KeepBuildRangeHelp')) {
        if ([regex]::Matches($text, '(?m)^' + [regex]::Escape($key) + '=.+').Count -ne 1) {
            throw "Missing or duplicate locale key: $($file.Name) $key"
        }
    }
}
Write-Output 'PASS: Keep range host setting, slider and locale contracts.'
