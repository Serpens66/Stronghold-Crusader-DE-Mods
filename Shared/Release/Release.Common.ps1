Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReleaseRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-ReleaseConfiguration {
    $root = Get-ReleaseRoot
    $publicConfigPath = Join-Path $PSScriptRoot 'release-projects.json'
    $config = Get-Content -LiteralPath $publicConfigPath -Raw | ConvertFrom-Json
    $localConfigPath = Join-Path $root 'release.local.json'
    $local = if (Test-Path -LiteralPath $localConfigPath) {
        Get-Content -LiteralPath $localConfigPath -Raw | ConvertFrom-Json
    } else {
        [PSCustomObject]@{}
    }

    $gameDir = if ($null -ne $local.PSObject.Properties['GameDir']) {
        [string]$local.GameDir
    } elseif (-not [string]::IsNullOrWhiteSpace($env:SHCDE_RELEASE_GAME_DIR)) {
        $env:SHCDE_RELEASE_GAME_DIR
    } else {
        'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
    }
    $msBuild = if ($null -ne $local.PSObject.Properties['MSBuild']) {
        [string]$local.MSBuild
    } elseif (-not [string]::IsNullOrWhiteSpace($env:SHCDE_RELEASE_MSBUILD)) {
        $env:SHCDE_RELEASE_MSBUILD
    } else {
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    }

    return [PSCustomObject]@{
        Root = $root
        Repository = [string]$config.Repository
        Branch = [string]$config.Branch
        Projects = @($config.Projects | ForEach-Object { [string]$_ })
        GameDir = $gameDir
        MSBuild = $msBuild
        LocalConfigPath = $localConfigPath
    }
}

function Resolve-ReleaseTool {
    param([Parameter(Mandatory)][string]$Name)
    $resolved = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Source }
    if ($Name -ieq 'gh') {
        $programFilesGh = 'C:\Program Files\GitHub CLI\gh.exe'
        $localGh = Join-Path $env:LOCALAPPDATA 'Programs\GitHub CLI\gh.exe'
        foreach ($candidate in @($programFilesGh, $localGh)) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter()][string[]]$Arguments = @(),
        [Parameter()][switch]$AllowFailure
    )
    $resolvedFilePath = Resolve-ReleaseTool -Name $FilePath
    if ([string]::IsNullOrWhiteSpace($resolvedFilePath)) { throw "Required command not found: $FilePath" }
    $output = @(& $resolvedFilePath @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "Command failed ($exitCode): $resolvedFilePath $($Arguments -join ' ')`r`n$($output -join "`r`n")"
    }
    return [PSCustomObject]@{ ExitCode = $exitCode; Output = $output }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    try {
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try {
            $bytes = $algorithm.ComputeHash($stream)
        } finally {
            $algorithm.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
    return ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}

function Get-PluginMetadata {
    param([Parameter(Mandatory)][string]$ModName)
    $config = Get-ReleaseConfiguration
    if ($ModName -notin $config.Projects) {
        throw "Project is not release-enabled: $ModName"
    }
    $modDir = Join-Path $config.Root $ModName
    $pluginRoot = Join-Path $modDir 'BepInEx\plugins'
    $infos = @(Get-ChildItem -LiteralPath $pluginRoot -Filter info.json -File -Recurse -ErrorAction Stop)
    if ($infos.Count -ne 1) {
        throw "Expected exactly one plugin info.json below $pluginRoot, found $($infos.Count)."
    }
    $manifest = Get-Content -LiteralPath $infos[0].FullName -Raw | ConvertFrom-Json
    foreach ($property in @('GUID', 'Name', 'Version', 'SerpChangelog')) {
        if ($manifest.PSObject.Properties.Name -notcontains $property) {
            throw "Missing property '$property' in $($infos[0].FullName)."
        }
    }
    if ([string]$manifest.Version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        throw "Invalid release version '$($manifest.Version)' in $($infos[0].FullName)."
    }
    $matchingChanges = @($manifest.SerpChangelog | Where-Object { [string]$_.Version -eq [string]$manifest.Version })
    if ($matchingChanges.Count -ne 1 -or @($matchingChanges[0].Changes).Count -eq 0) {
        throw "Expected one non-empty SerpChangelog entry for version $($manifest.Version)."
    }
    return [PSCustomObject]@{
        Config = $config
        ModName = $ModName
        ModDir = $modDir
        BuildBat = Join-Path $modDir 'build.bat'
        PackageDir = $infos[0].Directory.FullName
        PackageFolderName = $infos[0].Directory.Name
        ManifestPath = $infos[0].FullName
        Manifest = $manifest
        Changelog = $matchingChanges[0]
        Version = [string]$manifest.Version
        Tag = "$ModName/v$($manifest.Version)"
    }
}

function Get-ExtenderDirectory {
    param([Parameter(Mandatory)]$Metadata)
    $localRoot = Join-Path $Metadata.Config.Root 'shcde-script-extender'
    $candidates = @(
        (Join-Path $localRoot 'src\SHCDESE.BepInEx\bin\net481'),
        (Join-Path $localRoot 'mod_output\000shcdese'),
        (Join-Path $Metadata.Config.GameDir 'BepInEx\plugins\000shcdese')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'SHCDESE.dll')) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw 'SHCDESE.dll was not found in the local Script Extender or installed game.'
}

