function Get-PayloadChangelogBody {
    param([string]$PackageFolder, $Mod, [AllowEmptyString()][string]$PreviousVersion)
    $relative = [string]$Mod.RelativePath
    if ($relative -cne ('Mods/' + [string]$Mod.Guid) -or [string]$Mod.Guid -notmatch '^[A-Za-z0-9._-]+$') {
        throw "Invalid changelog payload path for $($Mod.Name)."
    }
    $path = Join-Path $PackageFolder ('Payload/BepInEx/plugins/SerpsMods_Serp/' + $relative + '/info.json')
    Assert-ApiWorkshopUnlinkedPath $path
    $records = @($Mod.Files | Where-Object { $_.Path -ceq 'info.json' })
    if ($records.Count -ne 1 -or (Get-Item -LiteralPath $path).Length -ne $records[0].Size -or
        (Get-WorkshopFileHash $path) -cne $records[0].Sha256) {
        throw "Changelog manifest differs from release provenance: $path"
    }
    $info = [IO.File]::ReadAllText($path) | ConvertFrom-Json
    if ($info.GUID -cne $Mod.Guid -or $info.Version -cne $Mod.Version) { throw "Changelog manifest identity mismatch: $path" }
    $current = [version]$Mod.Version
    $previous = if ($PreviousVersion) { [version]$PreviousVersion } else { $null }
    if ($previous -and $previous -gt $current) { throw "Changelog downgrade for $($Mod.Name): $PreviousVersion -> $current" }
    $seen = [Collections.Generic.HashSet[version]]::new()
    foreach ($entry in @($info.SerpChangelog)) {
        $version = [version]$entry.Version
        if (-not $seen.Add($version) -or @($entry.Changes).Count -eq 0) { throw "Duplicate/empty changelog entry for $($Mod.Name): $version" }
        foreach ($change in $entry.Changes) {
            if ($change -isnot [string] -or [string]::IsNullOrWhiteSpace($change)) { throw "Invalid changelog text for $($Mod.Name): $version" }
        }
    }
    if (-not $seen.Contains($current)) { throw "Missing current changelog entry for $($Mod.Name): $current" }
    $entries = @($info.SerpChangelog | Where-Object {
        [version]$_.Version -le $current -and ($null -eq $previous -or [version]$_.Version -gt $previous)
    } | Sort-Object { [version]$_.Version } -Descending)
    $sections = @(foreach ($entry in $entries) {
        $lines = @("### v$($entry.Version)", '') + @($entry.Changes | ForEach-Object { '- ' + $_ })
        $lines -join "`r`n"
    })
    return [pscustomobject]@{ Text = $sections -join "`r`n`r`n"; Path = $path }
}

