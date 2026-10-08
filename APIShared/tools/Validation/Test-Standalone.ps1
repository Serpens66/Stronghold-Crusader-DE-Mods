[CmdletBinding()]
param([string]$GameDir, [string]$ExtenderDir)
$ErrorActionPreference = 'Stop'
$apiRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $PSScriptRoot 'RuntimePreflight.Common.ps1')
Assert-SERuntimeModPreflight ([pscustomobject]@{ Name = 'APIShared'; Plugin = 'APIShared_Serp'; Project = 'APIShared.csproj' }) $apiRoot
. (Join-Path $PSScriptRoot 'DependencyMetadata.Common.ps1')
$source = (Get-ChildItem -LiteralPath (Join-Path $apiRoot 'src') -Recurse -File -Filter '*.cs' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join [Environment]::NewLine
Assert-PluginDependencyMetadata $source ([IO.File]::ReadAllText((Join-Path $apiRoot 'info.json')) | ConvertFrom-Json) 'APIShared'
$projects = @(Get-ChildItem -LiteralPath $apiRoot -Recurse -File -Filter '*.csproj' | Where-Object FullName -NotMatch '[\\/](bin|obj|BepInEx)[\\/]')
$links = 0
foreach ($project in $projects) {
    [xml]$xml = [IO.File]::ReadAllText($project.FullName)
    foreach ($entry in $xml.SelectNodes('//*[local-name()="Compile" or local-name()="ProjectReference"][@Include]')) {
        $include = [string]$entry.Include
        if ($include -match '\$\(|[*?]') { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $project.Directory.FullName $include))
        if (-not $target.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "External source/project link: $target" }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing source/project link: $target" }
        $links++
    }
}
$files = @(Get-ChildItem -LiteralPath $apiRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx|\.git)[\\/]' -and $_.Extension -match '^\.(cs|csproj|ps1|bat|md|json|xaml|props|targets)$'
})
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "CRLF required: $($file.FullName)" }
    if ($file.Extension -eq '.cs' -and $text -cmatch '\busing\s+Shared\s*;|\bnamespace\s+Shared\b|(?<!API)\bShared\.(?!MissionEvents\.")') {
        throw "Workspace Shared type dependency: $($file.FullName)"
    }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $apiRoot 'Patches') -Recurse -File -Filter '*.xaml') {
    [xml]$xml = [IO.File]::ReadAllText($file.FullName)
    foreach ($content in $xml.SelectNodes('//*[local-name()="Content"]')) {
        $roots = @($content.ChildNodes | Where-Object NodeType -eq ([Xml.XmlNodeType]::Element))
        if ($roots.Count -ne 1) { throw "XAML Content requires one root: $($file.FullName)" }
    }
}
& (Join-Path $PSScriptRoot 'Test-DependencyMetadata.ps1')
& (Join-Path $PSScriptRoot 'Test-PermanentHooks.ps1')
& (Join-Path $PSScriptRoot 'Verify-Interop.ps1') -GameDir $GameDir -ExtenderDir $ExtenderDir
Write-Host "PASS: standalone APIShared boundaries ($links links), runtime JSON/lifecycle/scheduling, CRLF, XAML and installed interop."
