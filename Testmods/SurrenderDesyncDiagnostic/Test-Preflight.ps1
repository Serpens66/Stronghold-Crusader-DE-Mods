[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
$mod = [pscustomobject]@{
    Name = 'SurrenderDesyncDiagnostic'
    Plugin = $true
    Project = 'TestMods\SurrenderDesyncDiagnostic\SurrenderDesyncDiagnostic.csproj'
}
Assert-SERuntimeModPreflight $mod $workspace

$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File)
$plugin = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\SurrenderDesyncDiagnosticPlugin.cs'))
$runtime = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\SurrenderDesyncDiagnosticRuntime.cs'))
$deferred = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\DeferredTraceRecorder.cs'))
$bridge = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\SurrenderDiagnosticBridge.cs'))
if ($plugin -match '\b(Update|LateUpdate|FixedUpdate|StartCoroutine|OnDestroy|OnDisable|OnApplicationQuit)\s*\(' -or
    $runtime -match '\b(StartCoroutine|OnDestroy|OnDisable|OnApplicationQuit)\s*\(' -or
    $runtime -notmatch 'GameTimeManagerAPI\.Instance\.OnTick\s*\+=\s*OnTick' -or
    $plugin -notmatch 'private static SurrenderDesyncDiagnosticRuntime runtime;' -or
    $runtime -match '\b(HookTransaction|NativeDetour|CodePatch|VirtualProtect|Marshal\.Write)\b' -or
    $bridge -notmatch 'if \(callbacks == null\)' -or
    $bridge -notmatch 'buffer\.Clone\(\)') {
    throw 'Surrender diagnostic lifetime, read-only or observer-isolation contract failed.'
}
if ($runtime -match 'Fields\s*<' -or $runtime -match 'Fields\s*\(\s*\*' -or
    $runtime -notmatch 'AddRecord\(category, key, typeof\(GamePlayerResources\), new IntPtr\(value\), sizeof\(GamePlayerResources\)\)' -or
    $runtime -notmatch 'finally \{ if \(end && active\) Finish\("first-resync-ended", tick\); \}' -or
    $runtime -match '\bfinished\b' -or
    $runtime -notmatch 'new DeferredTraceRecorder\(log\)' -or
    $deferred -notmatch 'IsBackground = true' -or
    $deferred -notmatch 'STATE_PROBE_OK' -or $deferred -notmatch 'STATE_PROBE_FAILED' -or
    $deferred -notmatch 'MaxQueuedBytes' -or $deferred -notmatch 'queueHighWater') {
    throw 'Surrender diagnostic large-record, rearm, resync or probe regression.'
}

$project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SurrenderDesyncDiagnostic.csproj'))
if ($project -match 'Assembly-CSharp-publicized|System\.Web\.Extensions|Newtonsoft\.Json|System\.Text\.Json' -or
    $project -notmatch '\\Assembly-CSharp\.dll') {
    throw 'Surrender diagnostic reference contract failed.'
}

foreach ($file in @($sources.FullName) + @(
    (Join-Path $PSScriptRoot 'SurrenderDesyncDiagnostic.csproj'),
    (Join-Path $PSScriptRoot 'info.json'),
    (Join-Path $PSScriptRoot 'build.bat'),
    (Join-Path $PSScriptRoot 'Test-Preflight.ps1'),
    (Join-Path $PSScriptRoot 'Test-Projection.ps1'),
    (Join-Path $PSScriptRoot 'Test-DeferredRecorder.ps1'),
    (Join-Path $PSScriptRoot 'Compare-Traces.ps1'),
    (Join-Path $workspace 'BugfixesAndQoL\src\SurrenderDiagnosticBridge.cs'))) {
    $text = [IO.File]::ReadAllText($file)
    $literalEscapes = [string][char]92
    $literalEscapes += 'r'
    $literalEscapes += [char]92
    $literalEscapes += 'n'
    if ($text -match '(?<!\r)\n' -or $text.Contains($literalEscapes)) {
        throw "CRLF validation failed: $file"
    }
}
Write-Output 'Surrender diagnostic JSON, lifecycle, read-only, hook and CRLF preflight succeeded.'