function Get-ConfirmedWorkshopChangelogState {
    param($PreviousState, [string]$ItemId, [string]$HistoryPath, [string]$SeedPath,
          [uint32]$AppId, [string]$ReleaseOutputRoot, [string]$ReferencePath)
    $uploads = @()
    foreach ($path in @($SeedPath, $HistoryPath)) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $history = [IO.File]::ReadAllText($path) | ConvertFrom-Json
            [void](Assert-SteamWorkshopUploadHistory $history $AppId 'SerpsMods')
            $uploads += @($history.Uploads | Where-Object { $_.ItemId -ceq $ItemId })
        }
    }
    $latest = @($uploads | Sort-Object { [DateTimeOffset]$_.UploadedUtc } -Descending | Select-Object -First 1)
    if ($latest.Count -eq 0) {
        if ($PreviousState) { throw 'Changelog state has no confirmed upload history for this item/mode.' }
        return $null
    }
    $upload = $latest[0]
    foreach ($entry in @($uploads | Where-Object { $_.PackVersion -ceq $upload.PackVersion })) {
        if ($entry.MapSha256 -cne $upload.MapSha256 -or
            ($entry.PSObject.Properties['ContentFingerprint'] -and $upload.PSObject.Properties['ContentFingerprint'] -and
             $entry.ContentFingerprint -cne $upload.ContentFingerprint)) { throw 'Conflicting confirmed changelog upload histories.' }
    }
    if ($PreviousState -and $PreviousState.ItemId -cne $ItemId) { throw 'Changelog state belongs to a different Workshop item.' }
    if ($PreviousState -and $PreviousState.PackVersion -ceq $upload.PackVersion -and $PreviousState.MapSha256 -cne $upload.MapSha256) {
        throw 'Changelog state conflicts with confirmed upload history.'
    }
    if ($PreviousState -and $PreviousState.PSObject.Properties['ContentFingerprint'] -and
        $upload.PSObject.Properties['ContentFingerprint'] -and $PreviousState.PackVersion -ceq $upload.PackVersion -and
        $PreviousState.ContentFingerprint -cne $upload.ContentFingerprint) { throw 'Changelog state fingerprint conflicts with confirmed upload history.' }
    $stateMatches = $PreviousState -and $PreviousState.ItemId -ceq $ItemId -and
        $PreviousState.PackVersion -ceq $upload.PackVersion -and $PreviousState.MapSha256 -ceq $upload.MapSha256
    $candidates = @($ReferencePath,
        (Join-Path $ReleaseOutputRoot "SerpsMods/v$($upload.PackVersion)/SerpsMods.provenance.json"),
        (Join-Path $ReleaseOutputRoot "SerpsMods/cache/pack/v$($upload.PackVersion)/SerpsMods.provenance.json"))
    foreach ($path in $candidates) {
        if (-not $path -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        Assert-ApiWorkshopUnlinkedPath $path
        $proof = [IO.File]::ReadAllText($path) | ConvertFrom-Json
        if ($proof.Pack -cne 'SerpsMods' -or $proof.PackGuid -cne 'SerpsMods_Serp' -or
            $proof.Version -cne $upload.PackVersion -or $proof.Map.Sha256 -cne $upload.MapSha256) { continue }
        if ($upload.PSObject.Properties['Format'] -and $upload.Format -ceq 'Loose' -and
            $proof.PSObject.Properties['Workshop'] -and $proof.Workshop.PSObject.Properties['ContentFingerprint'] -and
            $proof.Workshop.ContentFingerprint -cne $upload.ContentFingerprint) {
            throw 'Confirmed loose upload fingerprint differs from its changelog provenance.'
        }
        $recovered = [pscustomobject]@{SchemaVersion=1;ItemId=$ItemId;PackVersion=$proof.Version;MapSha256=$proof.Map.Sha256;
            Mods=@($proof.Mods | Where-Object { $_.State -ceq 'Active' } | ForEach-Object {
                [pscustomobject]@{Guid=$_.Guid;Name=$_.Name;Version=$_.Version}
            })}
        if ($stateMatches) {
            $savedVersions = Get-ConfirmedChangelogModVersions $PreviousState
            $recoveredVersions = Get-ConfirmedChangelogModVersions $recovered
            if ($savedVersions.Count -ne $recoveredVersions.Count) { throw 'Changelog mod inventory conflicts with confirmed release provenance.' }
            foreach ($guid in $recoveredVersions.Keys) {
                if (-not $savedVersions.ContainsKey($guid) -or $savedVersions[$guid] -cne $recoveredVersions[$guid]) {
                    throw "Changelog version conflicts with confirmed release provenance: $guid"
                }
            }
            return $PreviousState
        }
        [void](Get-ConfirmedChangelogModVersions $recovered)
        return $recovered
    }
    if ($stateMatches) { [void](Get-ConfirmedChangelogModVersions $PreviousState); return $PreviousState }
    throw "Confirmed changelog baseline v$($upload.PackVersion) has no matching local provenance. Restore that release evidence; do not rebuild the current package."
}

function Get-ConfirmedChangelogModVersions($State) {
    if ($State.SchemaVersion -ne 1 -or @($State.Mods).Count -eq 0) { throw 'Invalid confirmed changelog state schema/mod inventory.' }
    $versions = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($mod in $State.Mods) {
        if ([string]$mod.Guid -notmatch '^[A-Za-z0-9._-]+$' -or [string]$mod.Version -notmatch '^\d+\.\d+\.\d+$' -or $versions.ContainsKey($mod.Guid)) {
            throw "Invalid/duplicate mod in confirmed changelog state: $($mod.Guid)"
        }
        $versions.Add($mod.Guid, $mod.Version)
    }
    return ,$versions
}
