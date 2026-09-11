Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

function Write-NexusLog {
    param([Parameter(Mandatory)][string]$Message, [ConsoleColor]$Color = [ConsoleColor]::Gray)
    Write-Host "[$([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss.fff'))] $Message" -ForegroundColor $Color
}

function Compare-NexusSemanticVersion {
    param([Parameter(Mandatory)][string]$Left, [Parameter(Mandatory)][string]$Right)
    $pattern = '^(\d+)\.(\d+)\.(\d+)(?:-([^+]+))?(?:\+.*)?$'
    $leftMatch = [regex]::Match($Left, $pattern)
    $rightMatch = [regex]::Match($Right, $pattern)
    if (-not $leftMatch.Success -or -not $rightMatch.Success) {
        throw "Ungueltige semantische Version: '$Left' oder '$Right'."
    }
    for ($index = 1; $index -le 3; $index++) {
        $leftNumber = [uint64]$leftMatch.Groups[$index].Value
        $rightNumber = [uint64]$rightMatch.Groups[$index].Value
        if ($leftNumber -lt $rightNumber) { return -1 }
        if ($leftNumber -gt $rightNumber) { return 1 }
    }
    $leftPre = $leftMatch.Groups[4].Value
    $rightPre = $rightMatch.Groups[4].Value
    if ([string]::IsNullOrEmpty($leftPre)) { return $(if ([string]::IsNullOrEmpty($rightPre)) { 0 } else { 1 }) }
    if ([string]::IsNullOrEmpty($rightPre)) { return -1 }
    $leftParts = @($leftPre.Split('.'))
    $rightParts = @($rightPre.Split('.'))
    $count = [Math]::Max($leftParts.Count, $rightParts.Count)
    for ($index = 0; $index -lt $count; $index++) {
        if ($index -ge $leftParts.Count) { return -1 }
        if ($index -ge $rightParts.Count) { return 1 }
        $leftValue = 0L
        $rightValue = 0L
        $leftNumeric = [long]::TryParse($leftParts[$index], [ref]$leftValue)
        $rightNumeric = [long]::TryParse($rightParts[$index], [ref]$rightValue)
        if ($leftNumeric -and $rightNumeric) {
            if ($leftValue -lt $rightValue) { return -1 }
            if ($leftValue -gt $rightValue) { return 1 }
        } elseif ($leftNumeric) { return -1
        } elseif ($rightNumeric) { return 1
        } else {
            $comparison = [string]::CompareOrdinal($leftParts[$index], $rightParts[$index])
            if ($comparison -ne 0) { return $(if ($comparison -lt 0) { -1 } else { 1 }) }
        }
    }
    return 0
}

