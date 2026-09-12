Set-StrictMode -Version Latest

function Write-SteamWorkshopJson {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Value)

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [void](New-Item -ItemType Directory -Path $directory -Force)
    }
    $text = (($Value | ConvertTo-Json -Depth 20) -replace "`r?`n", "`r`n").TrimEnd("`r", "`n") + "`r`n"
    [IO.File]::WriteAllText($Path, $text, [Text.UTF8Encoding]::new($false))
    if (-not [string]::Equals([IO.File]::ReadAllText($Path), $text, [StringComparison]::Ordinal)) {
        throw "Workshop upload history write verification failed: $Path"
    }
}

function Assert-SteamWorkshopUploadHistory {
    param(
        [Parameter(Mandatory)]$History,
        [Parameter(Mandatory)][uint32]$AppId,
        [Parameter(Mandatory)][string]$StateName
    )

    if ([int]$History.SchemaVersion -ne 1 -or [uint32]$History.AppId -ne $AppId -or
        [string]$History.StateName -cne $StateName) {
        throw "Workshop upload history identity/schema mismatch for $AppId-$StateName."
    }
    $versions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($upload in @($History.Uploads)) {
        $version = [string]$upload.PackVersion
        $hash = [string]$upload.MapSha256
        $itemId = [string]$upload.ItemId
        $timestampValid = $true
        try { [void][DateTimeOffset]$upload.UploadedUtc } catch { $timestampValid = $false }
        if ($version -notmatch '^\d+\.\d+\.\d+$' -or $hash -notmatch '^[0-9a-f]{64}$' -or
            $itemId -notmatch '^[1-9][0-9]*$' -or
            -not $timestampValid) {
            throw "Invalid Workshop upload-history entry for pack '$version'."
        }
        if (-not $versions.Add($version)) {
            throw "Duplicate Workshop upload-history version: $version"
        }
    }
    return $History
}

function Get-SteamWorkshopUploadHistory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$SeedPath,
        [Parameter(Mandatory)][uint32]$AppId,
        [Parameter(Mandatory)][string]$StateName
    )

    if (-not (Test-Path -LiteralPath $SeedPath -PathType Leaf)) {
        throw "Workshop upload-history seed is missing: $SeedPath"
    }
    try {
        $seed = [IO.File]::ReadAllText($SeedPath) | ConvertFrom-Json
        [void](Assert-SteamWorkshopUploadHistory -History $seed -AppId $AppId -StateName $StateName)
    } catch {
        throw "Workshop upload-history seed is unreadable or invalid: $SeedPath ($($_.Exception.Message))"
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Write-SteamWorkshopJson -Path $Path -Value $seed
    }
    try {
        $history = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    } catch {
        throw "Workshop upload history is unreadable: $Path ($($_.Exception.Message))"
    }
    $history = Assert-SteamWorkshopUploadHistory -History $history -AppId $AppId -StateName $StateName
    foreach ($baseline in @($seed.Uploads)) {
        $matches = @($history.Uploads | Where-Object {
            [string]$_.PackVersion -ceq [string]$baseline.PackVersion -and
            [string]$_.ItemId -ceq [string]$baseline.ItemId -and
            [string]$_.MapSha256 -ceq [string]$baseline.MapSha256
        })
        if ($matches.Count -ne 1) {
            throw "Workshop upload history does not contain the confirmed baseline entry for pack $($baseline.PackVersion)."
        }
    }
    return $history
}

function Add-SteamWorkshopUploadHistoryEntry {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$SeedPath,
        [Parameter(Mandatory)][uint32]$AppId,
        [Parameter(Mandatory)][string]$StateName,
        [Parameter(Mandatory)][string]$ItemId,
        [Parameter(Mandatory)][string]$PackVersion,
        [Parameter(Mandatory)][string]$MapSha256,
        [Parameter(Mandatory)][DateTimeOffset]$UploadedUtc
    )

    $history = Get-SteamWorkshopUploadHistory -Path $Path -SeedPath $SeedPath -AppId $AppId -StateName $StateName
    $existing = @($history.Uploads | Where-Object { [string]$_.PackVersion -ceq $PackVersion })
    if ($existing.Count -gt 0) {
        if ($existing.Count -eq 1 -and [string]$existing[0].ItemId -ceq $ItemId -and
            [string]$existing[0].MapSha256 -ceq $MapSha256.ToLowerInvariant()) {
            return $false
        }
        throw "Workshop upload history already contains a conflicting entry for pack $PackVersion."
    }
    $entry = [pscustomobject][ordered]@{
        ItemId = $ItemId
        PackVersion = $PackVersion
        MapSha256 = $MapSha256.ToLowerInvariant()
        UploadedUtc = $UploadedUtc.UtcDateTime.ToString('o')
    }
    $history.Uploads = @($history.Uploads) + $entry
    Write-SteamWorkshopJson -Path $Path -Value $history
    return $true
}
