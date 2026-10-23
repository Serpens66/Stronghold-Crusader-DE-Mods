$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../tools/ProbeContinuation.ps1')
. (Join-Path $PSScriptRoot '../../../Shared/Tools/Paths/WorkPaths.ps1')
$root = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'Continuation'
$game=Join-Path $root 'game';$backup=Join-Path $root 'backup';$probe=Join-Path $root 'probe'
foreach($base in @($game,$backup,$probe)){
    foreach($p in @(Get-ContinuationFiles)+@('BepInEx/plugins/APIShared_Serp/APIShared.dll','BepInEx/core/BepInEx.dll','BepInEx/plugins/000shcdese/SHCDESE.dll')){
        $target=Join-Path $base $p;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
        [IO.File]::WriteAllText($target,'SYNTHETIC ONLY '+$p)
    }
}
$hash=Get-ContinuationHash (Join-Path $game 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll')
foreach($base in @($game,$backup)){foreach($suffix in @('active','correct')){[IO.File]::WriteAllText((Join-Path $base ('BepInEx/config/DependencyBootstrapProbe.'+$suffix)),$hash)}}
$proof=@{Schema=1;Files=@(foreach($p in Get-ContinuationFiles){@{Path=$p;SHA256=Get-ContinuationHash (Join-Path $backup $p)}});APISharedSHA256=Get-ContinuationHash (Join-Path $game 'BepInEx/plugins/APIShared_Serp/APIShared.dll')}
$proof.Loaders=@(foreach($p in @('BepInEx/core/BepInEx.dll','BepInEx/plugins/000shcdese/SHCDESE.dll')){@{Path=$p;SHA256=Get-ContinuationHash (Join-Path $game $p)}})
[IO.File]::WriteAllText((Join-Path $backup 'continuation.json'),($proof|ConvertTo-Json -Depth 5))
Assert-CorrectionBinding $game
$null=Assert-ProbeContinuation $game $backup -BeforeCopy
function RejectChange([string]$path,[scriptblock]$action){
    $bytes=[IO.File]::ReadAllBytes($path);[IO.File]::AppendAllText($path,'changed');$rejected=$false
    try{& $action}catch{$rejected=$true}finally{[IO.File]::WriteAllBytes($path,$bytes)}
    if(-not $rejected){throw "Accepted changed continuation: $path"}
}
RejectChange (Join-Path $game 'BepInEx/config/DependencyBootstrapProbe.correct') {Assert-CorrectionBinding $game}
RejectChange (Join-Path $backup 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll') {Assert-ProbeContinuation $game $backup}
RejectChange (Join-Path $game 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll') {Assert-ProbeContinuation $game $backup -BeforeCopy}
RejectChange (Join-Path $game 'BepInEx/plugins/APIShared_Serp/APIShared.dll') {Assert-ProbeContinuation $game $backup}
RejectChange (Join-Path $game 'BepInEx/plugins/000shcdese/SHCDESE.dll') {Assert-ProbeContinuation $game $backup -BeforeCopy}
RejectChange (Join-Path $game 'BepInEx/plugins/DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll') {Complete-ProbeContinuation $probe $game $backup}
foreach($base in @($game,$probe)){[IO.File]::AppendAllText((Join-Path $base 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll'),'NEW BUILD')}
foreach($base in @($game,$probe)){
    $pair=@{Schema=1;Guid='DependencyBootstrapProbe_Serp';Version='1.0.0';RuntimeSHA256=(Get-ContinuationHash (Join-Path $base 'BepInEx/plugins/DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll')).ToLowerInvariant();BootstrapSHA256=(Get-ContinuationHash (Join-Path $base 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll')).ToLowerInvariant()}
    [IO.File]::WriteAllText((Join-Path $base 'BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json'),($pair|ConvertTo-Json))
}
RejectChange (Join-Path $game 'BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json') {Complete-ProbeContinuation $probe $game $backup}
Complete-ProbeContinuation $probe $game $backup
Assert-CorrectionBinding $game
Write-Host 'PASS: continuation rejects stale switches, backup changes, changed old/new DLLs and APIShared; explicit new binding succeeds.'
