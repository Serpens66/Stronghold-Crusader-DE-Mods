param(
    [switch]$Preview,
    [switch]$ResetApiKey
)

# Whitelist: Existing chains are updated by their visible Nexus name. A missing
# chain is created only when CreateIfMissing is explicitly true for that target.
$NexusTargets = @(
    [PSCustomObject]@{ ModName='StartConditions'; NexusPageId='209'; NexusFileName='StartConditions'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='BuildingCosts'; NexusPageId='222'; NexusFileName='Building Costs'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='BuildingLimit'; NexusPageId='223'; NexusFileName='Building Limit'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='UnitCosts'; NexusPageId='224'; NexusFileName='Unit Costs'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='UnitLimit'; NexusPageId='225'; NexusFileName='Unit Limit'; AllowWrongTwoCorrection=$false },
    [PSCustomObject]@{ ModName='BugfixesAndQoL'; Artifact='Thin'; NexusPageId='226'; NexusFileName='Bugfixes and QoL'; AllowWrongTwoCorrection=$true; PublishChangelog=$true },
    [PSCustomObject]@{
        ModName='BugfixesAndQoL'; Artifact='Bundle'; NexusPageId='226'
        NexusFileName='Bugfixes and QoL - APIShared Bundle'; AllowWrongTwoCorrection=$false
        CreateIfMissing=$true; FileCategory='main'; PrimaryModManagerDownload=$false; PublishChangelog=$false
        NexusFileDescription='Includes Bugfixes and QoL plus the required APIShared version. Install this bundle instead of the thin archive; do not install both.'
    },
    [PSCustomObject]@{ ModName='ExtraFeatures'; Artifact='Thin'; NexusPageId='226'; NexusFileName='Extra Features'; AllowWrongTwoCorrection=$true; PublishChangelog=$true },
    [PSCustomObject]@{
        ModName='ExtraFeatures'; Artifact='Bundle'; NexusPageId='226'
        NexusFileName='Extra Features - APIShared Bundle'; AllowWrongTwoCorrection=$false
        CreateIfMissing=$true; FileCategory='main'; PrimaryModManagerDownload=$false; PublishChangelog=$false
        NexusFileDescription='Includes Extra Features plus the required APIShared version. Install this bundle instead of the thin archive; do not install both.'
    }
)

