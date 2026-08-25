param(
    [switch]$Preview,
    [switch]$ResetApiKey
)

# Whitelist: Fuer weitere Mods genau einen Eintrag mit lokalem Namen, Nexus-Seiten-ID
# und dem sichtbaren Namen der bereits bestehenden Nexus-Dateikette hinzufuegen.
$NexusTargets = @(
    [PSCustomObject]@{ ModName='StartConditions'; NexusPageId='209'; NexusFileName='StartConditions'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='BuildingCosts'; NexusPageId='222'; NexusFileName='Building Costs'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='BuildingLimit'; NexusPageId='223'; NexusFileName='Building Limit'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='UnitCosts'; NexusPageId='224'; NexusFileName='Unit Costs'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='UnitLimit'; NexusPageId='225'; NexusFileName='Unit Limit'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='BugfixesAndQoL'; NexusPageId='226'; NexusFileName='Bugfixes and QoL'; AllowWrongTwoCorrection=$true },
    [PSCustomObject]@{ ModName='ExtraFeatures'; NexusPageId='226'; NexusFileName='Extra Features'; AllowWrongTwoCorrection=$true }
)

. (Join-Path $PSScriptRoot 'NexusRelease.Common.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$keyPath = Join-Path $root '.nexusmods-api-key.dpapi'
$gameDomain = 'strongholdcrusaderdefinitiveedition'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

try {
    if ($ResetApiKey) {
        if (-not (Test-Path -LiteralPath $keyPath -PathType Leaf)) { Write-NexusLog 'Es ist kein gespeicherter API-Key vorhanden.' Yellow; exit 0 }
        if ((Read-Host 'Zum Entfernen des gespeicherten API-Keys RESET eingeben') -cne 'RESET') { throw 'Zuruecksetzen abgebrochen.' }
        Remove-Item -LiteralPath $keyPath -Force
        Write-NexusLog 'Der verschluesselte API-Key wurde entfernt.' Green
        exit 0
    }

    Write-NexusLog 'Pruefe lokale Release-Artefakte und Nexus-Ziele...' Cyan
    $apiKey = Read-NexusApiKey -Path $keyPath
    $headers = New-NexusHeaders -ApiKey $apiKey
    $plans = [System.Collections.Generic.List[object]]::new()

    foreach ($target in $NexusTargets) {
        $release = Get-LatestNexusLocalRelease -Root $root -ModName $target.ModName
        $validated = Test-NexusLocalRelease -Release $release
        try {
            $changelog = Get-NexusReleaseChangelog -Release $release
        } catch {
            $changelog = [PSCustomObject]@{
                Text=$null
                Path=(Join-Path $release.Directory 'release-notes.md')
                Error=$_.Exception.Message
            }
        }
        $modResponse = Invoke-NexusApi -Method Get -Path "/games/$gameDomain/mods/$($target.NexusPageId)" -Headers $headers
        $modId = [string]$modResponse.data.id
        if ([string]::IsNullOrWhiteSpace($modId)) { throw "Nexus-Seite $($target.NexusPageId) lieferte keine interne Mod-ID." }
        $filesResponse = Invoke-NexusApi -Method Get -Path "/mods/$modId/files" -Headers $headers
        $modFile = Resolve-NexusModFile -ModFiles @($filesResponse.data.mod_files) -ExpectedName $target.NexusFileName
        $versionsResponse = Invoke-NexusApi -Method Get -Path "/mod-files/$([string]$modFile.id)/versions" -Headers $headers
        $versions = @($versionsResponse.data.versions)
        $decision = Get-NexusUpdateDecision -Target $target -Release $release -Versions $versions
        $plans.Add([PSCustomObject]@{
            Target=$target; Release=$release; Hash=$validated.Sha256; ModId=$modId; ModFile=$modFile
            Versions=$versions; Decision=$decision; Changelog=$changelog
        })
    }

    $summary = @($plans | ForEach-Object {
        [PSCustomObject]@{
            Mod=$_.Target.ModName
            Lokal=$_.Release.Version
            Nexus=[string]$_.Decision.Current.version
            Aktion=$(switch ($_.Decision.Action) { 'Update' { 'UPDATE' } 'Correct' { 'KORREKTUR' } default { 'UEBERSPRUNGEN' } })
            Changelog=$(if ([string]::IsNullOrWhiteSpace([string]$_.Changelog.Text)) { 'FEHLT' } else { 'OK' })
            Grund=$_.Decision.Reason
        }
    })
    Write-Host ''
    $summary | Format-Table -AutoSize
    $pending = @($plans | Where-Object { $_.Decision.Action -in @('Update','Correct') })
    if ($pending.Count -eq 0) { Write-NexusLog 'Keine Nexus-Datei muss aktualisiert werden.' Green; exit 0 }
    foreach ($plan in $pending) {
        if ([string]::IsNullOrWhiteSpace([string]$plan.Changelog.Text)) {
            Write-NexusLog "Kein Changelog fuer $($plan.Target.ModName) v$($plan.Release.Version): $($plan.Changelog.Error)" Yellow
        } else {
            Write-NexusLog "Changelog fuer $($plan.Target.ModName) v$($plan.Release.Version) aus $($plan.Changelog.Path) ($($plan.Changelog.Text.Length) Zeichen):" DarkGray
            Write-Host $plan.Changelog.Text
            Write-Host ''
        }
    }
    if ($Preview) { Write-NexusLog "Vorschau beendet. Geplante Uploads: $($pending.Count)." Green; exit 0 }
    $withoutChangelog = @($pending | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.Changelog.Text) })
    if ($withoutChangelog.Count -gt 0) {
        $names = @($withoutChangelog | ForEach-Object { "$($_.Target.ModName) v$($_.Release.Version)" }) -join ', '
        if ((Read-Host "Changelog fehlt fuer: $names. Zum Hochladen ohne Changelog OHNE_CHANGELOG eingeben") -cne 'OHNE_CHANGELOG') {
            throw 'Aktualisierung ohne Changelog abgebrochen.'
        }
    }
    if ((Read-Host "Zum Hochladen und Archivieren von $($pending.Count) Dateiversion(en) UPDATE eingeben") -cne 'UPDATE') { throw 'Aktualisierung abgebrochen.' }

    foreach ($plan in $pending) {
        Write-NexusLog "Starte $($plan.Target.ModName) v$($plan.Release.Version)." Cyan
        $uploadResponse = Invoke-NexusApi -Method Post -Path '/uploads' -Headers $headers -Body @{
            size_bytes=(Get-Item -LiteralPath $plan.Release.ZipPath).Length
            filename=$plan.Release.ZipName
        }
        $uploadId = [string]$uploadResponse.data.id
        $presignedUrl = [string]$uploadResponse.data.presigned_url
        if ([string]::IsNullOrWhiteSpace($uploadId) -or [string]::IsNullOrWhiteSpace($presignedUrl)) { throw "Nexus lieferte keine vollstaendige Upload-Session fuer $($plan.Target.ModName)." }
        Write-NexusLog "Uebertrage $($plan.Release.ZipName) ($((Get-Item -LiteralPath $plan.Release.ZipPath).Length) Bytes)."
        Send-NexusUploadBytes -PresignedUrl $presignedUrl -FilePath $plan.Release.ZipPath -FileName $plan.Release.ZipName
        [void](Invoke-NexusApi -Method Post -Path "/uploads/$uploadId/finalise" -Headers $headers)
        Wait-NexusUploadAvailable -GetState {
            $stateResponse = Invoke-NexusApi -Method Get -Path "/uploads/$uploadId" -Headers $headers
            return [string]$stateResponse.data.state
        }
        $isPrimary = $false
        if ($null -ne $plan.Decision.Current.PSObject.Properties['is_primary']) { $isPrimary = [bool]$plan.Decision.Current.is_primary }
        $createResponse = Invoke-NexusApi -Method Post -Path "/mod-files/$([string]$plan.ModFile.id)/versions" -Headers $headers -Body @{
            upload_id=$uploadId
            name=$plan.Release.ZipName
            version=$plan.Release.Version
            file_category='main'
            primary_mod_manager_download=$isPrimary
            allow_mod_manager_download=$true
            show_requirements_pop_up=$false
            update_mod_version=$false
            archive_existing_file=$true
            previous_version_id=[string]$plan.Decision.Current.id
        }
        if ([string]$createResponse.data.version.id -eq '') { throw "Nexus lieferte keine neue Versions-ID fuer $($plan.Target.ModName)." }

        if ([string]::IsNullOrWhiteSpace([string]$plan.Changelog.Text)) {
            Write-NexusLog "$($plan.Target.ModName) v$($plan.Release.Version) wird wie bestaetigt ohne Changelog hochgeladen." Yellow
        } else {
            # Nexus v3 accepts changelogs through a separate, additive endpoint after
            # the file version exists. Send exactly the prepared release's Changes body.
            [void](Invoke-NexusApi -Method Post -Path "/mods/$($plan.ModId)/changelogs" -Headers $headers -Body @{
                version=$plan.Release.Version
                changelog=$plan.Changelog.Text
            })
            Write-NexusLog "Changelog fuer $($plan.Target.ModName) v$($plan.Release.Version) wurde uebergeben." Green
        }

        $verified = $false
        for ($attempt = 1; $attempt -le 15; $attempt++) {
            $verifyResponse = Invoke-NexusApi -Method Get -Path "/mod-files/$([string]$plan.ModFile.id)/versions" -Headers $headers
            $verifyVersions = @($verifyResponse.data.versions)
            $newActive = Get-NexusActiveVersion -Versions $verifyVersions
            $oldVersion = @($verifyVersions | Where-Object { [string]$_.id -ceq [string]$plan.Decision.Current.id })
            if ([string]$newActive.version -ceq $plan.Release.Version -and $oldVersion.Count -eq 1 -and [string]$oldVersion[0].category -ceq 'archived') { $verified = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $verified) { throw "Nexus-Verifikation fuer $($plan.Target.ModName) ist fehlgeschlagen." }
        Write-NexusLog "$($plan.Target.ModName) v$($plan.Release.Version) ist aktiv; die vorherige Version wurde archiviert." Green
    }
    Write-NexusLog 'Alle geplanten Nexus-Mods-Aktualisierungen wurden erfolgreich verifiziert.' Green
    exit 0
} catch {
    Write-NexusLog $_.Exception.Message Red
    exit 1
} finally {
    $apiKey = $null
    $headers = $null
}