function Get-NexusSha256 {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
        finally { $sha.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-NexusMd5 {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    try {
        $md5 = [Security.Cryptography.MD5]::Create()
        try {
            $bytes = $md5.ComputeHash($stream)
            return [PSCustomObject]@{
                Hex = ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
                Base64 = [Convert]::ToBase64String($bytes)
            }
        } finally { $md5.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-LatestNexusLocalRelease {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$ModName,
        [ValidateSet('Thin','Bundle')][string]$Artifact = 'Thin'
    )
    $modOutput = Join-Path (Join-Path $Root '.release-output') $ModName
    if (-not (Test-Path -LiteralPath $modOutput -PathType Container)) { throw "Kein Release-Ordner fuer ${ModName}: $modOutput" }
    $candidates = [System.Collections.Generic.List[object]]::new()
    foreach ($directory in @(Get-ChildItem -LiteralPath $modOutput -Directory)) {
        if ($directory.Name -notmatch '^v(.+)$') { continue }
        $version = $Matches[1]
        if ($version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') { continue }
        $zipFiles = @(if ($Artifact -ceq 'Thin') {
            Get-Item -LiteralPath (Join-Path $directory.FullName "$ModName-v$version.zip") -ErrorAction SilentlyContinue
        } else {
            Get-ChildItem -LiteralPath $directory.FullName -File -Filter "$ModName-v$version-with-APIShared-v*.zip"
        })
        if ($zipFiles.Count -gt 1) { throw "Mehrere $Artifact-Artefakte fuer $ModName v${version}: $(@($zipFiles.Name) -join ', ')" }
        if ($zipFiles.Count -eq 1) {
            $candidates.Add([PSCustomObject]@{
                ModName=$ModName; Version=$version; Artifact=$Artifact; Directory=$directory.FullName
                ZipName=$zipFiles[0].Name; ZipPath=$zipFiles[0].FullName
            })
        }
    }
    if ($candidates.Count -eq 0) { throw "Kein gueltiges lokales $Artifact-Release-ZIP fuer $ModName gefunden. Release-Mod.ps1 muss beide APIShared-Profile erzeugen." }
    $latest = $candidates[0]
    for ($index = 1; $index -lt $candidates.Count; $index++) {
        if ((Compare-NexusSemanticVersion -Left $candidates[$index].Version -Right $latest.Version) -gt 0) { $latest = $candidates[$index] }
    }
    return $latest
}

function Get-NexusReleaseChangelog {
    param([Parameter(Mandatory)]$Release)
    $notesPath = Join-Path $Release.Directory 'release-notes.md'
    if (-not (Test-Path -LiteralPath $notesPath -PathType Leaf)) {
        throw "Fehlende Release Notes fuer $($Release.ModName) v$($Release.Version): $notesPath"
    }

    $resolvedPath = (Resolve-Path -LiteralPath $notesPath).Path
    $notesText = [IO.File]::ReadAllText($resolvedPath)
    $changesMatch = [regex]::Match(
        $notesText,
        '(?ms)^##[ \t]+Changes[ \t]*\r?\n(?<body>.*?)(?=^##[ \t]+|\z)')
    if (-not $changesMatch.Success -or [string]::IsNullOrWhiteSpace($changesMatch.Groups['body'].Value)) {
        throw "Kein nichtleerer Abschnitt '## Changes' gefunden fuer $($Release.ModName) v$($Release.Version): $resolvedPath"
    }

    $text = $changesMatch.Groups['body'].Value.Trim()
    $text = [regex]::Replace($text, "`r`n|`r|`n", "`r`n")
    return [PSCustomObject]@{ Text=$text; Path=$resolvedPath }
}

function Test-NexusLocalRelease {
    param([Parameter(Mandatory)]$Release)
    $artifactProperty = $Release.PSObject.Properties['Artifact']
    $artifact = if ($null -eq $artifactProperty) { 'Thin' } else { [string]$artifactProperty.Value }
    if ($artifact -notin @('Thin', 'Bundle')) {
        throw "Unbekanntes Nexus-Artefaktprofil '$artifact' fuer $($Release.ModName)."
    }
    $hashPath = "$($Release.ZipPath).sha256"
    $provenancePath = Join-Path $Release.Directory "$($Release.ModName)-v$($Release.Version).provenance.json"
    if (-not (Test-Path -LiteralPath $hashPath -PathType Leaf)) { throw "Fehlende SHA-256-Datei: $hashPath" }
    if (-not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) { throw "Fehlende Provenance-Datei: $provenancePath" }
    $actualHash = Get-NexusSha256 -Path $Release.ZipPath
    $hashText = (Get-Content -LiteralPath $hashPath -Raw).Trim()
    if ($hashText -notmatch '^([0-9A-Fa-f]{64})\s+(.+)$') { throw "Ungueltiges SHA-256-Format: $hashPath" }
    if ($Matches[1].ToLowerInvariant() -cne $actualHash -or $Matches[2] -cne $Release.ZipName) { throw "SHA-256-Pruefung fehlgeschlagen: $($Release.ZipName)" }
    $provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
    if ([string]$provenance.Mod -cne $Release.ModName -or [string]$provenance.Version -cne $Release.Version) { throw "Provenance nennt einen anderen Mod oder eine andere Version: $provenancePath" }
    $artifactProvenance = if ($artifact -ceq 'Bundle') { $provenance.Bundle } else { $provenance.Package }
    if ($null -eq $artifactProvenance -or [string]$artifactProvenance.File -cne $Release.ZipName -or
        ([string]$artifactProvenance.Sha256).ToLowerInvariant() -cne $actualHash) {
        throw "Provenance-Paketdaten fuer $artifact stimmen nicht: $provenancePath"
    }

    $auditRoot = Join-Path ([IO.Path]::GetTempPath()) ("shcde-nexus-audit-" + [Guid]::NewGuid().ToString('N'))
    try {
        Expand-Archive -LiteralPath $Release.ZipPath -DestinationPath $auditRoot
        $infos = @(Get-ChildItem -LiteralPath $auditRoot -Filter info.json -File -Recurse)
        $manifests = @($infos | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
        if ($artifact -ceq 'Bundle') {
            $api = @($manifests | Where-Object { [string]$_.GUID -ceq 'APIShared_Serp' })
            $consumer = @($manifests | Where-Object { [string]$_.GUID -cne 'APIShared_Serp' })
            if ($api.Count -ne 1 -or $consumer.Count -ne 1) { throw "Bundle muss genau APIShared und einen Verbraucher enthalten." }
            if ([string]$consumer[0].Version -cne $Release.Version -or
                [string]$api[0].Version -cne [string]$artifactProvenance.ApiShared.Version) {
                throw "Bundle-Manifestversionen stimmen nicht mit der Provenance ueberein: $($Release.ZipName)"
            }
        } else {
            if ($manifests.Count -ne 1 -or [string]$manifests[0].Version -cne $Release.Version) {
                throw "Thin-Archiv muss genau ein passendes info.json enthalten: $($Release.ZipName)"
            }
        }
    } finally {
        if (Test-Path -LiteralPath $auditRoot) { Remove-Item -LiteralPath $auditRoot -Recurse -Force }
    }
    return [PSCustomObject]@{ Release=$Release; Sha256=$actualHash; ProvenancePath=$provenancePath }
}

function ConvertTo-NexusComparableName {
    param([Parameter(Mandatory)][string]$Name)
    return ([regex]::Replace($Name, '[^A-Za-z0-9]', '')).ToLowerInvariant()
}

function Test-NexusModFileName {
    param(
        [Parameter(Mandatory)][string]$CandidateName,
        [Parameter(Mandatory)][string]$ExpectedName
    )
    $candidate = ConvertTo-NexusComparableName -Name $CandidateName
    $expected = ConvertTo-NexusComparableName -Name $ExpectedName
    return $candidate -ceq $expected -or
        $candidate -ceq ($expected + 'serp') -or
        $candidate -match ('^' + [regex]::Escape($expected) + 'v\d+[a-z0-9]*$')
}

function Resolve-NexusModFile {
    param([Parameter(Mandatory)][object[]]$ModFiles, [Parameter(Mandatory)][string]$ExpectedName)
    # Only active file chains can be update targets. Archived companion and legacy
    # files must never become active again through this uploader.
    $activeFiles = @($ModFiles | Where-Object {
        $property = $_.PSObject.Properties['is_active']
        $null -ne $property -and [bool]$property.Value
    })
    # Existing active files use either the author's suffix or their current version
    # in the persistent file name (for example BugfixesAndQoL V1.0.69).
    $matches = @($activeFiles | Where-Object { Test-NexusModFileName -CandidateName ([string]$_.name) -ExpectedName $ExpectedName })
    if ($matches.Count -ne 1) {
        $candidates = @($activeFiles | ForEach-Object { "'$([string]$_.name)' (file_id $([string]$_.id))" }) -join ', '
        throw "Aktive Nexus-Main-Dateikette '$ExpectedName' ist nicht eindeutig zuordenbar. Aktive Dateien: $candidates"
    }
    return $matches[0]
}

function Resolve-NexusTargetFile {
    param(
        [Parameter(Mandatory)][object[]]$ModFiles,
        [Parameter(Mandatory)]$Target
    )
    $expectedName = [string]$Target.NexusFileName
    $matches = @($ModFiles | Where-Object { Test-NexusModFileName -CandidateName ([string]$_.name) -ExpectedName $expectedName })
    $activeMatches = @($matches | Where-Object {
        $activeProperty = $_.PSObject.Properties['is_active']
        $null -ne $activeProperty -and [bool]$activeProperty.Value
    })
    if ($activeMatches.Count -eq 1) {
        return [PSCustomObject]@{ Action='Existing'; ModFile=$activeMatches[0] }
    }
    if ($activeMatches.Count -gt 1) {
        throw "Mehrere aktive Nexus-Dateiketten passen zu '$expectedName'. Manuelle Pruefung erforderlich."
    }
    $inactiveMatches = @($matches | Where-Object { $_ -notin $activeMatches })
    if ($inactiveMatches.Count -gt 0) {
        $details = @($inactiveMatches | ForEach-Object { "'$([string]$_.name)' (file_id $([string]$_.id))" }) -join ', '
        throw "Fuer '$expectedName' existiert bereits eine inaktive oder archivierte Dateikette: $details. Manuelle Pruefung erforderlich."
    }
    $createProperty = $Target.PSObject.Properties['CreateIfMissing']
    if ($null -eq $createProperty -or -not [bool]$createProperty.Value) {
        throw "Die Nexus-Dateikette '$expectedName' fehlt und darf nicht automatisch erstellt werden."
    }
    return [PSCustomObject]@{ Action='Create'; ModFile=$null }
}

function New-NexusCreateModFileBody {
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)]$Release,
        [Parameter(Mandatory)][string]$ModId,
        [Parameter(Mandatory)][string]$UploadId
    )
    $categoryProperty = $Target.PSObject.Properties['FileCategory']
    $category = if ($null -eq $categoryProperty) { 'main' } else { [string]$categoryProperty.Value }
    if ($category -notin @('main','optional','miscellaneous')) { throw "Ungueltige Nexus-Dateikategorie '$category'." }
    $primaryProperty = $Target.PSObject.Properties['PrimaryModManagerDownload']
    $primary = $null -ne $primaryProperty -and [bool]$primaryProperty.Value
    $descriptionProperty = $Target.PSObject.Properties['NexusFileDescription']
    $description = if ($null -eq $descriptionProperty) { $null } else { [string]$descriptionProperty.Value }
    return @{
        upload_id=$UploadId
        mod_id=$ModId
        name=[string]$Target.NexusFileName
        version=[string]$Release.Version
        description=$description
        file_category=$category
        primary_mod_manager_download=$primary
        allow_mod_manager_download=$true
        show_requirements_pop_up=$false
        update_mod_version=$false
    }
}

function Test-NexusTargetPublishesChangelog {
    param([Parameter(Mandatory)]$Target)
    $property = $Target.PSObject.Properties['PublishChangelog']
    if ($null -ne $property) { return [bool]$property.Value }
    $artifactProperty = $Target.PSObject.Properties['Artifact']
    return $null -eq $artifactProperty -or [string]$artifactProperty.Value -cne 'Bundle'
}

function Get-NexusChangelogPublicationPlans {
    param([Parameter(Mandatory)][object[]]$Plans)
    $selected = [System.Collections.Generic.List[object]]::new()
    foreach ($group in @($Plans | Where-Object { Test-NexusTargetPublishesChangelog -Target $_.Target } |
        Group-Object { "$($_.ModId)|$($_.Release.Version)" })) {
        $entries = @($group.Group)
        $texts = @($entries | ForEach-Object { [string]$_.Changelog.Text } | Select-Object -Unique)
        if ($texts.Count -gt 1) {
            throw "Abweichende Changelogs fuer Nexus-Mod $($entries[0].ModId) v$($entries[0].Release.Version)."
        }
        $selected.Add($entries[0])
    }
    return @($selected)
}

function Get-NexusModFileVersionStateDiagnostic {
    param(
        [Parameter(Mandatory)]$ModFile,
        [Parameter(Mandatory)][object[]]$Versions,
        [Parameter(Mandatory)][string]$ExpectedName,
        [Parameter(Mandatory)][string]$ExpectedVersion,
        [Parameter(Mandatory)][string]$ExpectedCategory,
        [Nullable[bool]]$ExpectedPrimary,
        [string]$ExpectedFileId
    )
    if (-not (Test-NexusModFileName -CandidateName ([string]$ModFile.name) -ExpectedName $ExpectedName)) {
        return [PSCustomObject]@{ IsValid=$false; Diagnostic="Dateikettenname '$([string]$ModFile.name)' entspricht nicht '$ExpectedName'."; ActiveVersion=$null }
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedFileId) -and [string]$ModFile.id -cne $ExpectedFileId) {
        return [PSCustomObject]@{ IsValid=$false; Diagnostic="Dateiketten-ID '$([string]$ModFile.id)' entspricht nicht der Create-ID '$ExpectedFileId'."; ActiveVersion=$null }
    }
    try {
        $active = Get-NexusActiveVersion -Versions $Versions
    } catch {
        return [PSCustomObject]@{ IsValid=$false; Diagnostic="Kategorie oder aktiver Versionszustand ist ungueltig: $($_.Exception.Message)"; ActiveVersion=$null }
    }
    if ([string]$active.version -cne $ExpectedVersion) {
        return [PSCustomObject]@{ IsValid=$false; Diagnostic="Aktive Version '$([string]$active.version)' entspricht nicht '$ExpectedVersion'."; ActiveVersion=$active }
    }
    if ([string]$active.category -cne $ExpectedCategory) {
        return [PSCustomObject]@{ IsValid=$false; Diagnostic="Kategorie '$([string]$active.category)' entspricht nicht '$ExpectedCategory'."; ActiveVersion=$active }
    }
    if ($null -ne $ExpectedPrimary) {
        $primaryProperty = $active.PSObject.Properties['is_primary']
        if ($null -eq $primaryProperty) {
            return [PSCustomObject]@{ IsValid=$false; Diagnostic="Die aktive Version enthaelt keinen is_primary-Status."; ActiveVersion=$active }
        }
        if ([bool]$primaryProperty.Value -ne [bool]$ExpectedPrimary) {
            return [PSCustomObject]@{ IsValid=$false; Diagnostic="Primaerstatus '$([bool]$primaryProperty.Value)' entspricht nicht '$([bool]$ExpectedPrimary)'."; ActiveVersion=$active }
        }
    }
    return [PSCustomObject]@{ IsValid=$true; Diagnostic='Dateikettenzustand ist korrekt.'; ActiveVersion=$active }
}

function Test-NexusModFileVersionState {
    param(
        [Parameter(Mandatory)]$ModFile,
        [Parameter(Mandatory)][object[]]$Versions,
        [Parameter(Mandatory)][string]$ExpectedName,
        [Parameter(Mandatory)][string]$ExpectedVersion,
        [Parameter(Mandatory)][string]$ExpectedCategory,
        [Nullable[bool]]$ExpectedPrimary,
        [string]$ExpectedFileId
    )
    $result = Get-NexusModFileVersionStateDiagnostic @PSBoundParameters
    return [bool]$result.IsValid
}

function Wait-NexusCreatedFileVerification {
    param(
        [Parameter(Mandatory)][scriptblock]$Probe,
        [ValidateRange(1, 10000)][int]$MaxAttempts = 90,
        [ValidateRange(0, 60000)][int]$PollMilliseconds = 2000,
        [scriptblock]$OnPending
    )
    $lastResult = $null
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $lastResult = & $Probe
        if ($null -eq $lastResult -or $null -eq $lastResult.PSObject.Properties['Found']) {
            throw 'Nexus-Neuanlagenpruefung lieferte kein gueltiges Probe-Ergebnis.'
        }
        if ([bool]$lastResult.Found) { return $lastResult }
        if ($null -ne $OnPending) { & $OnPending $attempt $MaxAttempts ([string]$lastResult.Diagnostic) }
        if ($attempt -lt $MaxAttempts -and $PollMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $PollMilliseconds
        }
    }
    return $lastResult
}

function Get-NexusActiveVersion {
    param([Parameter(Mandatory)][object[]]$Versions)
    $active = @($Versions | Where-Object { [string]$_.category -ceq 'main' } | Sort-Object { [decimal]::Parse([string]$_.position, [Globalization.CultureInfo]::InvariantCulture) } -Descending)
    if ($active.Count -ne 1) { throw "Erwartet wurde genau eine aktive Main-Dateiversion, gefunden: $($active.Count)." }
    return $active[0]
}

function Get-NexusUpdateDecision {
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)]$Release, [Parameter(Mandatory)][object[]]$Versions)
    $active = Get-NexusActiveVersion -Versions $Versions
    $sameVersions = @($Versions | Where-Object { [string]$_.version -ceq $Release.Version })
    if ($sameVersions.Count -gt 0) {
        if ([string]$active.version -ceq $Release.Version) {
            return [PSCustomObject]@{ Action='Skip'; Reason='bereits aktuell'; Current=$active }
        }
        throw "Version $($Release.Version) existiert bereits, ist aber nicht aktiv. Manuelle Pruefung erforderlich."
    }
    if ($Target.AllowWrongTwoCorrection -and [string]$active.version -ceq '2.0.0') {
        return [PSCustomObject]@{ Action='Correct'; Reason='einmalige Korrektur von 2.0.0'; Current=$active }
    }
    $comparison = Compare-NexusSemanticVersion -Left $Release.Version -Right ([string]$active.version)
    if ($comparison -gt 0) { return [PSCustomObject]@{ Action='Update'; Reason='lokales Release ist neuer'; Current=$active } }
    return [PSCustomObject]@{ Action='Skip'; Reason='lokales Release ist nicht neuer'; Current=$active }
}

