. (Join-Path $PSScriptRoot 'NexusRelease.Common.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Test fehlgeschlagen: $Message" }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Assert-True ((Compare-NexusSemanticVersion -Left '1.0.95' -Right '1.0.9') -gt 0) '1.0.95 muss neuer als 1.0.9 sein.'
Assert-True ((Compare-NexusSemanticVersion -Left '1.0.0' -Right '1.0.0-beta.1') -gt 0) 'Release muss neuer als Prerelease sein.'

$updaterSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Update-NexusMods.ps1'))
$previewGuardIndex = $updaterSource.IndexOf('if ($Preview)')
$uploadMutationIndex = $updaterSource.IndexOf("Invoke-NexusApi -Method Post -Path '/uploads'")
Assert-True ($previewGuardIndex -ge 0 -and $uploadMutationIndex -gt $previewGuardIndex) 'Preview muss vor dem ersten mutierenden Nexus-Aufruf enden.'
Assert-True ([regex]::Matches($updaterSource, 'CreateIfMissing=\$true').Count -eq 2) 'Nur zwei explizite Bundle-Ziele duerfen automatisch erstellt werden.'
Assert-True ([regex]::Matches($updaterSource, "Post -Path '/mod-files'").Count -eq 1) 'Die Neuanlage darf nur einen POST-Codepfad besitzen.'
Assert-True ($updaterSource -match 'Find-NexusCreatedFile' -and $updaterSource -match 'kein zweiter Erstellungsaufruf') 'Unklare Neuanlagen muessen durch Abgleich statt Wiederholung behandelt werden.'
Assert-True ([regex]::Matches($updaterSource, '/changelogs').Count -eq 1) 'Der additive Changelog-Endpunkt darf nur einen Publikationscodepfad besitzen.'

$releaseSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Release-Mod.ps1'))
Assert-True ($releaseSource -match 'Thin APIShared consumer contains private runtime DLLs') 'Thin-Releases muessen private API-Laufzeitkopien ablehnen.'
Assert-True ($releaseSource -match 'bundle must contain exactly one APIShared\.dll') 'Bundle-Releases muessen genau eine APIShared-Kopie erzwingen.'
$steamSource = [IO.File]::ReadAllText((Join-Path $root 'Shared\Steam\Create-SteamModPack.ps1'))
Assert-True ($steamSource -match 'Steam infrastructure must contain exactly one APIShared\.dll') 'Steam muss genau eine zentrale APIShared-Kopie erzwingen.'
Assert-True ($steamSource -match 'Steam consumer .* is not thin') 'Steam muss private API-Kopien in Verbraucherpaketen ablehnen.'

$mods = @('StartConditions','BuildingCosts','BuildingLimit','UnitCosts','UnitLimit','BugfixesAndQoL','ExtraFeatures')
foreach ($mod in $mods) {
    $release = Get-LatestNexusLocalRelease -Root $root -ModName $mod
    $validated = Test-NexusLocalRelease -Release $release
    Assert-True ($validated.Sha256 -match '^[0-9a-f]{64}$') "Artefaktpruefung fuer $mod."
    $changelog = Get-NexusReleaseChangelog -Release $release
    Assert-True (-not [string]::IsNullOrWhiteSpace($changelog.Text)) "Changes-Extraktion fuer $mod."
    Assert-True ($changelog.Text -notmatch '(?m)^##[ \t]+Source and verification') "Changes-Extraktion darf Folgeabschnitte fuer $mod nicht enthalten."
}

$notesTestRoot = Join-Path ([IO.Path]::GetTempPath()) ("nexus-notes-test-" + [Guid]::NewGuid().ToString('N'))
try {
    [void](New-Item -ItemType Directory -Path $notesTestRoot)
    $notesRelease = [PSCustomObject]@{ ModName='NotesTest'; Version='1.2.3'; Directory=$notesTestRoot }
    $notesPath = Join-Path $notesTestRoot 'release-notes.md'
    [IO.File]::WriteAllText(
        $notesPath,
        "# NotesTest v1.2.3`r`n`r`n## Changes`r`n`r`n### v1.2.3`r`n`r`n- First.`r`n- Second.`r`n`r`n## Source and verification`r`n`r`nIgnored.",
        [Text.UTF8Encoding]::new($false))
    $notesResult = Get-NexusReleaseChangelog -Release $notesRelease
    Assert-True ($notesResult.Text -ceq "### v1.2.3`r`n`r`n- First.`r`n- Second.") 'Der komplette Changes-Text muss exakt extrahiert werden.'

    [IO.File]::WriteAllText($notesPath, "# NotesTest`r`n`r`n## Other`r`n", [Text.UTF8Encoding]::new($false))
    $missingChangesRejected = $false
    try { [void](Get-NexusReleaseChangelog -Release $notesRelease) } catch { $missingChangesRejected = $true }
    Assert-True $missingChangesRejected 'Fehlender Changes-Abschnitt muss abgewiesen werden.'
} finally {
    if (Test-Path -LiteralPath $notesTestRoot) { Remove-Item -LiteralPath $notesTestRoot -Recurse -Force }
}

$artifactTestRoot = Join-Path ([IO.Path]::GetTempPath()) ("nexus-artifact-test-" + [Guid]::NewGuid().ToString('N'))
try {
    $artifactReleaseDirectory = Join-Path $artifactTestRoot '.release-output\Synthetic\v1.2.3'
    $thinStage = Join-Path $artifactTestRoot 'thin-stage\BepInEx\plugins\Synthetic_Serp'
    $bundleConsumerStage = Join-Path $artifactTestRoot 'bundle-stage\BepInEx\plugins\Synthetic_Serp'
    $bundleApiStage = Join-Path $artifactTestRoot 'bundle-stage\BepInEx\plugins\APIShared_Serp'
    [void](New-Item -ItemType Directory -Path $artifactReleaseDirectory,$thinStage,$bundleConsumerStage,$bundleApiStage -Force)
    $consumerInfo = @{ GUID='Synthetic_Serp'; Name='Synthetic'; Version='1.2.3' } | ConvertTo-Json
    $apiInfo = @{ GUID='APIShared_Serp'; Name='APIShared'; Version='0.3.0' } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $thinStage 'info.json'), $consumerInfo, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $bundleConsumerStage 'info.json'), $consumerInfo, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $bundleApiStage 'info.json'), $apiInfo, [Text.UTF8Encoding]::new($false))
    $thinName = 'Synthetic-v1.2.3.zip'
    $bundleName = 'Synthetic-v1.2.3-with-APIShared-v0.3.0.zip'
    $thinPath = Join-Path $artifactReleaseDirectory $thinName
    $bundlePath = Join-Path $artifactReleaseDirectory $bundleName
    Compress-Archive -LiteralPath (Join-Path $artifactTestRoot 'thin-stage\BepInEx') -DestinationPath $thinPath
    Compress-Archive -LiteralPath (Join-Path $artifactTestRoot 'bundle-stage\BepInEx') -DestinationPath $bundlePath
    $thinHash = Get-NexusSha256 -Path $thinPath
    $bundleHash = Get-NexusSha256 -Path $bundlePath
    [IO.File]::WriteAllText("$thinPath.sha256", "$thinHash  $thinName", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText("$bundlePath.sha256", "$bundleHash  $bundleName", [Text.UTF8Encoding]::new($false))
    $artifactProvenance = @{
        Mod='Synthetic'; Version='1.2.3'
        Package=@{ Profile='Thin'; File=$thinName; Sha256=$thinHash }
        Bundle=@{ Profile='Bundle'; File=$bundleName; Sha256=$bundleHash; ApiShared=@{ Version='0.3.0' } }
    } | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText((Join-Path $artifactReleaseDirectory 'Synthetic-v1.2.3.provenance.json'), $artifactProvenance, [Text.UTF8Encoding]::new($false))

    $syntheticThin = Get-LatestNexusLocalRelease -Root $artifactTestRoot -ModName 'Synthetic' -Artifact Thin
    $syntheticBundle = Get-LatestNexusLocalRelease -Root $artifactTestRoot -ModName 'Synthetic' -Artifact Bundle
    Assert-True ((Test-NexusLocalRelease -Release $syntheticThin).Sha256 -ceq $thinHash) 'Synthetisches Thin-Artefakt.'
    Assert-True ((Test-NexusLocalRelease -Release $syntheticBundle).Sha256 -ceq $bundleHash) 'Synthetisches Bundle-Artefakt.'
} finally {
    if (Test-Path -LiteralPath $artifactTestRoot) { Remove-Item -LiteralPath $artifactTestRoot -Recurse -Force }
}

