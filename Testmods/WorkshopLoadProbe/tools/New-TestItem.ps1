# Creates a simulated Workshop item (published layout: Payload\ + apishared-workshop.json) from
# the built payload in a new .work/a/workshopload-item/<n> folder.
#   -Variant        1 or 2: variant.txt, shipped settings.json "default-<variant>" and
#                   only-in-variant-<variant>.txt differ, so an update shows replaced and removed files
#   -KeepRevision   reset revision for settings.json in apishared-keep.txt (0 = keep the player's copy)
param([ValidateSet(1, 2)][int]$Variant = 1, [ValidateRange(0, 99)][int]$KeepRevision = 0)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path (Get-ProbeWorkspace) 'Shared\Tools\Paths\WorkPaths.ps1')
$package = Join-Path $PSScriptRoot ('..\payload\package\' + $ProbePayloadGuid)
if (-not (Test-Path -LiteralPath (Join-Path $package 'WorkshopLoadProbe.Payload.dll'))) { throw 'Build the probe first (build.bat /noinstall).' }
$root = New-ShortWorkDirectory -Root (Get-ProbeWorkspace) -Kind a -Name 'workshopload-item' -Identity "WorkshopLoadProbe test item variant $Variant keep $KeepRevision"

$simulated = Join-Path $root 'simulated'
$plugin = Join-Path $simulated ('Payload\BepInEx\plugins\' + $ProbePayloadGuid)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($plugin))
Copy-Item -LiteralPath $package -Destination $plugin -Recurse
Write-ProbeText (Join-Path $plugin 'variant.txt') @("variant=$Variant")
Write-ProbeText (Join-Path $plugin 'settings.json') @("default-$Variant")
Write-ProbeText (Join-Path $plugin "only-in-variant-$Variant.txt") @("variant $Variant")
$settingsLine = if ($KeepRevision -gt 0) { "settings.json $KeepRevision" } else { 'settings.json' }
Write-ProbeText (Join-Path $plugin 'apishared-keep.txt') @('# Player files kept across updates', $settingsLine, 'config/')

$files = @(Get-ChildItem -LiteralPath (Join-Path $simulated 'Payload') -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ Path = $_.FullName.Substring((Join-Path $simulated 'Payload').Length + 1).Replace('\', '/'); Size = $_.Length; Sha256 = Get-ProbeSha256 $_.FullName }
})
$manifest = [ordered]@{ SchemaVersion = 1; PackageId = $ProbePayloadGuid; Version = "0.2.$Variant"; MinimumAPISharedVersion = '0.57.0';
    MinimumScriptExtenderVersion = '2.14.0'; MaximumScriptExtenderVersion = ''; Roots = @("BepInEx/plugins/$ProbePayloadGuid"); Files = $files }
Write-ProbeText (Join-Path $simulated 'apishared-workshop.json') @($manifest | ConvertTo-Json -Depth 5)
Write-Output "Simulated item: $simulated"
