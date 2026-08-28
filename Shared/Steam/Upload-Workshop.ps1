param(
    [Parameter(Mandatory)][string]$UploadFolder,
    [string]$ItemName,
    [string]$ConfiguredItemId,
    [Parameter(Mandatory)][string]$Visibility,
    [Parameter(Mandatory)][uint32]$AppId,
    [Parameter(Mandatory)][string]$ToolPath,
    [switch]$ContentOnlyUpdate,
    [switch]$Validate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputRoot = Join-Path $root '.release-output\SteamWorkshop'
$logDirectory = Join-Path $outputRoot 'logs'
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$logPath = Join-Path $logDirectory "$runId.log"
[void](New-Item -ItemType Directory -Path $logDirectory -Force)

function Write-UploadLog {
    param(
        [Parameter(Mandatory)][string]$Message,
        [ValidateSet('INFO','WARN','ERROR','OK')][string]$Level = 'INFO'
    )
    $line = '[{0}] [{1}] {2}' -f `
        ([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss.fff')), $Level, $Message
    [IO.File]::AppendAllText($logPath, $line + "`r`n", [Text.UTF8Encoding]::new($false))
    $color = switch ($Level) {
        'ERROR' { 'Red' }
        'WARN' { 'Yellow' }
        'OK' { 'Green' }
        default { 'Gray' }
    }
    Write-Host $line -ForegroundColor $color
}

function Stop-Upload {
    param([Parameter(Mandatory)][int]$Code, [Parameter(Mandatory)][string]$Message)
    Write-UploadLog -Message $Message -Level 'ERROR'
    Write-Host "Log: $logPath"
    exit $Code
}

function Remove-AnsiSequences {
    param([AllowEmptyString()][string]$Text)
    $escapeSequencePattern = [string][char]27 + '\[[0-?]*[ -/]*[@-~]'
    return [regex]::Replace($Text, $escapeSequencePattern, '')
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    } finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Get-WorkshopChangeNote {
    param(
        [Parameter(Mandatory)][string]$ReleaseOutputRoot,
        [Parameter(Mandatory)][IO.FileInfo]$MapFile
    )

    $packReleaseRoot = Join-Path $ReleaseOutputRoot 'SerpsMods'
    if (-not (Test-Path -LiteralPath $packReleaseRoot -PathType Container)) {
        Stop-Upload 2 "SerpsMods release output not found: $packReleaseRoot"
    }

    $mapHash = Get-Sha256 -Path $MapFile.FullName
    $matchingReleases = [Collections.Generic.List[object]]::new()
    $provenanceFiles = @(
        Get-ChildItem -LiteralPath $packReleaseRoot -Directory -Filter 'v*' |
            ForEach-Object { Join-Path $_.FullName 'SerpsMods.provenance.json' } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    )
    foreach ($provenancePath in $provenanceFiles) {
        try {
            $provenance = [IO.File]::ReadAllText($provenancePath) | ConvertFrom-Json
            if ([string]$provenance.Map.Sha256 -ieq $mapHash) {
                $matchingReleases.Add([pscustomobject]@{
                    Directory = Split-Path -Parent $provenancePath
                    Path = $provenancePath
                    Provenance = $provenance
                })
            }
        } catch {
            Write-UploadLog "Ignoring unreadable SerpsMods provenance candidate ${provenancePath}: $($_.Exception.Message)" 'WARN'
        }
    }
    if ($matchingReleases.Count -ne 1) {
        Stop-Upload 2 "Expected exactly one SerpsMods release matching map SHA-256 $mapHash; found $($matchingReleases.Count)."
    }

    $release = $matchingReleases[0]
    $activeMods = @($release.Provenance.Mods | Where-Object { [string]$_.State -ceq 'Active' })
    if ($activeMods.Count -eq 0) {
        Stop-Upload 2 "No active mods were found in Workshop pack provenance: $($release.Path)"
    }

    $sections = [Collections.Generic.List[string]]::new()
    $sources = [Collections.Generic.List[string]]::new()
    foreach ($mod in $activeMods) {
        $releaseTag = [string]$mod.ReleaseTag
        $releaseTagMatch = [regex]::Match($releaseTag, '^([A-Za-z0-9._-]+)/v([A-Za-z0-9._-]+)$')
        if (-not $releaseTagMatch.Success) {
            Stop-Upload 2 "Invalid release tag for active mod '$($mod.Name)': '$releaseTag'"
        }

        $modVersion = [string]$mod.Version
        if ($releaseTagMatch.Groups[2].Value -cne $modVersion) {
            Stop-Upload 2 "Release tag/version mismatch for active mod '$($mod.Name)': '$releaseTag' versus '$modVersion'"
        }

        $modReleaseRoot = Join-Path $ReleaseOutputRoot $releaseTagMatch.Groups[1].Value
        $notesPath = Join-Path (Join-Path $modReleaseRoot "v$modVersion") 'release-notes.md'
        if (-not (Test-Path -LiteralPath $notesPath -PathType Leaf)) {
            Stop-Upload 2 "Release notes not found for active mod '$($mod.Name)' v${modVersion}: $notesPath"
        }

        $notesText = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $notesPath).Path)
        $changesMatch = [regex]::Match(
            $notesText,
            '(?ms)^##[ \t]+Changes[ \t]*\r?\n(?<body>.*?)(?=^##[ \t]+|\z)')
        if (-not $changesMatch.Success -or [string]::IsNullOrWhiteSpace($changesMatch.Groups['body'].Value)) {
            Stop-Upload 2 "A non-empty '## Changes' section was not found for active mod '$($mod.Name)' v${modVersion}: $notesPath"
        }

        $body = $changesMatch.Groups['body'].Value.Trim()
        $body = [regex]::Replace($body, "`r`n|`r|`n", "`r`n")
        $sections.Add("## $($mod.Name) v$modVersion`r`n`r`n$body")
        $sources.Add((Resolve-Path -LiteralPath $notesPath).Path)
    }

    return [pscustomobject]@{
        Text = $sections -join "`r`n`r`n"
        Sources = $sources.ToArray()
        PackReleaseDirectory = $release.Directory
        MapSha256 = $mapHash
    }
}

function Save-ItemId {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$ItemId)
    $directory = Split-Path -Parent $Path
    [void](New-Item -ItemType Directory -Path $directory -Force)
    [IO.File]::WriteAllText(
        $Path,
        $ItemId + "`r`n",
        [Text.UTF8Encoding]::new($false))
    $saved = [IO.File]::ReadAllText($Path).Trim()
    if ($saved -cne $ItemId) {
        Stop-Upload 6 "Workshop item ID could not be persisted correctly: $Path"
    }
    Write-UploadLog -Message "Workshop item ID saved: $ItemId ($Path)" -Level 'OK'
}

function Get-SteamWorkshopItemState {
    param(
        [Parameter(Mandatory)][string]$ItemId,
        [Parameter(Mandatory)][uint32]$ExpectedAppId
    )

    $endpoint = 'https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/'
    try {
        $result = Invoke-RestMethod -Method Post -Uri $endpoint -Body @{
            itemcount = '1'
            'publishedfileids[0]' = $ItemId
        } -TimeoutSec 30
    } catch {
        Stop-Upload 2 "Steam Workshop item $ItemId could not be verified. No replacement item will be created after an inconclusive network/API failure: $($_.Exception.Message)"
    }

    if ($null -eq $result -or $null -eq $result.response -or
        [int]$result.response.result -ne 1 -or
        [int]$result.response.resultcount -ne 1) {
        Stop-Upload 2 "Steam returned an unexpected response while verifying Workshop item $ItemId. No replacement item will be created."
    }
    $details = @($result.response.publishedfiledetails)
    if ($details.Count -ne 1 -or [string]$details[0].publishedfileid -cne $ItemId) {
        Stop-Upload 2 "Steam did not return the requested Workshop item identity $ItemId. No replacement item will be created."
    }

    $itemResult = [int]$details[0].result
    if ($itemResult -eq 9) {
        return 'Missing'
    }
    if ($itemResult -ne 1) {
        Stop-Upload 2 "Steam returned result $itemResult while verifying Workshop item $ItemId. Only result 9 (FileNotFound) permits offering a replacement upload."
    }

    $consumerAppProperty = $details[0].PSObject.Properties['consumer_app_id']
    if ($null -ne $consumerAppProperty -and [uint32]$consumerAppProperty.Value -ne $ExpectedAppId) {
        Stop-Upload 2 "Workshop item $ItemId belongs to app $($consumerAppProperty.Value), not configured app $ExpectedAppId."
    }
    return 'Exists'
}

try {
    if (-not (Test-Path -LiteralPath $ToolPath -PathType Leaf)) {
        Stop-Upload 2 "Upload tool not found: $ToolPath"
    }
    if (-not (Test-Path -LiteralPath $UploadFolder -PathType Container)) {
        Stop-Upload 2 "Upload folder not found: $UploadFolder"
    }

    $resolvedFolder = (Resolve-Path -LiteralPath $UploadFolder).Path
    $previewPath = Join-Path $resolvedFolder 'preview.png'
    if (-not (Test-Path -LiteralPath $previewPath -PathType Leaf)) {
        Stop-Upload 2 "Preview image not found: $previewPath"
    }
    $mapFiles = @(Get-ChildItem -LiteralPath $resolvedFolder -File -Filter '*.map')
    if ($mapFiles.Count -ne 1) {
        Stop-Upload 2 "Upload folder must contain exactly one .map file; found $($mapFiles.Count): $resolvedFolder"
    }
    if ((Get-Item -LiteralPath $previewPath).Length -eq 0 -or $mapFiles[0].Length -eq 0) {
        Stop-Upload 2 'Preview image and map file must not be empty.'
    }
    if ($Visibility -notin @('Public','Private','FriendsOnly','Unlisted')) {
        Stop-Upload 2 "Unsupported visibility: $Visibility"
    }
    if ([string]::IsNullOrWhiteSpace($ItemName)) {
        $ItemName = Split-Path -Leaf $resolvedFolder
    }

    $changeNoteInfo = Get-WorkshopChangeNote `
        -ReleaseOutputRoot (Join-Path $root '.release-output') `
        -MapFile $mapFiles[0]

    $safeFolderName = ([regex]::Replace((Split-Path -Leaf $resolvedFolder), '[^A-Za-z0-9._-]', '_')).Trim('_')
    if ([string]::IsNullOrWhiteSpace($safeFolderName)) { $safeFolderName = 'workshop-item' }
    $itemIdPath = Join-Path $outputRoot "items\$AppId-$safeFolderName.item-id"
    $itemId = $ConfiguredItemId.Trim()
    $itemIdSource = 'configuration'
    if ([string]::IsNullOrWhiteSpace($itemId) -and (Test-Path -LiteralPath $itemIdPath -PathType Leaf)) {
        $itemId = [IO.File]::ReadAllText($itemIdPath).Trim()
        $itemIdSource = 'saved state'
    }
    if (-not [string]::IsNullOrWhiteSpace($itemId)) {
        $parsedItemId = [uint64]0
        if (-not [uint64]::TryParse($itemId, [ref]$parsedItemId) -or $parsedItemId -eq 0) {
            Stop-Upload 2 "Invalid Workshop item ID from ${itemIdSource}: '$itemId'"
        }
        $itemId = $parsedItemId.ToString()
    }

    $isUpdate = -not [string]::IsNullOrWhiteSpace($itemId)
    $uploadConfirmed = $false
    $missingSavedItem = $false
    if ($isUpdate) {
        $itemState = Get-SteamWorkshopItemState -ItemId $itemId -ExpectedAppId $AppId
        if ($itemState -eq 'Missing') {
            if ($itemIdSource -ne 'saved state') {
                Stop-Upload 2 "Configured Workshop item $itemId no longer exists. Clear ITEM_ID in UploadWorkshop.bat before creating a replacement."
            }
            $missingSavedItem = $true
            Write-UploadLog "Saved Workshop item $itemId no longer exists (Steam result 9: FileNotFound)." 'WARN'
            if (-not $Validate) {
                $replacementConfirmation = Read-Host 'Type NEU_HOCHLADEN to create a new Workshop item and replace the saved ID'
                if ($replacementConfirmation -cne 'NEU_HOCHLADEN') {
                    Stop-Upload 3 'Replacement Workshop upload cancelled. The previous saved ID was preserved.'
                }
                $itemId = ''
                $isUpdate = $false
                $uploadConfirmed = $true
                Write-UploadLog 'Replacement upload confirmed. Steam will be asked to create a new item.' 'WARN'
            }
        }
    }
    $mode = if ($missingSavedItem -and $Validate) {
        'SAVED ITEM MISSING; REPLACEMENT CONFIRMATION REQUIRED'
    } elseif ($isUpdate) {
        "UPDATE item $itemId ($itemIdSource)"
    } else {
        'CREATE NEW ITEM'
    }
    Write-UploadLog "Mode: $mode"
    Write-UploadLog "App ID: $AppId"
    Write-UploadLog "Source: $resolvedFolder"
    Write-UploadLog "Map: $($mapFiles[0].Name)"
    if ($isUpdate -and $ContentOnlyUpdate) {
        Write-UploadLog 'Workshop metadata: preserved (only content and changelog will be submitted).'
    } else {
        Write-UploadLog "Title: $ItemName"
        Write-UploadLog "Preview: $previewPath"
        Write-UploadLog "Visibility: $Visibility"
    }
    Write-UploadLog "Changelog pack release: $($changeNoteInfo.PackReleaseDirectory)"
    Write-UploadLog "Changelog map SHA-256: $($changeNoteInfo.MapSha256)"
    foreach ($source in $changeNoteInfo.Sources) {
        Write-UploadLog "Changelog source: $source"
    }
    Write-UploadLog "Combined changelog characters: $($changeNoteInfo.Text.Length)"
    Write-UploadLog 'Combined changelog follows:'
    foreach ($changeLogLine in [regex]::Split($changeNoteInfo.Text, "`r`n|`r|`n")) {
        Write-UploadLog "CHANGELOG: $changeLogLine"
    }
    if (-not $isUpdate) {
        if ($missingSavedItem) {
            Write-UploadLog 'The deleted saved item will be replaced with a newly created Workshop item.' 'WARN'
        } else {
            Write-UploadLog 'No saved item ID was found. Continuing will create a new Workshop item.' 'WARN'
        }
    }
    if ($Validate) {
        if ($missingSavedItem) {
            Write-UploadLog 'Validation confirmed that the saved item is missing. A real run will require NEU_HOCHLADEN.' 'WARN'
        }
        Write-UploadLog 'Validation completed without invoking the upload tool or uploading files.' 'OK'
        Write-Host "Log: $logPath"
        exit 0
    }

    if (-not $uploadConfirmed) {
        $confirmation = Read-Host 'Type HOCHLADEN to perform this Steam Workshop upload'
        if ($confirmation -cne 'HOCHLADEN') {
            Stop-Upload 3 'Upload cancelled.'
        }
    }

    $arguments = @('-v', '-a', $AppId.ToString())
    if ($isUpdate) {
        $arguments += @('-i', $itemId, '-u', '-c', $changeNoteInfo.Text)
        if (-not $ContentOnlyUpdate) {
            $arguments += @('-k', $ItemName, '-o', $previewPath, '-h', $Visibility)
        }
    } else {
        $arguments += @('-n', '-k', $ItemName, '-o', $previewPath, '-h', $Visibility)
    }
    $arguments += @('-s', $resolvedFolder)

    Write-UploadLog "Starting pdengine.steamugc.tool $mode."
    $toolOutput = [Collections.Generic.List[string]]::new()
    $previousErrorActionPreference = $ErrorActionPreference
    Push-Location (Split-Path -Parent $ToolPath)
    try {
        # Native stderr is diagnostic output; success is checked below using both
        # the exit code and the Steam callback result printed by this tool.
        $ErrorActionPreference = 'Continue'
        & $ToolPath @arguments 2>&1 | ForEach-Object {
            $line = [string]$_
            $toolOutput.Add($line)
            Write-UploadLog ("TOOL: " + (Remove-AnsiSequences $line))
        }
        $toolExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Pop-Location
    }

    $cleanOutput = (($toolOutput | ForEach-Object { Remove-AnsiSequences $_ }) -join "`n")
    $created = $cleanOutput -match 'Workshop Item:\s*Created'
    $updated = $cleanOutput -match 'Workshop Item:\s*Updated'
    $idMatch = [regex]::Match($cleanOutput, 'Updating Item:\s*([0-9]+)')

    # Persist a newly created ID even if its immediately following content update
    # fails, otherwise the next run could accidentally create a duplicate item.
    if (-not $isUpdate -and $created -and $idMatch.Success) {
        $itemId = $idMatch.Groups[1].Value
        Save-ItemId -Path $itemIdPath -ItemId $itemId
    }

    if ($toolExitCode -ne 0) {
        Stop-Upload 4 "Upload tool exited with code $toolExitCode."
    }
    if (-not $updated) {
        if ($created -and -not $idMatch.Success) {
            Stop-Upload 6 'Steam created an item, but its ID was not present in the tool output. Find the newest item in your Steam Workshop profile and set ITEM_ID in UploadWorkshop.bat before retrying.'
        }
        Stop-Upload 5 'Steam did not confirm a successful Workshop item update. Review the TOOL lines above.'
    }
    if (-not $isUpdate -and -not $created) {
        Stop-Upload 5 'Steam updated content but did not confirm creation of the expected new Workshop item.'
    }
    if ($isUpdate -and $ConfiguredItemId.Trim()) {
        Save-ItemId -Path $itemIdPath -ItemId $itemId
    }

    Write-UploadLog "Steam Workshop upload completed successfully. Item ID: $itemId" 'OK'
    Write-Host "Log: $logPath"
    exit 0
} catch {
    Write-UploadLog -Message $_.Exception.ToString() -Level 'ERROR'
    Write-Host "Log: $logPath"
    exit 1
}