$files = @(
    [PSCustomObject]@{ id='a'; name='Bugfixes and QoL'; is_active=$true },
    [PSCustomObject]@{ id='b'; name='Extra Features'; is_active=$true }
)
Assert-True ((Resolve-NexusModFile -ModFiles $files -ExpectedName 'BugfixesAndQoL').id -ceq 'a') 'Normalisierte Dateizuordnung.'
Assert-True ((Resolve-NexusModFile -ModFiles @(
    [PSCustomObject]@{ id='start'; name='StartConditions Serp'; is_active=$true },
    [PSCustomObject]@{ id='lobby'; name='LobbyModSettings'; is_active=$false }
) -ExpectedName 'StartConditions').id -ceq 'start') 'Serp-Suffix muss passen, LobbyModSettings muss ausgeschlossen bleiben.'
Assert-True ((Resolve-NexusModFile -ModFiles @(
    [PSCustomObject]@{ id='bugfix'; name='BugfixesAndQoL V1.0.69'; is_active=$true },
    [PSCustomObject]@{ id='extra'; name='ExtraFeatures V1.0.35'; is_active=$true },
    [PSCustomObject]@{ id='old'; name='SomeSettings Serp'; is_active=$false },
    [PSCustomObject]@{ id='lobby'; name='LobbyModSettings'; is_active=$false }
) -ExpectedName 'Bugfixes and QoL').id -ceq 'bugfix') 'Aktive versionierte Dateikette muss passen; archivierte Dateien muessen ignoriert werden.'
$ambiguous = $false
try { [void](Resolve-NexusModFile -ModFiles ($files + [PSCustomObject]@{ id='c'; name='BugfixesAndQoL'; is_active=$true }) -ExpectedName 'Bugfixes and QoL') }
catch { $ambiguous = $true }
Assert-True $ambiguous 'Mehrdeutige Dateizuordnung muss fehlschlagen.'
$createTarget = [PSCustomObject]@{
    NexusFileName='Bugfixes and QoL - APIShared Bundle'
    CreateIfMissing=$true
    FileCategory='main'
    PrimaryModManagerDownload=$false
    NexusFileDescription='Bundle description.'
}
$createResolution = Resolve-NexusTargetFile -ModFiles $files -Target $createTarget
Assert-True ($createResolution.Action -ceq 'Create' -and $null -eq $createResolution.ModFile) 'Explizit freigegebene fehlende Dateikette muss CREATE planen.'
$existingBundle = [PSCustomObject]@{ id='bundle'; name='Bugfixes and QoL - APIShared Bundle'; is_active=$true }
$existingResolution = Resolve-NexusTargetFile -ModFiles ($files + $existingBundle) -Target $createTarget
Assert-True ($existingResolution.Action -ceq 'Existing' -and $existingResolution.ModFile.id -ceq 'bundle') 'Vorhandene Bundle-Dateikette muss wiederverwendet werden.'
$ambiguousTargetRejected = $false
try { [void](Resolve-NexusTargetFile -ModFiles ($files + $existingBundle + [PSCustomObject]@{ id='bundle-2'; name='BugfixesAndQoL APIShared Bundle'; is_active=$true }) -Target $createTarget) }
catch { $ambiguousTargetRejected = $true }
Assert-True $ambiguousTargetRejected 'Mehrdeutige aktive Bundle-Dateiketten muessen die Planung blockieren.'
$archivedRejected = $false
try { [void](Resolve-NexusTargetFile -ModFiles ($files + [PSCustomObject]@{ id='archived-bundle'; name='Bugfixes and QoL - APIShared Bundle'; is_active=$false }) -Target $createTarget) }
catch { $archivedRejected = $true }
Assert-True $archivedRejected 'Archivierte passende Dateikette muss eine Neuanlage blockieren.'
$notAllowedRejected = $false
try { [void](Resolve-NexusTargetFile -ModFiles $files -Target ([PSCustomObject]@{ NexusFileName='Missing Thin' })) }
catch { $notAllowedRejected = $true }
Assert-True $notAllowedRejected 'Fehlende Dateikette ohne CreateIfMissing muss abgewiesen werden.'

