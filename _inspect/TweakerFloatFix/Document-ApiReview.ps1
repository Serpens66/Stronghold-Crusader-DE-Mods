$ErrorActionPreference = 'Stop'
$review = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
$path = Join-Path $review 'docs/CONFIGURATION_API.md'
$text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
$text = $text.Replace('is the own-file snapshot captured at startup. It is not a live native-memory dump and does not claim to describe a later received host configuration.', 'is the selected personal or temporary file snapshot captured at startup; `ContextId` is empty for personal files. It is not a live native-memory dump and does not claim to describe a later legacy host application.')
$text = $text.Replace('The optional `Changed` event reports API status changes on the calling thread.', 'The optional `Changed` event reports successful API status changes on the calling thread, after the internal configuration lock has been released. Exceptions from one subscriber are logged without preventing other subscribers from running.')
$text += @'

## Temporary startup configurations

`StageContextConfiguration(values, contextId, expectedOwnRevision)` prepares a complete independent startup file set under `Session/`. The opaque context identity must be nonblank and at most 1,024 characters. The personal files remain byte-for-byte unchanged. Loaders, CFG bindings and the outgoing host package use the selected startup set after restarting.

`StageReturnToOwnConfiguration()` freshly reads and validates the personal files and stages their selection without reformatting them. A rejected return leaves an existing pending package intact. `DiscardPendingConfiguration()` only cancels a not-yet-started change; it does not undo a configuration already loaded at startup.

The consumer owns context matching, explicit confirmation, mission start guards and return-to-personal decisions. A selected context persists across process restarts until a return or another configuration is applied; removing a consumer does not silently rewrite files or change that selection. Both file sets and the context marker participate in one transaction. The 16 MiB package limit includes all before/after contents of both sets, names and checksum; an individual file may not exceed 8 MiB. These limits differ from a consumer's preset-document limit.

## Opt-in restart-managed multiplayer

Without opt-in, the existing Tweaker host synchronization keeps its original behavior. API availability alone does not enable the restart workflow.

After registering its confirmation UI and persistent start guards, a consumer calls `EnableRestartManagedSynchronization()`. This process-lifetime operation is idempotent and does not stage or apply settings. Call it on the game/UI thread before any legacy host application; a late call fails with `InvalidOperationException` requiring a restart. Even a failed legacy application invalidates the startup evidence. There is no mid-session opt-out.

Once enabled, received host settings are validated and compared with the startup snapshot instead of being accepted as newly active. `IsNetworkConfigurationClient()` identifies a client. On explicit confirmation, `PrepareNetworkConfiguration()` returns true only when the required settings already match; otherwise it stages an isolated context and returns false. The consumer must prevent starting, explain the restart, and check again after rejoining. Missing or invalid host data raises an exception. Receiving data alone never stages anything. Host synchronization disabled by the owner prepares a return to personal settings when necessary. Local diagnostic options remain personal.

The existing authenticated Tweaker transport remains responsible for host identity and payloads. No consumer assembly, preset format or mission identifier is known to the Tweaker. Consumers must keep guards active even when their settings page is unopened or fails to attach, and must not treat the working copy or a failed opt-in as evidence of active game settings.

## Additional production-catalog verification

The default test run uses small isolated document fixtures. It also covers notification reentrancy, invalid personal returns, the original package migration and the production opt-in policy. To additionally test the **built production assembly's** real unit/structure registries, validator binding and a complete installed catalog, supply:

```powershell
& .\Tests\ConfigurationApi\bin\StatsTweakerApiTests.exe --runtime <Tweaker-DLL> <game-directory> <configuration-directory>
```

This optional test only reads the supplied configuration directory; output is confined to a new temporary test directory. It does not instantiate the plugin or call native getters/setters. The supplied files are a value/coverage fixture, not an independent proof of generator defaults. Real generation, standalone multiplayer, consumer-managed rejoin and startup behavior still require ingame acceptance.

The culture-independent TOML float formatting fix is submitted separately. Until it is merged, apply that fix before running generator/migration checks under cultures such as `de-DE`; it is intentionally not duplicated in this API change.
'@
$text = $text.Replace("`n", "`r`n")
[IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
if ([IO.File]::ReadAllText($path) -cne $text) { throw 'Documentation write mismatch' }
# Local-only driver. The clean review build deliberately uses upstream's official build script.
$driver = @'
@echo off
setlocal
set "PSModulePath=%SystemRoot%\System32\WindowsPowerShell\v1.0\Modules"
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" -Configuration Release -GamePath "E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "TASK_RESULT=%ERRORLEVEL%"
if /i not "%~1"=="/nopause" pause
exit /b %TASK_RESULT%
'@
[IO.File]::WriteAllText((Join-Path $review 'build.bat'), $driver.Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
# Normalize only this task's touched files, retaining the upstream BOM where one existed.
$files = @(& git -C $review diff --name-only) + @(& git -C $review ls-files --others --exclude-standard)
foreach ($relative in ($files | Sort-Object -Unique)) {
    if ([IO.Path]::GetExtension($relative) -notin @('.cs','.csproj','.md','.bat')) { continue }
    $file = Join-Path $review $relative
    $content = [IO.File]::ReadAllText($file).Replace("`r`n", "`n").Replace("`n", "`r`n")
    $upstream = @(& git -C $review ls-tree upstream/main -- $relative)
    $bom = $false
    if ($upstream.Count -ne 0) {
        $first = (& git -C $review show ('upstream/main:' + $relative) | Select-Object -First 1)
        $bom = $first.Length -gt 0 -and [int]$first[0] -eq 0xFEFF
    }
    [IO.File]::WriteAllText($file, $content, [Text.UTF8Encoding]::new($bom))
    if ([IO.File]::ReadAllText($file) -cne $content -or $content -match "(?<!`r)`n") { throw "CRLF verification failed: $relative" }
}
