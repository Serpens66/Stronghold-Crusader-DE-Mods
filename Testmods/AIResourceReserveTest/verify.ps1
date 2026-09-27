[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$workspace = (Resolve-Path (Join-Path $root '..\..')).Path
[xml]$projectXml = [IO.File]::ReadAllText((Join-Path $root 'AIResourceReserveTest.csproj'))
$linkedSources = @($projectXml.Project.ItemGroup.Compile | ForEach-Object {
    $include = [string]$_.Include
    if (-not [string]::IsNullOrWhiteSpace($include)) {
        [IO.Path]::GetFullPath((Join-Path $root $include))
    }
})
$files = @(
    (Join-Path $root 'AIResourceReserveTest.csproj'),
    (Join-Path $root 'info.json'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'verify.ps1')
) + $linkedSources
foreach ($path in $files) {
    $content = [IO.File]::ReadAllText($path)
    $literalNewlineEscape = [string]::Concat([char]92, 'r', [char]92, 'n')
    if ($content -match '(?<!\r)\n' -or $content.Contains($literalNewlineEscape)) {
        throw "Text file needs CRLF or contains literal escape characters: $path"
    }
}

$sources = @($linkedSources | ForEach-Object { [IO.File]::ReadAllText($_) })
$project = [IO.File]::ReadAllText((Join-Path $root 'AIResourceReserveTest.csproj'))
$allText = ($sources -join "`n") + $project
if ($allText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility') {
    throw 'Forbidden runtime JSON dependency.'
}
if ($allText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Unity component lifecycle or frame callback found in testmod.'
}
if ($allText -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|\.Undo\s*\(|\.Disable\s*\(') {
    throw 'Executable runtime mutation or hook teardown found in testmod.'
}
if ($project -match 'Assembly-CSharp-publicized|System\.Web\.Extensions') {
    throw 'Forbidden assembly reference in testmod.'
}
if ($allText -notmatch 'GameTimeManagerAPI\.Instance\.OnTick' -or
    $allText -notmatch 'AI_RESERVE_POST_STARTUP_TICK') {
    throw 'Long-lived tick publisher or post-cleanup log marker is missing.'
}
$manifest = Get-Content -Raw -LiteralPath (Join-Path $root 'info.json') | ConvertFrom-Json
if ($manifest.NetworkMode -ne 1) { throw 'NetworkMode must be 1.' }

$extendedData = Join-Path $workspace 'ExtendedData\src'
$extendedSource = @(Get-ChildItem -LiteralPath $extendedData -File -Filter '*.cs' |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($extendedSource -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility') {
    throw 'Forbidden JSON dependency in ExtendedData runtime.'
}
if ($extendedSource -match 'OnDestroy\s*\([^)]*\)\s*\{[^}]*\.Dispose\s*\(') {
    throw 'ExtendedData plugin teardown reaches Dispose.'
}
if (-not (Test-Path -LiteralPath 'D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\shcde-fixes-main' -PathType Container)) {
    throw 'Canonical local Fixes source is unavailable for compatibility review.'
}

Write-Host 'AI resource reserve source preflight passed.'
