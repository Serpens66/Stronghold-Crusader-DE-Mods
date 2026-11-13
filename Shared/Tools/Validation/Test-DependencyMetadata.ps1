[CmdletBinding()]
param([string]$Workspace, [string[]]$ProjectFiles)
$ErrorActionPreference = 'Stop'
Write-Host '[Preflight] Checking manifest and plugin minimum dependencies...'
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')) }
$selectedApiRoot = if ($env:SHCDE_BRANCH_API_ROOT) { [IO.Path]::GetFullPath($env:SHCDE_BRANCH_API_ROOT) } else { Join-Path $Workspace 'APIShared' }
. (Join-Path $selectedApiRoot 'tools/Validation/DependencyMetadata.Common.ps1')
function Get-DependencyRelativePath([string]$Path) {
    if ($Path.StartsWith($selectedApiRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { return 'APIShared\' + $Path.Substring($selectedApiRoot.Length+1) }
    return $Path.Substring($Workspace.Length).TrimStart('\')
}
$count = 0
$projects = if ($ProjectFiles) { @($ProjectFiles | ForEach-Object { Get-Item -LiteralPath $_ }) } else {
    @(Get-ChildItem -LiteralPath $Workspace -Recurse -File -Filter '*.csproj')
}
foreach ($project in $projects | Where-Object {
     ('\' + (Get-DependencyRelativePath $_.FullName)) -notmatch '[\\/](shcde-script-extender|tests|bin|obj|BepInEx|\.local|\.tools|\.inspect|_inspect|\.native-analysis|\.work|\.release-output|before)[\\/]'
}) {
    $manifestPath = Join-Path $project.Directory.FullName 'info.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        $infos = @(Get-ChildItem -LiteralPath (Join-Path $project.Directory.FullName 'BepInEx\plugins') -Recurse -File -Filter 'info.json' -ErrorAction SilentlyContinue)
        if ($infos.Count -eq 0) { continue }
        if ($infos.Count -ne 1) { throw "Ambiguous source manifest for $($project.BaseName)" }
        $manifestPath = $infos[0].FullName
    }
    $sourceFiles = @(Get-ChildItem -LiteralPath $project.Directory.FullName -Recurse -File -Filter '*.cs' | Where-Object {
         ('\' + (Get-DependencyRelativePath $_.FullName)) -notmatch '[\\/](tests|examples|bin|obj|BepInEx|\.local|\.work|\.release-output)[\\/]'
    })
    $source = ($sourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join [Environment]::NewLine
    if ($source -notmatch '\[BepInPlugin\(') { continue }
    $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
    # The diagnostic bootstrap must load without APIShared; info.json remains hard.
    if ($project.BaseName -eq 'SerpsModsHost') {
        # Compiled soft-dependency metadata and bootstrap loading are checked by
        # SerpsModsHostDuplicateTests after compilation; source spelling is not a contract.
        $apiDependencies = @($manifest.Dependencies | Where-Object { $_.GUID -eq 'APIShared_Serp' })
        $minimum = $null
        if ($apiDependencies.Count -ne 1 -or -not [version]::TryParse([string]$apiDependencies[0].MinimumVersion, [ref]$minimum)) { throw 'Host info.json must retain exactly one valid required APIShared minimum.' }
        $manifest.Dependencies = @($manifest.Dependencies | Where-Object { $_.GUID -ne 'APIShared_Serp' })
    }
    Assert-PluginDependencyMetadata $source $manifest $project.BaseName
    $count++
}
Write-Host "PASS: $count runtime manifests use consistent generic minimum dependencies; soft dependencies remain optional."
