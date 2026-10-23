[CmdletBinding()]
param(
    [string]$GameDir = $env:SHCDE_GAME_DIR,
    [string]$ApiSharedDir,
    [Parameter(Mandatory = $true)][string]$SavePath,
    [string]$MSBuild = $env:SHCDE_MSBUILD
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$configuration = Get-ReleaseConfiguration
if (-not $GameDir) { $GameDir = $configuration.GameDir }
if (-not $MSBuild) { $MSBuild = $configuration.MSBuild }
$game = (Resolve-Path -LiteralPath $GameDir).Path
$SavePath = (Resolve-Path -LiteralPath $SavePath).Path
if (-not $ApiSharedDir) { $ApiSharedDir = Join-Path $game 'BepInEx\plugins\APIShared_Serp' }
$api = Join-Path $ApiSharedDir 'APIShared.dll'
$manifest = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL/info.json')) | ConvertFrom-Json
$dependency = @($manifest.Dependencies | Where-Object GUID -CEQ 'APIShared_Serp')
if ($dependency.Count -ne 1) { throw 'The APIShared dependency must be declared exactly once.' }
if ([Reflection.AssemblyName]::GetAssemblyName($api).Version -lt [version]$dependency[0].MinimumVersion) {
    throw 'Selected APIShared SDK is below the manifest minimum.'
}
# The runtime validates the IL contract of the loaded methods; this checks member visibility only,
# so an update that keeps these members passes without a file-hash pin.
$managed = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
$null = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($managed)
try {
    $hud = $assembly.MainModule.Types | Where-Object FullName -eq 'CrusaderDE.HUD_IngameMenu'
    $restart = $hud.NestedTypes | Where-Object Name -eq 'RestartSkirmishMapInfo'
    if (-not $hud.IsPublic -or -not $restart.IsNestedPublic) { throw 'Inaccessible game types.' }
    foreach ($type in @($hud,$restart,($assembly.MainModule.Types | Where-Object FullName -eq 'FileHeader'))) {
        $names = if ($type -eq $hud) { @('restartSkirmishMapInfo') } elseif ($type -eq $restart) { @('selectedHeader','aivs') } else { @('fileName','workshopMap','builtinMap') }
        foreach ($name in $names) { if (-not ($type.Fields | Where-Object {$_.Name -eq $name -and $_.IsPublic})) { throw "Inaccessible member: $($type.FullName)::$name" } }
    }
    foreach ($name in @('decode','encode')) { if (-not ($restart.Methods | Where-Object {$_.Name -eq $name -and $_.IsPublic})) { throw "Inaccessible codec: $name" } }
    if (-not ($hud.Methods | Where-Object {$_.Name -eq 'SetAsNormal' -and $_.IsPrivate})) { throw 'Menu IL target changed.' }
} finally { $assembly.Dispose() }
$bepinex = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\core\BepInEx.dll'))
try {
    $paths = $bepinex.MainModule.Types | Where-Object FullName -eq 'BepInEx.Paths'
    $getter = $paths.Methods | Where-Object Name -eq 'get_ManagedPath'
    if (-not $paths.IsPublic -or -not $getter.IsPublic -or -not $getter.IsStatic -or $getter.ReturnType.FullName -ne 'System.String') { throw 'BepInEx Paths.ManagedPath contract changed.' }
} finally { $bepinex.Dispose() }
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot,(Join-Path $workspace 'BugfixesAndQoL\src\SavegameMissingMap') -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.json','.ps1','.bat','.md')}) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "Bare LF: $($file.FullName)" }
    if ($file.Extension -eq '.cs' -and $file.FullName -notmatch '[\\/]tests[\\/]') {
        if ($text -match 'Newtonsoft|System\.Text\.Json|JavaScriptSerializer|JsonUtility|DataContractJsonSerializer|System\.Web\.Extensions') { throw 'Forbidden runtime JSON.' }
        if ($text -match '\b(OnDestroy|OnDisable|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(|\.(Undo|Dispose|Unpatch|Apply)\s*\(') { throw "Unsafe lifecycle: $($file.Name)" }
    }
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'BugfixesAndQoL'
if ($LASTEXITCODE -ne 0) { throw 'Shared boundary checks failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared\Tools\Validation\Test-DependencyMetadata.ps1') -Workspace $workspace -ProjectFiles (Join-Path $PSScriptRoot '..\..\BugfixesAndQoL.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Dependency metadata checks failed.' }
# The mod-scoped boundary check above includes permanent-hook validation.
& $MSBuild (Join-Path $PSScriptRoot 'Tests.csproj') /nologo /verbosity:minimal "/p:GameDir=$game" "/p:ApiSharedDir=$ApiSharedDir"
if ($LASTEXITCODE -ne 0) { throw 'Contract tests failed to build.' }
& (Join-Path $PSScriptRoot 'bin\BugfixesAndQoL.SavegameMissingMap.Tests.exe') $game $SavePath $ApiSharedDir
if ($LASTEXITCODE -ne 0) { throw 'Contract execution tests failed.' }
Write-Output 'PASS: metadata, public members, lifecycle, CRLF and actual Vanilla codec transformations.'
