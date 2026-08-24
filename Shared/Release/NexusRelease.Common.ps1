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

function Get-LatestNexusLocalRelease {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$ModName)
    $modOutput = Join-Path (Join-Path $Root '.release-output') $ModName
    if (-not (Test-Path -LiteralPath $modOutput -PathType Container)) { throw "Kein Release-Ordner fuer ${ModName}: $modOutput" }
    $candidates = [System.Collections.Generic.List[object]]::new()
    foreach ($directory in @(Get-ChildItem -LiteralPath $modOutput -Directory)) {
        if ($directory.Name -notmatch '^v(.+)$') { continue }
        $version = $Matches[1]
        if ($version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') { continue }
        $zipName = "$ModName-v$version.zip"
        $zipPath = Join-Path $directory.FullName $zipName
        if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
            $candidates.Add([PSCustomObject]@{ ModName=$ModName; Version=$version; Directory=$directory.FullName; ZipName=$zipName; ZipPath=$zipPath })
        }
    }
    if ($candidates.Count -eq 0) { throw "Kein gueltiges lokales Release-ZIP fuer $ModName gefunden." }
    $latest = $candidates[0]
    for ($index = 1; $index -lt $candidates.Count; $index++) {
        if ((Compare-NexusSemanticVersion -Left $candidates[$index].Version -Right $latest.Version) -gt 0) { $latest = $candidates[$index] }
    }
    return $latest
}

function Test-NexusLocalRelease {
    param([Parameter(Mandatory)]$Release)
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
    if ([string]$provenance.Package.File -cne $Release.ZipName -or ([string]$provenance.Package.Sha256).ToLowerInvariant() -cne $actualHash) { throw "Provenance-Paketdaten stimmen nicht: $provenancePath" }

    $auditRoot = Join-Path ([IO.Path]::GetTempPath()) ("shcde-nexus-audit-" + [Guid]::NewGuid().ToString('N'))
    try {
        Expand-Archive -LiteralPath $Release.ZipPath -DestinationPath $auditRoot
        $infos = @(Get-ChildItem -LiteralPath $auditRoot -Filter info.json -File -Recurse)
        if ($infos.Count -ne 1) { throw "Release-ZIP muss genau ein info.json enthalten, gefunden: $($infos.Count)" }
        $manifest = Get-Content -LiteralPath $infos[0].FullName -Raw | ConvertFrom-Json
        if ([string]$manifest.Version -cne $Release.Version) { throw "info.json-Version stimmt nicht mit dem Release ueberein: $($Release.ZipName)" }
    } finally {
        if (Test-Path -LiteralPath $auditRoot) { Remove-Item -LiteralPath $auditRoot -Recurse -Force }
    }
    return [PSCustomObject]@{ Release=$Release; Sha256=$actualHash; ProvenancePath=$provenancePath }
}

function ConvertTo-NexusComparableName {
    param([Parameter(Mandatory)][string]$Name)
    return ([regex]::Replace($Name, '[^A-Za-z0-9]', '')).ToLowerInvariant()
}

function Resolve-NexusModFile {
    param([Parameter(Mandatory)][object[]]$ModFiles, [Parameter(Mandatory)][string]$ExpectedName)
    $expected = ConvertTo-NexusComparableName -Name $ExpectedName
    # Only active file chains can be update targets. Archived companion and legacy
    # files must never become active again through this uploader.
    $activeFiles = @($ModFiles | Where-Object {
        $property = $_.PSObject.Properties['is_active']
        $null -ne $property -and [bool]$property.Value
    })
    # Existing active files use either the author's suffix or their current version
    # in the persistent file name (for example BugfixesAndQoL V1.0.69).
    $matches = @($activeFiles | Where-Object {
        $candidate = ConvertTo-NexusComparableName -Name ([string]$_.name)
        $candidate -ceq $expected -or
        $candidate -ceq ($expected + 'serp') -or
        $candidate -match ('^' + [regex]::Escape($expected) + 'v\d+[a-z0-9]*$')
    })
    if ($matches.Count -ne 1) {
        $candidates = @($activeFiles | ForEach-Object { "'$([string]$_.name)' (file_id $([string]$_.id))" }) -join ', '
        throw "Aktive Nexus-Main-Dateikette '$ExpectedName' ist nicht eindeutig zuordenbar. Aktive Dateien: $candidates"
    }
    return $matches[0]
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

function Send-NexusUploadBytes {
    param([Parameter(Mandatory)][string]$PresignedUrl, [Parameter(Mandatory)][string]$FilePath, [Parameter(Mandatory)][string]$FileName)
    Add-Type -AssemblyName System.Net.Http
    $client = [Net.Http.HttpClient]::new()
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Put, $PresignedUrl)
    $stream = [IO.File]::OpenRead($FilePath)
    try {
        $content = [Net.Http.StreamContent]::new($stream)
        [void]$content.Headers.TryAddWithoutValidation('Content-Disposition', "attachment; filename=`"$FileName`"")
        $request.Content = $content
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) { throw "Dateiupload wurde mit HTTP $([int]$response.StatusCode) abgewiesen." }
    } finally {
        $stream.Dispose()
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
