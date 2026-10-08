$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$mods = 'CastlePlanner','ExtraFeatures','BugfixesAndQoL'
$json = '\b(System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft(?:\.Json)?|DataContractJsonSerializer|JsonUtility)\b'
foreach ($mod in $mods) {
    $root = Join-Path $workspace $mod
    $sources = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter '*.cs')
    $projects = @(Get-ChildItem $root -File -Filter '*.csproj')
    foreach ($file in @($sources) + @($projects)) {
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -match $json) { throw "Forbidden runtime JSON: $($file.FullName)" }
        if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $($file.FullName)" }
        if ($text -match '\b(OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw "Review lifecycle: $($file.FullName)" }
        if ($text -match '\bOnDestroy\s*\(' -and $file.Name -ne 'CastlePlannerPlugin.cs') { throw "Review OnDestroy: $($file.FullName)" }
        if ($text -match ':\s*BaseUnityPlugin' -and $text -match '\b(?:void\s+(?:Update|LateUpdate|FixedUpdate)|StartCoroutine)\s*\(') { throw "Plugin frame runner: $($file.FullName)" }
    }
    foreach ($file in Get-ChildItem $root -Recurse -Filter '*.xaml' | Where-Object { $_.FullName -match '\\Patches\\' }) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($content in $xml.SelectNodes('//*[local-name()="Content"]')) {
            if (@($content.ChildNodes | Where-Object NodeType -eq Element).Count -ne 1) { throw "XAML Content root count: $($file.FullName)" }
        }
    }
}
$plugin = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/CastlePlannerPlugin.cs'))
$destroy = [regex]::Match($plugin,'(?s)private void OnDestroy\(\)\s*\{(.*?)\n        \}').Groups[1].Value
if ($destroy -match 'Dispose|Undo|-=|Disable\(') { throw 'CastlePlanner teardown' }
$rangeFiles = 'CastlePlanner/src/AIVPlacement/KeepRangeSettingsBridge.cs',
    'CastlePlanner/AIVPlacement.Core/KeepRangePolicy.cs'
foreach ($relative in $rangeFiles) {
    $text = [IO.File]::ReadAllText((Join-Path $workspace $relative))
    if ($text -match 'Marshal\.|VirtualProtect|NativeDetour|StartCoroutine|OnDestroy|OnDisable|OnApplicationQuit|\b(?:Update|LateUpdate|FixedUpdate)\s*\(') {
        throw "New runtime mutation or lifecycle runner: $relative"
    }
}
# Verify the only new game member against the real installed (non-publicized) assembly.
Add-Type -Path (Join-Path $workspace '_inspect/StatsTweakerApiTests/bin/Mono.Cecil.dll')
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'))
$platform = $assembly.MainModule.Types | Where-Object Name -eq 'Platform_Multiplayer'
$lobby = $platform.NestedTypes | Where-Object Name -eq 'MPLobby'
$getTeam = @($lobby.Methods | Where-Object { $_.Name -eq 'getTeam' -and $_.IsPublic -and $_.ReturnType.FullName -eq 'System.Int32' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.Name -eq 'MPLobbyMember' })
if ($getTeam.Count -ne 1) { throw 'Real assembly getTeam contract' }
$se = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx/plugins/000shcdese/SHCDESE.dll'))
$api = $se.MainModule.Types | Where-Object FullName -eq 'SHCDESE.API.GameBuildingManagerAPI'
foreach ($name in 'get_Instance','get_KeepProximityOverride','GetKeepProximityRange') {
    if (-not ($api.Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic })) { throw "Installed public SE API missing: $name" }
}
$enum = $se.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.eMappers'
$expected = @{ MAPPER_KEEP2=61; MAPPER_KILLING_PIT=98; MAPPER_PITCH_DITCH=99; MAPPER_DRAWBRIDGE=105; MAPPER_DOG_CAGE=312 }
$names = 'MAPPER_GATEHOUSE','MAPPER_GATE_MAIN','MAPPER_GATE_INNER','MAPPER_GATE_WOOD','MAPPER_GATE_POSTERN'
for ($i=0; $i -lt $names.Count; $i++) { $expected[$names[$i]] = 100+$i }
for ($i=1; $i -le 5; $i++) { $expected['MAPPER_TOWER'+$i] = 109+$i }
$names = 'MAPPER_GATE_WOOD1A','MAPPER_GATE_WOOD1B','MAPPER_GATE_WOOD1C','MAPPER_GATE_WOOD1D','MAPPER_GATE_STONE1A','MAPPER_GATE_STONE1B','MAPPER_GATE_STONE2A','MAPPER_GATE_STONE2B'
for ($i=0; $i -lt $names.Count; $i++) { $expected[$names[$i]] = 140+$i }
foreach ($pair in $expected.GetEnumerator()) {
    $field = $enum.Fields | Where-Object Name -eq $pair.Key
    if ($field.Constant -ne $pair.Value) { throw "Installed enum mismatch $($pair.Key)" }
}
$localeRoot = Join-Path $workspace 'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Locales'
foreach ($file in Get-ChildItem $localeRoot -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in 'CastlePlanner.KeepRangeBlocked','CastlePlanner.KeepRangePossible','CastlePlanner.KeepRangeUnknown') {
        if ([regex]::Matches($text,'(?m)^'+[regex]::Escape($key)+'=.+').Count -ne 1) { throw "Locale parity: $($file.Name) $key" }
    }
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Locale CRLF: $($file.Name)" }
}
& (Join-Path $workspace 'Shared/Tools/Validation/Test-PermanentNativeRuntimePatches.ps1')
Write-Output ('PASS: range runtime JSON/lifecycle, XAML, CRLF, locale keys; real game member: ' + $getTeam[0].FullName)
Write-Output 'PASS: installed SE public range APIs and complete mapper list; no new executable mutation.'
