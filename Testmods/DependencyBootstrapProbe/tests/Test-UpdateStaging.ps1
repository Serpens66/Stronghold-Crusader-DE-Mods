$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../tools/ProbeContinuation.ps1')
. (Join-Path $PSScriptRoot '../tools/UpdateTrialVerification.ps1')
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'update-staging-fixture'))
$payload=@('BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll','BepInEx/plugins/DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll','BepInEx/plugins/DependencyBootstrapProbe_Serp/info.json','BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json','BepInEx/config/DependencyBootstrapProbe.active','BepInEx/config/DependencyBootstrapProbe.correct')
$observed=@(foreach($p in $payload+@('_SE/DependencyBootstrapProbeLocalTrial.json')){$file=Join-Path $root $p;[IO.Directory]::CreateDirectory((Split-Path $file))|Out-Null;[IO.File]::WriteAllText($file,'synthetic fixture '+$p);@{Path=$p;SHA256=Get-ContinuationHash $file}})
$expected=@($observed|Where-Object Path -in $payload)
Assert-TrialStaging $root $expected $observed
function Reject([scriptblock]$Action){$rejected=$false;try{& $Action}catch{$rejected=$true};if(-not $rejected){throw 'Unsafe staging accepted'}}
$foreign=Join-Path $root 'foreign.txt';[IO.File]::WriteAllText($foreign,'foreign');try{Reject {Assert-TrialStaging $root $expected $observed}}finally{Remove-Item -LiteralPath $foreign}
$target=Join-Path $root $payload[0];$bytes=[IO.File]::ReadAllBytes($target);[IO.File]::AppendAllText($target,'changed');try{Reject {Assert-TrialStaging $root $expected $observed}}finally{[IO.File]::WriteAllBytes($target,$bytes)}
Reject {Assert-TrialStaging $root $expected @($observed|Select-Object -Skip 1)}
$deletion=Join-Path $root 'delete_list.txt';[IO.File]::WriteAllText($deletion,'foreign');try{Reject {Assert-TrialStaging $root $expected $observed}}finally{Remove-Item -LiteralPath $deletion}
Write-Host 'PASS: actual staging verifier accepts exact payload; rejects foreign/deletion entries, changed bytes and incomplete report.'
