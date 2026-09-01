param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('search', 'function', 'callers', 'callees', 'managed', 'diff', 'stats')]
    [string]$Command,

    [Parameter(ValueFromRemainingArguments = $true, Position = 1)]
    [string[]]$Arguments
)

$semanticTools = Split-Path -Parent $MyInvocation.MyCommand.Path
$baselineRoot = Split-Path -Parent (Split-Path -Parent $semanticTools)
$currentFile = Join-Path $baselineRoot 'CURRENT.md'
$currentIndexPath = Join-Path $baselineRoot 'CURRENT.json'
if (-not (Test-Path -LiteralPath $currentIndexPath -PathType Leaf)) {
    throw "Machine-readable baseline identity is missing: $currentIndexPath. See $currentFile."
}
$currentIndex = Get-Content -LiteralPath $currentIndexPath -Raw | ConvertFrom-Json
$manifestPath = [IO.Path]::GetFullPath((Join-Path $baselineRoot $currentIndex.databaseManifest))
$baselinePrefix = [IO.Path]::GetFullPath($baselineRoot).TrimEnd('\') + '\'
if (-not $manifestPath.StartsWith($baselinePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "CURRENT.json points outside the baseline: $manifestPath"
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Database manifest is missing: $manifestPath"
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.identities.currentNativeHash -ne $currentIndex.currentNativeHash) {
    throw 'CURRENT.json and DATABASE_INFO.json identify different native DLLs.'
}
$database = [IO.Path]::GetFullPath((Join-Path $baselineRoot $manifest.database.path))
if (-not $database.StartsWith($baselinePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "DATABASE_INFO.json points outside the baseline: $database"
}

if (-not (Test-Path -LiteralPath $database -PathType Leaf)) {
    $restore = "& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' RestoreDatabase"
    throw "The current semantic database is not present locally: $database. Restore it from the tracked exports with: $restore"
}

$portablePython = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
if (-not (Test-Path -LiteralPath $portablePython)) {
    throw "Required portable Python was not found at $portablePython."
}

& $portablePython (Join-Path $semanticTools 'query.py') --database $database $Command @Arguments
exit $LASTEXITCODE
