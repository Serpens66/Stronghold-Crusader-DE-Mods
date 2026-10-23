function Get-ContinuationHash([string]$Path) {
    $s=[IO.File]::OpenRead($Path);$h=[Security.Cryptography.SHA256]::Create()
    try{[BitConverter]::ToString($h.ComputeHash($s)).Replace('-','')}finally{$s.Dispose();$h.Dispose()}
}
function Assert-ContinuationUnlinked([string]$Path) {
    for($p=[IO.Path]::GetFullPath($Path);$p;$p=[IO.Path]::GetDirectoryName($p)){
        if((Test-Path -LiteralPath $p) -and ([IO.File]::GetAttributes($p) -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked continuation path.'}
    }
}
function Get-ContinuationFiles {
    @('BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll','BepInEx/plugins/DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll','BepInEx/plugins/DependencyBootstrapProbe_Serp/info.json','BepInEx/config/DependencyBootstrapProbe.active','BepInEx/config/DependencyBootstrapProbe.correct')
}
function Assert-CorrectionBinding([string]$Game) {
    if(Test-Path -LiteralPath (Join-Path $Game 'BepInEx/config/DependencyBootstrapProbe.disabled')){throw 'Disabled probe cannot continue correction.'}
    foreach($p in Get-ContinuationFiles){Assert-ContinuationUnlinked (Join-Path $Game $p)}
    $hash=Get-ContinuationHash (Join-Path $Game 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll')
    foreach($name in @('active','correct')){if([IO.File]::ReadAllText((Join-Path $Game ('BepInEx/config/DependencyBootstrapProbe.'+$name))).Trim() -cne $hash){throw 'Previous correction binding is invalid.'}}
}
function New-ProbeContinuation([string]$Probe,[string]$Game,[string]$Accepted,[string]$Backup) {
    Assert-CorrectionBinding $Game
    $pairPath='BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json'
    $oldPair=$null
    if(Test-Path -LiteralPath (Join-Path $Game $pairPath)){
        . (Join-Path $Probe 'package/Pair.ps1')
        $null=Read-ProbePair $Game
        $oldPair=Get-ContinuationHash (Join-Path $Game $pairPath)
    }
    $lifecycle=Test-Path -LiteralPath (Join-Path $Accepted 'BepInEx/DependencyBootstrapProbe.lifecycle.json')
    & (Join-Path $Probe 'Check-Run.ps1') -Mode Active -RequireCorrection -RequireLifecycle:$lifecycle -LogPath (Join-Path $Accepted 'BepInEx/DependencyBootstrapProbe.trace.log') -ExpectedArtifactRoot $Accepted -GameDir $Accepted -RecordedGameDir $Game
    $earlySource=Join-Path (Split-Path $Accepted) 'Shadow-accepted-20261016-1946/early-source-hashes.json'
    $early=Get-Content -LiteralPath $earlySource -Raw|ConvertFrom-Json
    foreach($relative in @('patcher/DependencyBootstrapPatcher.cs','patcher/ReadyGate.cs','ProbeTrace.cs','ProbeIdentity.cs')){
        $entry=@($early|Where-Object {$_.Path.Replace('\','/') -ceq $relative})
        if($entry.Count -ne 1 -or (Get-ContinuationHash (Join-Path $Probe $relative)) -cne $entry[0].Hash){throw 'Early source changed; cannot inherit accepted startup.'}
    }
    foreach($p in @('BepInEx/core/BepInEx.dll')+@(Get-ContinuationFiles|Where-Object {$_ -like '*.dll'})){
        if((Get-ContinuationHash (Join-Path $Game $p)) -cne (Get-ContinuationHash (Join-Path $Accepted $p))){throw "Current binary differs from accepted startup: $p"}
    }
    # User explicitly authorized a new 2.14.2 audit; this is not an inherited runtime acceptance.
    $expectedSE=[regex]::Match([IO.File]::ReadAllText((Join-Path $Probe 'patcher/HookRuntime.cs')),'ExtenderHash = "([A-F0-9]{64})"').Groups[1].Value
    if(-not $expectedSE -or (Get-ContinuationHash (Join-Path $Game 'BepInEx/plugins/000shcdese/SHCDESE.dll')) -cne $expectedSE){throw 'Extender differs from newly audited probe target.'}
    Assert-ContinuationUnlinked $Backup
    if(Test-Path -LiteralPath $Backup){throw 'Backup must be a fresh directory.'}
    [IO.Directory]::CreateDirectory($Backup)|Out-Null
    $records=@(foreach($p in Get-ContinuationFiles){$target=Join-Path $Backup $p;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath (Join-Path $Game $p) -Destination $target;[ordered]@{Path=$p;SHA256=Get-ContinuationHash $target}})
    if($oldPair){Copy-Item -LiteralPath (Join-Path $Game $pairPath) -Destination (Join-Path $Backup $pairPath)}
    foreach($p in @('BepInEx/DependencyBootstrapProbe.trace.log','BepInEx/LogOutput.log')){if(Test-Path -LiteralPath (Join-Path $Game $p)){Copy-Item -LiteralPath (Join-Path $Game $p) -Destination (Join-Path $Backup ([IO.Path]::GetFileName($p)))}}
    $unity=Join-Path $env:USERPROFILE 'AppData/LocalLow/Firefly Studios/Stronghold Crusader Definitive Edition/Player.log'
    if(Test-Path -LiteralPath $unity){Copy-Item -LiteralPath $unity -Destination (Join-Path $Backup 'Player.log')}
    $loaders=@(foreach($p in @('BepInEx/core/BepInEx.dll','BepInEx/plugins/000shcdese/SHCDESE.dll')){@{Path=$p;SHA256=Get-ContinuationHash (Join-Path $Game $p)}})
    $proof=[ordered]@{Schema=1;OldPairSHA256=$oldPair;Accepted=[IO.Path]::GetFullPath($Accepted);Files=$records;Loaders=$loaders;APISharedSHA256=Get-ContinuationHash (Join-Path $Game 'BepInEx/plugins/APIShared_Serp/APIShared.dll');Early=$early;RuntimeAcceptance='Pending new pair-bound probe game start'}
    [IO.File]::WriteAllText((Join-Path $Backup 'continuation.json'),($proof|ConvertTo-Json -Depth 6))
}
function Assert-ProbeContinuation([string]$Game,[string]$Backup,[switch]$BeforeCopy) {
    Assert-ContinuationUnlinked $Backup
    $proof=Get-Content -LiteralPath (Join-Path $Backup 'continuation.json') -Raw|ConvertFrom-Json
    $pairPath='BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json'
    Assert-ContinuationUnlinked (Join-Path $Game $pairPath)
    if($proof.OldPairSHA256){
        Assert-ContinuationUnlinked (Join-Path $Backup $pairPath)
        if((Get-ContinuationHash (Join-Path $Backup $pairPath)) -cne $proof.OldPairSHA256){throw 'Changed pair backup.'}
    }
    if($BeforeCopy){
        $present=Test-Path -LiteralPath (Join-Path $Game $pairPath)
        if($present -ne [bool]$proof.OldPairSHA256){throw 'Pair appeared/disappeared during preparation.'}
        if($present -and (Get-ContinuationHash (Join-Path $Game $pairPath)) -cne $proof.OldPairSHA256){throw 'Pair changed during preparation.'}
    }
    if($proof.Schema -ne 1 -or (@($proof.Files.Path|Sort-Object)-join '|') -cne (@(Get-ContinuationFiles|Sort-Object)-join '|')){throw 'Incomplete continuation proof.'}
    if((@($proof.Loaders.Path|Sort-Object)-join '|') -cne 'BepInEx/core/BepInEx.dll|BepInEx/plugins/000shcdese/SHCDESE.dll'){throw 'Incomplete loader proof.'}
    foreach($row in $proof.Loaders){if((Get-ContinuationHash (Join-Path $Game $row.Path)) -cne $row.SHA256){throw 'Loader changed during preparation.'}}
    foreach($row in $proof.Files){
        Assert-ContinuationUnlinked (Join-Path $Game $row.Path)
        Assert-ContinuationUnlinked (Join-Path $Backup $row.Path)
        if((Get-ContinuationHash (Join-Path $Backup $row.Path)) -cne $row.SHA256){throw 'Changed backup.'}
        if(($BeforeCopy -or $row.Path -like '*.active' -or $row.Path -like '*.correct') -and (Get-ContinuationHash (Join-Path $Game $row.Path)) -cne $row.SHA256){throw 'Installed probe changed since preparation.'}
    }
    if((Get-ContinuationHash (Join-Path $Game 'BepInEx/plugins/APIShared_Serp/APIShared.dll')) -cne $proof.APISharedSHA256){throw 'Installed APIShared changed during preparation.'}
    return $proof
}
function Complete-ProbeContinuation([string]$Probe,[string]$Game,[string]$Backup) {
    $null=Assert-ProbeContinuation $Game $Backup
    $pairPath='BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json'
    if((Get-ContinuationHash (Join-Path $Game $pairPath)) -cne (Get-ContinuationHash (Join-Path $Probe $pairPath))){throw 'Installed pair differs from build.'}
    # Parse with the same fixed package contract before rebinding correction.
    . (Join-Path $PSScriptRoot '../package/Pair.ps1')
    $null=Read-ProbePair $Game
    foreach($p in @(Get-ContinuationFiles|Where-Object {$_ -like '*.dll' -or $_ -like '*/info.json'})){
        if((Get-ContinuationHash (Join-Path $Game $p)) -cne (Get-ContinuationHash (Join-Path $Probe $p))){throw 'New installed probe differs from build.'}
    }
    $hash=Get-ContinuationHash (Join-Path $Game 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll')
    foreach($name in @('active','correct')){[IO.File]::WriteAllText((Join-Path $Game ('BepInEx/config/DependencyBootstrapProbe.'+$name)),$hash)}
    Assert-CorrectionBinding $Game
    Write-Host 'Continued accepted correction mode; lifecycle observation requires a new game acceptance.'
}
