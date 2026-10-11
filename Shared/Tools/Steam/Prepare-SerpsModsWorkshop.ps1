[CmdletBinding()]
param([Parameter(Mandatory)][string]$HostPackage, [Parameter(Mandatory)][string]$OutputDirectory, [Parameter(Mandatory)][string]$Packager,
    [Parameter(Mandatory)][string]$APISharedVersion, [Parameter(Mandatory)][string]$APISharedWorkshopId, [object[]]$LegacyReleases=@())
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'APIShared/tools/WorkshopPackage.ps1')
$info=[IO.File]::ReadAllText((Join-Path $HostPackage 'info.json')) | ConvertFrom-Json
if ($info.GUID -cne 'SerpsMods_Serp') { throw 'Expected the existing SerpsMods host identity.' }
foreach ($legacy in $LegacyReleases) { if ([version]$info.Version -le [version]$legacy.Version) { throw 'First bootstrap and payload versions must be newer than every audited legacy delivery.' } }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh output directory; existing release files will not be merged.' }
$bootstrapSource=$OutputDirectory+'.bootstrap-source'
$bootstrapStage=$OutputDirectory+'.bootstrap-stage'
foreach ($directory in @($OutputDirectory,$bootstrapSource,$bootstrapStage)) {
    for ($part=$directory; $part; $part=[IO.Path]::GetDirectoryName($part)) { if ((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked staging directory.' } }
}
# Retain the old host identity and map basename; only the bootstrap receives an additional folder/GUID.
$bootstrapGuid='SerpsModsWorkshopBootstrap_Serp'
& (Join-Path $workspace 'APIShared/tools/New-WorkshopBootstrap.ps1') -Directory $bootstrapSource -Guid $bootstrapGuid -Name 'Serps Mods' -Version $info.Version -MinimumAPISharedVersion $APISharedVersion -APISharedWorkshopId $APISharedWorkshopId
& (Join-Path $bootstrapSource 'build.bat') /nopause | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Workshop bootstrap build failed.' }
$bootstrapPlugin=Join-Path $bootstrapStage ('BepInEx/plugins/'+$bootstrapGuid)
[void][IO.Directory]::CreateDirectory($bootstrapPlugin)
Copy-Item -LiteralPath (Join-Path $bootstrapSource ($bootstrapGuid+'.dll')) -Destination $bootstrapPlugin
Copy-Item -LiteralPath (Join-Path $bootstrapSource 'info.json') -Destination $bootstrapPlugin
Copy-Item -LiteralPath (Join-Path $bootstrapSource 'info.json') -Destination (Join-Path $bootstrapStage 'info.json')
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$map=Join-Path $OutputDirectory 'SerpsMods.map'
& $Packager -s $bootstrapStage -o $map | Out-Host
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $map)) { throw 'Bootstrap map packaging failed.' }
Set-ApiBootstrapMapPayload -MapPath $map -Stage $bootstrapStage
Assert-ApiWorkshopMap -MapPath $map -Stage $bootstrapStage
$plugins=Join-Path $OutputDirectory 'Payload/BepInEx/plugins'
[void][IO.Directory]::CreateDirectory($plugins)
Copy-Item -LiteralPath $HostPackage -Destination (Join-Path $plugins 'SerpsMods_Serp') -Recurse
if (Test-Path -LiteralPath (Join-Path $plugins 'SerpsMods_Serp/Infrastructure/APIShared_Serp')) { throw 'Payload must not embed APIShared.' }
$dependency=[ordered]@{Guid='APIShared_Serp';MinimumVersion=$APISharedVersion;MaximumVersion='';WorkshopId=$APISharedWorkshopId}
$manifest=& (Join-Path $workspace 'APIShared/tools/Prepare-WorkshopPackage.ps1') -Directory $OutputDirectory -PackageId 'SerpsMods_Serp' -Version $info.Version -MinimumAPISharedVersion $APISharedVersion -MinimumScriptExtenderVersion $info.MinimumScriptExtenderVersion -BootstrapMap 'SerpsMods.map' -BootstrapGuid $bootstrapGuid -BootstrapVersion $info.Version -BootstrapFolder ('BepInEx/plugins/'+$bootstrapGuid) -Dependencies @($dependency) -LegacyReleases $LegacyReleases
return [pscustomobject]@{Directory=$OutputDirectory;MapPath=$map;Manifest=$manifest;BootstrapGuid=$bootstrapGuid;BootstrapVersion=[string]$info.Version}
