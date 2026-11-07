function Get-InGameTestPath([string]$Path) {
    return [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
}

function Test-InGameTestInside([string]$Path, [string]$Root) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    return $full -ieq $rootPath -or $full.StartsWith($rootPath + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-InGameTestUnlinked([string]$Path) {
    for ($part = [IO.Path]::GetFullPath($Path); $part; $part = [IO.Path]::GetDirectoryName($part)) {
        try { $attributes = [IO.File]::GetAttributes($part) }
        catch [IO.FileNotFoundException] { continue }
        catch [IO.DirectoryNotFoundException] { continue }
        if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked path is unsupported: $part" }
    }
}

function Get-InGameTestTree([string]$Path) {
    Assert-InGameTestUnlinked $Path
    if ([IO.File]::Exists($Path)) { throw "Expected a directory: $Path" }
    $files = @{}
    $directories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $exists = [IO.Directory]::Exists($Path)
    if ($exists) {
        $prefix = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') + '\'
        $pending = [Collections.Generic.Stack[string]]::new()
        $pending.Push($Path)
        while ($pending.Count) {
            foreach ($entry in [IO.Directory]::GetFileSystemEntries($pending.Pop())) {
                $attributes = [IO.File]::GetAttributes($entry)
                if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked entry is unsupported: $entry" }
                $relative = $entry.Substring($prefix.Length)
                if ($attributes -band [IO.FileAttributes]::Directory) {
                    $null = $directories.Add($relative)
                    $pending.Push($entry)
                } else { $files.Add($relative, (Get-ApiRepositoryHash $entry)) }
            }
        }
    }
    return [pscustomobject]@{ Exists = $exists; Files = $files; Directories = $directories }
}

function Assert-InGameTestTree([string]$Path, $Expected) {
    $actual = Get-InGameTestTree $Path
    if ($actual.Exists -ne $Expected.Exists -or $actual.Files.Count -ne $Expected.Files.Count -or
        -not $actual.Directories.SetEquals($Expected.Directories)) { throw "Directory inventory differs: $Path" }
    foreach ($relative in $Expected.Files.Keys) {
        if ($actual.Files[$relative] -cne $Expected.Files[$relative]) { throw "File digest differs: $(Join-Path $Path $relative)" }
    }
}

function Copy-InGameTestTree([string]$Source, [string]$Target, $Inventory) {
    if (-not $Inventory.Exists) { return }
    $null = [IO.Directory]::CreateDirectory($Target)
    foreach ($relative in $Inventory.Directories) { $null = [IO.Directory]::CreateDirectory((Join-Path $Target $relative)) }
    foreach ($relative in $Inventory.Files.Keys) {
        [IO.File]::Copy((Join-Path $Source $relative), (Join-Path $Target $relative), $false)
    }
    Assert-InGameTestTree $Target $Inventory
}

function Remove-InGameTestTree([string]$Path, [string]$Parent) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/')
    if ([IO.Path]::GetDirectoryName($full) -ine $parentPath) { throw "Refusing recursive removal outside $Parent." }
    $inventory = Get-InGameTestTree $full
    if ($inventory.Exists) { [IO.Directory]::Delete($full, $true) }
}

function Save-InGameTestState([string]$Path, [string]$Backup, [switch]$Directory) {
    Assert-InGameTestUnlinked $Path
    if ($Directory) {
        $inventory = Get-InGameTestTree $Path
        Copy-InGameTestTree $Path $Backup $inventory
    } else {
        if ([IO.Directory]::Exists($Path)) { throw "Expected a file: $Path" }
        $inventory = $null
        if ([IO.File]::Exists($Path)) {
            $inventory = Get-ApiRepositoryHash $Path
            [IO.File]::Copy($Path, $Backup, $false)
            if ((Get-ApiRepositoryHash $Backup) -cne $inventory) { throw "File changed during backup: $Path" }
        }
    }
    return [pscustomobject]@{ Path = $Path; Backup = $Backup; Directory = [bool]$Directory; Inventory = $inventory }
}

function Restore-InGameTestState($Snapshot) {
    $path = $Snapshot.Path
    $expected = $Snapshot.Inventory
    Assert-InGameTestUnlinked $path
    if (-not $Snapshot.Directory) {
        if (-not $expected) { [IO.File]::Delete($path); return }
        if (-not [IO.File]::Exists($path) -or (Get-ApiRepositoryHash $path) -cne $expected) {
            [IO.File]::Copy($Snapshot.Backup, $path, $true)
        }
        if ((Get-ApiRepositoryHash $path) -cne $expected) { throw "File restoration differs: $path" }
        return
    }

    $actual = Get-InGameTestTree $path
    foreach ($relative in $actual.Files.Keys) {
        if (-not $expected.Files.ContainsKey($relative)) { [IO.File]::Delete((Join-Path $path $relative)) }
    }
    foreach ($relative in @($actual.Directories | Sort-Object Length -Descending)) {
        if (-not $expected.Directories.Contains($relative)) { [IO.Directory]::Delete((Join-Path $path $relative), $false) }
    }
    if ($expected.Exists) {
        $null = [IO.Directory]::CreateDirectory($path)
        foreach ($relative in $expected.Directories) { $null = [IO.Directory]::CreateDirectory((Join-Path $path $relative)) }
        foreach ($relative in $expected.Files.Keys) {
            if ($actual.Files[$relative] -cne $expected.Files[$relative]) {
                [IO.File]::Copy((Join-Path $Snapshot.Backup $relative), (Join-Path $path $relative), $true)
            }
        }
    } elseif ($actual.Exists) { [IO.Directory]::Delete($path, $false) }
    Assert-InGameTestTree $path $expected
}

function Set-InGameTestPackage([string]$Source, [string]$Target, [string]$Backup, $Swaps, [switch]$Directory) {
    Assert-InGameTestUnlinked $Source
    Assert-InGameTestUnlinked $Target
    $stage = $Backup + '.new'
    if ($Directory) {
        $original = Get-InGameTestTree $Target
        $package = Get-InGameTestTree $Source
        if (-not $package.Exists) { throw "Missing plugin package: $Source" }
        Copy-InGameTestTree $Source $stage $package
        $hadOriginal = $original.Exists
    } else {
        if ([IO.Directory]::Exists($Target)) { throw "Expected a patcher file: $Target" }
        $hadOriginal = [IO.File]::Exists($Target)
        $original = if ($hadOriginal) { Get-ApiRepositoryHash $Target }
        $package = Get-ApiRepositoryHash $Source
        [IO.File]::Copy($Source, $stage, $false)
        if ((Get-ApiRepositoryHash $stage) -cne $package) { throw "Patcher package changed during staging: $Source" }
    }
    $swap = [pscustomobject]@{
        Target = $Target; Backup = $Backup; Stage = $stage; Directory = [bool]$Directory
        Original = $original; Saved = $false; Published = $false
    }
    $Swaps.Add($swap)
    if ($hadOriginal) {
        if ($Directory) { [IO.Directory]::Move($Target, $Backup) } else { [IO.File]::Move($Target, $Backup) }
        $swap.Saved = $true
    }
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Target))
    if ($Directory) { [IO.Directory]::Move($stage, $Target) } else { [IO.File]::Move($stage, $Target) }
    $swap.Published = $true
}

