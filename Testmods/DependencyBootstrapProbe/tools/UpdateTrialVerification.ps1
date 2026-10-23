function Get-TrialStagedFiles([string]$Directory){
    Assert-ContinuationUnlinked $Directory
    if(Test-Path -LiteralPath $Directory){foreach($entry in Get-ChildItem -LiteralPath $Directory -Force){Assert-ContinuationUnlinked $entry.FullName;if($entry.PSIsContainer){Get-TrialStagedFiles $entry.FullName}else{$entry}}}
}
function Assert-TrialStaging([string]$Directory,$Payload,$Observed){
    $allowed=@($Payload.Path)+@('_SE/DependencyBootstrapProbeLocalTrial.json')
    $actual=@(Get-TrialStagedFiles $Directory)
    $paths=@($actual|ForEach-Object {$_.FullName.Substring($Directory.Length+1).Replace('\','/')})
    if((@($paths|Sort-Object)-join '|') -cne (@($allowed|Sort-Object)-join '|')){throw 'Staging contains missing/unexpected files; do not approve.'}
    if((@($Observed.Path|Sort-Object)-join '|') -cne (@($allowed|Sort-Object)-join '|')){throw 'Reported staging differs.'}
    foreach($row in $Payload){if((Get-ContinuationHash (Join-Path $Directory $row.Path)) -cne $row.SHA256){throw 'Staged B bytes differ.'}}
    foreach($row in $Observed){if((Get-ContinuationHash (Join-Path $Directory $row.Path)) -cne $row.SHA256){throw 'Staging changed after observation.'}}
}