$createBody = New-NexusCreateModFileBody -Target $createTarget -Release ([PSCustomObject]@{ Version='1.2.3' }) -ModId 'mod-226' -UploadId 'upload-1'
Assert-True ($createBody.mod_id -ceq 'mod-226' -and $createBody.upload_id -ceq 'upload-1') 'Create-Mod-File-Body muss Mod und Upload zuordnen.'
Assert-True ($createBody.name -ceq $createTarget.NexusFileName -and $createBody.version -ceq '1.2.3') 'Create-Mod-File-Body muss Name und Version enthalten.'
Assert-True ($createBody.file_category -ceq 'main' -and -not $createBody.primary_mod_manager_download) 'Bundle muss Main und nicht primaer sein.'

$createdVersions = @([PSCustomObject]@{ id='version-1'; version='1.2.3'; category='main'; position='1'; is_primary=$false })
Assert-True (Test-NexusModFileVersionState -ModFile $existingBundle -Versions $createdVersions -ExpectedName $createTarget.NexusFileName -ExpectedVersion '1.2.3' -ExpectedCategory 'main' -ExpectedPrimary $false -ExpectedFileId 'bundle') 'Neu erstellte Dateikette muss exakt verifiziert werden.'
Assert-True (-not (Test-NexusModFileVersionState -ModFile $existingBundle -Versions $createdVersions -ExpectedName $createTarget.NexusFileName -ExpectedVersion '1.2.3' -ExpectedCategory 'main' -ExpectedPrimary $false -ExpectedFileId 'other')) 'Abweichende Datei-ID muss die Wiederholungspruefung blockieren.'
$observedBundle = [PSCustomObject]@{ id='7948587'; name='Bugfixes and QoL - APIShared Bundle'; is_active=$true }
$observedVersions = @([PSCustomObject]@{ id='34183644709611'; version='1.0.142'; category='main'; position='1.0'; is_primary=$false })
Assert-True (Test-NexusModFileVersionState -ModFile $observedBundle -Versions $observedVersions -ExpectedName $observedBundle.name -ExpectedVersion '1.0.142' -ExpectedCategory 'main' -ExpectedPrimary $false -ExpectedFileId '7948587') 'Der beobachtete Nexus-Bundlezustand muss akzeptiert werden.'