function Restore-InGameTestPackage($Swap) {
    Assert-InGameTestUnlinked $Swap.Target
    if ($Swap.Directory) {
        if ($Swap.Published) { [IO.Directory]::Move($Swap.Target, $Swap.Stage) }
        if ($Swap.Saved) { [IO.Directory]::Move($Swap.Backup, $Swap.Target) }
        Assert-InGameTestTree $Swap.Target $Swap.Original
        return
    }
    if ($Swap.Published) { [IO.File]::Move($Swap.Target, $Swap.Stage) }
    if ($Swap.Saved) { [IO.File]::Move($Swap.Backup, $Swap.Target) }
    if ($Swap.Original) {
        if ((Get-ApiRepositoryHash $Swap.Target) -cne $Swap.Original) { throw "Restored patcher digest differs: $($Swap.Target)" }
    } elseif (Test-Path -LiteralPath $Swap.Target) { throw "Unexpected patcher left in the installation: $($Swap.Target)" }
}

function Assert-InGameTestMod([string]$Directory, [string]$Guid) {
    $info = Join-Path $Directory 'info.json'
    Assert-InGameTestUnlinked $info
    $metadata = [IO.File]::ReadAllText($info) | ConvertFrom-Json
    if ($metadata.GUID -cne $Guid) { throw "Package GUID does not match $Guid." }
}

function Assert-InGameTestApiPair([string]$Plugin, [string]$Patcher) {
    Assert-InGameTestMod $Plugin 'APIShared_Serp'
    $manifest = Join-Path $Plugin 'dependency-bootstrap-pair.json'
    Assert-InGameTestUnlinked $manifest
    $pair = [IO.File]::ReadAllText($manifest) | ConvertFrom-Json
    $info = [IO.File]::ReadAllText((Join-Path $Plugin 'info.json')) | ConvertFrom-Json
    $version = $null
    if ($pair.Guid -cne 'APIShared_Serp' -or $pair.Schema -ne 1 -or $pair.Version -cne $info.Version -or
        -not [Version]::TryParse([string]$pair.Version, [ref]$version)) { throw 'Invalid APIShared pair manifest.' }
    $files = [ordered]@{ RuntimeSHA256 = (Join-Path $Plugin 'APIShared.dll'); BootstrapSHA256 = $Patcher }
    $content = Join-Path $Plugin 'APIShared.Content.dll'
    if ($pair.PSObject.Properties['ContentSHA256']) { $files.ContentSHA256 = $content }
    elseif (Test-Path -LiteralPath $content) { throw 'APIShared.Content.dll is not covered by the pair manifest.' }
    foreach ($field in $files.Keys) {
        $file = $files[$field]
        Assert-InGameTestUnlinked $file
        if ($pair.$field -cnotmatch '^[a-f0-9]{64}$' -or (Get-ApiRepositoryHash $file) -cne $pair.$field) {
            throw "APIShared package pair digest mismatch: $field"
        }
    }
}

function Get-InGameTestResult([string]$Log, [string]$Run) {
    if (-not [IO.File]::Exists($Log)) { throw "NO_RESULT: no BepInEx log for run=$Run." }
    $pattern = '\bINGAME_TEST run=' + [regex]::Escape($Run) + ' result=(\S+)'
    $markers = [regex]::Matches([IO.File]::ReadAllText($Log), $pattern)
    if ($markers.Count -ne 1) { throw "INVALID_RESULT: expected one terminal marker for run=$Run, found $($markers.Count)." }
    return $markers[0].Groups[1].Value
}
