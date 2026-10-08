$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$inventory = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'inventory.json') | ConvertFrom-Json
$paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $inventory.files) { [void]$paths.Add([string]$file.path) }
foreach ($entry in $inventory.projects.psobject.Properties) {
    [void]$paths.Add($entry.Name)
    $projectRoot = Split-Path -Parent $entry.Name
    [void]$paths.Add((Join-Path $projectRoot 'build.bat'))
}
foreach ($file in (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'indirect.json') | ConvertFrom-Json)) {
    if ($file.method -ne 'IsValid') { [void]$paths.Add([string]$file.path) }
}
foreach ($path in @(
    'APIShared\src\Units\UnitAccess.cs', 'APIShared\src\Core\APISharedPlugin.cs', 'Shared\Test-UnitAccess.ps1',
    'Helpers\HunterQueryTargetDiagnostic\src\HunterQueryTargetDiagnosticPlugin.cs',
    'Helpers\HunterQueryTargetDiagnostic\info.json', 'Testmods\FormationTest\src\FormationTestPlugin.cs', 'Testmods\FormationTest\info.json',
    'BugfixesAndQoL\tests\Program.cs', 'Testmods\SkinTest\tests\Program.cs', 'Testmods\EnemyGatePathfindingTest\tests\Program.cs',
    '_inspect\BugfixesAndQoLNativeTests\Program.cs', 'BugfixesAndQoL\tests\FriendlyMoatMovement.Tests\Program.cs',
    'BugfixesAndQoL\tests\FriendlyMoatMovement.Tests\RuntimeHarness.cs', 'Testmods\MoatMove\tests\Program.cs',
    'Testmods\MoatMove\tests\RuntimeHarness.cs', 'Testmods\MoatMove\tests\StandaloneContracts.cs', 'Testmods\MoatMove\UNIT_ACCESS_PROVENANCE.json', '_inspect\Test-ExtraFeaturesSessionCallbacks.ps1',
    '_inspect\ExtraFeaturesSessionTests\ExtraFeaturesSessionTests.csproj', '_inspect\TemporaryGateAcceptanceRuntimeTests\Program.cs',
    '_inspect\TemporaryGateAcceptanceRuntimeTests\Fixture.cs', 'Testmods\OutpostTest\Build-OutpostTest.ps1'
)) { [void]$paths.Add($path) }
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in '.cs','.csproj','.ps1','.py','.json','.txt' }) {
    [void]$paths.Add($file.FullName.Substring($workspace.Length + 1))
}
$rows = @(foreach ($relative in $paths) {
    $path = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (-not $path.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Target outside workspace' }
    $original = [IO.File]::ReadAllText($path)
    $expected = [regex]::Replace($original, '\r?\n', [Environment]::NewLine)
    if (-not [string]::Equals($expected, $original, [StringComparison]::Ordinal)) {
        [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    }
    $actual = [IO.File]::ReadAllText($path)
    if (-not [string]::Equals($expected, $actual, [StringComparison]::Ordinal)) { throw "Text verification failed: $relative" }
    $bare = [regex]::Matches($actual, '(?<!\r)\n').Count
    if ($bare -ne 0) { throw "Bare LF: $relative" }
    # Regex literals may intentionally contain backslash-r/backslash-n; reject only inserted standalone escape lines.
    if ($actual -match '(?m)^\\r\\n$') { throw "Literal escape line: $relative" }
    [pscustomobject]@{ Path=$relative; CRLF=[regex]::Matches($actual, '\r\n').Count; BareLF=$bare; FirstLine=($actual -split "`r`n")[0] }
})
$rows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'text-check.json') -Encoding utf8
Write-Output "PASS: $($rows.Count) explicit text targets; ordinal readback, CRLF counts, no bare LF, representative first lines recorded."
