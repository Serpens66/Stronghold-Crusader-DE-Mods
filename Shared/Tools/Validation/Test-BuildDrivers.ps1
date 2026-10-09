[CmdletBinding()]
param([string]$Workspace)
$ErrorActionPreference = 'Stop'
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
$drivers = @(& git -C $Workspace ls-files -- '**/build.bat' | Where-Object { $_ -notmatch '^(_inspect|shcde-script-extender)/' })
$drivers += 'APIShared/build.bat'
$drivers = @($drivers | Sort-Object -Unique)
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('SHCDE Build Drivers ' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
$previousDirectory = (Get-Location).Path
$cases = 0
try {
    Set-Location -LiteralPath $fixtureRoot
    foreach ($relative in $drivers) {
        $source = Join-Path $Workspace $relative
        $text = [IO.File]::ReadAllText($source)
        if ($text -match '(?<!\r)\n') { throw "Bare LF: $relative" }
        $split = $text.IndexOf(":build_driver_main`r`n", [StringComparison]::Ordinal)
        if ($split -lt 0) { throw "Missing entrypoint: $relative" }
        $prefix = $text.Substring(0, $split + ":build_driver_main`r`n".Length)
        $fixture = Join-Path $fixtureRoot ($relative.Replace('/','_').Replace('\','_'))
        # Exercise the real batch control flow with an isolated external boundary.
        # No compiler, package copier or game process is reached by these fixtures.
        foreach ($code in @(0,27)) {
            $body = "setlocal EnableDelayedExpansion`r`necho FIXTURE_CWD=%CD%`r`necho FIXTURE_ARGS=%*`r`nexit /b $code`r`n"
            [IO.File]::WriteAllText($fixture,$prefix + $body,[Text.UTF8Encoding]::new($false))
            foreach ($arguments in @(@('/nopause'),@('/noinstall','/nopause'))) {
                $output = @(& $fixture @arguments 2>&1)
                $exitCode = $LASTEXITCODE
                $cases++
                if ($exitCode -ne $code -or $output.Count -lt 3 -or [string]$output[0] -notmatch 'START ' -or -not ($output -match "END: exit code $code")) { throw "Batch control flow failed: $relative ($code)" }
                if (-not ($output -match ('FIXTURE_CWD=' + [regex]::Escape($fixtureRoot))) -or (Get-Location).Path -ne $fixtureRoot) { throw "Working-directory contract failed: $relative" }
                if (-not ($output -match 'FIXTURE_ARGS=.*?/nopause')) { throw "NoPause not forwarded: $relative" }
                if ($arguments -contains '/noinstall' -and -not ($output -match 'FIXTURE_ARGS=.*?/noinstall')) { throw "NoInstall lost: $relative" }
            }
        }
        # The first real external check must fail before subsequent build steps.
        $body = $text.Substring($split + ":build_driver_main`r`n".Length)
        $firstCheck = [regex]::Match($body, '(?m)^powershell\.exe[^\r\n]*\r\nif errorlevel 1 exit /b 1')
        if ($firstCheck.Success) {
            $body = $body.Substring(0,$firstCheck.Index) + 'powershell.exe -NoProfile -Command "exit 27"' + "`r`nif errorlevel 1 exit /b 1`r`necho ERROR_UNREACHABLE`r`nexit /b 99`r`n"
            [IO.File]::WriteAllText($fixture,$prefix + $body,[Text.UTF8Encoding]::new($false))
            $output = @(& $fixture /nopause 2>&1)
            $cases++
            if ($LASTEXITCODE -ne 1 -or $output -match 'ERROR_UNREACHABLE' -or -not ($output -match 'END: exit code 1')) { throw "Early preflight failure not propagated: $relative" }
        }
    }
    foreach ($code in @(0,27)) {
        $fixture = Join-Path $fixtureRoot 'interactive build.bat'
        [IO.File]::WriteAllText($fixture,$prefix + "exit /b $code`r`n",[Text.UTF8Encoding]::new($false))
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = 'powershell.exe'
        $quotedFixture = $fixture.Replace("'", "''")
        $startInfo.Arguments = '-NoProfile -Command "& ''' + $quotedFixture + '''; exit $LASTEXITCODE"'
        $startInfo.WorkingDirectory = $fixtureRoot
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardInput = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        try {
            $null = $process.Start()
            $first = $process.StandardOutput.ReadLine()
            if ($first -notmatch 'START ') { throw 'Interactive fixture does not show its start immediately.' }
            do { $line = $process.StandardOutput.ReadLine() } while ($null -ne $line -and $line -notmatch 'END: exit code')
            if ($line -notmatch "END: exit code $code" -or $process.WaitForExit(250)) { throw 'Interactive build did not wait for acknowledgement.' }
            $process.StandardInput.WriteLine('')
            if (-not $process.WaitForExit(10000) -or $process.ExitCode -ne $code) { throw 'Interactive acknowledgement lost the exit code.' }
            $cases++
        } finally {
            if (-not $process.HasExited) { $process.Kill() }
            $process.Dispose()
        }
    }
    Write-Host "PASS: $($drivers.Count) build drivers; $cases isolated success/error, argument, working-directory and interactive-pause cases."
    Write-Host "Fixture evidence: $fixtureRoot"
} finally { Set-Location -LiteralPath $previousDirectory }
