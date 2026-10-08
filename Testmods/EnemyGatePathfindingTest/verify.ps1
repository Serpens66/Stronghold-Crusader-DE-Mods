[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $PSScriptRoot 'EnemyGatePathfindingTest.csproj'
[xml]$project = [IO.File]::ReadAllText($projectPath)
$sources = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_.Include)).Path
})
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
$runtime = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/EnemyGatePathfindingRuntime.cs'))
if ($runtime -match 'registers->Rflags|AddContextHook') { throw 'Capturer callback must not use the old flags transport.' }
$textFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.md','.json','.xaml')
})
foreach ($file in $textFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -Filter '*.xaml')) {
    [xml]$xaml = [IO.File]::ReadAllText($file.FullName)
    foreach ($content in $xaml.SelectNodes("//*[local-name()='Content']")) {
        $elements = @($content.ChildNodes | Where-Object NodeType -eq Element)
        if ($elements.Count -ne 1) { throw "XAML Content requires exactly one root: $($file.FullName)" }
    }
}
& (Join-Path $workspace 'Shared/Tools/Validation/Test-PermanentNativeRuntimePatches.ps1')
Write-Host "PASS: EnemyGate runtime JSON/lifecycle, permanent hooks, XAML and CRLF ($($sources.Count) runtime sources)."
