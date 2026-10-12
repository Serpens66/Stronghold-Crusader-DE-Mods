# Validators must verify the requested release identity or the expected content hash.
# Downloading never replaces a cached file until the candidate passes that validator.
function Get-VerifiedReleaseAsset {
    param([string]$Cache, [string]$Name, [scriptblock]$Validate, [scriptblock]$Download,
          [string[]]$LocalCandidates = @())
    if ([IO.Path]::GetFileName($Name) -cne $Name -or $Name -match '[*?]' -or -not $Name) {
        throw "Unsafe release asset name: $Name"
    }
    $path = Join-Path $Cache $Name
    Assert-ApiWorkshopUnlinkedPath $path
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $valid = $false
        try { $valid = [bool](& $Validate $path) } catch { Write-Verbose "Invalid cache ${path}: $($_.Exception.Message)" }
        if ($valid) { Write-Host "Using verified cached asset: $path"; return $path }
    }
    [void][IO.Directory]::CreateDirectory($Cache)
    $stage = Join-Path $Cache ('.download-' + [guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($stage)
    $candidate = Join-Path $stage $Name
    $localSource = $null
    foreach ($local in $LocalCandidates) {
        Assert-ApiWorkshopUnlinkedPath $local
        if (-not (Test-Path -LiteralPath $local -PathType Leaf)) { continue }
        $valid = $false
        try { $valid = [bool](& $Validate $local) } catch { Write-Verbose "Invalid local release ${local}: $($_.Exception.Message)" }
        if ($valid) { $localSource = $local; break }
    }
    if ($localSource) {
        Write-Host "Using verified local release asset: $localSource"
        [IO.File]::Copy($localSource, $candidate)
    } else { & $Download $stage $Name | Out-Null }
    Assert-ApiWorkshopUnlinkedPath $candidate
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf) -or -not (& $Validate $candidate)) {
        throw "Downloaded release asset failed verification: $Name. Previous cache retained."
    }
    Assert-ApiWorkshopUnlinkedPath $path
    if ([IO.File]::Exists($path)) {
        $backup = Join-Path $stage 'previous-cache-file'
        [IO.File]::Replace($candidate, $path, $backup)
        [IO.File]::Delete($backup)
    }
    else { [IO.File]::Move($candidate, $path) }
    [IO.Directory]::Delete($stage)
    return $path
}

function Test-ReleaseAssetHash([string]$Path, [string]$Hash) {
    return $Hash -match '^[a-fA-F0-9]{64}$' -and (Get-WorkshopFileHash $Path) -ieq $Hash
}

function Test-ReleaseChecksum([string]$Path, [string]$Hash) {
    $match = [regex]::Match([IO.File]::ReadAllText($Path), '(?i)\b([0-9a-f]{64})\b')
    return $match.Success -and $match.Groups[1].Value -ieq $Hash
}

function Get-CachedSteamReleaseAsset {
    param([string]$Cache, [string]$Name, [string]$Tag, [string]$Repository,
          [scriptblock]$Validate, [int]$FailureCode = 10)
    $locals = @()
    $tagMatch = [regex]::Match($Tag, '^(?<project>[A-Za-z0-9._-]+)/(?:test/)?v(?<version>\d+\.\d+\.\d+)(?:-[a-f0-9]{12})?$')
    if ($tagMatch.Success) {
        $workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
        $releaseRoot = Join-Path $workspace ('.release-output/' + $tagMatch.Groups['project'].Value + '/v' + $tagMatch.Groups['version'].Value)
        # Identity/hash validation below also protects against a same-version test/production collision.
        $locals = @((Join-Path $releaseRoot $Name), (Join-Path $releaseRoot ('Workshop/' + $Name)))
    }
    return Get-VerifiedReleaseAsset $Cache $Name $Validate {
        param($stage, $asset)
        Invoke-Checked -FilePath 'gh' -Arguments @('release','download',$Tag,'--repo',$Repository,
            '--pattern',$asset,'--dir',$stage) -FailureCode $FailureCode | Out-Null
    } $locals
}
