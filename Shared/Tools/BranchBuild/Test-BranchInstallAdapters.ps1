[CmdletBinding()]
param([string]$Workspace)
try {
$ErrorActionPreference = 'Stop'
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
. (Join-Path $PSScriptRoot 'BranchBuild.Common.ps1')
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'BranchInstallAdapters' -Transient
[void][IO.Directory]::CreateDirectory($fixture)
$game = Join-Path $fixture 'game'
$cases = 0
$savedInstallSuffix = $env:SHCDE_BRANCH_INSTALL_SUFFIX
$env:SHCDE_BRANCH_INSTALL_SUFFIX = '_dev'
function Assert-Install([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $script:cases++ }
try {
    # Execute the real APIShared destination expressions and its leaf-name guard.
    $apiBuild = [IO.File]::ReadAllText((Join-Path $Workspace 'APIShared/tools/Build.ps1'))
    $lines = @($apiBuild -split '\r?\n' | Where-Object { $_ -match '^    \$(destination = Join-Path|toolTarget = Join-Path)|GetFileName\(\$destination\)' })
    if ($lines.Count -ne 3) { throw 'APIShared installation expressions changed; update the fixture.' }
    $body = ($lines -join "`r`n") + @'

[void][IO.Directory]::CreateDirectory($destination)
[void][IO.Directory]::CreateDirectory($toolTarget)
[IO.File]::WriteAllText((Join-Path $destination 'fixture.txt'),'new plugin')
[IO.File]::WriteAllText((Join-Path $toolTarget 'fixture.txt'),'new publisher')
'@
    $pluginRoot = [IO.Path]::GetFullPath((Join-Path $game 'BepInEx/plugins')) + '\'
    foreach ($path in @('BepInEx/plugins/APIShared_Serp','BepInEx/tools/APISharedWorkshopPublisher')) { Write-BranchText (Join-Path $game ($path+'/fixture.txt')) 'MAIN_UNCHANGED' }
    & ([scriptblock]::Create((Convert-BranchPowerShell $body 'tools/Build.ps1' '_dev')))
    foreach ($path in @('BepInEx/plugins/APIShared_Serp','BepInEx/tools/APISharedWorkshopPublisher')) {
        Assert-Install ([IO.File]::ReadAllText((Join-Path $game ($path+'/fixture.txt'))) -ceq 'MAIN_UNCHANGED') "Main changed: $path"
        Assert-Install (Test-Path -LiteralPath (Join-Path $game ($path+'_dev/fixture.txt'))) "Dev output missing: $path"
    }
    # Startup diagnostic: keep the main root DLL, place the dev DLL in its own folder.
    $batch = @'
@echo off
set "GAME_DIR=FIXTURE_GAME"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
set "PATCHER_NAME=StartupPerformanceDiagnostic.Patcher.dll"
set "GAME_PATCHER=%GAME_DIR%\BepInEx\patchers\%PATCHER_NAME%"
if "%NO_INSTALL%"=="1" exit /b 0
if not exist "%GAME_DIR%\BepInEx\patchers\" mkdir "%GAME_DIR%\BepInEx\patchers"
echo DEV_PATCHER>"%GAME_PATCHER%"
exit /b 0
'@.Replace('FIXTURE_GAME',$game)
    $mainPatcher = Join-Path $game 'BepInEx/patchers/StartupPerformanceDiagnostic.Patcher.dll'
    Write-BranchText $mainPatcher 'MAIN_PATCHER'
    $driver = Join-Path $fixture 'patcher fixture.bat'
    Write-BranchText $driver (Convert-BranchBatch $batch 'Testmods/StartupPerformanceDiagnostic/build.bat' '_dev')
    & $driver
    Assert-Install ($LASTEXITCODE -eq 0) 'Patcher BAT failed'
    Assert-Install ([IO.File]::ReadAllText($mainPatcher) -ceq 'MAIN_PATCHER') 'Main patcher replaced'
    Assert-Install ([IO.File]::ReadAllText((Join-Path $game 'BepInEx/patchers/StartupPerformanceDiagnostic_dev/StartupPerformanceDiagnostic.Patcher.dll')).Trim() -ceq 'DEV_PATCHER') 'Dev patcher missing'
    # Dependency probe: execute the adapted destination expression from its real installer.
    $probe = [IO.File]::ReadAllText((Join-Path $Workspace 'Testmods/DependencyBootstrapProbe/Build.ps1'))
    $adapted = Convert-BranchPowerShell $probe 'Testmods/DependencyBootstrapProbe/Build.ps1' '_dev'
    $targetBlock = [regex]::Match($adapted, '(?s)\$installRelative =.*?\$target = Join-Path \$game \$installRelative').Value
    if (-not $targetBlock) { throw 'Probe installation expression changed.' }
    foreach ($relative in @('BepInEx\plugins\DependencyBootstrapProbe_Serp\info.json','BepInEx\plugins\DependencyBootstrapProbeControl_Serp\info.json','BepInEx\patchers\DependencyBootstrapProbe.Patcher.dll')) {
        Write-BranchText (Join-Path $game $relative) 'MAIN_UNCHANGED'
        . ([scriptblock]::Create($targetBlock))
        Assert-Install ($target -ne (Join-Path $game $relative) -and $target -match '_dev\\') 'Probe destination not isolated'
        Write-BranchText $target 'DEVELOP'
        Assert-Install ([IO.File]::ReadAllText((Join-Path $game $relative)) -ceq 'MAIN_UNCHANGED') 'Probe overwrote main'
    }
    # Execute an existing driver's real cleanup/copy block against fixture folders.
    $costs = [IO.File]::ReadAllText((Join-Path $Workspace 'BuildingCosts/build.bat'))
    $begin = $costs.IndexOf('  if exist "!GAME_PLUGIN_DIR!\" (')
    $end = $costs.IndexOf('echo [%date% %time%] PowerShell checks / build step',$begin)
    if ($begin -lt 0 -or $end -lt $begin) { throw 'BuildingCosts cleanup boundary changed.' }
    $localPackage = Join-Path $fixture 'local package'
    Write-BranchText (Join-Path $localPackage 'package.txt') 'NEW_PACKAGE'
    foreach ($name in @('BuildingCosts_Serp','BuildingCosts_Serp_dev','BuildingCosts','BuildingCosts_dev')) {
        Write-BranchText (Join-Path $game ('BepInEx/plugins/'+$name+'/old.txt')) 'OLD_PACKAGE'
        Write-BranchText (Join-Path $game ('BepInEx/plugins/'+$name+'/LobbyModSettings/player.json')) 'KEEP_SETTINGS'
    }
    $header = "@echo off`r`nsetlocal EnableDelayedExpansion`r`nset `"GAME_DIR=$game`"`r`nset `"LOCAL_PLUGIN_DIR=$localPackage`"`r`n"
    $header += 'set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\BuildingCosts_Serp"' + "`r`n"
    $header += 'set "OLD_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\BuildingCosts"' + "`r`n"
    $cleanup = $header + $costs.Substring($begin,$end-$begin) + "exit /b 0`r`n:copy_failed`r`nexit /b 1`r`n"
    # Every recursive removal in this fixture is confined to the explicitly named fake game.
    if (-not [IO.Path]::GetFullPath($game).StartsWith([IO.Path]::GetFullPath($fixture)+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture game path.' }
    $cleanupDriver = Join-Path $fixture 'cleanup fixture.bat'
    Write-BranchText $cleanupDriver (Convert-BranchBatch $cleanup 'BuildingCosts/build.bat' '_dev')
    & $cleanupDriver
    Assert-Install ($LASTEXITCODE -eq 0) 'Real cleanup/copy fixture failed'
    foreach ($name in @('BuildingCosts_Serp','BuildingCosts')) {
        Assert-Install ([IO.File]::ReadAllText((Join-Path $game ('BepInEx/plugins/'+$name+'/old.txt'))) -ceq 'OLD_PACKAGE') 'Dev cleanup changed main/legacy main'
    }
    foreach ($name in @('BuildingCosts_Serp_dev','BuildingCosts_dev')) {
        Assert-Install (-not (Test-Path -LiteralPath (Join-Path $game ('BepInEx/plugins/'+$name+'/old.txt')))) 'Dev cleanup retained stale packaged file'
        Assert-Install ([IO.File]::ReadAllText((Join-Path $game ('BepInEx/plugins/'+$name+'/LobbyModSettings/player.json'))) -ceq 'KEEP_SETTINGS') 'Dev cleanup removed player settings'
    }
    Assert-Install ([IO.File]::ReadAllText((Join-Path $game 'BepInEx/plugins/BuildingCosts_Serp_dev/package.txt')) -ceq 'NEW_PACKAGE') 'Dev package was not copied'
    # /trace must not overwrite the shared GUID-based main configuration from dev.
    $traceConfig = Join-Path $game 'BepInEx/config/ActiveAIVDetector_Serp.cfg'
    $traceSource = Join-Path $fixture 'trace.cfg'
    Write-BranchText $traceConfig 'MAIN_TRACE_SETTINGS'
    Write-BranchText $traceSource 'OTHER_TRACE_SETTINGS'
    $traceSourceText = [IO.File]::ReadAllText((Join-Path $Workspace 'Helpers/ActiveAIVDetector/build.bat'))
    $traceBlock = [regex]::Match($traceSourceText, '(?s)    rem SHCDE_TRACE_INSTALL_BEGIN\r?\n(.*?)    rem SHCDE_TRACE_INSTALL_END').Groups[1].Value
    if (-not $traceBlock) { throw 'Trace installation block missing.' }
    $traceBatch = "@echo off`r`nsetlocal EnableDelayedExpansion`r`nset `"GAME_DIR=$game`"`r`nset `"CHAT10_TRACE_CONFIG=$traceSource`"`r`n"
    $traceBatch += 'for %%A in (%*) do if /I "%%~A"=="/noinstall" exit /b 0' + "`r`n" + $traceBlock + "`r`nexit /b 0`r`n:copy_failed`r`nexit /b 1`r`n"
    $traceDriver = Join-Path $fixture 'trace fixture.bat'
    Write-BranchText $traceDriver (Convert-BranchBatch $traceBatch 'Helpers/ActiveAIVDetector/build.bat' '_dev')
    & $traceDriver
    Assert-Install ($LASTEXITCODE -eq 1 -and [IO.File]::ReadAllText($traceConfig) -ceq 'MAIN_TRACE_SETTINGS') 'Dev trace replaced main settings'
    Write-BranchText $traceSource 'MAIN_TRACE_SETTINGS'
    $writeTime = [IO.File]::GetLastWriteTimeUtc($traceConfig)
    & $traceDriver
    Assert-Install ($LASTEXITCODE -eq 0 -and [IO.File]::GetLastWriteTimeUtc($traceConfig) -eq $writeTime) 'Identical trace settings were rewritten'
    # Execute the production game-running gates with only process observation doubled.
    # Isolated no-install builds may run beside the game; installers and direct local
    # builds retain their closed-game requirement. These fixtures never touch a real game.
    foreach ($mod in @('ExtraFeatures','ImprovedHunters')) {
        $driverSource = [IO.File]::ReadAllText((Join-Path $Workspace ($mod+'/build.bat')))
        $gateStart = $driverSource.IndexOf('rem Isolated /noinstall builds only write')
        $gateEnd = $driverSource.IndexOf(':build_driver_game_ready', $gateStart)
        # The first occurrence is the GOTO operand; locate the label on its own line.
        $label = [regex]::Match($driverSource.Substring($gateStart), '(?m)^:build_driver_game_ready\r?$')
        if ($gateStart -lt 0 -or -not $label.Success) { throw "$mod game-running guard boundary changed." }
        $gateText = $driverSource.Substring($gateStart, $label.Index+$label.Length)
        $processLine = [regex]::Matches($gateText, '(?m)^powershell\.exe[^\r\n]*Get-Process[^\r\n]*')
        if ($processLine.Count -ne 1) { throw "$mod process-observation contract changed." }
        $gateText = $gateText.Replace($processLine[0].Value, 'call :fixture_game_running >nul 2>&1')
        foreach ($noInstall in @(0,1)) {
            foreach ($isolated in @(0,1)) {
                foreach ($running in @(0,1)) {
                    $header = "@echo off`r`nsetlocal EnableExtensions`r`nset `"NO_INSTALL=$noInstall`"`r`n"
                    $sourceRoot = if ($isolated) { $fixture } else { '' }
                    $header += "set `"SHCDE_BRANCH_SOURCE_ROOT=$sourceRoot`"`r`nset `"SHCDE_BUILD_ISOLATED=$isolated`"`r`n"
                    $header += "set `"FIXTURE_GAME_RUNNING=$running`"`r`n"
                    $guardDriver = Join-Path $fixture ($mod+' running gate.bat')
                    Write-BranchText $guardDriver ($header+$gateText+"`r`nexit /b 0`r`n:fixture_game_running`r`nexit /b %FIXTURE_GAME_RUNNING%`r`n")
                    & $guardDriver
                    $expected = if ($running -and -not ($noInstall -and $isolated)) { 1 } else { 0 }
                    Assert-Install ($LASTEXITCODE -eq $expected) "$mod game-running gate: noinstall=$noInstall isolated=$isolated running=$running"
                }
            }
        }
    }
    # Optional mod installers vary by branch; retired directories are not a build
    # contract. The required APIShared builder was read and exercised above.
    $installers = @('APIShared/tools/Build.ps1','Testmods/OutpostTest/Build-OutpostTest.ps1','Pathfinding/tests/Install-Package.ps1','Testmods/DependencyBootstrapProbe/Build.ps1','Testmods/PlagueHeightTest/verify-installed.ps1','Testmods/CrenelatedWallTest/verify-installed.ps1','ExtraFeatures/tools/Verify-Package.ps1')
    foreach ($relative in $installers | Where-Object { Test-Path -LiteralPath (Join-Path $Workspace $_) -PathType Leaf }) {
        $path = Join-Path $Workspace $relative
        $key = $relative -replace '^APIShared/', ''
        $text = Convert-BranchPowerShell ([IO.File]::ReadAllText($path)) $key '_dev'
        $errors = @(); [void][Management.Automation.Language.Parser]::ParseInput($text,[ref]$null,[ref]$errors)
        Assert-Install ($errors.Count -eq 0) "Adapted PowerShell syntax failed: $relative"
    }
    Assert-Install ($adapted -match 'development-only|uses fixed main installation paths') 'Probe continuation guard missing'
    Assert-Install ($apiBuild -notmatch "throw 'Dependency bootstrap is development-only") 'Obsolete APIShared development installation gate'
    Write-Host "PASS: $cases installer, patcher, publisher, guard and PowerShell syntax checks."
    Write-Host "Fixture evidence retained: $fixture"
} catch { Write-Host "Fixture: $fixture"; throw } finally { $env:SHCDE_BRANCH_INSTALL_SUFFIX = $savedInstallSuffix }

} finally {
    if (Get-Command Complete-TransientWorkDirectories -ErrorAction SilentlyContinue) { Complete-TransientWorkDirectories -Owner $PSCommandPath }
}
