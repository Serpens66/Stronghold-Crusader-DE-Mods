. (Join-Path $PSScriptRoot 'Pair.ps1')
# Execution remains restricted to offline fixtures until a real ownership adapter is audited.
function Assert-ProbeFixtureRoot([string]$Root) {
    $allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../_inspect/DependencyBootstrapProbe/lifecycle-implementation')).TrimEnd('\')+'\'
    $full=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    if(-not $full.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Removal is restricted to lifecycle workspace fixtures.'}
    Assert-ProbeUnlinked $full
}
function Read-ProbeOwnership([string]$Root) {
    $path=Join-Path $Root 'probe-lifecycle-state.json';Assert-ProbeUnlinked $path
    $state=[IO.File]::ReadAllText($path)|ConvertFrom-Json
    Assert-ProbeFields $state @('Schema','OwnershipKnown','Owners','CompetingClaim','ExternallyManaged')
    if(($state.Schema -isnot [int] -and $state.Schema -isnot [long]) -or $state.Schema -ne 1 -or $state.OwnershipKnown -isnot [bool] -or -not $state.OwnershipKnown -or $state.CompetingClaim -isnot [bool] -or $state.CompetingClaim -or $state.ExternallyManaged -isnot [bool] -or $state.ExternallyManaged -or $env:SCDEModManagerLaunchId){throw 'Ownership unknown, competing or externally managed.'}
    if($state.Owners -isnot [array]){throw 'Invalid owners.'}
    return $state
}
function New-ProbeRemovalReceipt([string]$Root,[string]$PackageRoot,[int]$ProcessId=0) {
    Assert-ProbeFixtureRoot $Root
    $pair=Read-ProbePair $Root;$package=Read-ProbePair $PackageRoot
    if($pair.RuntimeSHA256 -cne $package.RuntimeSHA256 -or $pair.BootstrapSHA256 -cne $package.BootstrapSHA256 -or $pair.Version -cne $package.Version){throw 'Package proof differs.'}
    $state=Read-ProbeOwnership $Root
    if(@($state.Owners).Count -ne 1 -or $state.Owners[0] -cne $script:ProbeGuid){throw 'Receipt requires one verified owner.'}
    $start=''
    if($ProcessId -gt 0){$start=(Get-Process -Id $ProcessId -ErrorAction Stop).StartTime.ToUniversalTime().ToString('o')}
    # No target path from the receipt is ever used: target is fixed by Pair.ps1.
    return [pscustomobject][ordered]@{Schema=1;Guid=$script:ProbeGuid;Root=[IO.Path]::GetFullPath($Root);PackageRoot=[IO.Path]::GetFullPath($PackageRoot);PairSHA256=Get-ProbeHash (Get-PairPath $PackageRoot $script:PairRelative);BootstrapSHA256=$pair.BootstrapSHA256;ProcessId=$ProcessId;ProcessStartUtc=$start}
}
function Assert-ProbeRemoval($Receipt) {
    Assert-ProbeFields $Receipt @('Schema','Guid','Root','PackageRoot','PairSHA256','BootstrapSHA256','ProcessId','ProcessStartUtc')
    if($Receipt.Schema -ne 1 -or $Receipt.Guid -cne $script:ProbeGuid -or $Receipt.BootstrapSHA256 -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid receipt.'}
    Assert-ProbeFixtureRoot $Receipt.Root
    $package=Read-ProbePair $Receipt.PackageRoot
    if((Get-ProbeHash (Get-PairPath $Receipt.PackageRoot $script:PairRelative)) -cne $Receipt.PairSHA256 -or $package.BootstrapSHA256 -cne $Receipt.BootstrapSHA256){throw 'Package evidence changed.'}
    if($Receipt.ProcessId -lt 0 -or ($Receipt.ProcessId -gt 0 -and -not $Receipt.ProcessStartUtc)){throw 'Invalid process evidence.'}
    if($Receipt.ProcessId -gt 0){$process=Get-Process -Id $Receipt.ProcessId -ErrorAction SilentlyContinue;if($process -and $process.StartTime.ToUniversalTime().ToString('o') -ceq $Receipt.ProcessStartUtc){throw 'Origin process is still running.'}}
    foreach($process in Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){if([IO.Path]::GetDirectoryName($process.MainModule.FileName) -ieq $Receipt.Root){throw 'Target game is running; removal deferred.'}}
    $state=Read-ProbeOwnership $Receipt.Root
    if(@($state.Owners).Count -ne 0){throw 'Owner still subscribed.'}
    $staging=Join-Path $Receipt.Root '_SE/.staging';Assert-ProbeUnlinked $staging
    if(Test-Path -LiteralPath $staging){throw 'Pending staging; removal deferred.'}
    $patchers=Split-Path (Get-PairPath $Receipt.Root $script:BootstrapRelative)
    foreach($file in Get-ProbeFiles $patchers){if($file.FullName -ine (Get-PairPath $Receipt.Root $script:BootstrapRelative) -and (Get-ProbeHash $file.FullName) -ceq $Receipt.BootstrapSHA256){throw 'Competing bootstrap file.'}}
    $target=Get-PairPath $Receipt.Root $script:BootstrapRelative
    if([IO.File]::Exists($target) -and (Get-ProbeHash $target) -cne $Receipt.BootstrapSHA256){throw 'Bootstrap changed since receipt.'}
    return $target
}
function Invoke-ProbeRemoval($Receipt,[switch]$Apply) {
    try {
        $target=Assert-ProbeRemoval $Receipt
        if(-not [IO.File]::Exists($target)){return [pscustomobject]@{Status='Absent';Reason='Already absent'}}
        if(-not $Apply){return [pscustomobject]@{Status='Preview';Reason='Exact bootstrap would be removed'}}
        if(-not ('DependencyBootstrapProbe.PackageTools.VerifiedFileDeletion' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'VerifiedFileDeletion.cs')}
        $target=Assert-ProbeRemoval $Receipt
        # Hash, link identity and delete disposition refer to the same locked file handle.
        [DependencyBootstrapProbe.PackageTools.VerifiedFileDeletion]::Delete($target,$Receipt.BootstrapSHA256)
        return [pscustomobject]@{Status='Removed';Reason='Exact verified bootstrap removed'}
    }catch{return [pscustomobject]@{Status='Rejected';Reason=$_.Exception.Message}}
}
