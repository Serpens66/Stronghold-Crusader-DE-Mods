$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$textFiles = @(
    (Join-Path $root 'KeepCampfireGroundProbe.csproj'),
    (Join-Path $root 'Properties\AssemblyInfo.cs'),
    (Join-Path $root 'src\KeepCampfireGroundProbePlugin.cs'),
    (Join-Path $root 'src\GroundProbeRuntime.cs'),
    (Join-Path $root 'info.json'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'Build-KeepCampfireGroundProbe.ps1'),
    $PSCommandPath
)

foreach ($path in $textFiles) {
    if (-not [IO.File]::Exists($path)) { throw "Missing source: $path" }
    $content = [IO.File]::ReadAllText($path)
    if ([regex]::IsMatch($content, '(?<!\r)\n')) { throw "Non-CRLF text: $path" }
    $literalEscapedNewline = [string][char]92 + 'r' + [char]92 + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r/backslash-n sequence: $path" }
}

$sources = @((Join-Path $root 'KeepCampfireGroundProbe.csproj')) +
    @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$sourceText = ($sources | ForEach-Object { [IO.File]::ReadAllText($_) }) -join "`n"
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
if ([regex]::IsMatch($sourceText, $forbiddenJson)) { throw 'Forbidden runtime JSON dependency' }
$forbiddenLifecycle = '\b(?:OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|Update|LateUpdate|FixedUpdate)\s*\('
if ([regex]::IsMatch($sourceText, $forbiddenLifecycle)) { throw 'Forbidden plugin lifecycle callback or coroutine' }
$forbiddenMutation = 'CodePatch\.Write|Marshal\.Write(?:Byte|Int16|Int32|Int64)|VirtualProtect|NativeDetour|X64InlineHook|\.Undo\s*\(|\.Apply\s*\(|\.Dispose\s*\('
if ([regex]::IsMatch($sourceText, $forbiddenMutation)) { throw 'Unexpected runtime hook or executable memory mutation' }
if ($sourceText -match 'Assembly-CSharp-publicized') { throw 'Unexpected publicized Assembly-CSharp reference' }

$manifest = Get-Content -LiteralPath (Join-Path $root 'info.json') -Raw | ConvertFrom-Json
if ($manifest.GUID -ne 'KeepCampfireGroundProbe_Serp' -or $manifest.NetworkMode -ne 0 -or
    $manifest.Version -ne '0.1.0') { throw 'Manifest identity, network mode or version mismatch' }

# Inspect added tracked C# lines across the workspace as a regression signal.
$workspace = (Resolve-Path (Join-Path $root '..\..')).Path
$addedLines = @(git -C $workspace diff --unified=0 -- '*.cs' | Where-Object {
    $_.StartsWith('+') -and -not $_.StartsWith('+++')
})
$otherHookChanges = @($addedLines | Where-Object {
    $_ -match 'CodePatch\.Write|Marshal\.Write(?:Byte|Int16|Int32|Int64)|VirtualProtect|NativeDetour|X64InlineHook|\.Undo\s*\(|\.Apply\s*\('
})
if ($otherHookChanges.Count -gt 0) {
    Write-Warning "Workspace has $($otherHookChanges.Count) added hook/mutation lines outside this new mod; inspect separately."
}
Write-Output 'KeepCampfireGroundProbe preflight passed: CRLF, JSON, lifecycle, hook mutation, manifest.'
