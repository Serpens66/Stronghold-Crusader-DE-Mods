$ErrorActionPreference = 'Stop'
# Runtime rules of the new mod: no JSON serializers, no teardown of published hooks, consistent identity.
$modDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sources = [IO.Directory]::GetFiles((Join-Path $modDir 'src'), '*.cs', [IO.SearchOption]::AllDirectories)
$forbidden = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility'
foreach ($file in $sources) {
    $text = [IO.File]::ReadAllText($file)
    if ([regex]::IsMatch($text, $forbidden)) { throw "Forbidden JSON dependency: $file" }
    if ([regex]::IsMatch($text, 'void\s+(OnDestroy|OnDisable|OnApplicationQuit)\s*\(')) { throw "Forbidden lifecycle teardown: $file" }
    # The engine provider is published once; other features may roll back unpublished candidates.
    if ($file -like '*\src\Engine\*' -and [regex]::IsMatch($text, '\bruntime\??\.Dispose\s*\(')) { throw "Published runtime teardown: $file" }
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Bare LF: $file" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $modDir 'src\PathfindingPlugin.cs'))
$workspace = [IO.Path]::GetFullPath((Join-Path $modDir '..'))
. (Join-Path $workspace 'APIShared/tools/Validation/DependencyMetadata.Common.ps1')
$manifest = [IO.File]::ReadAllText((Join-Path $modDir 'info.json')) | ConvertFrom-Json
Assert-PluginDependencyMetadata $plugin $manifest 'Pathfinding'
if ($manifest.GUID -ne 'Pathfinding_Serp' -or $manifest.NetworkMode -ne 1) { throw 'Manifest identity mismatch.' }
if ($plugin -notmatch ('PluginVersion = "' + [regex]::Escape([string]$manifest.Version) + '"')) { throw 'Plugin version mismatch.' }
Write-Output "PASS preflight: $($sources.Count) runtime sources, JSON and lifecycle rules, CRLF, manifest identity."
