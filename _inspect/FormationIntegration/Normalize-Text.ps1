param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$tracked = @(& git -C $workspace diff --name-only --diff-filter=AM)
$new = @(& git -C $workspace ls-files --others --exclude-standard)
$targets = @($tracked + $new | Where-Object {
    $_ -match '^(APIShared/|BugfixesAndQoL/|_inspect/FormationIntegration/|_inspect/BugfixesAndQoLNativeTests/Program\.cs$|_inspect/Test-ShiftWorkBuffers\.ps1$|Shared/UnitCommandSourceChecks/Program\.cs$|Testmods/MoatMove/src/MoatMoveOptions\.cs$)' -and
    $_ -match '\.(cs|csproj|xaml|ps1|py|md|txt|bat|json)$'
} | Sort-Object -Unique)
foreach ($relative in $targets) {
    $path = Join-Path $workspace $relative
    $original = [IO.File]::ReadAllText($path)
    $expected = [regex]::Replace($original, '\r\n|\r|\n', "`r`n")
    [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    $actual = [IO.File]::ReadAllText($path)
    if (-not [string]::Equals($actual, $expected, [StringComparison]::Ordinal) -or $actual -match '(?<!\r)\n') { throw "CRLF mismatch $relative" }
    Write-Output "$relative CRLF=$([regex]::Matches($actual, '\r\n').Count) bareLF=0"
}