$wrongName = Get-NexusModFileVersionStateDiagnostic -ModFile ([PSCustomObject]@{ id='7948587'; name='Wrong bundle' }) -Versions $observedVersions -ExpectedName $observedBundle.name -ExpectedVersion '1.0.142' -ExpectedCategory 'main' -ExpectedPrimary $false -ExpectedFileId '7948587'
$wrongVersion = Get-NexusModFileVersionStateDiagnostic -ModFile $observedBundle -Versions @([PSCustomObject]@{ id='v'; version='1.0.141'; category='main'; position='1'; is_primary=$false }) -ExpectedName $observedBundle.name -ExpectedVersion '1.0.142' -ExpectedCategory 'main' -ExpectedPrimary $false -ExpectedFileId '7948587'
$wrongCategory = Get-NexusModFileVersionStateDiagnostic -ModFile $observedBundle -Versions @([PSCustomObject]@{ id='v'; version='1.0.142'; category='optional'; position='1'; is_primary=$false }) -ExpectedName $observedBundle.name -ExpectedVersion '1.0.142' -ExpectedCategory 'main' -ExpectedPrimary $false -ExpectedFileId '7948587'
$wrongPrimary = Get-NexusModFileVersionStateDiagnostic -ModFile $observedBundle -Versions @([PSCustomObject]@{ id='v'; version='1.0.142'; category='main'; position='1'; is_primary=$true }) -ExpectedName $observedBundle.name -ExpectedVersion '1.0.142' -ExpectedCategory 'main' -ExpectedPrimary $false -ExpectedFileId '7948587'
Assert-True (-not $wrongName.IsValid -and $wrongName.Diagnostic -match 'Dateikettenname') 'Falscher Name muss mit Diagnose abgewiesen werden.'
Assert-True (-not $wrongVersion.IsValid -and $wrongVersion.Diagnostic -match 'Aktive Version') 'Falsche Version muss mit Diagnose abgewiesen werden.'
Assert-True (-not $wrongCategory.IsValid -and $wrongCategory.Diagnostic -match 'Kategorie') 'Falsche Kategorie muss mit Diagnose abgewiesen werden.'
Assert-True (-not $wrongPrimary.IsValid -and $wrongPrimary.Diagnostic -match 'Primaerstatus') 'Falscher Primaerstatus muss mit Diagnose abgewiesen werden.'

