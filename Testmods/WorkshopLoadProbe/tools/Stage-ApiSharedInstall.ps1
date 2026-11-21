# Game test of the real APIShared safe installation without UI clicks: calls APIShared's own
# planning and staging (WorkshopInstallation.Prepare + WorkshopStaging.StagePlan from the built
# APIShared.dll) for a subscribed Workshop item; the real APIShared preloader installs it at the
# next game start. -CrashAt runs the swap now and simulates process death at that step, leaving
# an interrupted swap for the preloader to recover.
param(
    [Parameter(Mandatory)][ulong]$ItemId,
    [Parameter(Mandatory)][ValidateSet('Install', 'Update', 'Reinstall', 'Downgrade', 'Remove')][string]$Action,
    [string]$CrashAt = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
$game = Get-ProbeGame
$api = Join-Path $game 'BepInEx\plugins\APIShared_Serp\APIShared.dll'
$probe = @((Join-Path $game 'BepInEx\core'), (Join-Path $game 'BepInEx\plugins\000shcdese'), (Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed'), (Split-Path $api))
[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $e)
    $name = (New-Object Reflection.AssemblyName($e.Name)).Name + '.dll'
    foreach ($dir in $probe) { $p = Join-Path $dir $name; if (Test-Path -LiteralPath $p) { return [Reflection.Assembly]::UnsafeLoadFrom($p) } }
    return $null
})
$asm = [Reflection.Assembly]::UnsafeLoadFrom($api)
$flags = [Reflection.BindingFlags]'Static,NonPublic,Public'
$installation = $asm.GetType('APIShared.Workshop.WorkshopInstallation', $true)
$staging = $asm.GetType('APIShared.Workshop.WorkshopStaging', $true)
$pathsType = $asm.GetType('APIShared.Workshop.Install.InstallPaths', $true)
$actionType = $asm.GetType('APIShared.Workshop.WorkshopAction', $true)
$source = if ($Action -eq 'Remove') { $null } else { [string](Join-Path (Get-ProbeLibrary) "workshop\content\3024040\$ItemId") }
$plan = $installation.GetMethod('Prepare', $flags).Invoke($null, @('WorkshopLoadProbe', [string]$game, $ItemId, $source, [Enum]::Parse($actionType, $Action)))
Write-Output ("Plan: {0} {1} {2}; files={3}; removed={4}" -f $plan.Action, $plan.PackageId, $plan.Version, $plan.Files.Count, $plan.RemovedFiles.Count)
$plan.RemovedFiles | ForEach-Object { Write-Output "  removes $_" }
$paths = [Activator]::CreateInstance($pathsType, [Reflection.BindingFlags]'Instance,NonPublic,Public', $null, @([string]$game), $null)
$log = [Action[string]] { param($m) Write-Output ("  " + $m) }
# Reflection passes optional parameters explicitly: request = true publishes the request at once.
$staging.GetMethod('StagePlan', $flags).Invoke($null, @($paths, $plan, $log, $true))
if ($CrashAt) {
    # Real process death at the step: no catch block or in-process recovery runs. Start this
    # script as its own process (pwsh -File), since it ends the hosting process.
    $swap = $asm.GetType('APIShared.Workshop.Install.InstallSwap', $true)
    $step = $CrashAt
    $journal = Join-Path $game "_APIShared\I\j\$ItemId.journal"
    $swap.GetProperty('Fault', $flags).SetValue($null, [Action[string]] { param($s) if ($s -eq $step) {
        [Console]::Out.WriteLine("Process ends at $s; journal present: " + (Test-Path -LiteralPath $journal)); [Console]::Out.Flush(); [Environment]::Exit(3) } })
    $swap.GetMethod('Apply', $flags).Invoke($null, @($paths, $ItemId, $log)) | Out-Null
    throw "Step $step was not reached."
} else { Write-Output 'Staged; the next game start applies it.' }
