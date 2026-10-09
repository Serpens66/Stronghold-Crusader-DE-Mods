$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$env:SHCDE_GAME_DIR = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$drivers = @('BugfixesAndQoL\build.bat','Testmods\AIBuildDiagnoseTest\build.bat','Testmods\AICoarsePathComponentFixTest\build.bat','Testmods\MoatMove\build.bat')
$results = [Collections.Generic.List[object]]::new()
Set-Location -LiteralPath ([IO.Path]::GetTempPath())
foreach ($relative in $drivers) {
    $start = [DateTime]::UtcNow
    $log = Join-Path $PSScriptRoot ($relative.Replace('\','_').Replace('.bat','') + '-retry.log')
    Write-Output ('[{0:o}] RETRY {1}' -f $start,$relative)
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before retry.' }
    & (Join-Path $root 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') *> ($log + '.preflight')
    if (-not $?) { throw 'Workspace preflight failed.' }
    if ($relative -eq 'BugfixesAndQoL\build.bat') {
        & (Join-Path $root 'Shared/Tools/Validation/Test-UnitCommandSplit.ps1') *>> ($log + '.preflight')
        if (-not $?) { throw 'Real assembly preflight failed.' }
    }
    & (Join-Path $root $relative) /nopause *> $log
    $code = $LASTEXITCODE
    $results.Add([pscustomobject]@{Driver=$relative;Status=if ($code) {'Failed'} else {'Built'};ExitCode=$code;StartedUtc=$start.ToString('o');FinishedUtc=[DateTime]::UtcNow.ToString('o');Seconds=[math]::Round(([DateTime]::UtcNow-$start).TotalSeconds,1);Log=$log})
    $json = ($results.ToArray() | ConvertTo-Json -Depth 5).Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n"
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'retry-results.json'),$json,[Text.UTF8Encoding]::new($false))
    Write-Output ('[{0:o}] END RETRY {1}: exit={2}' -f [DateTime]::UtcNow,$relative,$code)
    if ($code) { Get-Content -LiteralPath $log -Tail 14; exit 1 }
}