$delayedProbeCounter = [PSCustomObject]@{ Count=0 }
$delayedResult = Wait-NexusCreatedFileVerification -MaxAttempts 20 -PollMilliseconds 0 -Probe {
    $delayedProbeCounter.Count++
    if ($delayedProbeCounter.Count -le 16) { return [PSCustomObject]@{ Found=$false; Diagnostic="noch nicht sichtbar $($delayedProbeCounter.Count)" } }
    return [PSCustomObject]@{ Found=$true; Diagnostic='sichtbar'; ModFile=$observedBundle; Versions=$observedVersions }
}
Assert-True ($delayedResult.Found -and $delayedProbeCounter.Count -eq 17) 'Eine erst nach mehr als 15 Abfragen sichtbare Dateikette muss erfolgreich verifiziert werden.'
$timeoutProbeCounter = [PSCustomObject]@{ Count=0 }
$timeoutResult = Wait-NexusCreatedFileVerification -MaxAttempts 3 -PollMilliseconds 0 -Probe {
    $timeoutProbeCounter.Count++
    return [PSCustomObject]@{ Found=$false; Diagnostic="letzte Diagnose $($timeoutProbeCounter.Count)" }
}
Assert-True (-not $timeoutResult.Found -and $timeoutProbeCounter.Count -eq 3 -and $timeoutResult.Diagnostic -ceq 'letzte Diagnose 3') 'Timeout muss nach exakt den erlaubten Abfragen die letzte Diagnose liefern.'

$thinPlan = [PSCustomObject]@{
    Target=[PSCustomObject]@{ Artifact='Thin'; PublishChangelog=$true }
    ModId='mod-226'; Release=[PSCustomObject]@{ Version='1.2.3' }
    Changelog=[PSCustomObject]@{ Text='One changelog.' }
}
$bundlePlan = [PSCustomObject]@{
    Target=[PSCustomObject]@{ Artifact='Bundle'; PublishChangelog=$false }
    ModId='mod-226'; Release=[PSCustomObject]@{ Version='1.2.3' }
    Changelog=[PSCustomObject]@{ Text='One changelog.' }
}
$changelogPlans = @(Get-NexusChangelogPublicationPlans -Plans @($thinPlan,$bundlePlan))
Assert-True ($changelogPlans.Count -eq 1 -and $changelogPlans[0] -eq $thinPlan) 'Thin plus Bundle duerfen nur einen Thin-Changelog planen.'
Assert-True (@(Get-NexusChangelogPublicationPlans -Plans @($bundlePlan)).Count -eq 0) 'Ein reiner Bundle-Upload darf keinen Changelog planen.'

$targetNormal = [PSCustomObject]@{ AllowWrongTwoCorrection=$false }
$targetCorrection = [PSCustomObject]@{ AllowWrongTwoCorrection=$true }
$release = [PSCustomObject]@{ Version='1.0.69' }
$activeOld = @([PSCustomObject]@{ id='old'; version='1.0.68'; category='main'; position='1'; is_primary=$true })
$activeSame = @([PSCustomObject]@{ id='same'; version='1.0.69'; category='main'; position='2'; is_primary=$true })
$activeWrong = @([PSCustomObject]@{ id='wrong'; version='2.0.0'; category='main'; position='3'; is_primary=$true })
Assert-True ((Get-NexusUpdateDecision -Target $targetNormal -Release $release -Versions $activeOld).Action -ceq 'Update') 'Neuere Version muss Update sein.'
Assert-True ((Get-NexusUpdateDecision -Target $targetNormal -Release $release -Versions $activeSame).Action -ceq 'Skip') 'Gleiche Version muss uebersprungen werden.'
Assert-True ((Get-NexusUpdateDecision -Target $targetNormal -Release $release -Versions $activeWrong).Action -ceq 'Skip') 'Aeltere Version muss uebersprungen werden.'
Assert-True ((Get-NexusUpdateDecision -Target $targetCorrection -Release $release -Versions $activeWrong).Action -ceq 'Correct') '2.0.0-Ausnahme muss korrigieren.'
$archivedDuplicate = @(
    [PSCustomObject]@{ id='active'; version='1.0.68'; category='main'; position='2'; is_primary=$true },
    [PSCustomObject]@{ id='archived'; version='1.0.69'; category='archived'; position='1'; is_primary=$false }
)
$duplicateRejected = $false
try { [void](Get-NexusUpdateDecision -Target $targetNormal -Release $release -Versions $archivedDuplicate) }
catch { $duplicateRejected = $true }
Assert-True $duplicateRejected 'Bereits archivierte identische Version muss manuelle Pruefung verlangen.'

