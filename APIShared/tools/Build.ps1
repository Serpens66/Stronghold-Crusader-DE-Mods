[CmdletBinding()]
param([int]$NoInstall = 0)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Invoke-Tool([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE." }
}
try {
    $game = $env:SHCDE_GAME_DIR
    if (-not $game) { throw 'Set SHCDE_GAME_DIR to your Stronghold Crusader Definitive Edition installation.' }
    $game = [IO.Path]::GetFullPath($game)
    $extender = $env:SHCDESE_EXTENDER_DIR
    if (-not $extender) { $extender = Join-Path $game 'BepInEx\plugins\000shcdese' }
    if (-not (Test-Path -LiteralPath (Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'))) { throw 'The real installed Assembly-CSharp.dll was not found in SHCDE_GAME_DIR.' }
    if (-not (Test-Path -LiteralPath (Join-Path $extender 'SHCDESE.dll'))) { throw 'Install Script Extender or set SHCDESE_EXTENDER_DIR.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK (dotnet).' }
    $msbuild = $env:SHCDE_MSBUILD
    if (-not $msbuild) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio Build Tools or set SHCDE_MSBUILD.' }
        $msbuild = (& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
    }
    if (-not $msbuild -or -not (Test-Path -LiteralPath $msbuild)) { throw 'MSBuild was not found. Set SHCDE_MSBUILD.' }
    if ($NoInstall -eq 0 -and (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue)) { throw 'Close the game before installation.' }
    & (Join-Path $root 'tools\Validation\Test-Standalone.ps1') -GameDir $game -ExtenderDir $extender
    $results = Join-Path $root '.local\test-results'
    foreach ($project in @('Core.Tests\Core.Tests.csproj','APISharedTests\APISharedTests.csproj','LobbyModSettingsPresetTests\LobbyModSettingsPresetTests.csproj')) {
        Invoke-Tool 'dotnet' @('test', (Join-Path $root ('tests\' + $project)), '--configuration', 'Release', "-p:GameDir=$game", "-p:ExtenderDir=$extender", '--logger', 'trx', '--results-directory', $results)
    }
    Invoke-Tool $msbuild @((Join-Path $root 'APIShared.csproj'), '/t:Rebuild', '/p:Configuration=Release', "/p:GameDir=$game", "/p:ExtenderDir=$extender")
    foreach ($project in @('tests\PublicPresetConsumer\APISharedPresetConsumerTests.csproj','examples\ThirdPartyMod\ThirdPartyMod.csproj')) {
        Invoke-Tool $msbuild @((Join-Path $root $project), '/t:Rebuild', '/p:Configuration=Release', "/p:GameDir=$game", "/p:ExtenderDir=$extender")
    }
    $package = Join-Path $root 'BepInEx\plugins\APIShared_Serp'
    Copy-Item -LiteralPath (Join-Path $root 'info.json') -Destination $package
    Copy-Item -LiteralPath (Join-Path $root 'Patches') -Destination $package -Recurse -Force
    foreach ($required in @('APIShared.dll','APIShared.xml','info.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $package $required))) { throw ($required + ' is missing from the prepared package.') }
    }
    $unexpected = @(Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object { $_.Extension -in @('.dll','.exe') -and $_.Name -ne 'APIShared.dll' })
    if ($unexpected.Count) { throw 'The package contains unexpected runtime binaries.' }
    if ($NoInstall -ne 0) { Write-Host 'APIShared built and tested successfully; installation skipped.'; exit 0 }
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'The game started during the build; close it before installing.' }
    $destination = Join-Path $game 'BepInEx\plugins\APIShared_Serp'
    $packed = Join-Path $game 'BepInEx\plugins\SerpsMods_Serp\Infrastructure\APIShared_Serp'
    if (Test-Path -LiteralPath (Join-Path $packed 'APIShared.dll')) { $destination = $packed }
    # Remove only this plugin's validated destination; keep neighboring plugins intact.
    $pluginRoot = [IO.Path]::GetFullPath((Join-Path $game 'BepInEx\plugins')) + [IO.Path]::DirectorySeparatorChar
    $destination = [IO.Path]::GetFullPath($destination)
    if (-not $destination.StartsWith($pluginRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($destination) -ne 'APIShared_Serp') { throw 'Unsafe installation destination.' }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
    Copy-Item -LiteralPath $package -Destination $destination -Recurse
    if ($destination -eq $packed) {
        $standalone = [IO.Path]::GetFullPath((Join-Path $pluginRoot 'APIShared_Serp'))
        if (-not $standalone.StartsWith($pluginRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe duplicate plugin directory.' }
        if (Test-Path -LiteralPath $standalone) { Remove-Item -LiteralPath $standalone -Recurse -Force }
    }
    Write-Host 'APIShared built, tested and installed successfully.'
} catch { Write-Error $_; exit 1 }
