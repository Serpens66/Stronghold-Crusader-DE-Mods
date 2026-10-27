param(
    [string]$GhidraDirectory,
    [string]$NativeDll,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# This launcher is standalone: it needs only this package, Ghidra and the user's DLL.
function Get-PackageSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToUpperInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
function Read-PathValue([string]$Value, [string]$Prompt) {
    if ([string]::IsNullOrWhiteSpace($Value)) { $Value = Read-Host $Prompt }
    if ([string]::IsNullOrWhiteSpace($Value)) { throw 'A path is required.' }
    return [IO.Path]::GetFullPath($Value.Trim().Trim('"'))
}
$packageRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$manifest = [IO.File]::ReadAllText((Join-Path $packageRoot 'PACKAGE.json')) | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) { throw 'Unsupported annotation package schema.' }
foreach ($entry in $manifest.files) {
    $file = [IO.Path]::GetFullPath((Join-Path $packageRoot $entry.path))
    if (-not $file.StartsWith($packageRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid manifest path.' }
    if ((Get-PackageSha256 $file) -cne $entry.sha256) { throw "Package checksum mismatch: $($entry.path)" }
}
$GhidraDirectory = Read-PathValue $GhidraDirectory 'Ghidra installation directory (tested: 12.1.3)'
$NativeDll = Read-PathValue $NativeDll 'Path to your own installed CrusaderDE.dll'
$OutputDirectory = Read-PathValue $OutputDirectory 'New project directory (short path, e.g. D:\Ghidra\CrusaderDE-Knowledge)'
$headless = Join-Path $GhidraDirectory 'support\analyzeHeadless.bat'
if (-not [IO.File]::Exists($headless)) { throw 'Ghidra analyzeHeadless.bat was not found.' }
$properties = [IO.File]::ReadAllText((Join-Path $GhidraDirectory 'Ghidra\application.properties'))
if ($properties -notmatch '(?m)^application.version=12\.1\.3\s*$') { throw 'Use Ghidra 12.1.3, the version verified for this package.' }
if ((Get-PackageSha256 $NativeDll) -cne $manifest.nativeHash) { throw 'Native DLL SHA-256 does not match this package. No project was created.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Output directory already exists. Choose a new directory; existing projects are preserved.' }
if (($OutputDirectory -split '[\\/]') | Where-Object { $_.StartsWith('.') }) { throw 'Ghidra project paths cannot contain directory components starting with a dot.' }
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$scriptDirectory = Join-Path $packageRoot 'scripts'
$dataDirectory = Join-Path $packageRoot 'data'
$plan = Join-Path $dataDirectory 'native-field-ghidra.json'
$importReport = Join-Path $OutputDirectory 'import.json'
Write-Host ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + ' Creating project from your DLL and importing annotations...')
& $headless $OutputDirectory 'CrusaderDE-Knowledge' -import $NativeDll -noanalysis -scriptPath $scriptDirectory -postScript TransferAnnotations.java import $dataDirectory $importReport -log (Join-Path $OutputDirectory 'import.log') *> (Join-Path $OutputDirectory 'import-console.log')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $importReport)) { throw "Ghidra import failed. Inspect $OutputDirectory\import-console.log" }
$verification = Join-Path $OutputDirectory 'verify.json'
$fields = Join-Path $OutputDirectory 'verify-fields.json'
$functions = Join-Path $OutputDirectory 'verify-functions.json'
Write-Host ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + ' Reopening and verifying annotations...')
& $headless $OutputDirectory 'CrusaderDE-Knowledge' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $scriptDirectory -postScript TransferAnnotations.java verify $dataDirectory $verification -postScript ImportNativeKnowledge.java $plan verify $fields -postScript ImportNativeFunctionNames.java $plan verify $functions -log (Join-Path $OutputDirectory 'verify.log') *> (Join-Path $OutputDirectory 'verify-console.log')
if ($LASTEXITCODE -ne 0) { throw 'Ghidra verification process failed.' }
foreach ($report in @($importReport, $verification, $fields, $functions)) {
    if (-not (Test-Path -LiteralPath $report)) { throw "Verification did not complete: $report" }
    if (([IO.File]::ReadAllText($report) | ConvertFrom-Json).status -ne 'ok') { throw "Verification failed: $report" }
}
Write-Host ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + ' Verified. Open this project in Ghidra:')
Write-Host (Join-Path $OutputDirectory 'CrusaderDE-Knowledge.gpr')
Write-Host 'Data Type Manager -> CrusaderDE.dll -> NativeKnowledge -> FBCB9319 -> START_HERE'