$states = [System.Collections.Generic.Queue[string]]::new()
$states.Enqueue('created')
$states.Enqueue('available')
Wait-NexusUploadAvailable -GetState { return $states.Dequeue() } -TimeoutSeconds 2 -PollMilliseconds 1
$timedOut = $false
try { Wait-NexusUploadAvailable -GetState { return 'created' } -TimeoutSeconds 1 -PollMilliseconds 1 }
catch { $timedOut = $true }
Assert-True $timedOut 'Polling-Timeout muss fehlschlagen.'

$signedHeaders = @(Get-NexusPresignedSignedHeaders -PresignedUrl 'https://upload.invalid/file?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-SignedHeaders=content-disposition%3Bcontent-md5%3Bcontent-type%3Bhost&X-Amz-Signature=hidden')
Assert-True ($signedHeaders.Count -eq 4) 'Alle signierten Upload-Header muessen erkannt werden.'
Assert-True ('content-disposition' -in $signedHeaders -and 'content-md5' -in $signedHeaders -and 'content-type' -in $signedHeaders -and 'host' -in $signedHeaders) 'Signierte Upload-Header muessen dekodiert werden.'
$md5Path = Join-Path ([IO.Path]::GetTempPath()) ("nexus-md5-" + [Guid]::NewGuid().ToString('N'))
try {
    [IO.File]::WriteAllBytes($md5Path, [Text.Encoding]::ASCII.GetBytes('abc'))
    $md5 = Get-NexusMd5 -Path $md5Path
    Assert-True ($md5.Hex -ceq '900150983cd24fb0d6963f7d28e17f72') 'MD5 muss als Hexwert berechnet werden.'
    Assert-True ($md5.Base64 -ceq 'kAFQmDzST7DWlj99KOF/cg==') 'MD5 muss als Base64 fuer Content-MD5 berechnet werden.'
    $uploadContent = New-NexusUploadContent -Bytes ([Text.Encoding]::ASCII.GetBytes('abc')) -FileName 'test.zip' -SignedHeaders $signedHeaders -ContentMd5Base64 $md5.Base64
    try {
        Assert-True ([Convert]::ToBase64String($uploadContent.Headers.ContentMD5) -ceq $md5.Base64) 'Content-MD5 muss am PUT-Inhalt gesetzt werden.'
        Assert-True ([string]$uploadContent.Headers.ContentDisposition -match 'test\.zip') 'Content-Disposition muss den Dateinamen enthalten.'
    } finally { $uploadContent.Dispose() }
} finally {
    if (Test-Path -LiteralPath $md5Path) { Remove-Item -LiteralPath $md5Path -Force }
}
$missingSignedHeadersRejected = $false
try { [void](Get-NexusPresignedSignedHeaders -PresignedUrl 'https://upload.invalid/file?X-Amz-Signature=hidden') }
catch { $missingSignedHeadersRejected = $true }
Assert-True $missingSignedHeadersRejected 'Upload-URL ohne SignedHeaders muss abgewiesen werden.'

$dpapiPath = Join-Path ([IO.Path]::GetTempPath()) ("nexus-dpapi-" + [Guid]::NewGuid().ToString('N'))
try {
    Protect-NexusApiKey -ApiKey 'roundtrip-test-value' -Path $dpapiPath
    Assert-True ((Unprotect-NexusApiKey -Path $dpapiPath) -ceq 'roundtrip-test-value') 'DPAPI-Roundtrip.'
} finally {
    if (Test-Path -LiteralPath $dpapiPath) { Remove-Item -LiteralPath $dpapiPath -Force }
}

