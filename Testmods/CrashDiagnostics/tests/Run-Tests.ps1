[CmdletBinding()]
param([Parameter(Mandatory)][string]$TestHost,[Parameter(Mandatory)][string]$Package,[Parameter(Mandatory)][string]$OutputDirectory,
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$CollectorTests)
$ErrorActionPreference = 'Stop'
$TestHost = [IO.Path]::GetFullPath($TestHost)
$Package = [IO.Path]::GetFullPath($Package)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dll = Join-Path $Package 'CrashDiagnostics.Native.dll'
$collector = Join-Path $Package 'CrashDiagnostics.Collector.exe'
$layout = ((& $TestHost --layout) -join '') | ConvertFrom-Json
if ($LASTEXITCODE -or $layout.capture -ne 1048576 -or $layout.shared -gt 32MB) { throw 'Native protocol layout mismatch.' }
& $TestHost --chunk-registry
if ($LASTEXITCODE) { throw 'Native chunk lifetime registry regression failed.' }
$results = [Collections.Generic.List[object]]::new()
function Read-Capsules([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ne $layout.shared -or [BitConverter]::ToUInt32($bytes,8) -ne 1) { throw 'Invalid production capture length/version.' }
    $rows = @(for ($i=0; $i -lt 16; $i++) {
        $at = [int]($layout.capturesOffset + $i * $layout.capture)
        $seq = [BitConverter]::ToInt64($bytes,$at)
        $commit = [BitConverter]::ToInt64($bytes,$at+8)
        if ($seq -le 0 -or $seq -ne $commit) { continue }
        $rangeCount = [BitConverter]::ToUInt32($bytes,$at+44)
        $payloadBytes = [BitConverter]::ToUInt32($bytes,$at+48)
        if ($rangeCount -gt 96 -or $payloadBytes -gt ($layout.capture-$layout.captureHeader)) { throw 'Out-of-bounds native capsule.' }
        $rip = [BitConverter]::ToUInt64($bytes,($at+$layout.contextOffset+248))
        $codeFound = $false
        for ($r=0; $r -lt $rangeCount; $r++) {
            $ra = [int]($at+$layout.rangesOffset+$r*40)
            $address = [BitConverter]::ToUInt64($bytes,$ra)
            $copied = [BitConverter]::ToUInt32($bytes,$ra+12)
            $offset = [BitConverter]::ToUInt32($bytes,$ra+16)
            if ([uint64]$offset+$copied -gt $payloadBytes) { throw 'Range outside the production payload.' }
            if ($address -le $rip -and $rip-$address -lt $copied) { $codeFound=$true }
        }
        [pscustomobject]@{Sequence=$seq;Code=[BitConverter]::ToUInt32($bytes,($at+$layout.exceptionOffset));Rip=('0x{0:X}' -f $rip);CodeCaptured=$codeFound;PayloadBytes=$payloadBytes;RangeCount=$rangeCount}
    })
    return $rows
}
$probe=Join-Path $OutputDirectory 'MonoProbe.dll'
$probeSource=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'native/MonoProbe.cs')).Path
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:library "/out:$probe" $probeSource
if ($LASTEXITCODE) { throw 'Managed Mono ABI probe compilation failed.' }
$gameMono=Join-Path $GameDir 'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll'
$unsupportedDirectory=Join-Path $OutputDirectory 'unsupported-runtime'
[void][IO.Directory]::CreateDirectory($unsupportedDirectory)
$unsupportedMono=Join-Path $unsupportedDirectory 'mono-2.0-bdwgc.dll'
$altered=[IO.File]::ReadAllBytes($gameMono)
# Change only the copied PE timestamp; executable code remains the real runtime.
$peOffset=[BitConverter]::ToInt32($altered,60)
$altered[$peOffset+8]=$altered[$peOffset+8] -bxor 1
[IO.File]::WriteAllBytes($unsupportedMono,$altered)
foreach ($mode in @('clean','caught','crash','guard','concurrent','rotate','trail','mono','collectorloss','mono-unsupported')) {
    $session = Join-Path $OutputDirectory $mode
    $arguments = '"'+$dll+'" "'+$collector+'" "'+$session+'" '+$mode
    if ($mode -eq 'mono' -or $mode -eq 'mono-unsupported') {
        $mono=if ($mode -eq 'mono') { $gameMono } else { $unsupportedMono }
        $managed=Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data/Managed'
        $arguments += ' "'+$mono+'" "'+$managed+'" "'+$probe+'"'
    }
    Write-Host ("[{0:O}] Native regression: {1}" -f [DateTime]::UtcNow,$mode)
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName=$TestHost
    $startInfo.Arguments=$arguments
    $startInfo.UseShellExecute=$false
    $startInfo.CreateNoWindow=$true
    $startInfo.RedirectStandardOutput=$true
    $startInfo.RedirectStandardError=$true
    $hostProcess=[Diagnostics.Process]::Start($startInfo)
    $stdout=$hostProcess.StandardOutput.ReadToEndAsync()
    $stderr=$hostProcess.StandardError.ReadToEndAsync()
    if (-not $hostProcess.WaitForExit(20000)) { $hostProcess.Kill(); throw "Native test host timed out: $mode" }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($mode+'.out.txt')),$stdout.Result)
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($mode+'.err.txt')),$stderr.Result)
    if ($mode -eq 'crash') {
        if ($hostProcess.ExitCode -ne -1073741819) { throw "Expected access-violation exit, got $($hostProcess.ExitCode)." }
    } elseif ($hostProcess.ExitCode -ne 0) {
        throw "Native test host $mode failed: $($hostProcess.ExitCode); $([IO.File]::ReadAllText((Join-Path $OutputDirectory ($mode+'.err.txt'))))"
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $report = Join-Path $session 'report.txt'
    while ($mode -ne 'collectorloss' -and -not (Test-Path -LiteralPath $report) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if ($mode -ne 'collectorloss' -and -not (Test-Path -LiteralPath $report)) { throw "Collector did not finalize $mode." }
    $rows = @(Read-Capsules (Join-Path $session 'emergency.bin'))
    if ($mode -eq 'mono') {
        $capture=[IO.File]::ReadAllBytes((Join-Path $session 'emergency.bin'))
        $mappedMethod=$false
        for ($i=0; $i -lt 8192; $i++) {
            $at=[int]($layout.mapsOffset+$i*$layout.map)
            if ([BitConverter]::ToInt64($capture,$at) -le 0 -or [BitConverter]::ToInt64($capture,$at) -ne [BitConverter]::ToInt64($capture,$at+8)) { continue }
            $name=[Text.Encoding]::UTF8.GetString($capture,$at+56,512)
            if ($name.Contains('CrashDiagnostics.Tests.MonoProbe:Compute')) { $mappedMethod=$true; break }
        }
        if (-not $mappedMethod) { throw 'The actual Unity Mono runtime did not publish the invoked method mapping.' }
    }
    if ($mode -eq 'mono-unsupported') {
        $capture=[IO.File]::ReadAllBytes((Join-Path $session 'emergency.bin'))
        # Header.monoState at136 is part of the documented x64 format v1.
        if ([BitConverter]::ToInt32($capture,136) -ne -5) { throw 'Unknown runtime was not rejected by the native hash gate.' }
    }
    $minimum = switch ($mode) { 'caught' {1} 'crash' {1} 'guard' {1} 'concurrent' {1} 'rotate' {16} 'collectorloss' {1} 'mono-unsupported' {1} default {0} }
    if ($rows.Count -lt $minimum) { throw "Missing committed exception data: $mode." }
    if ($minimum -and @($rows | Where-Object { -not $_.CodeCaptured }).Count) { throw "Fault code bytes missing: $mode." }
    $size = (Get-ChildItem -LiteralPath $session -Recurse -File | Measure-Object Length -Sum).Sum
    if ($size -gt 128MB) { throw 'Collector exceeded the session budget.' }
    $reportText = if (Test-Path -LiteralPath $report) { [IO.File]::ReadAllText($report) } else { 'Collector intentionally terminated; raw evidence validated without collector.' }
    if ($mode -eq 'caught' -and -not $reportText.Contains('progress_after_event')) { throw 'Handled exception lost subsequent same-thread progress.' }
    if ($mode -eq 'crash' -and ($reportText.Contains('progress_after_event') -or -not $reportText.Contains('0xc0000005'))) { throw 'Unhandled fault exit classification mismatch.' }
    & $collector --export $session
    if ($LASTEXITCODE) { throw "Collector export failed: $mode" }
    $zipPath=$session+'.zip'
    $python='D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
    & $python (Join-Path $PSScriptRoot 'Verify-Zip.py') $zipPath $mode
    if ($LASTEXITCODE) { throw "ZIP content/CRC validation failed: $mode" }
    $results.Add([ordered]@{Mode=$mode;ExitCode=$hostProcess.ExitCode;CommittedCaptures=$rows.Count;SessionBytes=$size;Capsules=$rows;Report=$reportText})
}
$python='D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
$env:PYTHONDONTWRITEBYTECODE='1'
if ($CollectorTests) {
    & $CollectorTests (Join-Path $OutputDirectory 'collector-tail-regression')
    if ($LASTEXITCODE) { throw 'Collector rolling-tail and final-generation regression failed.' }
}
& $python -m unittest discover -s (Join-Path $mod 'tools') -p 'test_*.py'
if ($LASTEXITCODE) { throw 'Offline parser regression failed.' }
[IO.File]::WriteAllText((Join-Path $mod 'obj/tests.json'),((@{SchemaVersion=1;CompletedUtc=[DateTime]::UtcNow.ToString('o');Result='passed';Cases=$results} | ConvertTo-Json -Depth 9)-replace '\r?\n',"`r`n")+"`r`n",[Text.UTF8Encoding]::new($false))
Write-Host 'PASS: production native capture, chained handler, abrupt exit, concurrent exceptions, rotation, payload bounds, ZIP integrity and offline parser.'
