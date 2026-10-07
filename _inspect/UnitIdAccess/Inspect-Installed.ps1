$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
[void][Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx\core\Mono.Cecil.dll'))
$snapshot = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'latest-run.json') | ConvertFrom-Json
$paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($row in $snapshot.ownInstalledAssemblies) { foreach ($item in $row.installed) { [void]$paths.Add([string]$item.path) } }
foreach ($path in @(
    'BepInEx\plugins\000shcdese\SHCDESE.dll', 'BepInEx\plugins\Fixes\Fixes.dll',
    'BepInEx\plugins\EnemyGatePathfindingTest_Serp\EnemyGatePathfindingTest.dll',
    'BepInEx\plugins\SpectatorEditorBuildTest_Serp\SpectatorEditorBuildTest.dll'
)) { $full = Join-Path $game $path; if (Test-Path -LiteralPath $full) { [void]$paths.Add($full) } }
$unitIdMethods = @('TryGetUnitById','TryGetUnitByIdEx','GetGlobalId','GetType','GetOwner','GetTribe','DeleteUnit','DeleteUnitSafe','KillUnit','MoveToTile','GetCurrentHealth','GetMaxHealth','GetSpeed','SetCurrentHealth','SetMaxHealth','SetSpeed','SetCurrentLocalTilePosition')
function Read-UnitCalls($Types, [string]$AssemblyPath) {
    foreach ($type in $Types) {
        foreach ($method in $type.Methods) {
            if (-not $method.HasBody) { continue }
            $instructions = $method.Body.Instructions
            for ($i = 0; $i -lt $instructions.Count; $i++) {
                $operand = $instructions[$i].Operand
                if ($operand -isnot [Mono.Cecil.MethodReference] -or $operand.DeclaringType.FullName -ne 'SHCDESE.API.GameUnitManagerAPI' -or $operand.Name -notin $unitIdMethods) { continue }
                $nearby = @(for ($j=[Math]::Max(0,$i-10); $j -le [Math]::Min($instructions.Count-1,$i+3); $j++) { [string]$instructions[$j] })
                [pscustomobject]@{ Assembly=$AssemblyPath; Caller=$method.FullName; Api=$operand.Name; Offset=$instructions[$i].Offset; IL=$nearby }
            }
        }
        Read-UnitCalls $type.NestedTypes $AssemblyPath
    }
}
$rows = @(foreach ($path in $paths) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    try { Read-UnitCalls $assembly.MainModule.Types $path } finally { $assembly.Dispose() }
})
$rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'installed-unit-calls.json') -Encoding utf8
$groups = $rows | Group-Object Assembly
foreach ($group in $groups) { Write-Output "$([IO.Path]::GetFileName($group.Name)): $($group.Count) installed IL call sites" }
# Public signatures used by the helper, checked without executing any game assembly.
$se = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll'))
try {
    $type = @($se.MainModule.Types | Where-Object FullName -eq 'SHCDESE.API.GameUnitManagerAPI')[0]
    foreach ($name in 'get_Instance','IsValidId','TryGetUnitById') {
        $methods = @($type.Methods | Where-Object Name -eq $name)
        if ($methods.Count -ne 1 -or -not $methods[0].IsPublic) { throw "Public helper dependency not confirmed: $name" }
        Write-Output "PUBLIC: $($methods[0].FullName)"
    }
} finally { $se.Dispose() }
Write-Output "Recorded $($rows.Count) installed unit-call sites; IL evidence does not by itself prove runtime caller attribution."
