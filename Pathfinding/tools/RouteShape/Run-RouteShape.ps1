param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    # Exported loads (Testmods/GroupPathProbe --export-anchor-loads); '-' runs only synthetic scenarios.
    [string]$Loads = '-',
    # Git revision of the baseline core; empty compares nothing and measures only the working tree.
    [string]$Baseline = 'HEAD',
    [switch]$SkipBaseline
)
$ErrorActionPreference = 'Stop'
# Offline route shape benchmark for TODO G10. Compiles RouteShapeBench.cpp against the baseline core
# from Git and against the working tree core, runs both on identical queries and compares their
# routes (status equal, candidate cost never higher). Writes only into -OutputDirectory.
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$repo = [IO.Path]::GetFullPath((Join-Path $mod '..'))
. (Join-Path $repo 'Shared/Tools/BuildCache/MsvcToolchain.ps1')
$toolchain = Get-MsvcToolchain
$env:INCLUDE = $toolchain.IncludeDirectories -join ';'
$env:LIB = $toolchain.LibraryDirectories -join ';'
$cl = Join-Path $toolchain.CompilerRoot 'bin/Hostx64/x64/cl.exe'
$out = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($out) | Out-Null
$bench = Join-Path $PSScriptRoot 'RouteShapeBench.cpp'
$loadsPath = if ($Loads -eq '-') { '-' } else { [IO.Path]::GetFullPath($Loads) }

function Build-Bench([string]$name, [string]$source) {
    $dir = Join-Path $out $name; [IO.Directory]::CreateDirectory($dir) | Out-Null
    $exe = Join-Path $dir 'RouteShapeBench.exe'
    & $cl /nologo /std:c++17 /O2 /Ob2 /EHsc /MT /W3 $source "/Fo$dir/" "/Fe$exe" | Out-Host
    if ($LASTEXITCODE) { throw "route shape bench compilation failed: $name" }
    return $exe
}
function Invoke-Bench([string]$name, [string]$exe) {
    $dir = Join-Path $out $name
    $started = [DateTime]::Now
    & $exe $loadsPath (Join-Path $dir 'summary.csv') (Join-Path $dir 'routes.bin') | Tee-Object -FilePath (Join-Path $dir 'console.txt') | Out-Host
    if ($LASTEXITCODE) { throw "route shape bench failed: $name" }
    Write-Host ("{0} finished in {1:n1} s" -f $name, ([DateTime]::Now - $started).TotalSeconds)
}

$candidate = Build-Bench 'candidate' $bench
if (-not $SkipBaseline) {
    $baseDir = Join-Path $out 'baseline'; [IO.Directory]::CreateDirectory($baseDir) | Out-Null
    $core = Join-Path $baseDir 'PathfindingAnchor.cpp'
    # cmd redirection keeps the blob bytes unchanged (PowerShell pipes would re-encode them).
    & cmd.exe /d /c ('git -C "' + $repo + '" show "' + $Baseline + ':Pathfinding/native/PathfindingAnchor.cpp" > "' + $core + '"')
    if ($LASTEXITCODE) { throw "baseline core not found at $Baseline" }
    $wrapper = Join-Path $baseDir 'BaselineBench.cpp'
    $lines = @('#define ROUTE_SHAPE_KERNEL', ('#include "' + ($core -replace '\\', '/') + '"'), ('#include "' + ($bench -replace '\\', '/') + '"'))
    [IO.File]::WriteAllText($wrapper, ($lines -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
    $revision = (& git -C $repo rev-parse $Baseline).Trim()
    @{ baseline = $revision; createdUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $baseDir 'baseline.json') -Encoding UTF8
    $baselineExe = Build-Bench 'baseline' $wrapper
    Invoke-Bench 'baseline' $baselineExe
}
Invoke-Bench 'candidate' $candidate
if (-not $SkipBaseline) {
    & $candidate --compare (Join-Path $out 'baseline/routes.bin') (Join-Path $out 'candidate/routes.bin') | Tee-Object -FilePath (Join-Path $out 'compare.txt') | Out-Host
    if ($LASTEXITCODE) { throw 'route comparison failed' }
}
