# Build/install tooling only; no code from this file runs in the Unity process.
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Release/Release.Common.ps1')

function Get-PopularityPackageLayout {
    param([string]$ModDirectory = (Split-Path -Parent $PSScriptRoot),
        [string]$Configuration = 'Debug', [string]$ApiSharedDir = $env:SHCDE_API_SHARED_DIR,
        [string]$OutputDirectory = '',
        [string]$MSBuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe')
    $project = Join-Path $ModDirectory 'PopularityTest.csproj'
    $arguments = @($project, '/nologo', '/verbosity:quiet', '/t:AssignTargetPaths',
        '/getItem:ContentWithTargetPath', '/getProperty:TargetDir,TargetPath', "/p:Configuration=$Configuration")
    if ($ApiSharedDir) { $arguments += "/p:ApiSharedDir=$ApiSharedDir" }
    if ($OutputDirectory) { $arguments += "/p:OutputPath=$OutputDirectory" }
    $json = & $MSBuild @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate PopularityTest package content.' }
    $evaluated = ($json -join [Environment]::NewLine) | ConvertFrom-Json
    $package = [IO.Path]::GetFullPath($evaluated.Properties.TargetDir).TrimEnd('\','/')
    $targets = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $assets = @(foreach ($item in $evaluated.Items.ContentWithTargetPath) {
        $copy = $item.PSObject.Properties['CopyToOutputDirectory']
        if ($null -eq $copy -or -not $copy.Value -or $copy.Value -eq 'Never') { continue }
        $relative = [string]$item.TargetPath
        $destination = Resolve-PopularityPackagePath $package $relative
        if (-not $targets.Add($destination)) { throw "Duplicate package target: $relative" }
        [pscustomobject]@{ Source = [string]$item.FullPath; RelativePath = $relative; Package = $destination }
    })
    if (-not $assets.Count) { throw 'No copied package content was evaluated.' }
    [pscustomobject]@{ Directory = $package; Assembly = [string]$evaluated.Properties.TargetPath;
        Manifest = (Join-Path $ModDirectory 'info.json'); Assets = $assets }
}

function Resolve-PopularityPackagePath([string]$Directory, [string]$RelativePath) {
    if (-not $RelativePath -or [IO.Path]::IsPathRooted($RelativePath)) { throw 'Package target must be relative.' }
    $prefix = [IO.Path]::GetFullPath($Directory).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    $path = [IO.Path]::GetFullPath((Join-Path $Directory $RelativePath))
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Package target escapes output: $RelativePath" }
    return $path
}

function Assert-PopularityPackage {
    param([Parameter(Mandatory)]$Layout, [string]$InstalledDirectory = '')
    foreach ($asset in $Layout.Assets) {
        if (-not [IO.File]::Exists($asset.Package)) { throw "Missing package content: $($asset.RelativePath)" }
        $expected = Get-Sha256Hex $asset.Source
        if ($expected -cne (Get-Sha256Hex $asset.Package)) { throw "Source/package mismatch: $($asset.RelativePath)" }
        if ($InstalledDirectory) {
            $installed = Resolve-PopularityPackagePath $InstalledDirectory $asset.RelativePath
            if (-not [IO.File]::Exists($installed) -or $expected -cne (Get-Sha256Hex $installed)) {
                throw "Source/installation mismatch: $($asset.RelativePath)"
            }
        }
    }
    $manifest = [IO.File]::ReadAllText($Layout.Manifest) | ConvertFrom-Json
    $identity = [Reflection.AssemblyName]::GetAssemblyName($Layout.Assembly)
    if ($manifest.GUID -cne 'PopularityTest_Serp' -or $identity.Name -cne 'PopularityTest' -or
        $identity.Version -ne [version](([version]$manifest.Version).ToString(3) + '.0')) {
        throw 'PopularityTest DLL/manifest identity or version mismatch.'
    }
    if ([IO.File]::Exists((Join-Path $Layout.Directory 'APIShared.dll'))) { throw 'Consumer package must not contain APIShared.dll.' }
    if ($InstalledDirectory) {
        # Check every packaged file too, including DLL/PDB; unrelated installed files remain intact.
        foreach ($file in Get-ChildItem -LiteralPath $Layout.Directory -Recurse -File) {
            $relative = $file.FullName.Substring($Layout.Directory.Length + 1)
            $installed = Resolve-PopularityPackagePath $InstalledDirectory $relative
            if (-not [IO.File]::Exists($installed) -or (Get-Sha256Hex $file.FullName) -cne (Get-Sha256Hex $installed)) {
                throw "Package/installation mismatch: $relative"
            }
        }
        $installedAssembly = Join-Path $InstalledDirectory ([IO.Path]::GetFileName($Layout.Assembly))
        if ([Reflection.AssemblyName]::GetAssemblyName($installedAssembly).FullName -cne $identity.FullName) {
            throw 'Installed assembly identity mismatch.'
        }
    }
}

function Install-PopularityPackage {
    param([Parameter(Mandatory)]$Layout, [Parameter(Mandatory)][string]$Destination)
    # This gate must complete before even creating the destination directory.
    Assert-PopularityPackage $Layout
    foreach ($file in Get-ChildItem -LiteralPath $Layout.Directory -Recurse -File) {
        $relative = $file.FullName.Substring($Layout.Directory.Length + 1)
        $target = Resolve-PopularityPackagePath $Destination $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    Assert-PopularityPackage $Layout -InstalledDirectory $Destination
}
