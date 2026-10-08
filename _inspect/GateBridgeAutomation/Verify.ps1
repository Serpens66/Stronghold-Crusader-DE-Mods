$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
if ((Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash -ne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') {
    throw 'Native provenance changed; repeat the feature audit.'
}
$forbidden = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
foreach ($mod in @('APIShared','ExtraFeatures')) {
    $root = Join-Path $workspace $mod
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -File -Filter '*.cs')
    $projects = @(Get-ChildItem -LiteralPath $root -File -Filter '*.csproj')
    foreach ($file in @($sources) + @($projects)) {
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -match $forbidden) { throw "Forbidden JSON dependency: $($file.FullName)" }
        if ($text -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|StartCoroutine)\s*\(') {
            throw "Lifecycle/runner pattern needs review: $($file.FullName)"
        }
        if ($file.Name -like '*Plugin.cs' -and $text -match '\b(Update|LateUpdate|FixedUpdate)\s*\(') {
            throw "Plugin frame callback: $($file.FullName)"
        }
    }
    foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $root 'Patches') -Recurse -File -Filter '*.xaml')) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($content in $xml.SelectNodes('//*[local-name()="Content"]')) {
            if (@($content.ChildNodes | Where-Object NodeType -eq Element).Count -ne 1) {
                throw "XAML Content requires exactly one root: $($file.FullName)"
            }
        }
    }
}
$runtime = [IO.File]::ReadAllText((Join-Path $workspace 'ExtraFeatures/src/GatehouseAutomationRuntime.cs'))
if ($runtime.Contains('args.ShouldClose = false;')) { throw 'Manual gate still vetoes the bridge enemy scan.' }
if (-not $runtime.Contains('SetManualGateTimer(building, true)') -or
    -not $runtime.Contains('building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE') -or
    -not $runtime.Contains('IsGatehouseType(building->r_BuildingType)')) { throw 'Typed gate/bridge policy contract missing.' }
$automation = [IO.File]::ReadAllText((Join-Path $workspace 'APIShared/src/Buildings/GatehouseAutomationNativeState.cs'))
if ($runtime -notmatch 'SetManualGateTimer\(building,\s*automationReady && Shared.GameplayModActivationGate.IsEnabled\(settings.EnableMod\)\)') {
    throw 'Late editor locator restoration must honor activation and release disabled sentinels.'
}
if ($automation -match '\bpublic\s+void\s+Dispose\(' -or
    $automation -match '\.Hook\.(Undo|Dispose|Disable|Enable)\(' -or
    $automation -notmatch 'Volatile.Write\(ref resolver, value\)') { throw 'Permanent automation publication contract changed.' }
foreach ($locale in Get-ChildItem -LiteralPath (Join-Path $workspace 'ExtraFeatures/Locales') -File -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($locale.FullName)
    foreach ($key in @('SomeSettings.DrawbridgeAutomaticEnabledTooltip','SomeSettings.DrawbridgeManualOnlyTooltip')) {
        if ([regex]::Matches($text, '(?m)^' + [regex]::Escape($key) + '=').Count -ne 1) { throw "Missing/duplicate translation $key in $locale" }
    }
}
& (Join-Path $workspace 'Shared/Tools/Validation/Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace permanent hook regression failed.' }
Write-Host 'PASS: gate/bridge preflight (hash, JSON, lifecycle, callbacks, XAML, publication, locales).'
