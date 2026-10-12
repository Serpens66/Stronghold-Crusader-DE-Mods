param([ValidateSet('SerpsMods','APIShared')][string]$Target='SerpsMods',[int]$TestMode=0,[switch]$Validate)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SteamWorkshopHistory.ps1')
. (Join-Path $PSScriptRoot 'WorkshopChangelog.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$outputRoot = Join-Path $root '.release-output\SteamWorkshop'
if ($TestMode -eq 1) { $outputRoot = Join-Path $outputRoot 'test' }
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

function Get-UploadedModVersions {
    param([AllowNull()]$State)

    $versions = @{}
    if ($null -ne $State -and $null -ne $State.Mods) {
        foreach ($mod in @($State.Mods)) {
            $guid = [string]$mod.Guid
            if (-not [string]::IsNullOrWhiteSpace($guid)) {
                if ($versions.ContainsKey($guid) -or [string]$mod.Version -notmatch '^\d+\.\d+\.\d+$') {
                    throw "Invalid/duplicate mod in confirmed changelog state: $guid"
                }
                $versions[$guid] = [string]$mod.Version
            }
        }
    }
    return $versions
}

function Get-WorkshopChangeNote {
    param(
        [Parameter(Mandatory)][string]$ReleaseOutputRoot,
        [Parameter(Mandatory)][IO.FileInfo]$MapFile,
        [AllowNull()]$PreviousState,
        [string]$ProvenancePath='',
        [Parameter(Mandatory)][string]$PackageFolder
    )

    $packReleaseRoot = Join-Path $ReleaseOutputRoot 'SerpsMods'
    if (-not $ProvenancePath -and -not (Test-Path -LiteralPath $packReleaseRoot -PathType Container)) {
        Stop-Upload 2 "SerpsMods release output not found: $packReleaseRoot"
    }

    $mapHash = Get-Sha256 -Path $MapFile.FullName
    $matchingReleases = [Collections.Generic.List[object]]::new()
    $provenanceFiles = if($ProvenancePath){@($ProvenancePath)}else{@(
        Get-ChildItem -LiteralPath $packReleaseRoot -Directory -Filter 'v*' |
            ForEach-Object { Join-Path $_.FullName 'SerpsMods.provenance.json' } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    )}
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
    $previousVersions = Get-UploadedModVersions -State $PreviousState
    foreach ($mod in $activeMods) {
        $releaseTag = [string]$mod.ReleaseTag
        $releaseTagMatch = [regex]::Match($releaseTag, '^([A-Za-z0-9._-]+)/(?:test/)?v(\d+\.\d+\.\d+)(?:-[a-f0-9]{12})?$')
        if (-not $releaseTagMatch.Success) {
            Stop-Upload 2 "Invalid release tag for active mod '$($mod.Name)': '$releaseTag'"
        }

        $modVersion = [string]$mod.Version
        if ($releaseTagMatch.Groups[2].Value -cne $modVersion) {
            Stop-Upload 2 "Release tag/version mismatch for active mod '$($mod.Name)': '$releaseTag' versus '$modVersion'"
        }

        $modGuid = [string]$mod.Guid
        $previousVersion = if ($previousVersions.ContainsKey($modGuid)) {
            [string]$previousVersions[$modGuid]
        } else {
            ''
        }
        if ($previousVersion -ceq $modVersion) {
            Write-UploadLog "Changelog unchanged: $($mod.Name) v$modVersion"
            continue
        }

        $changes = Get-PayloadChangelogBody $PackageFolder $mod $previousVersion
        if ([string]::IsNullOrWhiteSpace($changes.Text)) { throw "Missing incremental changelog for $($mod.Name)." }
        $sections.Add("## $($mod.Name) v$modVersion`r`n`r`n$($changes.Text)")
        $sources.Add($changes.Path)
    }

    return [pscustomobject]@{
        Text = if ($sections.Count -gt 0) {
            $sections -join "`r`n`r`n"
        } else {
            'Workshop pack updated.'
        }
        Sources = $sources.ToArray()
        PackReleaseDirectory = $release.Directory
        PackVersion = [string]$release.Provenance.Version
        MapSha256 = $mapHash
        Mods = @($activeMods | ForEach-Object {
            [pscustomobject]@{
                Guid = [string]$_.Guid
                Name = [string]$_.Name
                Version = [string]$_.Version
            }
        })
    }
}

function Read-ChangelogState {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }
    try {
        return [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    } catch {
        Stop-Upload 2 "Workshop changelog state is unreadable: $Path ($($_.Exception.Message))"
    }
}

function Save-ChangelogState {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$ChangeNoteInfo,
        [Parameter(Mandatory)][string]$ItemId
    )

    $state = [ordered]@{
        SchemaVersion = 1
        ItemId = $ItemId
        PackVersion = $ChangeNoteInfo.PackVersion
        MapSha256 = $ChangeNoteInfo.MapSha256
        UploadedUtc = [DateTime]::UtcNow.ToString('o')
        Mods = @($ChangeNoteInfo.Mods)
    }
    if ($ChangeNoteInfo.PSObject.Properties['ContentFingerprint']) { $state.ContentFingerprint = $ChangeNoteInfo.ContentFingerprint }
    $directory = Split-Path -Parent $Path
    [void](New-Item -ItemType Directory -Path $directory -Force)
    [IO.File]::WriteAllText(
        $Path,
        (($state | ConvertTo-Json -Depth 5) + "`r`n"),
        [Text.UTF8Encoding]::new($false))
    Write-UploadLog "Workshop changelog state saved: $Path" 'OK'
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


try {
    . (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
    . (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
    $config=Get-WorkshopConfiguration;$test=$TestMode -eq 1;$targetConfig=$config.$Target
    $folder=Get-WorkshopFolder $config $targetConfig $test
    $proof=Assert-WorkshopPreparation $config $targetConfig $test
    $publisher=Join-Path $config.Root 'APIShared/.local/WorkshopUploader/WorkshopUploader.exe'
    if(-not(Test-Path -LiteralPath $publisher)){throw 'Build APIShared/tools/WorkshopUploader/build.bat first.'}
    Assert-WorkshopPublisherBuild (Join-Path $config.Root 'APIShared')
    $state=Get-WorkshopStateRoot $config $test
    $id=Get-WorkshopItemId $config $targetConfig $test
    $itemPath=Join-Path $state ('items/'+$config.AppId+'-'+$Target+'.item-id')
    $changelogPath=Join-Path $state ('items/'+$config.AppId+'-'+$Target+'.changelog-state.json')
    $previous=Read-ChangelogState $changelogPath
    if($previous -and $id -and $previous.ItemId -cne $id){throw 'Changelog state belongs to a different Workshop item.'}
    $map=Get-Item -LiteralPath (Join-Path $folder ($Target+'.map'))
    if($Target -eq 'SerpsMods') {
        $releaseOutput=if($test){Join-Path $config.Root '.release-output/SteamWorkshop/test'}else{Join-Path $config.Root '.release-output'}
        $historyPath=Join-Path $state ('items/'+$config.AppId+'-SerpsMods.upload-history.json')
        $seed=if($test){Join-Path $state 'SerpsMods.empty-seed.json'}else{Join-Path $PSScriptRoot 'SerpsMods.upload-history.seed.json'}
        $previous=Get-ConfirmedWorkshopChangelogState $previous $id $historyPath $seed $config.AppId $releaseOutput $proof.ReleaseProvenance
        $note=Get-WorkshopChangeNote -ReleaseOutputRoot $releaseOutput -MapFile $map -PreviousState $previous -ProvenancePath $proof.ReleaseProvenance -PackageFolder $folder
    } else {
        $changeText=[string]$proof.ReleaseNotes
        if(-not $changeText){$changeText='APIShared package updated.'}
        $note=[pscustomobject]@{Text=$changeText;PackVersion=$proof.Version;MapSha256=(Get-Sha256 $map.FullName);Mods=@();Sources=@();PackReleaseDirectory=$proof.ReleaseTag}
    }
    $run=Join-Path $state ('uploads/'+[guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($run)
    $requestPath=Join-Path $run 'request.json';$resultPath=Join-Path $run 'result.json'
    $forbidden=@(@($config.APIShared,$config.SerpsMods)|ForEach-Object {Get-WorkshopItemId $config $_ $false}|Where-Object {$_})
    $request=[ordered]@{SchemaVersion=1;Mode=$(if($test){'Test'}else{'Production'});PackageId=$targetConfig.Guid;Version=$proof.Version;
        SourceDirectory=$folder;StateRoot=(Join-Path $state 'publisher');PreviewFile=(Join-Path $folder 'preview.png');Title=$targetConfig.Title;Description=($targetConfig.Title+' / '+$proof.ReleaseTag);
        ChangeNote=$note.Text;Visibility=$(if($test){'Private'}else{$targetConfig.Visibility});WorkshopId=$id;PreservePresentation=(-not $test -and [bool]$id);ForbiddenItemIds=$forbidden;ExpectedFiles=@($proof.Files)}
    Write-SteamWorkshopJson $requestPath $request
    & $publisher --validate $requestPath $resultPath
    if($LASTEXITCODE -ne 0){throw 'Complete package validation failed.'}
    $checked=[IO.File]::ReadAllText($resultPath)|ConvertFrom-Json
    $request.Fingerprint=$checked.Fingerprint;Write-SteamWorkshopJson $requestPath $request
    $note | Add-Member -NotePropertyName ContentFingerprint -NotePropertyValue $checked.Fingerprint
    [IO.File]::WriteAllText((Join-Path $run 'changelog.txt'), $note.Text + "`r`n", [Text.UTF8Encoding]::new($false))
    Write-UploadLog ('CHANGELOG: '+$note.Text)
    if($Validate){Write-UploadLog 'Complete package validated without Steam initialization or upload.' 'OK';exit 0}
    & $publisher --inspect $requestPath $resultPath
    if($LASTEXITCODE -ne 0){throw 'Steam item/prerequisite validation failed; no upload started.'}
    Write-UploadLog "Target=$Target; mode=$($request.Mode); version=$($proof.Version); item=$id; files=$(@($proof.Files).Count); fingerprint=$($request.Fingerprint)"
    if((Read-Host 'Type HOCHLADEN to submit this reviewed snapshot') -cne 'HOCHLADEN'){throw 'Upload cancelled.'}
    & $publisher --publish $requestPath $resultPath
    $publisherExit=$LASTEXITCODE
    $result=[IO.File]::ReadAllText($resultPath)|ConvertFrom-Json
    # The core also persists IDs at CreateItem completion, including after interruption.
    if($result.WorkshopId -match '^[1-9][0-9]*$'){Save-ItemId $itemPath $result.WorkshopId}
    if($publisherExit -ne 0 -or $result.Status -cne 'Succeeded'){throw ('Publisher failed: '+$result.Diagnostic)}
    $historyPath=Join-Path $state ('items/'+$config.AppId+'-'+$Target+'.upload-history.json')
    $seed=Join-Path $PSScriptRoot ($Target+'.upload-history.seed.json')
    if($test){$seed=Join-Path $state ($Target+'.empty-seed.json');if(-not(Test-Path -LiteralPath $seed)){Write-SteamWorkshopJson $seed ([ordered]@{SchemaVersion=1;AppId=$config.AppId;StateName=$Target;Uploads=@()})}}
    $null=Add-SteamWorkshopUploadHistoryEntry -Path $historyPath -SeedPath $seed -AppId $config.AppId -StateName $Target -ItemId $result.WorkshopId -PackVersion $proof.Version -MapSha256 $note.MapSha256 -UploadedUtc ([DateTimeOffset]::UtcNow) -Format $(if($Target -eq 'APIShared'){'Map'}else{'Loose'}) -ContentFingerprint $result.Fingerprint
    Save-ChangelogState -Path $changelogPath -ChangeNoteInfo $note -ItemId $result.WorkshopId
    Write-UploadLog "Steam Workshop upload completed successfully. Item ID: $($result.WorkshopId)" 'OK'
    exit 0
} catch {Write-UploadLog $_.Exception.ToString() 'ERROR';exit 1}
