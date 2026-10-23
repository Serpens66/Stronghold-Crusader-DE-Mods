[CmdletBinding()]
param([string]$Workspace, [string[]]$ModDirectories)
$ErrorActionPreference = 'Stop'
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
$drivers = @(& git -C $Workspace ls-files -- '**/build.bat' | Where-Object { $_ -notmatch '^(_inspect|shcde-script-extender)/' })
$drivers += 'APIShared/build.bat'
$drivers = @($drivers | Sort-Object -Unique)
if ($ModDirectories) {
    $selected = @($ModDirectories | ForEach-Object { $_.Replace('\','/').TrimEnd('/') + '/build.bat' })
    $drivers = @($drivers | Where-Object { $_ -in $selected })
    if (@($selected | Where-Object { $_ -notin $drivers }).Count) { throw 'A selected build driver was not found.' }
}
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixtureRoot = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'BuildDrivers'
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
$previousDirectory = (Get-Location).Path
$cases = 0
$standardDrivers = 0
try {
    Set-Location -LiteralPath $fixtureRoot
    foreach ($relative in $drivers) {
        $source = Join-Path $Workspace $relative
        $text = [IO.File]::ReadAllText($source)
        if ($text -match '(?<!\r)\n') { throw "Bare LF: $relative" }
        $dispatchEnd = $text.IndexOf(":shcde_branch_selected`r`nendlocal`r`n", [StringComparison]::Ordinal)
        if ($dispatchEnd -ge 0) { $text = $text.Substring($dispatchEnd + ":shcde_branch_selected`r`nendlocal`r`n".Length) }
        $split = $text.IndexOf(":build_driver_main`r`n", [StringComparison]::Ordinal)
        # Bespoke drivers are exercised by BranchBuild/Test-BranchBuild.ps1.
        if ($split -lt 0) { continue }
        $standardDrivers++
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
        # Both equivalent positive-failure forms are supported; preserve the real
        # gate rather than replacing it with the fixture's own implementation.
        $firstCheck = [regex]::Match($body, '(?m)^powershell\.exe[^\r\n]*\r\n(?<gate>if (?:errorlevel 1|not "%ERRORLEVEL%"=="0") exit /b 1)')
        if ($firstCheck.Success) {
            $gate = $firstCheck.Groups['gate'].Value
            $failureCodes = @(27)
            if ($gate.StartsWith('if not ')) { $failureCodes += -1073741819 }
            foreach ($failureCode in $failureCodes) {
                $failureBody = $body.Substring(0,$firstCheck.Index) + 'powershell.exe -NoProfile -Command "exit ' + $failureCode + '"' + "`r`n" + $gate + "`r`necho ERROR_UNREACHABLE`r`nexit /b 99`r`n"
                [IO.File]::WriteAllText($fixture,$prefix + $failureBody,[Text.UTF8Encoding]::new($false))
                $output = @(& $fixture /nopause 2>&1)
                $cases++
                if ($LASTEXITCODE -ne 1 -or $output -match 'ERROR_UNREACHABLE' -or -not ($output -match 'END: exit code 1')) { throw "Early preflight failure not propagated: $relative ($failureCode)" }
            }
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
        # Windows PowerShell 5.1 (.NET Framework) creates the redirected stdin writer with
        # Console.InputEncoding and flushes its preamble at once. A UTF-8 BOM would reach the
        # fixture as input and end its pause before the acknowledgement this test sends.
        $previousInputEncoding = [Console]::InputEncoding
        try {
            [Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
            try { $null = $process.Start() }
            finally { [Console]::InputEncoding = $previousInputEncoding }
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
    Write-Host "PASS: $standardDrivers standard build drivers; $cases isolated success/error, argument, working-directory and interactive-pause cases."
    Write-Host "Fixture evidence: $fixtureRoot"
} finally { Set-Location -LiteralPath $previousDirectory }
