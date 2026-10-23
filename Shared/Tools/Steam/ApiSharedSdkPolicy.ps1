# Artifact identities are data from the selected release, never a global version whitelist.
function Assert-ApiSharedMinimumVersion([string]$Version, [string]$MinimumVersion) {
    if ($Version -cnotmatch '^\d+\.\d+\.\d+$' -or $MinimumVersion -cnotmatch '^\d+\.\d+\.\d+$') {
        throw 'Invalid APIShared version or consumer minimum.'
    }
    if ([version]$Version -lt [version]$MinimumVersion) { throw "APIShared $Version is below required minimum $MinimumVersion." }
}
function Get-ModpackApiSharedIdentity($Provenance, [bool]$Test = $false) {
    $records = @($Provenance.Infrastructure | Where-Object { $_.Guid -ceq 'APIShared_Serp' })
    if ($records.Count -ne 1) { throw 'Exactly one APIShared infrastructure record is required.' }
    $api = $records[0]
    $commit = [string]$api.SourceCommit
    $version = [string]$api.Version
    $main = [string]$Provenance.ApiSharedMainCommit
    $tag = 'APIShared/v' + $version
    if ($Test) { $tag = 'APIShared/test/v' + $version + '-' + $commit.Substring(0, [Math]::Min(12, $commit.Length)) }
    if ($version -cnotmatch '^\d+\.\d+\.\d+$' -or $commit -cnotmatch '^[0-9a-f]{40}$' -or
        $main -cnotmatch '^[0-9a-f]{40}$' -or $Provenance.ApiSharedCommit -cne $commit -or
        $api.ReleaseTag -cne $tag -or $api.PackageSha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'APIShared package/source/mode identity mismatch in modpack provenance.'
    }
    return [ordered]@{ Version=$version; Commit=$commit; MainCommit=$main; ReleaseTag=$tag; PackageSha256=[string]$api.PackageSha256 }
}
function Assert-ModpackApiSharedIdentity($Expected, $Actual) {
    foreach ($name in @('Version','Commit','MainCommit','ReleaseTag','PackageSha256')) {
        if (-not $Expected -or [string]$Expected.$name -cne [string]$Actual.$name) {
            throw 'Prepared modpack SDK identity changed or predates artifact identities; prepare again.'
        }
    }
}

# Anchor all cached evidence to the canonical release's uploaded asset digests. A ZIP and
# locally edited checksum/provenance must not be able to authenticate each other.
function Get-ApiSharedReleaseAssetRecords($Assets, [string[]]$Names) {
    return @(foreach($name in $Names){
        $asset=@($Assets|Where-Object { $_.name -ceq $name })
        if($asset.Count -ne 1 -or $asset[0].state -cne 'uploaded' -or
           $asset[0].digest -cnotmatch '^sha256:[0-9a-f]{64}$' -or [long]$asset[0].size -le 0){
            throw "APIShared release asset has no verifiable identity: $name"
        }
        [pscustomobject]@{Name=$name;Size=[long]$asset[0].size;Sha256=([string]$asset[0].digest).Substring(7)}
    })
}
function Assert-ApiSharedReleaseAssetFiles([string]$Directory, $Records) {
    foreach($record in $Records){
        if([IO.Path]::GetFileName([string]$record.Name) -cne $record.Name){throw 'Unsafe APIShared asset name.'}
        $path=Join-Path $Directory $record.Name
        Assert-ApiWorkshopUnlinkedPath $path
        if(-not(Test-Path -LiteralPath $path -PathType Leaf) -or
           (Get-Item -LiteralPath $path).Length -ne $record.Size -or
           (Get-SteamPackFileSha256 $path) -cne $record.Sha256){throw "Prepared APIShared asset changed: $($record.Name)"}
    }
}

function Get-ApiSharedPackageIdentity($Provenance,[string]$MainCommit,[bool]$Test) {
    $record=[pscustomobject]@{ApiSharedCommit=$Provenance.Commit;ApiSharedMainCommit=$MainCommit;Infrastructure=@(
        [pscustomobject]@{Guid='APIShared_Serp';Version=$Provenance.Version;SourceCommit=$Provenance.Commit;
            ReleaseTag=$Provenance.Tag;PackageSha256=$Provenance.Package.Sha256})}
    return Get-ModpackApiSharedIdentity $record $Test
}
