$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Test-RuntimeHealthLog.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$fixture=New-ShortWorkDirectory -Root $workspace -Kind t -Name failure-log
$path=Join-Path $fixture 'fixture.log'
$header='[Message:   BepInEx] BepInEx fixture - Stronghold Crusader Definitive Edition'
[IO.File]::WriteAllLines($path,@($header,'[Info: mod] normal historical message'))
$report=Read-RuntimeHealthLog $path @('mod')
if($report.Runs[0].Status -ne 'NoFailuresLogged' -or $report.Runs[0].GameplayVerified){throw 'Silence must not verify gameplay.'}
[IO.File]::WriteAllLines($path,@($header,'[Warning: mod] [2026-10-18 20:00:00.001] requested capability missing',$header,'[Error: mod] failure'))
$report=Read-RuntimeHealthLog $path
if($report.Runs.Count -ne 2 -or $report.Runs[0].Status -ne 'WarningsDetected' -or $report.Runs[1].Status -ne 'Failed'){throw 'Appended runs/severity classification failed.'}
if($report.Runs[0].FirstTime -ne '2026-10-18 20:00:00.001'){throw 'Own timestamp not read.'}
if((Read-RuntimeHealthLog (Join-Path $fixture 'absent.log')).Status -ne 'Unavailable'){throw 'Unreadable log falsely accepted.'}
Write-Host 'PASS: failure-only offline log parsing; no success marker requirement or implicit gameplay pass.'
