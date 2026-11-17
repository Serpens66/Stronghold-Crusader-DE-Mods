[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Source guardrails for the runtime project only (heuristics, not a behaviour proof):
# forbidden runtime JSON serializers, Unity lifecycle/polling callbacks and executable
# patching or teardown of published hooks outside the audited detour helper.
$projectPath = Join-Path $PSScriptRoot 'EnemyBridgePathTest.csproj'
[xml]$project = [IO.File]::ReadAllText($projectPath)
$sources = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_.Include)).Path
})
if ($sources.Count -eq 0) { throw 'No runtime sources found in EnemyBridgePathTest.csproj' }
$forbidden = 'JavaScriptSerializer|System\.Web\.Extensions|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility'
foreach ($path in @($projectPath) + $sources) {
    $text = [IO.File]::ReadAllText($path)
    if ($text -match $forbidden) { throw "Forbidden runtime JSON dependency: $path" }
    if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|Start|OnEnable)\s*\(' -or
        $text -match '\bStartCoroutine\s*\(') { throw "Runtime lifecycle callback requires audit: $path" }
    if ($text -match '\b(?:VirtualProtect|FlushInstructionCache)\s*\(|\bCodePatch\.Write\s*\(|\bMarshal\.Write\w*\s*\(|\.(?:Disable|Undo)\s*\(') {
        throw "Runtime executable mutation or published hook teardown requires audit: $path"
    }
}
# This acceptance mod installs no hooks and never writes game memory (read-only inspector).
foreach ($path in $sources) {
    $text = [IO.File]::ReadAllText($path)
    if ($text -match 'NativeDetour|DetourRequest|HookTransaction|SetDirections') { throw "Unexpected hook or grid write in the acceptance mod: $path" }
}
foreach ($path in @($projectPath) + $sources) {
    $bytes = [IO.File]::ReadAllBytes($path)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i - 1] -ne 13)) { throw "Bare LF line ending: $path" }
    }
}
Write-Host 'EnemyBridgePathTest source checks passed.'
