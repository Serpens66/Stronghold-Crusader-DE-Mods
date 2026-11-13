[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SdkRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('ready-cycles','slow-peasant','slow-engineer','control-peasant')][string]$Scenario='slow-engineer',
    [switch]$WithoutFixesDelay,
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
)
$ErrorActionPreference='Stop'
$FixesDelay=-not $WithoutFixesDelay
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$fixture=New-ShortWorkDirectory -Root $workspace -Kind t -Name ai-crew -Identity ($Scenario+' delay='+$FixesDelay) -Transient
$config=Join-Path $GameDir 'BepInEx/config/fixes.cfg'
$original=$null
try {
    if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
    if(Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx/SerpAutomationRollback')){throw 'A prior test rollback must be resolved before another test.'}
    if(@(Get-ChildItem -LiteralPath (Join-Path $GameDir 'BepInEx/config/SerpAutomation') -Filter '*.run' -ErrorAction SilentlyContinue).Count){throw 'Another automation request is pending.'}
    $layout=Join-Path $fixture 'layout'
    [void][IO.Directory]::CreateDirectory($layout)
    foreach($plugin in @('000shcdese','fixes')){
        Copy-Item -LiteralPath (Join-Path $GameDir ('BepInEx/plugins/'+$plugin)) -Destination $layout -Recurse
    }
    Copy-Item -LiteralPath (Join-Path $workspace 'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp') -Destination $layout -Recurse
    $original=[IO.File]::ReadAllBytes($config)
    [IO.File]::WriteAllBytes((Join-Path $fixture 'fixes.original.cfg'),$original)
    $text=[Text.Encoding]::UTF8.GetString($original)
    $pattern='(?ms)(^\[HarassingTentIdleEngineersFix\]\r?\n(?:(?!^\[).)*?^Enabled = )(true|false)'
    if([regex]::Matches($text,$pattern).Count -ne 1){throw 'Fixes delay setting is missing or ambiguous.'}
    $text=[regex]::Replace($text,$pattern,('$1'+$FixesDelay.ToString().ToLowerInvariant()))
    [IO.File]::WriteAllText($config,$text,[Text.UTF8Encoding]::new($false))
    & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') -GameDir $GameDir -ModGuid AiSiegeCrewTest_Serp `
        -ModPackage (Join-Path $PSScriptRoot 'BepInEx/plugins/AiSiegeCrewTest_Serp') `
        -ApiPluginPackage (Join-Path $SdkRoot 'BepInEx/plugins/APIShared_Serp') `
        -ApiPatcherPackage (Join-Path $SdkRoot 'BepInEx/patchers/APIShared.DependencyBootstrap.dll') `
        -PluginLayout $layout -Scenario $Scenario -OutputDirectory $OutputDirectory -TimeoutSeconds 420
} finally {
    if($null -ne $original){
        if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Game still running; restore the preserved Fixes configuration after closing it.'}
        [IO.File]::WriteAllBytes($config,$original)
    }
    Complete-TransientWorkDirectories -Owner $PSCommandPath
}
