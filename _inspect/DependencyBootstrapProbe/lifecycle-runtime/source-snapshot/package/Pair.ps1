# Standalone offline package contract. No APIShared imports or plugin execution.
Set-StrictMode -Version Latest
$script:ProbeGuid='DependencyBootstrapProbe_Serp'
$script:RuntimeRelative='BepInEx/plugins/DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll'
$script:BootstrapRelative='BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll'
$script:PairRelative='BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json'
function Get-ProbeHash([string]$Path) {
    $stream=[IO.File]::OpenRead($Path);$sha=[Security.Cryptography.SHA256]::Create()
    try{[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
}
function Assert-ProbeUnlinked([string]$Path) {
    for($part=[IO.Path]::GetFullPath($Path);$part;$part=[IO.Path]::GetDirectoryName($part)) {
        # GetAttributes also rejects inaccessible paths rather than treating them as absent.
        if(Test-Path -LiteralPath $part){if([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse path rejected.'}}
    }
}
function Get-PairPath([string]$Root,[string]$Relative) {
    if($Relative -cnotin @($script:RuntimeRelative,$script:BootstrapRelative,$script:PairRelative)){throw 'Unknown pair path.'}
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $path=[IO.Path]::GetFullPath((Join-Path $base $Relative))
    if(-not $path.StartsWith($base+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Pair path escaped root.'}
    Assert-ProbeUnlinked $path
    return $path
}
function Get-ProbeFiles([string]$Directory) {
    Assert-ProbeUnlinked $Directory
    foreach($entry in Get-ChildItem -LiteralPath $Directory -Force){
        Assert-ProbeUnlinked $entry.FullName
        if($entry.PSIsContainer){Get-ProbeFiles $entry.FullName}else{$entry}
    }
}
function Assert-ProbeFields($Value,[string[]]$Fields) {
    if($null -eq $Value -or (@($Value.PSObject.Properties.Name|Sort-Object)-join ',') -cne (@($Fields|Sort-Object)-join ',')){throw 'Unexpected fields.'}
}
function Read-ProbePair([string]$Root) {
    $pair=[IO.File]::ReadAllText((Get-PairPath $Root $script:PairRelative))|ConvertFrom-Json
    Assert-ProbeFields $pair @('Schema','Guid','Version','RuntimeSHA256','BootstrapSHA256')
    if(($pair.Schema -isnot [int] -and $pair.Schema -isnot [long]) -or $pair.Schema -ne 1 -or $pair.Guid -cne $script:ProbeGuid){throw 'Invalid pair identity.'}
    $version=$null
    if($pair.Version -isnot [string] -or -not [version]::TryParse($pair.Version,[ref]$version)){throw 'Invalid pair version.'}
    foreach($hash in @($pair.RuntimeSHA256,$pair.BootstrapSHA256)){if($hash -isnot [string] -or $hash -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid digest.'}}
    if((Get-ProbeHash (Get-PairPath $Root $script:RuntimeRelative)) -cne $pair.RuntimeSHA256 -or (Get-ProbeHash (Get-PairPath $Root $script:BootstrapRelative)) -cne $pair.BootstrapSHA256){throw 'Missing, changed or mixed pair.'}
    return $pair
}
function Get-ProbePairDecision {
    param([string]$Root,[string]$SelectedGuid,[string]$SelectedVersion,[string]$SelectedPath,[string]$SelectedHash,[string]$LoadedBootstrapHash,[int]$BootstrapInstances=1,[bool]$CompetingHookOwner=$false)
    try {
        $pair=Read-ProbePair $Root
        if($BootstrapInstances -ne 1 -or $CompetingHookOwner){throw 'Exclusive hook ownership unavailable.'}
        if($SelectedGuid -cne $pair.Guid -or -not ([version]$SelectedVersion).Equals([version]$pair.Version) -or [IO.Path]::GetFullPath($SelectedPath) -ine (Get-PairPath $Root $script:RuntimeRelative) -or $SelectedHash -cne $pair.RuntimeSHA256 -or $LoadedBootstrapHash -cne $pair.BootstrapSHA256){throw 'Loaded/selected identity differs from pair.'}
        # A renamed byte-identical own bootstrap is also a conflicting instance.
        $patchers=Split-Path (Get-PairPath $Root $script:BootstrapRelative)
        foreach($file in Get-ProbeFiles $patchers){if($file.FullName -ine (Get-PairPath $Root $script:BootstrapRelative) -and (Get-ProbeHash $file.FullName) -ceq $pair.BootstrapSHA256){throw 'Duplicate bootstrap file.'}}
        [pscustomobject]@{Active=$true;Reason='Exact selected pair'}
    }catch{[pscustomobject]@{Active=$false;Reason=$_.Exception.Message}}
}
