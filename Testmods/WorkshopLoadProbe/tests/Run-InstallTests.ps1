# Offline tests of install/SafeInstall.cs (the safe complete installation prototype):
#   1. deterministic fault injection (InstallHarness selftest)
#   2. real process kills at random moments during the swap and during staging,
#      each followed by the recovery a next game start would run
#   3. timing with the real SerpsMods Workshop item (read-only source)
# Fixtures live in .work/t/installtest/<n> and are removed afterwards (kept on failure).
param([int]$SwapKills = 150, [int]$StageKills = 40, [string]$SerpsModsItem = 'E:\ProgrammeE\Steam\steamapps\workshop\content\3024040\3788821961')
$ErrorActionPreference = 'Stop'
$probe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = [IO.Path]::GetFullPath((Join-Path $probe '..\..'))
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
& dotnet msbuild (Join-Path $PSScriptRoot 'InstallHarness\InstallHarness.csproj') /t:Build /p:Configuration=Release /verbosity:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Harness build failed.' }
$harness = Join-Path $PSScriptRoot 'InstallHarness\bin\InstallHarness.exe'
$root = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'installtest' -Identity 'WorkshopLoadProbe safe install tests'
$failed = 0
function Tree-Remove([string]$Path) { if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force } }
function Fail([string]$Text) { $script:failed++; Write-Output "FAIL $Text" }
function Run([string[]]$Arguments) { $out = & $harness @Arguments 2>&1; if ($LASTEXITCODE -ne 0) { throw ("harness " + ($Arguments -join ' ') + ": " + ($out -join ' ')) }; return ,@($out) }

try {
    # 1. Deterministic scenarios.
    $self = & $harness selftest (Join-Path $root 'self') 2>&1
    $self | Where-Object { $_ -match '^(PASS|FAIL|SELFTEST)' } | ForEach-Object { Write-Output $_ }
    if ($LASTEXITCODE -ne 0) { Fail 'selftest' }

    # 2a. Kill the swap process at random moments (25 ms pause per step widens the window).
    $random = [Random]::new(20261118)
    $outcomes = @{}
    for ($i = 1; $i -le $SwapKills; $i++) {
        $parts = (Run @('prepare', (Join-Path $root 'kill'), "k$i"))[-1].Split('|')
        $target, $staged, $backup, $journal, $oldId, $newId = $parts
        $process = Start-Process -FilePath $harness -ArgumentList @('apply', "`"$target`"", "`"$staged`"", "`"$backup`"", "`"$journal`"", '25') -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds ($random.Next(30, 260))
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() ; $killed = 'killed' } else { $killed = 'finished' }
        $seen = (Run @('tree', $target))[-1]
        if ($seen -notin @('missing', $oldId, $newId)) { Fail "kill $i partial state $seen" }
        $recover = (Run @('recover', $journal))[-1]
        # Last token: the target path itself contains spaces.
        $outcome = if ($recover -match 'NO_JOURNAL') { 'no-journal' } elseif ($recover -match 'RECOVERED ') { ($recover -split ' ')[-1] } else { $recover }
        # Killed before or right after the journal: nothing changed, the request is still pending,
        # and the next start applies it again (like the preloader does).
        if ($outcome -like 'not-started*' -or ($outcome -eq 'no-journal' -and $seen -eq $oldId)) { $null = Run @('apply', $target, $staged, $backup, $journal) }
        $final = (Run @('tree', $target))[-1]
        if ($final -ne $newId -or (Test-Path -LiteralPath $backup) -or (Test-Path -LiteralPath $journal)) { Fail "kill $i final=$final outcome=$outcome" }
        $key = "$killed/$outcome/$(if($seen -eq $newId){'new'}elseif($seen -eq $oldId){'old'}else{$seen})"
        $outcomes[$key] = 1 + [int]$outcomes[$key]
        Tree-Remove (Join-Path $root "kill\k$i")
    }
    Write-Output "Swap kills: $SwapKills"
    $outcomes.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Output ("  {0,-40} {1}" -f $_.Name, $_.Value) }

    # 2b. Kill staging while it copies the real SerpsMods package slowly; the retry must publish a verified copy.
    $stageOutcomes = @{}
    for ($i = 1; $i -le $StageKills; $i++) {
        $parent = Join-Path $root "stagekill\s$i"
        $process = Start-Process -FilePath $harness -ArgumentList @('stage-item', "`"$SerpsModsItem`"", 'BepInEx/plugins/SerpsMods_Serp', "`"$parent`"", 'v1', '1') -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds ($random.Next(50, 1500))
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit(); $killed = 'killed' } else { $killed = 'finished' }
        $published = Test-Path -LiteralPath (Join-Path $parent 'v1\.wlp-complete')
        $partialPublished = (Test-Path -LiteralPath (Join-Path $parent 'v1')) -and -not $published
        if ($partialPublished) { Fail "stage kill $i left an unmarked final folder" }
        $null = Run @('stage-item', $SerpsModsItem, 'BepInEx/plugins/SerpsMods_Serp', $parent, 'v1')
        if (-not (Test-Path -LiteralPath (Join-Path $parent 'v1\.wlp-complete')) -or (Test-Path -LiteralPath (Join-Path $parent 'v1.tmp'))) { Fail "stage kill $i retry incomplete" }
        $key = "$killed/publishedBeforeRetry=$published"
        $stageOutcomes[$key] = 1 + [int]$stageOutcomes[$key]
        Tree-Remove $parent
    }
    Write-Output "Stage kills: $StageKills"
    $stageOutcomes.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Output ("  {0,-40} {1}" -f $_.Name, $_.Value) }

    # 3. Timing with the real package: stage (copy + verify), fresh swap, update swap.
    $timing = Join-Path $root 'timing'
    $target = Join-Path $timing 'plugins\SerpsMods_Serp'
    [void][IO.Directory]::CreateDirectory((Join-Path $timing 'plugins'))
    $out1 = Run @('stage-item', $SerpsModsItem, 'BepInEx/plugins/SerpsMods_Serp', (Join-Path $timing 'cache\s'), 'v1')
    $out2 = Run @('apply', $target, (Join-Path $timing 'cache\s\v1'), (Join-Path $timing 'cache\b'), (Join-Path $timing 'cache\j\p.journal'))
    $out3 = Run @('stage-item', $SerpsModsItem, 'BepInEx/plugins/SerpsMods_Serp', (Join-Path $timing 'cache\s'), 'v2')
    $out4 = Run @('apply', $target, (Join-Path $timing 'cache\s\v2'), (Join-Path $timing 'cache\b'), (Join-Path $timing 'cache\j\p.journal'))
    Write-Output 'Timing (SerpsMods 1.0.38, 936 files, 64 MB):'
    @($out1, $out2, $out3, $out4) | ForEach-Object { $_ | Where-Object { $_ -match 'STAGED|SWAPPED' } } | ForEach-Object { Write-Output "  $_" }
} finally {
    if ($failed -eq 0) { Remove-ShortWorkDirectory $root } else { Write-Output "Fixtures kept: $root" }
}
if ($failed -ne 0) { throw "$failed install test failure(s)." }
Write-Output 'PASS: safe install offline tests.'
