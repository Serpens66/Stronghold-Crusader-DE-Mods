# Deployment mechanics only. The caller still validates the SDK, sources and artifact.
# These byte identities are the former owned disclosure patch, not a general XAML matcher.
function Get-AutoSendLegacyPatchRelativePath {
    'BepInEx\plugins\AutoSendTest_Serp\Patches\Assets\GUI\XAMLResources\HUD_AlliesPanel.xaml'
}
function Assert-AutoSendInstallPath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Path)
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $full = [IO.Path]::GetFullPath($Path)
    if (!$full.StartsWith($rootPath + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe owned installation path: $full" }
    for ($part = $full; $part.Length -ge $rootPath.Length; $part = [IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Linked installation path is not allowed: $part"
        }
        if ($part -ieq $rootPath) { break }
    }
}
function New-AutoSendInstallPlan {
    param([Parameter(Mandatory)][string]$GameDir, [Parameter(Mandatory)][object[]]$Payload)
    $game = [IO.Path]::GetFullPath($GameDir).TrimEnd('\','/')
    $modRoot = Join-Path $game 'BepInEx\plugins\AutoSendTest_Serp'
    $targets = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $Payload) {
        $full = [IO.Path]::GetFullPath((Join-Path $game $entry.Relative))
        if ($full -ine $entry.Target) { throw 'Payload target and relative path disagree.' }
        Assert-AutoSendInstallPath -Root $game -Path $full
        Assert-AutoSendInstallPath -Root $modRoot -Path $full
        if (Test-Path -LiteralPath $full -PathType Container) { throw "Expected an installed file, found a directory: $full" }
        if (!$targets.Add($full)) { throw 'Duplicate installation target.' }
    }
    $removed = @()
    $relative = Get-AutoSendLegacyPatchRelativePath
    $legacy = [IO.Path]::GetFullPath((Join-Path $game $relative))
    if (!$targets.Contains($legacy)) {
        Assert-AutoSendInstallPath -Root $game -Path $legacy
        Assert-AutoSendInstallPath -Root $modRoot -Path $legacy
        if (Test-Path -LiteralPath $legacy -PathType Container) { throw 'The obsolete owned patch path is a directory.' }
        if (Test-Path -LiteralPath $legacy) {
            $hash = Get-ApiFileHash $legacy
            # 0.4.3: centred host (522 CRLF bytes); 0.4.2: left host (521 CRLF bytes).
            if ($hash -notin @('734F0D014B09F30FEE907F2B8FF444FFA1712AF7D247D823B1C2436E5A647045',
                '4C52615A391F7A0E97B547FDB3001FAA06754B5E5609C9484A75CC7EC1BF76F2')) {
                throw 'The obsolete AutoSend HUD patch is unknown or edited; nothing was installed.'
            }
            $removed = @([pscustomobject]@{Relative=$relative;Target=$legacy;Sha256=$hash})
            [void]$targets.Add($legacy)
        }
    }
    [pscustomobject]@{GameDir=$game;ModRoot=$modRoot;Payload=@($Payload);RemovedFiles=$removed;Targets=$targets}
}
function Invoke-AutoSendInstallTransaction {
    param([Parameter(Mandatory)]$Plan, [Parameter(Mandatory)][string]$EvidenceDirectory,
        [Parameter(Mandatory)][scriptblock]$ValidateAfter, [scriptblock]$BeforeWrite = {})
    $backupRoot = Join-Path ([IO.Path]::GetFullPath($EvidenceDirectory)) 'backup'
    $actions = [Collections.Generic.List[object]]::new()
    foreach ($entry in $Plan.RemovedFiles) {
        $actions.Add([pscustomobject]@{Kind='Remove';Relative=$entry.Relative;Target=$entry.Target;Source=$null;Sha256=$entry.Sha256})
    }
    foreach ($entry in $Plan.Payload) {
        if ((Get-ApiFileHash $entry.Source) -ne $entry.Sha256) { throw "Payload hash changed: $($entry.Relative)" }
        $actions.Add([pscustomobject]@{Kind='Copy';Relative=$entry.Relative;Target=$entry.Target;Source=$entry.Source;Sha256=$entry.Sha256})
    }
    # Prepare and verify every backup before any installed target is changed.
    foreach ($action in $actions) {
        Assert-AutoSendInstallPath -Root $Plan.GameDir -Path $action.Target
        Assert-AutoSendInstallPath -Root $Plan.ModRoot -Path $action.Target
        $exists = Test-Path -LiteralPath $action.Target -PathType Leaf
        $hash = if ($exists) { Get-ApiFileHash $action.Target } else { $null }
        if ($action.Kind -eq 'Remove' -and (!$exists -or $hash -ne $action.Sha256)) { throw 'The obsolete patch changed during installation preparation.' }
        $backup = [IO.Path]::GetFullPath((Join-Path $backupRoot $action.Relative))
        Assert-AutoSendInstallPath -Root $backupRoot -Path $backup
        if (Test-Path -LiteralPath $backup) { throw 'Installation backup already exists; allocate fresh evidence.' }
        $action | Add-Member -NotePropertyName Backup -NotePropertyValue $backup
        $action | Add-Member -NotePropertyName Existed -NotePropertyValue ([bool]$exists)
        $action | Add-Member -NotePropertyName OriginalSha256 -NotePropertyValue $hash
        if ($exists) {
            [void][IO.Directory]::CreateDirectory((Split-Path -Parent $backup))
            Copy-Item -LiteralPath $action.Target -Destination $backup
            if ((Get-ApiFileHash $backup) -ne $hash) { throw 'Installation backup verification failed.' }
        }
    }
    foreach ($action in $actions) {
        Assert-AutoSendInstallPath -Root $Plan.GameDir -Path $action.Target
        Assert-AutoSendInstallPath -Root $Plan.ModRoot -Path $action.Target
        $exists = Test-Path -LiteralPath $action.Target -PathType Leaf
        if ($exists -ne $action.Existed -or ($exists -and (Get-ApiFileHash $action.Target) -ne $action.OriginalSha256)) {
            throw 'An installed target changed during preparation; nothing was installed.'
        }
    }
    & $BeforeWrite
    $touched = [Collections.Generic.List[object]]::new()
    try {
        foreach ($action in $actions) {
            Assert-AutoSendInstallPath -Root $Plan.GameDir -Path $action.Target
            Assert-AutoSendInstallPath -Root $Plan.ModRoot -Path $action.Target
            if ($action.Kind -eq 'Remove') {
                $touched.Add($action)
                Remove-Item -LiteralPath $action.Target -Force
            } else {
                [void][IO.Directory]::CreateDirectory((Split-Path -Parent $action.Target))
                # Copy can fail after partially overwriting its target.
                $touched.Add($action)
                Copy-Item -LiteralPath $action.Source -Destination $action.Target -Force
                if ((Get-ApiFileHash $action.Target) -ne $action.Sha256) { throw "Installed file mismatch: $($action.Target)" }
            }
        }
        $validation = & $ValidateAfter
        [pscustomobject]@{RemovedFiles=@($Plan.RemovedFiles);Validation=$validation}
    } catch {
        $failure = $_
        $rollbackErrors = [Collections.Generic.List[string]]::new()
        for ($index = $touched.Count - 1; $index -ge 0; $index--) {
            $action = $touched[$index]
            try {
                Assert-AutoSendInstallPath -Root $Plan.GameDir -Path $action.Target
                Assert-AutoSendInstallPath -Root $Plan.ModRoot -Path $action.Target
                if ($action.Existed) {
                    if ((Get-ApiFileHash $action.Backup) -ne $action.OriginalSha256) { throw 'Rollback backup changed.' }
                    Copy-Item -LiteralPath $action.Backup -Destination $action.Target -Force
                    if ((Get-ApiFileHash $action.Target) -ne $action.OriginalSha256) { throw 'Rollback hash mismatch.' }
                } elseif (Test-Path -LiteralPath $action.Target -PathType Leaf) { Remove-Item -LiteralPath $action.Target -Force }
            } catch { $rollbackErrors.Add($action.Relative + ': ' + $_.Exception.Message) }
        }
        if ($rollbackErrors.Count) { throw ($failure.Exception.Message + ' Rollback incomplete; keep backups at ' + $backupRoot + ': ' + ($rollbackErrors -join '; ')) }
        throw $failure
    }
}