function Get-SetupReport {
    param([string]$ModName)
    $config = Get-ReleaseConfiguration
    $checks = [System.Collections.Generic.List[object]]::new()
    function Add-Check([string]$Name, [bool]$Ok, [string]$Detail) {
        $checks.Add([PSCustomObject]@{ Check = $Name; Ok = $Ok; Detail = $Detail })
    }

    foreach ($command in @('git', 'gh', 'dotnet')) {
        $resolved = Resolve-ReleaseTool -Name $command
        Add-Check $command ($null -ne $resolved) $(if ($resolved) { $resolved } else { 'not found' })
    }
    if ($null -ne (Resolve-ReleaseTool -Name 'gh')) {
        $auth = Invoke-CheckedCommand -FilePath 'gh' -Arguments @('auth', 'status') -AllowFailure
        Add-Check 'GitHub authentication' ($auth.ExitCode -eq 0) $(if ($auth.ExitCode -eq 0) { 'authenticated' } else { ($auth.Output -join ' ') })
    }
    Add-Check 'MSBuild' (Test-Path -LiteralPath $config.MSBuild -PathType Leaf) $config.MSBuild
    Add-Check 'Game directory' (Test-Path -LiteralPath $config.GameDir -PathType Container) $config.GameDir
    $bepInEx = [IO.Path]::Combine($config.GameDir, 'BepInEx\core\BepInEx.dll')
    Add-Check 'BepInEx.dll' (Test-Path -LiteralPath $bepInEx -PathType Leaf) $bepInEx
    $crusader = [IO.Path]::Combine($config.GameDir, 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
    Add-Check 'CrusaderDE.dll' (Test-Path -LiteralPath $crusader -PathType Leaf) $crusader
    if (-not [string]::IsNullOrWhiteSpace($ModName)) {
        try {
            $metadata = Get-PluginMetadata -ModName $ModName
            Add-Check 'Release whitelist' $true $ModName
            Add-Check 'build.bat' (Test-Path -LiteralPath $metadata.BuildBat -PathType Leaf) $metadata.BuildBat
            $extender = Get-ExtenderDirectory -Metadata $metadata
            Add-Check 'SHCDESE.dll' $true (Join-Path $extender 'SHCDESE.dll')
        } catch {
            Add-Check 'Mod metadata' $false $_.Exception.Message
        }
    }
    return @($checks)
}

function Get-FileHashRecord {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$BasePath)
    $resolvedBase = (Resolve-Path -LiteralPath $BasePath).Path.TrimEnd('\') + '\'
    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    if (-not $resolvedPath.StartsWith($resolvedBase, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected base directory: $resolvedPath"
    }
    $relative = $resolvedPath.Substring($resolvedBase.Length).Replace('\', '/')
    return [PSCustomObject]@{
        Path = $relative
        Sha256 = Get-Sha256Hex -Path $Path
        Size = (Get-Item -LiteralPath $Path).Length
    }
}

function Get-DependencyRecords {
    param([Parameter(Mandatory)]$Metadata, [Parameter(Mandatory)][string]$ExtenderDir)
    $paths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$paths.Add((Join-Path $Metadata.Config.GameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'))
    $projectFiles = @(Get-ChildItem -LiteralPath $Metadata.ModDir -Filter *.csproj -File -Recurse)
    foreach ($projectFile in $projectFiles) {
        [xml]$xml = Get-Content -LiteralPath $projectFile.FullName -Raw
        $hintNodes = @($xml.SelectNodes('//*[local-name()="HintPath"]'))
        foreach ($node in $hintNodes) {
            $candidate = [string]$node.InnerText
            $candidate = $candidate.Replace('$(GameDir)', $Metadata.Config.GameDir)
            $candidate = $candidate.Replace('$(ExtenderDir)', $ExtenderDir)
            $candidate = $candidate.Replace('$(MSBuildThisFileDirectory)', $projectFile.DirectoryName + '\')
            $candidate = $candidate.Replace('$(LocalScriptExtenderBuildOutput)', (Join-Path $Metadata.Config.Root 'shcde-script-extender\src\SHCDESE.BepInEx\bin\net481'))
            $candidate = $candidate.Replace('$(LocalScriptExtenderModOutput)', (Join-Path $Metadata.Config.Root 'shcde-script-extender\mod_output\000shcdese'))
            if (-not [IO.Path]::IsPathRooted($candidate)) {
                $candidate = Join-Path $projectFile.DirectoryName $candidate
            }
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                [void]$paths.Add((Resolve-Path -LiteralPath $candidate).Path)
            }
        }
    }
    $records = @(foreach ($path in $paths) {
        $item = Get-Item -LiteralPath $path
        $displayPath = if ($item.FullName.StartsWith($Metadata.Config.GameDir + '\', [StringComparison]::OrdinalIgnoreCase)) {
            '$GameDir/' + $item.FullName.Substring($Metadata.Config.GameDir.Length + 1).Replace('\', '/')
        } elseif ($item.FullName.StartsWith($ExtenderDir + '\', [StringComparison]::OrdinalIgnoreCase)) {
            '$ExtenderDir/' + $item.FullName.Substring($ExtenderDir.Length + 1).Replace('\', '/')
        } elseif ($item.FullName.StartsWith($Metadata.Config.Root + '\', [StringComparison]::OrdinalIgnoreCase)) {
            '$Repository/' + $item.FullName.Substring($Metadata.Config.Root.Length + 1).Replace('\', '/')
        } else {
            '$External/' + $item.Name
        }
        [PSCustomObject]@{
            Name = $item.Name
            Path = $displayPath
            Sha256 = Get-Sha256Hex -Path $item.FullName
            Size = $item.Length
        }
    })
    return @($records | Sort-Object Path)
}

function Write-Utf8CrLfFile {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Text)
    $normalized = ($Text -replace "`r?`n", "`r`n").TrimEnd("`r", "`n") + "`r`n"
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($Path, $normalized, $encoding)
    $actual = [IO.File]::ReadAllText($Path, $encoding)
    if (-not [string]::Equals($normalized, $actual, [StringComparison]::Ordinal)) {
        throw "CRLF write verification failed: $Path"
    }
}