. (Join-Path $PSScriptRoot 'NexusRelease.Common.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$keyPath = Join-Path $root '.nexusmods-api-key.dpapi'
$gameDomain = 'strongholdcrusaderdefinitiveedition'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

function Find-NexusCreatedFile {
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)][hashtable]$Headers,
        [string]$ExpectedFileId
    )
    try {
        $filesResponse = Invoke-NexusApi -Method Get -Path "/mods/$($Plan.ModId)/files" -Headers $Headers
        $modFile = Resolve-NexusModFile -ModFiles @($filesResponse.data.mod_files) -ExpectedName $Plan.Target.NexusFileName
        $versionsResponse = Invoke-NexusApi -Method Get -Path "/mod-files/$([string]$modFile.id)/versions" -Headers $Headers
        $valid = Test-NexusModFileVersionState -ModFile $modFile -Versions @($versionsResponse.data.versions) `
            -ExpectedName $Plan.Target.NexusFileName -ExpectedVersion $Plan.Release.Version -ExpectedCategory 'main' `
            -ExpectedPrimary $false -ExpectedFileId $ExpectedFileId
        if ($valid) { return [PSCustomObject]@{ ModFile=$modFile; Versions=@($versionsResponse.data.versions) } }
    } catch {
        Write-NexusLog "Erstellte Dateikette ist noch nicht eindeutig abrufbar: $($_.Exception.Message)" DarkGray
    }
    return $null
}

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
        $artifact = if ($null -ne $target.PSObject.Properties['Artifact']) { [string]$target.Artifact } else { 'Thin' }
        $release = Get-LatestNexusLocalRelease -Root $root -ModName $target.ModName -Artifact $artifact
        $validated = Test-NexusLocalRelease -Release $release
        if (Test-NexusTargetPublishesChangelog -Target $target) {
            try {
                $changelog = Get-NexusReleaseChangelog -Release $release
            } catch {
                $changelog = [PSCustomObject]@{
                    Text=$null
                    Path=(Join-Path $release.Directory 'release-notes.md')
                    Error=$_.Exception.Message
                }
            }
        } else {
            $changelog = [PSCustomObject]@{ Text=$null; Path=$null; Error=$null }
        }

        $modResponse = Invoke-NexusApi -Method Get -Path "/games/$gameDomain/mods/$($target.NexusPageId)" -Headers $headers
        $modId = [string]$modResponse.data.id
        if ([string]::IsNullOrWhiteSpace($modId)) { throw "Nexus-Seite $($target.NexusPageId) lieferte keine interne Mod-ID." }
        $filesResponse = Invoke-NexusApi -Method Get -Path "/mods/$modId/files" -Headers $headers
        $resolution = Resolve-NexusTargetFile -ModFiles @($filesResponse.data.mod_files) -Target $target
        if ($resolution.Action -ceq 'Create') {
            $modFile = $null
            $versions = @()
            $decision = [PSCustomObject]@{ Action='Create'; Reason='Dateikette fehlt und ist zur Neuanlage freigegeben'; Current=$null }
        } else {
            $modFile = $resolution.ModFile
            $versionsResponse = Invoke-NexusApi -Method Get -Path "/mod-files/$([string]$modFile.id)/versions" -Headers $headers
            $versions = @($versionsResponse.data.versions)
            $decision = Get-NexusUpdateDecision -Target $target -Release $release -Versions $versions
        }
        $plans.Add([PSCustomObject]@{
            Target=$target; Release=$release; Hash=$validated.Sha256; ModId=$modId; ModFile=$modFile
            Versions=$versions; Decision=$decision; Changelog=$changelog
        })
    }

    $summary = @($plans | ForEach-Object {
        $currentVersion = if ($null -eq $_.Decision.Current) { '<neu>' } else { [string]$_.Decision.Current.version }
        [PSCustomObject]@{
            Mod="$($_.Target.ModName) [$($_.Release.Artifact)]"
            Lokal=$_.Release.Version
            Nexus=$currentVersion
            Aktion=$(switch ($_.Decision.Action) {
                'Create' { 'CREATE' }
                'Update' { 'UPDATE' }
                'Correct' { 'KORREKTUR' }
                default { 'UEBERSPRUNGEN' }
            })
            Changelog=$(if (-not (Test-NexusTargetPublishesChangelog -Target $_.Target)) { 'N/A' }
                elseif ([string]::IsNullOrWhiteSpace([string]$_.Changelog.Text)) { 'FEHLT' }
                else { 'OK' })
            Grund=$_.Decision.Reason
        }
    })
    Write-Host ''
    $summary | Format-Table -AutoSize
    $pending = @($plans | Where-Object { $_.Decision.Action -in @('Create','Update','Correct') })
    if ($pending.Count -eq 0) { Write-NexusLog 'Keine Nexus-Datei muss aktualisiert werden.' Green; exit 0 }

    $changelogPlans = @(Get-NexusChangelogPublicationPlans -Plans $pending)
    foreach ($plan in $changelogPlans) {
        if ([string]::IsNullOrWhiteSpace([string]$plan.Changelog.Text)) {
            Write-NexusLog "Kein Changelog fuer $($plan.Target.ModName) v$($plan.Release.Version): $($plan.Changelog.Error)" Yellow
        } else {
            Write-NexusLog "Changelog fuer $($plan.Target.ModName) v$($plan.Release.Version) aus $($plan.Changelog.Path) ($($plan.Changelog.Text.Length) Zeichen):" DarkGray
            Write-Host $plan.Changelog.Text
            Write-Host ''
        }
    }
    if ($Preview) { Write-NexusLog "Vorschau beendet. Geplante Uploads: $($pending.Count)." Green; exit 0 }

    $withoutChangelog = @($changelogPlans | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.Changelog.Text) })
    if ($withoutChangelog.Count -gt 0) {
        $names = @($withoutChangelog | ForEach-Object { "$($_.Target.ModName) v$($_.Release.Version)" }) -join ', '
        if ((Read-Host "Changelog fehlt fuer: $names. Zum Hochladen ohne Changelog OHNE_CHANGELOG eingeben") -cne 'OHNE_CHANGELOG') {
            throw 'Aktualisierung ohne Changelog abgebrochen.'
        }
    }
    $containsCreate = @($pending | Where-Object { $_.Decision.Action -ceq 'Create' }).Count -gt 0
    $confirmation = if ($containsCreate) { 'CREATE' } else { 'UPDATE' }
    $prompt = if ($containsCreate) {
        "Zum Anlegen fehlender Dateiketten und Aktualisieren von $($pending.Count) Dateiversion(en) CREATE eingeben"
    } else {
        "Zum Hochladen und Archivieren von $($pending.Count) Dateiversion(en) UPDATE eingeben"
    }
    if ((Read-Host $prompt) -cne $confirmation) { throw 'Aktualisierung abgebrochen.' }

    $changelogPlanKeys = @{}
    foreach ($plan in $changelogPlans) { $changelogPlanKeys["$($plan.ModId)|$($plan.Release.Version)"] = $true }
    $publishedChangelogKeys = @{}

    foreach ($plan in $pending) {
        Write-NexusLog "Starte $($plan.Target.ModName) [$($plan.Release.Artifact)] v$($plan.Release.Version)." Cyan
        $md5 = Get-NexusMd5 -Path $plan.Release.ZipPath
        $uploadResponse = Invoke-NexusApi -Method Post -Path '/uploads' -Headers $headers -Body @{
            size_bytes=(Get-Item -LiteralPath $plan.Release.ZipPath).Length
            filename=$plan.Release.ZipName
            md5=$md5.Hex
        }
        $uploadId = [string]$uploadResponse.data.id
        $presignedUrl = [string]$uploadResponse.data.presigned_url
        if ([string]::IsNullOrWhiteSpace($uploadId) -or [string]::IsNullOrWhiteSpace($presignedUrl)) { throw "Nexus lieferte keine vollstaendige Upload-Session fuer $($plan.Target.ModName)." }
        Write-NexusLog "Uebertrage $($plan.Release.ZipName) ($((Get-Item -LiteralPath $plan.Release.ZipPath).Length) Bytes)."
        Send-NexusUploadBytes -PresignedUrl $presignedUrl -FilePath $plan.Release.ZipPath -FileName $plan.Release.ZipName -ContentMd5Base64 $md5.Base64
        [void](Invoke-NexusApi -Method Post -Path "/uploads/$uploadId/finalise" -Headers $headers)
        Wait-NexusUploadAvailable -GetState {
            $stateResponse = Invoke-NexusApi -Method Get -Path "/uploads/$uploadId" -Headers $headers
            return [string]$stateResponse.data.state
        }

        if ($plan.Decision.Action -ceq 'Create') {
            $createBody = New-NexusCreateModFileBody -Target $plan.Target -Release $plan.Release -ModId $plan.ModId -UploadId $uploadId
            $expectedFileId = $null
            $creationError = $null
            try {
                $createResponse = Invoke-NexusApi -Method Post -Path '/mod-files' -Headers $headers -Body $createBody
                $expectedFileId = [string]$createResponse.data.id
                if ([string]::IsNullOrWhiteSpace($expectedFileId)) { throw "Nexus lieferte keine neue Datei-ID fuer $($plan.Target.ModName)." }
            } catch {
                $creationError = $_.Exception.Message
                Write-NexusLog "Neuanlage lieferte kein eindeutiges Ergebnis; gleiche Dateikette wird vor jedem weiteren Schritt erneut gesucht." Yellow
            }

            $createdState = $null
            for ($attempt = 1; $attempt -le 15; $attempt++) {
                $createdState = Find-NexusCreatedFile -Plan $plan -Headers $headers -ExpectedFileId $expectedFileId
                if ($null -ne $createdState) { break }
                Start-Sleep -Seconds 2
            }
            if ($null -eq $createdState) {
                if ($null -ne $creationError) { throw "$creationError Die anschliessende Abgleichpruefung fand keine eindeutig passende Dateikette; es wird kein zweiter Erstellungsaufruf gesendet." }
                throw "Nexus-Verifikation der neu erstellten Dateikette fuer $($plan.Target.ModName) ist fehlgeschlagen."
            }
            $plan.ModFile = $createdState.ModFile
            Write-NexusLog "$($plan.Target.NexusFileName) v$($plan.Release.Version) wurde neu angelegt und verifiziert." Green
        } else {
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

        $changelogKey = "$($plan.ModId)|$($plan.Release.Version)"
        if ($changelogPlanKeys.ContainsKey($changelogKey) -and -not $publishedChangelogKeys.ContainsKey($changelogKey)) {
            if ([string]::IsNullOrWhiteSpace([string]$plan.Changelog.Text)) {
                Write-NexusLog "$($plan.Target.ModName) v$($plan.Release.Version) wird wie bestaetigt ohne Changelog hochgeladen." Yellow
            } else {
                [void](Invoke-NexusApi -Method Post -Path "/mods/$($plan.ModId)/changelogs" -Headers $headers -Body @{
                    version=$plan.Release.Version
                    changelog=$plan.Changelog.Text
                })
                Write-NexusLog "Changelog fuer $($plan.Target.ModName) v$($plan.Release.Version) wurde einmalig uebergeben." Green
            }
            $publishedChangelogKeys[$changelogKey] = $true
        }
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
