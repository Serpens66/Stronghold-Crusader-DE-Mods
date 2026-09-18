$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$release = Get-Content -LiteralPath (Join-Path $workspace 'Shared\Release\release-projects.json') -Raw | ConvertFrom-Json
function Write-TaskText([string]$relative, [string]$value) {
    $path = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (-not $path.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw $path }
    $expected = [regex]::Replace($value, '\r?\n', "`r`n")
    [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    if (-not [string]::Equals([IO.File]::ReadAllText($path), $expected, [StringComparison]::Ordinal)) { throw "Readback failed: $relative" }
    Write-Output $relative
}
$projects = @(
    'UnitLimit\UnitLimit.csproj', 'SerpsModsHost\SerpsModsHost.csproj', 'RandomEvents\RandomEvents.csproj',
    'ExtremePowers\ExtremePowers.csproj', 'ImprovedHunters\ImprovedHunters.csproj', 'UnitCosts\UnitCosts.csproj',
    'Helpers\ActiveAIVDetector\ActiveAIVDetector.csproj', 'Testmods\SkinTest\SkinTest.csproj', 'CheatMod\CheatMod.csproj',
    'Testmods\AIAttackTest\AIAttackTest.csproj', 'ExtraFeatures\ExtraFeatures.csproj', 'Testmods\MoatMove\MoatMove.csproj',
    'Testmods\EnemyGatePathfindingTest\EnemyGatePathfindingTest.csproj', 'StartConditions\StartConditions.csproj',
    'CastlePlanner\CastlePlanner.csproj', 'BuildingCosts\BuildingCosts.csproj', 'BugfixesAndQoL\BugfixesAndQoL.csproj',
    'BuildingLimit\BuildingLimit.csproj', 'ExtendedData\ExtendedData.csproj'
)
foreach ($relative in $projects) {
    $path = Join-Path $workspace $relative
    $source = [IO.File]::ReadAllText($path)
    if ($source -notmatch '<Reference Include="APIShared"') {
        $addition = @'
  <PropertyGroup Condition="'$(ApiSharedDir)' == ''"><ApiSharedDir>$(GameDir)\BepInEx\plugins\APIShared_Serp</ApiSharedDir></PropertyGroup>
  <ItemGroup><Reference Include="APIShared"><HintPath>$(ApiSharedDir)\APIShared.dll</HintPath><Private>false</Private></Reference></ItemGroup>
  <Target Name="ValidateEditorLifecycleReference" BeforeTargets="BeforeBuild">
    <Error Condition="!Exists('$(ApiSharedDir)\APIShared.dll')" Text="APIShared.dll with editor lifecycle capability is required in ApiSharedDir." />
  </Target>
'@
        $closingProject = $source.LastIndexOf('</Project>', [StringComparison]::Ordinal)
        if ($closingProject -lt 0) { throw 'Missing root Project closing tag.' }
        $source = $source.Insert($closingProject, $addition + "`r`n")
        Write-TaskText $relative $source
    }
    $root = Split-Path -Parent $path
    $plugins = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*Plugin.cs' | Where-Object { [IO.File]::ReadAllText($_.FullName) -match '\[BepInPlugin\(' })
    if ($plugins.Count -ne 1) { throw "Expected one plugin: $relative" }
    $plugin = $plugins[0]
    $source = [IO.File]::ReadAllText($plugin.FullName)
    if ($source -notmatch 'BepInDependency\((?:"APIShared_Serp"|ApiSharedGuid)') {
        $consumer = [IO.Path]::GetFileNameWithoutExtension($relative)
        $apiSharedMinimum = [string]$release.ApiShared.Consumers.$consumer
        if (-not $apiSharedMinimum) { throw "Missing APIShared release dependency for $consumer." }
        $source = $source.Replace('[BepInPlugin(', "[BepInDependency(`"APIShared_Serp`", `"$apiSharedMinimum`")]`r`n    [BepInPlugin(")
        Write-TaskText $plugin.FullName.Substring($workspace.Length + 1) $source
    }
}
$relative = 'Shared\ScriptExtenderUpdate\mods.json'
$inventory = [IO.File]::ReadAllText((Join-Path $workspace $relative))
$inventory = [regex]::Replace($inventory, '(?ms)^  \{\r?\n.*?^  \}', [Text.RegularExpressions.MatchEvaluator]{
    param($match)
    $item = $match.Value | ConvertFrom-Json
    if ($projects -contains $item.Project -and @($item.DependsOn) -notcontains 'APIShared') {
        return [regex]::Replace($match.Value, '("DependsOn":\s*\[)([^\]]*)(\])', [Text.RegularExpressions.MatchEvaluator]{
            param($dependency)
            $existing = $dependency.Groups[2].Value.Trim()
            $body = if ($existing) { $existing + ', "APIShared"' } else { '"APIShared"' }
            return $dependency.Groups[1].Value + $body + $dependency.Groups[3].Value
        })
    }
    return $match.Value
})
Write-TaskText $relative $inventory

# Explicit mappings preserve each feature's existing reset contract.
$resets = @{
    'BuildingCosts\src\BuildingCostsRuntime.cs' = @('SubscribeStarted(log, OnSessionStarted)', 'SubscribeStarted(log, OnSessionStarted, ResetTooltipCache)')
    'BuildingLimit\src\BuildingLimitRuntime.cs' = @('SubscribeStarted(log, OnSessionStarted)', 'SubscribeStarted(log, OnSessionStarted, () => OnUnloadMap(null))')
    'UnitCosts\src\UnitCostsRuntime.cs' = @('SubscribeStarted(log, OnSessionStarted)', 'SubscribeStarted(log, OnSessionStarted, () => OnUnloadMap(null))')
    'UnitLimit\src\UnitLimitRuntime.cs' = @('SubscribeStarted(log, OnSessionStarted)', 'SubscribeStarted(log, OnSessionStarted, () => OnUnloadMap(null))')
    'RandomEvents\src\RandomEventsRuntime.cs' = @('SubscribeStarted(log, OnSessionStarted)', 'SubscribeStarted(log, OnSessionStarted, ResetMapState)')
    'CheatMod\src\CheatModRuntime.cs' = @('SubscribeStarted(log, _ => BeginMap())', 'SubscribeStarted(log, _ => BeginMap(), EndMap)')
    'UnitLimit\src\ActiveUnitCache.cs' = @('_ => ResyncAll(true)))', '_ => ResyncAll(true), onEditorEnded: Clear))')
    'UnitLimit\src\ActiveSiegeTentCache.cs' = @('_ => ResyncAll(true)))', '_ => ResyncAll(true), onEditorEnded: Clear))')
    'BuildingLimit\src\ActiveBuildingCache.cs' = @('_ => ResyncAll(true)))', '_ => ResyncAll(true), onEditorEnded: Clear))')
    'ExtraFeatures\src\AIDefenseRepairRuntime.cs' = @('OnSessionStarted));', 'OnSessionStarted, onEditorEnded: ResetMap));')
    'BugfixesAndQoL\src\AITowerRuinRepairFix.cs' = @('OnSessionStarted));', 'OnSessionStarted, onEditorEnded: ResetMap));')
}
foreach ($relative in $resets.Keys) {
    $source = [IO.File]::ReadAllText((Join-Path $workspace $relative))
    $pair = $resets[$relative]
    if (-not $source.Contains($pair[0])) { throw "Missing replacement: $relative $($pair[0])" }
    Write-TaskText $relative $source.Replace($pair[0], $pair[1])
}
