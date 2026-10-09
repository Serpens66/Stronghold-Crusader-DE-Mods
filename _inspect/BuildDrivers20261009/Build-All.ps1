$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$baseline = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'baseline.json') | ConvertFrom-Json
$inventory = Get-Content -Raw -LiteralPath (Join-Path $root '_inspect/APISharedOwnership/final-consumer-inventory.json') | ConvertFrom-Json
$apiConsumers = @($inventory.Driver | ForEach-Object { $_.Replace('/','\') })
$order = @('Helpers\MapParser\build.bat','Helpers\AIVParser\build.bat','AIVPlacement\build.bat','APIShared\build.bat','ExtendedData\build.bat','SerpsModsHost\build.bat','BugfixesAndQoL\build.bat')
$drivers = @($baseline.Drivers | Sort-Object @{Expression={ $position = [Array]::IndexOf($order, $_); if ($position -ge 0) {$position} elseif ($_ -eq 'Testmods\MoatMove\build.bat') {99} elseif ($_ -eq 'Helpers\AtlasBuilder\build.bat') {98} else {50} }}, @{Expression={$_}})
$results = [Collections.Generic.List[object]]::new()
$failed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$env:SHCDE_GAME_DIR = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$env:ATLAS_BUILDER_PYTHON = 'C:\Users\Serpens66\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
function Get-SourceFingerprint([string]$Directory) {
    $files = @(Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|BepInEx|dist|build|\.deps|\.git|\.local|__pycache__)[\\/]' -and $_.Extension -in @('.cs','.csproj','.props','.targets','.json','.xaml','.bat','.ps1','.py','.spec')
    } | Sort-Object FullName)
    return (@($files | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash }) -join "`n")
}
Set-Location -LiteralPath ([IO.Path]::GetTempPath())
$index = 0
foreach ($relative in $drivers) {
    $index++
    $start = [DateTime]::UtcNow
    $driver = Join-Path $root $relative
    $name = $relative.Replace('\','_').Replace('/','_').Replace('.bat','')
    $log = Join-Path $PSScriptRoot ($name + '.log')
    $blocked = ($apiConsumers -contains $relative -and $failed.Contains('APIShared\build.bat')) -or ($relative -in @('Testmods\MoatMove\build.bat','Testmods\AIBuildDiagnoseTest\build.bat','Testmods\AICoarsePathComponentFixTest\build.bat') -and $failed.Contains('BugfixesAndQoL\build.bat')) -or ($relative -eq 'CastlePlanner\build.bat' -and ($failed.Contains('Helpers\MapParser\build.bat') -or $failed.Contains('Helpers\AIVParser\build.bat') -or $failed.Contains('AIVPlacement\build.bat'))) -or ($relative -eq 'Helpers\TrailEditor\build.bat' -and $failed.Contains('Helpers\MapParser\build.bat'))
    Write-Output ('[{0:o}] START {1}/{2}: {3}' -f $start,$index,$drivers.Count,$relative)
    $reason = ''
    $status = 'Built'
    if ($blocked) {
        $code = 2; $status = 'Blocked'; $reason = 'Required provider failed; build not started.'
        [IO.File]::WriteAllText($log,$reason + "`r`n")
    } else {
        try {
            if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Game is running; installation blocked.' }
            $modRoot = Split-Path -Parent $driver
            $before = Get-SourceFingerprint $modRoot
            $sharedBefore = Get-SourceFingerprint (Join-Path $root 'Shared/Runtime')
            # The workspace hard gates apply even to runtime drivers with their own preflight.
            if ($relative -ne 'APIShared\build.bat' -and $relative -notmatch '^(AIVPlacement|Helpers\\(?:AIVParser|MapParser|TrailEditor|AtlasBuilder))\\') {
                & (Join-Path $root 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') *> ($log + '.preflight')
                if (-not $?) { throw 'Workspace runtime preflight failed.' }
            }
            & $driver /nopause *> $log
            $code = $LASTEXITCODE
            if ($code -ne 0) { $status = 'Failed'; $reason = 'Build driver returned nonzero.' }
            if ((Get-SourceFingerprint $modRoot) -cne $before -or (Get-SourceFingerprint (Join-Path $root 'Shared/Runtime')) -cne $sharedBefore) {
                $code = 1; $status = 'ChangedInputs'; $reason = 'Source inputs changed during build; verify and rebuild.'
            }
        } catch {
            $code = 1; $status = 'Failed'; $reason = $_.Exception.Message
            [IO.File]::AppendAllText($log,"`r`nERROR: $reason`r`n")
        }
    }
    if ($code -ne 0) { $null = $failed.Add($relative) }
    $row = [pscustomobject]@{Driver=$relative;Status=$status;ExitCode=$code;Reason=$reason;StartedUtc=$start.ToString('o');FinishedUtc=[DateTime]::UtcNow.ToString('o');Seconds=[math]::Round(([DateTime]::UtcNow-$start).TotalSeconds,1);Log=$log}
    $results.Add($row)
    $json = ($results.ToArray() | ConvertTo-Json -Depth 5).Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n"
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'build-results.json'),$json,[Text.UTF8Encoding]::new($false))
    Write-Output ('[{0:o}] END {1}/{2}: {3}; {4}; exit={5}; {6}s' -f [DateTime]::UtcNow,$index,$drivers.Count,$relative,$status,$code,$row.Seconds)
    if ($code -ne 0) { Get-Content -LiteralPath $log -Tail 12 }
}
Write-Output ('Completed: {0} built, {1} failed/blocked.' -f @($results | Where-Object ExitCode -EQ 0).Count,$failed.Count)
if ($failed.Count) { exit 1 }
