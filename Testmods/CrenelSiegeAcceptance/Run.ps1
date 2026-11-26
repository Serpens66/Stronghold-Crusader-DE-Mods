[CmdletBinding()]
param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [ValidateSet('crenel-siege','crenel-damage')][string]$Scenario = 'crenel-siege')

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) {
    throw 'The game is already running.'
}
$staging = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'crenel-siege' -Identity 'Crenel siege A/B plugin layout' -Transient
$evidence = New-ShortWorkDirectory -Root $workspace -Kind a -Name 'crenel-siege' -Identity 'Spectator siege and pathing A/B'
$previous = [Environment]::GetEnvironmentVariable('SCDEModManagerLaunchId', 'Process')
try {
    $layout = Join-Path $staging 'plugins'
    [void][IO.Directory]::CreateDirectory($layout)
    $installed = Join-Path $GameDir 'BepInEx/plugins'
    foreach ($name in @('000shcdese', 'fixes', 'BugfixesAndQoL_Serp', 'Spectator_Serp')) {
        Copy-Item -LiteralPath (Join-Path $installed $name) -Destination $layout -Recurse
    }
    $profile = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Firefly Studios/Stronghold Crusader Definitive Edition'
    $fixtureFiles = [ordered]@{
        (Join-Path $profile 'Maps/4spielerkarte.map') = '17E118C1E809F599C824BAB2EEDDCCFE08F0574C13E5FD1731CA671809B3E9B3'
    }
    if ($Scenario -eq 'crenel-siege') {
        $fixtureFiles[(Join-Path $profile 'CustomLords/DoNothing/DoNothing.lordjson')] = 'E20D0BDA6B1AC9D97F9A1E2BDE3F26EAC6385C5D49F84BE92915E32152463348'
        $fixtureFiles[(Join-Path $profile 'CustomLords/DoNothing/DoNothing.aivjson')] = '733FCBA924438DC3142EAC8A0326376DE9DD422A0FE0EC2A2F8B7068574A5606'
        $fixtureFiles[(Join-Path $layout 'fixes/Override/AIV/SK_SNAKE_4.aivjson')] = '5AC0E53862526556B013C17D17E4F4B5C2F7E546F64830FB79B447FF9644364B'
    }
    foreach ($path in $fixtureFiles.Keys) {
        $hash = (Get-FileHash -LiteralPath $path).Hash
        if ($hash -ne $fixtureFiles[$path]) { throw "Unexpected SHA256 for $path; found $hash" }
        Write-Host "Fixture $(Split-Path $path -Leaf) SHA256=$hash"
    }
    $env:SCDEModManagerLaunchId = 'SerpAcceptance-CrenelSiege-' + [Guid]::NewGuid().ToString('N')
    $options = @{
        GameDir = $GameDir
        ModGuid = 'CrenelSiegeAcceptance_Serp'
        ModPackage = Join-Path $PSScriptRoot 'BepInEx/plugins/CrenelSiegeAcceptance_Serp'
        ApiPluginPackage = Join-Path $installed 'APIShared_Serp'
        ApiPatcherPackage = Join-Path $GameDir 'BepInEx/patchers/APIShared.DependencyBootstrap.dll'
        PluginLayout = $layout
        Scenario = $Scenario
        OutputDirectory = $evidence
        TimeoutSeconds = 600
        HiddenDesktop = $true
    }
    # Use separate read-only inputs, because the runner parks the installed plugin tree.
    $api = Join-Path $staging 'APIShared_Serp'
    Copy-Item -LiteralPath $options.ApiPluginPackage -Destination $api -Recurse
    $patcher = Join-Path $staging 'APIShared.DependencyBootstrap.dll'
    Copy-Item -LiteralPath $options.ApiPatcherPackage -Destination $patcher
    $options.ApiPluginPackage = $api
    $options.ApiPatcherPackage = $patcher
    Write-Host "Evidence: $evidence"
    & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') @options
    foreach ($path in $fixtureFiles.Keys) {
        if ((Get-FileHash -LiteralPath $path).Hash -ne $fixtureFiles[$path]) {
            throw "Fixture changed during the test: $path"
        }
    }
} finally {
    [Environment]::SetEnvironmentVariable('SCDEModManagerLaunchId', $previous, 'Process')
    $resolved = (Resolve-Path -LiteralPath $staging).ProviderPath
    $parent = [IO.Path]::GetFullPath((Join-Path $workspace '.work/t/crenel-siege')) + '\'
    if ($resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
