param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../package/Removal.ps1')
if(-not $EvidenceRoot){$EvidenceRoot=Join-Path $workspace '_inspect/DependencyBootstrapProbe/lifecycle-implementation/final'}
Assert-ProbeFixtureRoot $EvidenceRoot
$EvidenceRoot=[IO.Path]::GetFullPath($EvidenceRoot)
[IO.Directory]::CreateDirectory($EvidenceRoot)|Out-Null
# One short, temporary tree per invocation; only the latest report survives.
$root=Join-Path (Split-Path $EvidenceRoot) ('t-'+[guid]::NewGuid().ToString('N').Substring(0,8))
Assert-ProbeFixtureRoot $root
[IO.Directory]::CreateDirectory($root)|Out-Null
$previousTemp=$env:TEMP;$previousTmp=$env:TMP
$results=New-Object 'Collections.Generic.List[object]'
$failure=$null
function Remove-FixtureTree([string]$Path) {
    $full=[IO.Path]::GetFullPath($Path)
    if($full -ine $root -and -not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe fixture cleanup'}
    $item=Get-Item -LiteralPath $full -Force
    if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){
        if($item.PSIsContainer){[IO.Directory]::Delete($full)}else{[IO.File]::Delete($full)}
    } elseif($item.PSIsContainer){
        foreach($child in Get-ChildItem -LiteralPath $full -Force){Remove-FixtureTree $child.FullName}
        [IO.Directory]::Delete($full)
    } else {[IO.File]::Delete($full)}
}
try {
$env:TEMP=Join-Path $root 'temp';$env:TMP=$env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
$results=New-Object 'Collections.Generic.List[object]'
function Check([string]$Name,[bool]$Condition){if(-not $Condition){throw "FAIL: $Name"};$results.Add([pscustomobject]@{Case=$Name;Passed=$true})}
function Reject([string]$Name,[scriptblock]$Action){$failed=$false;try{& $Action|Out-Null}catch{$failed=$true};Check $Name $failed}
function WriteText($Path,$Text){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))}
function CopyPayload($Source,$Target,$Relative){$destination=Get-PairPath $Target $Relative;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null;[IO.File]::WriteAllBytes($destination,[IO.File]::ReadAllBytes((Get-PairPath $Source $Relative)))}
function Install($Source,$Target){foreach($rel in @($script:RuntimeRelative,$script:BootstrapRelative,$script:PairRelative)){CopyPayload $Source $Target $rel}}
function State($Target,$Owners=@($script:ProbeGuid),$Known=$true,$Competing=$false,$External=$false){WriteText (Join-Path $Target 'probe-lifecycle-state.json') (@{Schema=1;OwnershipKnown=$Known;Owners=@($Owners);CompetingClaim=$Competing;ExternallyManaged=$External}|ConvertTo-Json)}
function Decision($Target,$Pair){Get-ProbePairDecision -Root $Target -SelectedGuid $script:ProbeGuid -SelectedVersion $Pair.Version -SelectedPath (Get-PairPath $Target $script:RuntimeRelative) -SelectedHash $Pair.RuntimeSHA256 -LoadedBootstrapHash $Pair.BootstrapSHA256}
$a=Join-Path $root 'A';$b=Join-Path $root 'B';$live=Join-Path $root 'live'
# Opaque archived probe bytes, never loaded. Revision B intentionally differs at equal version.
$archive=Join-Path $workspace '_inspect/DependencyBootstrapProbe/Correction-accepted-20261016-2154'
foreach($revision in @($a,$b)){
    foreach($rel in @($script:RuntimeRelative,$script:BootstrapRelative)){
        $bytes=[IO.File]::ReadAllBytes((Join-Path $archive $rel))
        if($revision -eq $b){$bytes+=,[byte]42}
        $path=Get-PairPath $revision $rel;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))|Out-Null;[IO.File]::WriteAllBytes($path,$bytes)
    }
    WriteText (Get-PairPath $revision $script:PairRelative) (@{Schema=1;Guid=$script:ProbeGuid;Version='1.0.0';RuntimeSHA256=Get-ProbeHash (Get-PairPath $revision $script:RuntimeRelative);BootstrapSHA256=Get-ProbeHash (Get-PairPath $revision $script:BootstrapRelative)}|ConvertTo-Json)
}
$pa=Read-ProbePair $a;$pb=Read-ProbePair $b
$mapping=@{R=$script:RuntimeRelative;B=$script:BootstrapRelative;P=$script:PairRelative}
foreach($order in @('RBP','RPB','BRP','BPR','PRB','PBR')){foreach($upgrade in @($false,$true)){
    $target=Join-Path $root ($order+'-'+$upgrade)
    if($upgrade){Install $a $target;Check "$order old complete pair" (Decision $target $pa).Active}
    for($i=0;$i -lt 3;$i++){CopyPayload $b $target $mapping[[string]$order[$i]];Check "$order upgrade=$upgrade prefix=$($i+1)" ((Decision $target $pb).Active -eq ($i -eq 2))}
}}
foreach($missing in $mapping.Values){Install $a $live;Remove-Item -LiteralPath (Get-PairPath $live $missing);Check "missing $missing" (-not (Decision $live $pa).Active)}
Install $a $live
$originalManifest=[IO.File]::ReadAllText((Get-PairPath $a $script:PairRelative))
foreach($invalid in @('{broken','{}',$originalManifest.Replace('1.0.0','invalid'),$originalManifest.Replace('"Schema":  1','"Schema":  "1"').Replace('"Schema": 1','"Schema": "1"'),$originalManifest.Replace($script:ProbeGuid,'Foreign'))){WriteText (Get-PairPath $live $script:PairRelative) $invalid;Check 'invalid manifest rejected' (-not (Decision $live $pa).Active)}
Install $a $live
foreach($rel in @($script:RuntimeRelative,$script:BootstrapRelative)){CopyPayload $b $live $rel;Check "same version different bytes $rel" (-not (Decision $live $pa).Active);Install $a $live}
$runtimeBytes=[IO.File]::ReadAllBytes((Get-PairPath $live $script:RuntimeRelative));[IO.File]::WriteAllBytes((Get-PairPath $live $script:RuntimeRelative),[IO.File]::ReadAllBytes((Get-PairPath $live $script:BootstrapRelative)));[IO.File]::WriteAllBytes((Get-PairPath $live $script:BootstrapRelative),$runtimeBytes)
Check 'swapped files rejected' (-not (Decision $live $pa).Active);Install $a $live
$argsPair=@{Root=$live;SelectedGuid=$script:ProbeGuid;SelectedVersion=$pa.Version;SelectedPath=Get-PairPath $live $script:RuntimeRelative;SelectedHash=$pa.RuntimeSHA256;LoadedBootstrapHash=$pa.BootstrapSHA256}
foreach($field in @('SelectedGuid','SelectedVersion','SelectedPath','SelectedHash','LoadedBootstrapHash')){$bad=$argsPair.Clone();$bad[$field]=if($field -eq 'SelectedPath'){Get-PairPath $a $script:RuntimeRelative}else{'wrong'};Check "wrong $field" (-not (Get-ProbePairDecision @bad).Active)}
Check 'duplicate loaded bootstrap' (-not (Get-ProbePairDecision @argsPair -BootstrapInstances 2).Active)
Check 'other hook owner' (-not (Get-ProbePairDecision @argsPair -CompetingHookOwner $true).Active)
$duplicate=Join-Path $live 'BepInEx/patchers/renamed.dll';Copy-Item -LiteralPath (Get-PairPath $live $script:BootstrapRelative) -Destination $duplicate
Check 'renamed bootstrap file' (-not (Decision $live $pa).Active);Remove-Item -LiteralPath $duplicate
$foreign=Join-Path $live 'BepInEx/patchers/Foreign.dll';WriteText $foreign 'unrelated patcher';$foreignHash=Get-ProbeHash $foreign
Check 'unrelated patcher allowed' (Decision $live $pa).Active
State $live
$receipt=New-ProbeRemovalReceipt $live $a
Reject 'receipt requires matching package proof' {New-ProbeRemovalReceipt $live $b}
Check 'subscribed owner blocks removal' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected')
State $live @()
Check 'preview preserves bootstrap' ((Invoke-ProbeRemoval $receipt).Status -eq 'Preview' -and [IO.File]::Exists((Get-PairPath $live $script:BootstrapRelative)))
foreach($mode in @('Unknown','Competing','External','Owners')){
    switch($mode){Unknown{State $live @() $false}Competing{State $live @() $true $true}External{State $live @() $true $false $true}Owners{State $live @('Foreign')}}
    Check "$mode state rejected at execution" ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected')
}
State $live @()
$staging=Join-Path $live '_SE/.staging';[IO.Directory]::CreateDirectory($staging)|Out-Null
Check 'staged replacement blocks deletion' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected');Remove-Item -LiteralPath $staging
CopyPayload $b $live $script:BootstrapRelative
Check 'changed after receipt protected' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected' -and (Get-ProbeHash (Get-PairPath $live $script:BootstrapRelative)) -ceq $pb.BootstrapSHA256)
Install $a $live
$lock=[IO.File]::Open((Get-PairPath $live $script:BootstrapRelative),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
try{Check 'locked bootstrap preserved' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected')}finally{$lock.Dispose()}
State $live
$running=New-ProbeRemovalReceipt $live $a -ProcessId $PID;State $live @()
Check 'live origin process blocks deletion' ((Invoke-ProbeRemoval $running -Apply).Status -eq 'Rejected')
$child=Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 2' -WindowStyle Hidden -PassThru
State $live;$exited=New-ProbeRemovalReceipt $live $a -ProcessId $child.Id;State $live @();$child.WaitForExit()
Check 'exited origin process permits preview' ((Invoke-ProbeRemoval $exited).Status -eq 'Preview')
$receiptPath=Join-Path $root 'receipt.json';WriteText $receiptPath ($exited|ConvertTo-Json)
$worker=Join-Path $PSScriptRoot '../package/Invoke-PostExitRemoval.ps1'
$output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ReceiptPath $receiptPath
Check 'fresh worker defaults preview' ($LASTEXITCODE -eq 0 -and ($output|ConvertFrom-Json).Status -eq 'Preview')
CopyPayload $b $live $script:BootstrapRelative
$output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ReceiptPath $receiptPath -Apply
Check 'fresh worker refuses stale receipt' ($LASTEXITCODE -eq 2 -and ($output|ConvertFrom-Json).Status -eq 'Rejected')
Install $a $live
# The native helper also refuses a changed target independently of the earlier checks.
if(-not ('DependencyBootstrapProbe.PackageTools.VerifiedFileDeletion' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot '../package/VerifiedFileDeletion.cs')}
Reject 'same-handle deletion verifies digest' {[DependencyBootstrapProbe.PackageTools.VerifiedFileDeletion]::Delete((Get-PairPath $live $script:BootstrapRelative),$pb.BootstrapSHA256)}
Check 'hash mismatch leaves target present' ([IO.File]::Exists((Get-PairPath $live $script:BootstrapRelative)))
$hardlink=Join-Path $root 'bootstrap-hardlink.dll'
$null=New-Item -ItemType HardLink -Path $hardlink -Target (Get-PairPath $live $script:BootstrapRelative)
Check 'hard-linked bootstrap protected' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected')
Check 'hardlink target unchanged' ((Get-ProbeHash $hardlink) -ceq $pa.BootstrapSHA256)
Remove-Item -LiteralPath $hardlink
$output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ReceiptPath $receiptPath -Apply
Check 'fresh post-exit worker removes exact target' ($LASTEXITCODE -eq 0 -and ($output|ConvertFrom-Json).Status -eq 'Removed')
Check 'runtime and foreign patcher preserved' ([IO.File]::Exists((Get-PairPath $live $script:RuntimeRelative)) -and (Get-ProbeHash $foreign) -ceq $foreignHash)
Check 'repeated removal harmless' ((Invoke-ProbeRemoval $exited -Apply).Status -eq 'Absent')
Install $b $live
Check 'manual complete restore works' (Decision $live $pb).Active
Check 'manual restore preserves foreign file' ((Get-ProbeHash $foreign) -ceq $foreignHash)
Install $a $live
State $live
Reject 'unknown previous owner cannot create receipt' {State $live @('Foreign');New-ProbeRemovalReceipt $live $a}
State $live
Reject 'mixed pair cannot create receipt' {CopyPayload $b $live $script:RuntimeRelative;New-ProbeRemovalReceipt $live $a}
Install $a $live;State $live @()
$badReceipt=$receipt.PSObject.Copy();$badReceipt|Add-Member Target '../Foreign.dll'
Check 'receipt cannot redirect target' ((Invoke-ProbeRemoval $badReceipt -Apply).Status -eq 'Rejected')
$badReceipt=$receipt.PSObject.Copy();$badReceipt.Root=$workspace
Check 'worker refuses nonfixture root' ((Invoke-ProbeRemoval $badReceipt -Apply).Status -eq 'Rejected')
$badReceipt=$receipt.PSObject.Copy();$badReceipt.PairSHA256=''
Check 'missing package evidence rejected' ((Invoke-ProbeRemoval $badReceipt -Apply).Status -eq 'Rejected')
CopyPayload $b $a $script:BootstrapRelative
Check 'altered evidence package rejected' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected')
# Restore A from the still valid installed fixture; no plugin is executed.
CopyPayload $live $a $script:BootstrapRelative
Remove-Item -LiteralPath (Get-PairPath $live $script:RuntimeRelative)
Check 'orphan is inactive' (-not (Decision $live $pa).Active)
WriteText (Join-Path $live 'probe-lifecycle-state.json') '{}'
Check 'orphan without current ownership proof preserved' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Rejected')
State $live @()
Check 'orphan removed with retained valid package and ownership proof' ((Invoke-ProbeRemoval $receipt -Apply).Status -eq 'Removed')
Install $a $live;State $live @()
# Real junction, including replacement AFTER planning. Targets remain as evidence.
$linked=Join-Path $root 'linked';Install $a $linked;State $linked
$linkReceipt=New-ProbeRemovalReceipt $linked $a;State $linked @()
$patchers=Join-Path $linked 'BepInEx/patchers';$saved=Join-Path $linked 'saved-patchers'
if(-not ([IO.Path]::GetFullPath($patchers)).StartsWith($root+'\') -or -not ([IO.Path]::GetFullPath($saved)).StartsWith($root+'\')){throw 'Unsafe fixture move'}
Move-Item -LiteralPath $patchers -Destination $saved
$null=New-Item -ItemType Junction -Path $patchers -Target $saved
Check 'post-plan reparse replacement preserved' ((Invoke-ProbeRemoval $linkReceipt -Apply).Status -eq 'Rejected' -and [IO.File]::Exists((Join-Path $saved 'DependencyBootstrapProbe.Patcher.dll')))
Reject 'linked activation path rejected' {Get-PairPath $linked $script:BootstrapRelative}
Write-Host "PASS: $($results.Count) actual package/removal checks. Report: $EvidenceRoot"
} catch {
    $failure=$_.ToString()
    throw
} finally {
    $sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../package') -File)+@(Get-Item -LiteralPath $PSCommandPath)
    $sourceEvidence=@(foreach($file in $sourceFiles){[ordered]@{File=$file.Name;SHA256=Get-ProbeHash $file.FullName}})
    try {
        $report=[ordered]@{Schema=1;Utc=[DateTime]::UtcNow.ToString('o');Passed=($null -eq $failure);Failure=$failure;Cases=@($results.ToArray());Sources=$sourceEvidence;Limits='Offline fixtures; no Steam/Extender ownership or live updater proof. Assemblies are opaque data, never executed.'}
        [IO.File]::WriteAllText((Join-Path $EvidenceRoot 'result.json'),($report|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    } finally {
        $env:TEMP=$previousTemp;$env:TMP=$previousTmp
        Remove-FixtureTree $root
    }
}