. (Join-Path $PSScriptRoot 'NexusRelease.Common.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Test fehlgeschlagen: $Message" }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Assert-True ((Compare-NexusSemanticVersion -Left '1.0.95' -Right '1.0.9') -gt 0) '1.0.95 muss neuer als 1.0.9 sein.'
Assert-True ((Compare-NexusSemanticVersion -Left '1.0.0' -Right '1.0.0-beta.1') -gt 0) 'Release muss neuer als Prerelease sein.'

$mods = @('StartConditions','BuildingCosts','BuildingLimit','UnitCosts','UnitLimit','BugfixesAndQoL','ExtraFeatures')
foreach ($mod in $mods) {
    $release = Get-LatestNexusLocalRelease -Root $root -ModName $mod
    $validated = Test-NexusLocalRelease -Release $release
    Assert-True ($validated.Sha256 -match '^[0-9a-f]{64}$') "Artefaktpruefung fuer $mod."
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

$signedHeaders = @(Get-NexusPresignedSignedHeaders -PresignedUrl 'https://upload.invalid/file?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-SignedHeaders=content-disposition%3Bcontent-type%3Bhost&X-Amz-Signature=hidden')
Assert-True ($signedHeaders.Count -eq 3) 'Alle signierten Upload-Header muessen erkannt werden.'
Assert-True ('content-disposition' -in $signedHeaders -and 'content-type' -in $signedHeaders -and 'host' -in $signedHeaders) 'Signierte Upload-Header muessen dekodiert werden.'
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

Write-Host 'Nexus-Release-Tests erfolgreich.' -ForegroundColor Green
