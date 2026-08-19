param(
    [Parameter(Mandatory)][string]$UploadFolder,
    [string]$ItemName,
    [string]$ConfiguredItemId,
    [Parameter(Mandatory)][string]$Visibility,
    [Parameter(Mandatory)][uint32]$AppId,
    [Parameter(Mandatory)][string]$ToolPath,
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
    $mode = if ($isUpdate) { "UPDATE item $itemId ($itemIdSource)" } else { 'CREATE NEW ITEM' }
    Write-UploadLog "Mode: $mode"
    Write-UploadLog "App ID: $AppId"
    Write-UploadLog "Title: $ItemName"
    Write-UploadLog "Source: $resolvedFolder"
    Write-UploadLog "Map: $($mapFiles[0].Name)"
    Write-UploadLog "Preview: $previewPath"
    Write-UploadLog "Visibility: $Visibility"
    if (-not $isUpdate) {
        Write-UploadLog 'No saved item ID was found. Continuing will create a new Workshop item.' 'WARN'
    }
    if ($Validate) {
        Write-UploadLog 'Validation completed without contacting Steam or uploading files.' 'OK'
        Write-Host "Log: $logPath"
        exit 0
    }

    $confirmation = Read-Host 'Type HOCHLADEN to perform this Steam Workshop upload'
    if ($confirmation -cne 'HOCHLADEN') {
        Stop-Upload 3 'Upload cancelled.'
    }

    $arguments = @('-v', '-a', $AppId.ToString())
    if ($isUpdate) {
        $arguments += @('-i', $itemId, '-u')
    } else {
        $arguments += '-n'
    }
    $arguments += @(
        '-k', $ItemName,
        '-s', $resolvedFolder,
        '-o', $previewPath,
        '-h', $Visibility)

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