$tamperRoot = Join-Path ([IO.Path]::GetTempPath()) ("shcde-nexus-test-" + [Guid]::NewGuid().ToString('N'))
try {
    [void](New-Item -ItemType Directory -Path $tamperRoot)
    $source = Get-LatestNexusLocalRelease -Root $root -ModName 'StartConditions'
    $fakeRelease = [PSCustomObject]@{ ModName=$source.ModName; Version=$source.Version; Directory=$tamperRoot; ZipName=$source.ZipName; ZipPath=(Join-Path $tamperRoot $source.ZipName) }
    Copy-Item -LiteralPath $source.ZipPath -Destination $fakeRelease.ZipPath
    [IO.File]::AppendAllText($fakeRelease.ZipPath, 'tampered')
    Copy-Item -LiteralPath "$($source.ZipPath).sha256" -Destination "$($fakeRelease.ZipPath).sha256"
    Copy-Item -LiteralPath (Join-Path $source.Directory "$($source.ModName)-v$($source.Version).provenance.json") -Destination $tamperRoot
    $rejected = $false
    try { [void](Test-NexusLocalRelease -Release $fakeRelease) } catch { $rejected = $true }
    Assert-True $rejected 'Manipuliertes ZIP muss abgewiesen werden.'

    $missingRoot = Join-Path $tamperRoot 'missing-provenance'
    [void](New-Item -ItemType Directory -Path $missingRoot)
    $missingRelease = [PSCustomObject]@{ ModName=$source.ModName; Version=$source.Version; Directory=$missingRoot; ZipName=$source.ZipName; ZipPath=(Join-Path $missingRoot $source.ZipName) }
    Copy-Item -LiteralPath $source.ZipPath -Destination $missingRelease.ZipPath
    Copy-Item -LiteralPath "$($source.ZipPath).sha256" -Destination "$($missingRelease.ZipPath).sha256"
    $missingRejected = $false
    try { [void](Test-NexusLocalRelease -Release $missingRelease) } catch { $missingRejected = $true }
    Assert-True $missingRejected 'Fehlende Provenance muss abgewiesen werden.'

    $mismatchRoot = Join-Path $tamperRoot 'manifest-mismatch'
    $mismatchStage = Join-Path $mismatchRoot 'stage'
    [void](New-Item -ItemType Directory -Path $mismatchStage -Force)
    Expand-Archive -LiteralPath $source.ZipPath -DestinationPath $mismatchStage
    $manifestPath = @(Get-ChildItem -LiteralPath $mismatchStage -Filter info.json -File -Recurse)[0].FullName
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $manifest.Version = '9.9.9'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    $mismatchRelease = [PSCustomObject]@{ ModName=$source.ModName; Version=$source.Version; Directory=$mismatchRoot; ZipName=$source.ZipName; ZipPath=(Join-Path $mismatchRoot $source.ZipName) }
    Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $mismatchStage | ForEach-Object { $_.FullName }) -DestinationPath $mismatchRelease.ZipPath
    $mismatchHash = Get-NexusSha256 -Path $mismatchRelease.ZipPath
    [IO.File]::WriteAllText("$($mismatchRelease.ZipPath).sha256", "$mismatchHash  $($mismatchRelease.ZipName)", [Text.UTF8Encoding]::new($false))
    $sourceProvenance = Get-Content -LiteralPath (Join-Path $source.Directory "$($source.ModName)-v$($source.Version).provenance.json") -Raw | ConvertFrom-Json
    $sourceProvenance.Package.Sha256 = $mismatchHash
    [IO.File]::WriteAllText((Join-Path $mismatchRoot "$($source.ModName)-v$($source.Version).provenance.json"), ($sourceProvenance | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    $mismatchRejected = $false
    try { [void](Test-NexusLocalRelease -Release $mismatchRelease) } catch { $mismatchRejected = $true }
    Assert-True $mismatchRejected 'Abweichende info.json-Version muss abgewiesen werden.'
} finally {
    if (Test-Path -LiteralPath $tamperRoot) { Remove-Item -LiteralPath $tamperRoot -Recurse -Force }
}

$global:LASTEXITCODE = 0
Write-Host 'Nexus-Release-Tests erfolgreich.' -ForegroundColor Green