function Protect-NexusApiKey {
    param([Parameter(Mandatory)][string]$ApiKey, [Parameter(Mandatory)][string]$Path)
    $bytes = [Text.Encoding]::UTF8.GetBytes($ApiKey)
    $protected = [Security.Cryptography.ProtectedData]::Protect($bytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($Path, $protected)
}

function Unprotect-NexusApiKey {
    param([Parameter(Mandatory)][string]$Path)
    try {
        $protected = [IO.File]::ReadAllBytes($Path)
        $bytes = [Security.Cryptography.ProtectedData]::Unprotect($protected, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        return [Text.Encoding]::UTF8.GetString($bytes)
    } catch {
        throw 'Der gespeicherte API-Key konnte nicht entschluesselt werden. Update-NexusMods.bat -ResetApiKey ausfuehren.'
    }
}

function Read-NexusApiKey {
    param([Parameter(Mandatory)][string]$Path)
    if (Test-Path -LiteralPath $Path -PathType Leaf) { return Unprotect-NexusApiKey -Path $Path }
    Write-NexusLog 'Kein gespeicherter API-Key gefunden. Bitte den NEUEN, rotierten Key eingeben.' Yellow
    $secure = Read-Host 'Nexus Mods API-Key' -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $apiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'Der API-Key darf nicht leer sein.' }
    Protect-NexusApiKey -ApiKey $apiKey -Path $Path
    Write-NexusLog 'API-Key mit Windows DPAPI fuer dieses Benutzerkonto gespeichert.' Green
    return $apiKey
}

function New-NexusHeaders {
    param([Parameter(Mandatory)][string]$ApiKey)
    return @{ apikey=$ApiKey; Accept='application/json'; 'Application-Name'='Serpens66 SHCDE Nexus Uploader'; 'Application-Version'='1.0.0' }
}

function Invoke-NexusApi {
    param(
        [Parameter(Mandatory)][ValidateSet('Get','Post')][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][hashtable]$Headers,
        [AllowNull()]$Body
    )
    $uri = 'https://api.nexusmods.com/v3' + $Path
    try {
        if ($PSBoundParameters.ContainsKey('Body') -and $null -ne $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 8 -Compress)
        }
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers
    } catch {
        throw "Nexus API $Method $Path ist fehlgeschlagen: $($_.Exception.Message)"
    }
}

