[CmdletBinding()]
param([string]$Workspace)
$ErrorActionPreference = 'Stop'
Write-Host '[Preflight] Checking manifest and plugin minimum dependencies...'
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')) }
. (Join-Path $Workspace 'APIShared\tools\Validation\DependencyMetadata.Common.ps1')
$count = 0
foreach ($project in Get-ChildItem -LiteralPath $Workspace -Recurse -File -Filter '*.csproj' | Where-Object {
    $_.FullName -notmatch '[\\/](shcde-script-extender|tests|bin|obj|BepInEx|\.tools|\.inspect|_inspect|\.native-analysis|\.release-output|before)[\\/]'
}) {
    $manifestPath = Join-Path $project.Directory.FullName 'info.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        $infos = @(Get-ChildItem -LiteralPath (Join-Path $project.Directory.FullName 'BepInEx\plugins') -Recurse -File -Filter 'info.json' -ErrorAction SilentlyContinue)
        if ($infos.Count -eq 0) { continue }
        if ($infos.Count -ne 1) { throw "Ambiguous source manifest for $($project.BaseName)" }
        $manifestPath = $infos[0].FullName
    }
    $sourceFiles = @(Get-ChildItem -LiteralPath $project.Directory.FullName -Recurse -File -Filter '*.cs' | Where-Object {
        $_.FullName -notmatch '[\\/](tests|examples|bin|obj|BepInEx)[\\/]'
    })
    $source = ($sourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join [Environment]::NewLine
    if ($source -notmatch '\[BepInPlugin\(') { continue }
    $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
    Assert-PluginDependencyMetadata $source $manifest $project.BaseName
    $count++
}
Write-Host "PASS: $count runtime manifests use consistent generic minimum dependencies; soft dependencies remain optional."
