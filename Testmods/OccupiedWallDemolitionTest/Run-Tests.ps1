[CmdletBinding()]
param(
    [ValidateSet('Both','Minimal','Fixes')][string]$Layout = 'Both',
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$ModPackage = (Join-Path $PSScriptRoot 'BepInEx/plugins/OccupiedWallDemolitionTest_Serp'),
    [ValidateRange(1,86400)][int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
. (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.Transaction.ps1')

$mod = 'OccupiedWallDemolitionTest_Serp'
$game = Get-InGameTestPath $GameDir
$package = Get-InGameTestPath $ModPackage
$plugins = Join-Path $game 'BepInEx/plugins'
$rollback = Join-Path $game 'BepInEx/SerpAutomationRollback'
$bootstrap = Join-Path $game 'BepInEx/patchers/APIShared.DependencyBootstrap.dll'
$profiles = if ($Layout -eq 'Both') { @('Minimal','Fixes') } else { @($Layout) }
$failures = [Collections.Generic.List[string]]::new()
$seriesMutex = [Threading.Mutex]::new($false, 'Local\SHCDE.InGameTest.3024040')
$locked = $false
$work = $null

try {
    # The standard runner acquires the same mutex recursively on this thread.
    try { $locked = $seriesMutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $locked = $true }
    if (-not $locked) { throw 'Another in-game test holds the installation mutex.' }
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) {
        throw 'A game process is already running. The installation was untouched.'
    }
    if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
        throw 'The running Steam session is not visible to this process.'
    }
    if (Test-Path -LiteralPath $rollback) { throw "Existing game-test recovery data: $rollback" }
    Assert-InGameTestMod $package $mod
    Assert-InGameTestApiPair (Join-Path $plugins 'APIShared_Serp') $bootstrap

    $work = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'occupied-wall' -Identity "Wall demolition dependency layouts ($Layout)"
    $evidence = New-ShortWorkDirectory -Root $workspace -Kind a -Name 'occupied-wall' -Identity "Wall demolition comparison ($Layout)"
    Write-Host "Wall demolition evidence: $evidence"
    $api = Join-Path $work 'api'
    $stagedPatcher = Join-Path $work 'APIShared.DependencyBootstrap.dll'
    $installedApi = Join-Path $plugins 'APIShared_Serp'
    Copy-InGameTestTree $installedApi $api (Get-InGameTestTree $installedApi)
    [IO.File]::Copy($bootstrap, $stagedPatcher, $false)
    Assert-InGameTestApiPair $api $stagedPatcher

    foreach ($profile in $profiles) {
        $staged = Join-Path $work $profile
        $profilesPackages = @('000shcdese')
        if ($profile -eq 'Fixes') { $profilesPackages += 'fixes' }
        foreach ($dependency in $profilesPackages) {
            $source = Join-Path $plugins $dependency
            $inventory = Get-InGameTestTree $source
            if (-not $inventory.Exists) { throw "Installed dependency missing: $source" }
            Copy-InGameTestTree $source (Join-Path $staged $dependency) $inventory
        }
        $output = Join-Path $evidence $profile
        $null = [IO.Directory]::CreateDirectory($output)
        if ($profile -eq 'Fixes') {
            $fixesAssembly = Join-Path $staged 'fixes/fixes.dll'
            [IO.File]::WriteAllLines((Join-Path $output 'fixes-identity.txt'), [string[]]@(
                [Reflection.AssemblyName]::GetAssemblyName($fixesAssembly).FullName
                'SHA-256 ' + (Get-ApiRepositoryHash $fixesAssembly)
            ))
        }
        try {
            # This runner owns the plugin layout. Disable APIShared's competing
            # Workshop installer only in the child game's inherited environment.
            $previousLaunchId = [Environment]::GetEnvironmentVariable('SCDEModManagerLaunchId', 'Process')
            try {
                [Environment]::SetEnvironmentVariable('SCDEModManagerLaunchId', "occupied-wall-$profile", 'Process')
                & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') -GameDir $game `
                    -ModGuid $mod -ModPackage $package -PluginLayout $staged -ApiPluginPackage $api `
                    -ApiPatcherPackage $stagedPatcher -Scenario 'occupied-wall-demolition' `
                    -OutputDirectory $output -TimeoutSeconds $TimeoutSeconds -HiddenDesktop
            }
            finally {
                [Environment]::SetEnvironmentVariable('SCDEModManagerLaunchId', $previousLaunchId, 'Process')
            }
        }
        catch {
            $failures.Add("$profile - $($_.Exception.Message)")
            Write-Warning $failures[$failures.Count - 1]
            if (Test-Path -LiteralPath $rollback) { break }
        }
    }
    if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
}
finally {
    try {
        if ($work -and -not (Test-Path -LiteralPath $rollback)) {
            Remove-ShortWorkDirectory -Path $work
        }
    }
    finally {
        if ($locked) { $seriesMutex.ReleaseMutex() }
        $seriesMutex.Dispose()
    }
}
