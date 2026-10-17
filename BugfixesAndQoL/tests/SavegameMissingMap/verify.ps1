[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$api = Join-Path $game 'BepInEx\plugins\APIShared_Serp\APIShared.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($api).Version -ne [version]'0.9.2.0') { throw 'Installed APIShared 0.9.2 is required; never rebuild it.' }
$managed = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
if ((Get-Sha256Hex $managed) -ne 'bc8b6a395f01d48557db413600c8dd8d1fdfd3abdf97bfbbb68a3c56b04fd789') { throw 'Game assembly contract changed.' }
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
$builder = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $builder (Join-Path $PSScriptRoot 'Tests.csproj') /nologo /verbosity:minimal "/p:ApiSharedDir=$game\BepInEx\plugins\APIShared_Serp"
if ($LASTEXITCODE -ne 0) { throw 'Contract tests failed to build.' }
& (Join-Path $PSScriptRoot 'bin\BugfixesAndQoL.SavegameMissingMap.Tests.exe') $game 'C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Saves\test_dusty.sav'
if ($LASTEXITCODE -ne 0) { throw 'Contract execution tests failed.' }
Write-Output 'PASS: metadata, public members, lifecycle, CRLF and actual Vanilla codec transformations.'
