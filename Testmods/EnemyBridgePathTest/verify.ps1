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
# The only hook release is the unpublished candidate inside Prepare (catch path before Enable).
$runtimeText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\DrawbridgeDeckRuntime.cs'))
if ([regex]::Matches($runtimeText, '\.Dispose\(\)').Count -ne 1 -or $runtimeText -notmatch 'catch \{ \(\(IDisposable\)hook\)\.Dispose\(\); throw; \}') {
    throw 'Hook release outside the unpublished-candidate path requires audit.'
}
foreach ($path in @($projectPath, (Join-Path $PSScriptRoot 'EnemyBridgePathTest.PolicyTests.csproj'), (Join-Path $PSScriptRoot 'tests\Program.cs')) + $sources) {
    $bytes = [IO.File]::ReadAllBytes($path)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i - 1] -ne 13)) { throw "Bare LF line ending: $path" }
    }
}
Write-Host 'EnemyBridgePathTest source checks passed.'