function Get-NexusPresignedSignedHeaders {
    param([Parameter(Mandatory)][string]$PresignedUrl)
    $uri = [Uri]$PresignedUrl
    foreach ($pair in @($uri.Query.TrimStart('?').Split('&'))) {
        if ([string]::IsNullOrWhiteSpace($pair)) { continue }
        $separator = $pair.IndexOf('=')
        $encodedName = if ($separator -ge 0) { $pair.Substring(0, $separator) } else { $pair }
        $name = [Uri]::UnescapeDataString($encodedName)
        if ($name -ine 'X-Amz-SignedHeaders') { continue }
        $value = if ($separator -ge 0) { [Uri]::UnescapeDataString($pair.Substring($separator + 1)) } else { '' }
        return @($value.Split(';') | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    throw 'Die Nexus-Upload-URL enthaelt keine X-Amz-SignedHeaders-Angabe.'
}

function New-NexusUploadContent {
    param(
        [Parameter(Mandatory)][byte[]]$Bytes,
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string[]]$SignedHeaders,
        [Parameter(Mandatory)][string]$ContentMd5Base64
    )
    Add-Type -AssemblyName System.Net.Http
    $content = [Net.Http.ByteArrayContent]::new($Bytes)
    $content.Headers.ContentMD5 = [Convert]::FromBase64String($ContentMd5Base64)
    if ('content-type' -in $SignedHeaders) {
        [void]$content.Headers.TryAddWithoutValidation('Content-Type', 'application/octet-stream')
    }
    if ('content-disposition' -in $SignedHeaders) {
        [void]$content.Headers.TryAddWithoutValidation('Content-Disposition', "attachment; filename=`"$FileName`"")
    }
    return $content
}

function Send-NexusUploadBytes {
    param(
        [Parameter(Mandatory)][string]$PresignedUrl,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string]$ContentMd5Base64
    )
    Add-Type -AssemblyName System.Net.Http
    $signedHeaders = @(Get-NexusPresignedSignedHeaders -PresignedUrl $PresignedUrl)
    $supportedHeaders = @('host','content-length','content-type','content-disposition','content-md5')
    $unsupportedHeaders = @($signedHeaders | Where-Object { $_ -notin $supportedHeaders })
    if ($unsupportedHeaders.Count -gt 0) { throw "Nicht unterstuetzte signierte Upload-Header: $($unsupportedHeaders -join ', ')" }

    $client = [Net.Http.HttpClient]::new()
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Put, $PresignedUrl)
    try {
        # ByteArrayContent guarantees Content-Length and avoids chunked transfer,
        # which signed S3-compatible PUT endpoints reject.
        $content = New-NexusUploadContent -Bytes ([IO.File]::ReadAllBytes($FilePath)) -FileName $FileName -SignedHeaders $signedHeaders -ContentMd5Base64 $ContentMd5Base64
        $request.Content = $content
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            $responseText = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $safeDetail = if ([string]::IsNullOrWhiteSpace($responseText)) { 'keine Serverdetails' } else { ([regex]::Replace($responseText, '<RequestId>.*?</RequestId>|<HostId>.*?</HostId>', '', [Text.RegularExpressions.RegexOptions]::Singleline)).Trim() }
            throw "Dateiupload wurde mit HTTP $([int]$response.StatusCode) abgewiesen: $safeDetail"
        }
    } finally {
        $request.Dispose()
        $client.Dispose()
    }
}

function Wait-NexusUploadAvailable {
    param([Parameter(Mandatory)][scriptblock]$GetState, [int]$TimeoutSeconds = 300, [int]$PollMilliseconds = 2000)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $nextLog = 0
    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $state = [string](& $GetState)
        if ($state -ceq 'available') { return }
        if ($watch.Elapsed.TotalSeconds -ge $nextLog) {
            Write-NexusLog "Nexus verarbeitet den Upload (Status: $state, $([int]$watch.Elapsed.TotalSeconds)s)." DarkGray
            $nextLog += 10
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    }
    throw "Upload wurde nicht innerhalb von $TimeoutSeconds Sekunden verfuegbar."
}
