[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SdkRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$PathfindingPackage,
    [switch]$KeepInstalledPlugins,
    [ValidateSet('surface-matrix','surface-groups-no-singles')][string]$Scenario='surface-matrix',
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $PathfindingPackage){$PathfindingPackage=Join-Path $workspace 'Pathfinding/BepInEx/plugins/Pathfinding_Serp'}
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
. (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.Transaction.ps1')
$fixture=New-ShortWorkDirectory -Root $workspace -Kind t -Name pf-surface -Identity $Scenario -Transient
$pathSnapshot=$null
$cacheSnapshot=$null
$mutex=[Threading.Mutex]::new($false,'Local\SHCDE.InGameTest.3024040')
$locked=$false
$restored=$false
try {
    try {$locked=$mutex.WaitOne(0)} catch [Threading.AbandonedMutexException] {$locked=$true}
    if(-not $locked){throw 'Another in-game test is running.'}
    if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
    $extra=@{}
    if($KeepInstalledPlugins){
        # Integration acceptance with the installed companion mods. Only Pathfinding's
        # existing package files change; unrelated plugin directories are never parked.
        $target=Join-Path $GameDir 'BepInEx/plugins/Pathfinding_Serp'
        # The Extender updates its address cache during a normal startup, even
        # when its binaries are unchanged. Preserve that runtime output too.
        $cacheSnapshot=Save-InGameTestState (Join-Path $GameDir 'BepInEx/plugins/000shcdese/data/aobcache.json') (Join-Path $fixture 'original-aobcache.json')
        $old=Get-InGameTestTree $target
        $next=Get-InGameTestTree ([IO.Path]::GetFullPath($PathfindingPackage))
        if(-not $old.Exists -or $old.Files.Count -ne $next.Files.Count -or -not $old.Directories.SetEquals($next.Directories)){
            throw 'In-place acceptance requires the same existing package file/directory set.'
        }
        foreach($relative in $next.Files.Keys){if(-not $old.Files.ContainsKey($relative)){throw 'Package filename set differs.'}}
        $pathSnapshot=Save-InGameTestState $target (Join-Path $fixture 'original-pathfinding') -Directory
        foreach($relative in $next.Files.Keys){
            [IO.File]::Copy((Join-Path $PathfindingPackage $relative),(Join-Path $target $relative),$true)
        }
        Assert-InGameTestTree $target $next
        Write-Host 'Testing with installed companion plugins; temporary Pathfinding package verified.'
    } else {
        $layout=Join-Path $fixture 'layout'
        [void][IO.Directory]::CreateDirectory($layout)
        foreach($name in @('000shcdese','fixes')){
            $source=Join-Path $GameDir ('BepInEx/plugins/'+$name)
            if(Test-Path -LiteralPath $source -PathType Container){Copy-Item -LiteralPath $source -Destination $layout -Recurse}
            elseif($name -eq '000shcdese'){throw 'Installed Script Extender package is missing.'}
        }
        Copy-Item -LiteralPath ([IO.Path]::GetFullPath($PathfindingPackage)) -Destination $layout -Recurse
        $extra.PluginLayout=$layout
    }
    & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') -GameDir $GameDir `
        -ModGuid PathfindingSurfaceTest_Serp -ModPackage (Join-Path $PSScriptRoot 'BepInEx/plugins/PathfindingSurfaceTest_Serp') `
        -ApiPluginPackage (Join-Path $SdkRoot 'BepInEx/plugins/APIShared_Serp') `
        -ApiPatcherPackage (Join-Path $SdkRoot 'BepInEx/patchers/APIShared.DependencyBootstrap.dll') `
        @extra -Scenario $Scenario -OutputDirectory $OutputDirectory -TimeoutSeconds 900
} finally {
    try {
        try {
            if($pathSnapshot){Restore-InGameTestState $pathSnapshot; Write-Host 'Original Pathfinding package restored and verified.'}
        } finally {
            if($cacheSnapshot){Restore-InGameTestState $cacheSnapshot; Write-Host 'Original Script Extender address cache restored and verified.'}
        }
        $restored=$true
    } finally {
        if($locked){$mutex.ReleaseMutex()}
        $mutex.Dispose()
        if($restored){Complete-TransientWorkDirectories -Owner $PSCommandPath}
        else {Write-Warning ('Preserved restoration snapshot: '+$fixture)}
    }
}
