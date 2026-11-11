[CmdletBinding()]
param(
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [Parameter(Mandatory)][string]$ModPackage,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Both','Walls','Combined')][string]$Case='Both',
    [string]$PluginLayout,
    [string]$ApiPluginPackage,
    [string]$ApiPatcherPackage,
    [ValidateRange(30,300)][int]$TimeoutSeconds=300,
    [switch]$Execute
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$runner=Join-Path $workspace 'Shared\Tools\Automation\Invoke-InGameTest.ps1'
$cases=if($Case -eq 'Both'){@('walls','combined')}elseif($Case -eq 'Walls'){@('walls')}else{@('combined')}
$failures=[Collections.Generic.List[string]]::new()
if([bool]$ApiPluginPackage -ne [bool]$ApiPatcherPackage){throw 'Supply both APIShared package paths together, or neither.'}
if($PluginLayout -and !$ApiPluginPackage){throw 'An isolated plugin layout requires the matching APIShared plugin/patcher pair.'}

foreach($scenarioCase in $cases){
    $arguments=@{
        GameDir=$GameDir;ModGuid='AreaDemolitionTest_Serp';ModPackage=$ModPackage
        Scenario=('wolf1-'+$scenarioCase);OutputDirectory=(Join-Path $OutputDirectory $scenarioCase);TimeoutSeconds=$TimeoutSeconds
    }
    if($PluginLayout){$arguments.PluginLayout=$PluginLayout}
    if($ApiPluginPackage){$arguments.ApiPluginPackage=$ApiPluginPackage;$arguments.ApiPatcherPackage=$ApiPatcherPackage}
    if(!$Execute){
        Write-Output ('Prepared only: '+$runner)
        $arguments.GetEnumerator() | Sort-Object Key | ForEach-Object {Write-Output ('  -'+$_.Key+' '+$_.Value)}
        continue
    }
    # The shared runner performs the game/process gate, package/settings/save rollback,
    # request-file activation and exact terminal PASS check. No global mouse/key input.
    $started=[DateTime]::UtcNow
    try{ & $runner @arguments }
    catch{
        if($_.Exception.Message -like 'RESTORE_FAILED:*'){throw}
        $failures.Add($scenarioCase+': '+$_.Exception.Message)
        Write-Warning ($scenarioCase+': '+$_.Exception.Message)
    }
    finally{
        # Screenshots are explicit test artifacts, outside personal settings and save trees.
        # Preserve them even when acceptance fails; never delete an existing game-side artifact.
        if([IO.Directory]::Exists($arguments.OutputDirectory)){
            $logs=Get-ChildItem -LiteralPath $arguments.OutputDirectory -File -Filter '*.log' -Recurse |
                Where-Object {$_.LastWriteTimeUtc -ge $started.AddSeconds(-2)}
            foreach($artifactLog in $logs){
                $text=[IO.File]::ReadAllText($artifactLog.FullName)
                $identities=[regex]::Matches($text,'INGAME_TEST run=([a-fA-F0-9]{32})') |
                    ForEach-Object {$_.Groups[1].Value.ToLowerInvariant()} | Select-Object -Unique
                foreach($identity in $identities){
                    $source=Join-Path $GameDir ('BepInEx\SerpAutomationArtifacts\'+$identity)
                    if(![IO.Directory]::Exists($source)){continue}
                    $destination=Join-Path $artifactLog.DirectoryName 'screenshots'
                    [void][IO.Directory]::CreateDirectory($destination)
                    foreach($shot in Get-ChildItem -LiteralPath $source -File -Filter '*.png'){
                        [IO.File]::Copy($shot.FullName,(Join-Path $destination $shot.Name),$true)
                    }
                }
            }
        }
    }
}
if($failures.Count){throw ($failures -join [Environment]::NewLine)}
if(!$Execute){Write-Output 'No game, input, installation or configuration changes performed. Add -Execute only for an authorized test window.'}
