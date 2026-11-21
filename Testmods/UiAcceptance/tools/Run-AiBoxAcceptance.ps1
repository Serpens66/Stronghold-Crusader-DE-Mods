[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('de-DE','en-US')][string]$Locale,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
. (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.Transaction.ps1')
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$plugins = Join-Path $game 'BepInEx/plugins'
$processName = 'Stronghold Crusader Definitive Edition'
if (Get-Process -Name $processName -ErrorAction SilentlyContinue) { throw 'Game must be closed before package snapshots.' }
$destinations = @(Get-ChildItem -LiteralPath $plugins -Recurse -Filter 'Spectator.dll')
if ($destinations.Count -ne 1 -or $destinations[0].DirectoryName -ine (Join-Path $plugins 'Spectator_Serp')) {
    throw 'Exactly one active Spectator_Serp package is required; resolve duplicate/nested packages first.'
}
$output = New-ShortWorkDirectory -Root $workspace -Kind a -Name 'ai-box-game' -Identity $Label
$backup = Join-Path $output 'rollback'
[void][IO.Directory]::CreateDirectory($backup)
$originalPlugins = Get-InGameTestTree $plugins
$originalPatchers = Get-InGameTestTree (Join-Path $game 'BepInEx/patchers')
$snapshots = [Collections.Generic.List[object]]::new()
$installed = [ordered]@{}
$completed = $false
try {
    foreach ($name in 'Spectator_Serp','UiAcceptance_Serp') {
        $target = Join-Path $plugins $name
        $source = if ($name -eq 'Spectator_Serp') { Join-Path $workspace 'Spectator/BepInEx/plugins/Spectator_Serp' }
            else { Join-Path $workspace 'Testmods/UiAcceptance/BepInEx/plugins/UiAcceptance_Serp' }
        $snapshots.Add((Save-InGameTestState $target (Join-Path $backup $name) -Directory))
        $inventory = Get-InGameTestTree $source
        Restore-InGameTestState ([pscustomobject]@{ Path=$target; Backup=$source; Directory=$true; Inventory=$inventory })
        $installed[$name] = $inventory.Files
    }
    foreach ($relative in 'UiAcceptance_Serp.cfg','UiAcceptance/SP-ai-box.json') {
        $target = Join-Path $game ('BepInEx/config/' + $relative)
        $snapshots.Add((Save-InGameTestState $target (Join-Path $backup ([IO.Path]::GetFileName($relative)))))
    }
    $installed['APIShared.dll'] = Get-ApiRepositoryHash (Join-Path $plugins 'APIShared_Serp/APIShared.dll')
    [IO.File]::WriteAllText((Join-Path $output 'installed.json'), ($installed | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    Write-Host ('[{0:HH:mm:ss.fff}] AI box run {1}, locale {2}, evidence {3}' -f [DateTime]::Now,$Label,$Locale,$output)
    & (Join-Path $PSScriptRoot 'Run-Acceptance.ps1') -Scenario 'SP-ai-box.json' -OutputDirectory $output -Label $Label -Locale $Locale -Resolution '1920x1080' -TimeoutSeconds 420 -StallSeconds 100 |
        Tee-Object -FilePath (Join-Path $output 'driver.log')
    $completed = $true
}
finally {
    if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
        throw "Game is still running; package restoration withheld. Preserve recovery data: $backup"
    }
    for ($i=$snapshots.Count-1; $i -ge 0; $i--) { Restore-InGameTestState $snapshots[$i] }
    Assert-InGameTestTree $plugins $originalPlugins
    Assert-InGameTestTree (Join-Path $game 'BepInEx/patchers') $originalPatchers
    $proof = [ordered]@{ ObservedUtc=[DateTime]::UtcNow.ToString('o'); Label=$Label; Locale=$Locale;
        ScenarioCompleted=$completed; PluginInventoryRestored=$true; PatcherInventoryUnchanged=$true;
        GameClosed=$true; Evidence=$output; Installed=$installed }
    [IO.File]::WriteAllText((Join-Path $output 'result.json'), ($proof | ConvertTo-Json -Depth 7), [Text.UTF8Encoding]::new($false))
    # The verified installed originals are restored. Only this attempt's disposable backup goes.
    Remove-InGameTestTree $backup $output
    Write-Host ('[{0:HH:mm:ss.fff}] Plugin/patcher restoration verified; evidence {1}' -f [DateTime]::Now,$output)
}
