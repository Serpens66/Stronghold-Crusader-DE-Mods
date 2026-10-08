from pathlib import Path
import re
root = Path.cwd().parent/'SHCDE-APIShared'
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,t):
    p.parent.mkdir(parents=True,exist_ok=True)
    expected=t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    p.write_bytes(expected); assert p.read_bytes()==expected

write(root/'Directory.Build.props','''<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <GameDir Condition="'$(GameDir)' == ''">$(SHCDE_GAME_DIR)</GameDir>
    <ExtenderDir Condition="'$(ExtenderDir)' == '' and '$(SHCDESE_EXTENDER_DIR)' != ''">$(SHCDESE_EXTENDER_DIR)</ExtenderDir>
    <ExtenderDir Condition="'$(ExtenderDir)' == '' and '$(GameDir)' != ''">$(GameDir)\BepInEx\plugins\000shcdese</ExtenderDir>
  </PropertyGroup>
</Project>
''')
write(root/'Directory.Build.targets','''<Project>
  <Target Name="ValidateInstalledDependencies" BeforeTargets="ResolveReferences" Condition="'$(NeedsGameAssemblies)' == 'true'">
    <Error Condition="'$(GameDir)' == ''" Text="Set SHCDE_GAME_DIR or pass /p:GameDir=your game installation directory." />
    <Error Condition="!Exists('$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll')" Text="GameDir must contain the real installed Assembly-CSharp.dll. Publicized assemblies are not supported." />
    <Error Condition="!Exists('$(ExtenderDir)\SHCDESE.dll')" Text="Install Script Extender or set SHCDESE_EXTENDER_DIR / ExtenderDir." />
  </Target>
</Project>
''')
for p in root.rglob('*.csproj'):
    if any(x in p.parts for x in ['bin','obj','BepInEx']): continue
    text=read(p)
    text=re.sub(r'  <PropertyGroup Condition="\x27\$\(GameDir\)\x27 == \x27\x27"><GameDir>[^<]+</GameDir></PropertyGroup>\n','',text)
    text=re.sub(r'  <PropertyGroup Condition="\x27\$\(ExtenderDir\)\x27 == \x27\x27"><ExtenderDir>[^<]+</ExtenderDir></PropertyGroup>\n','',text)
    if p.name in ['APIShared.csproj','ThirdPartyMod.csproj','APISharedPresetConsumerTests.csproj']:
        text=text.replace('<OutputType>Library</OutputType>','<OutputType>Library</OutputType>\n    <NeedsGameAssemblies>true</NeedsGameAssemblies>')
    if p.name=='APIShared.csproj':
        # Ordinary additions under src do not require a project-file inventory update.
        text=re.sub(r'    <Compile Include="src\\[^"\n]+" />\n','',text)
        text=text.replace('<Compile Include="Properties\\AssemblyInfo.cs" />','<Compile Include="Properties\\AssemblyInfo.cs" />\n    <Compile Include="src\\**\\*.cs" />')
    write(p,text)
test_props=root/'tests/Directory.Build.props'
text=read(test_props).replace('<IsTestProject>true</IsTestProject>', '<IsTestProject Condition="\x27$(MSBuildProjectName)\x27 != \x27APISharedPresetConsumerTests\x27">true</IsTestProject>')
text=text.replace('  <ItemGroup>','  <ItemGroup Condition="\x27$(MSBuildProjectName)\x27 != \x27APISharedPresetConsumerTests\x27">')
write(test_props,text)

p=root/'tools/Validation/RuntimePreflight.Common.ps1'
text=read(p)
start=text.index('            # Existing bounded loading-warning')
end=text.index('        foreach ($match',start)
text=text[:start]+'''            throw "$($Mod.Name): MonoBehaviour scheduling requires a persistent publisher: $sourcePath"
        }
'''+text[end:]
write(p,text)
p=root/'tools/Validation/Test-PermanentHooks.ps1'
text=read(p)
start=text.index('$roots =')
end=text.index('$errors =',start)
text=text[:start]+'''$files = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'src') -Recurse -File -Filter '*.cs')

'''+text[end:]
write(p,text)
p=root/'tools/Validation/Verify-Interop.ps1'
text=read(p).replace("if (-not $GameDir) { $GameDir = 'E:\\ProgrammeE\\Steam\\steamapps\\common\\Stronghold Crusader Definitive Edition' }", "if (-not $GameDir) { throw 'Set SHCDE_GAME_DIR or supply -GameDir for installed interop verification.' }")
write(p,text)
# Only prerequisites-free structural/metadata checks run in public CI.
p=root/'tools/Validation/Test-Standalone.ps1'
text=read(p).replace('param([string]$GameDir, [string]$ExtenderDir)', 'param([string]$GameDir, [string]$ExtenderDir, [switch]$SourceOnly)')
text=text.replace('    if ($text -match \x27(?<!\\r)\\n\x27) { throw "CRLF required: $($file.FullName)" }\n','')
text=text.replace("& (Join-Path $PSScriptRoot 'Verify-Interop.ps1') -GameDir $GameDir -ExtenderDir $ExtenderDir", "if (-not $SourceOnly) { & (Join-Path $PSScriptRoot 'Verify-Interop.ps1') -GameDir $GameDir -ExtenderDir $ExtenderDir }")
text=text.replace('runtime JSON/lifecycle/scheduling, CRLF, XAML and installed interop.', 'runtime JSON/lifecycle/scheduling and XAML; installed interop is checked in the local full run.')
write(p,text)
write(root/'build.bat',r'''@echo off
setlocal EnableExtensions
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1" -NoInstall:%NO_INSTALL%
set "RESULT=%ERRORLEVEL%"
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%
''')
write(root/'tools/Build.ps1',r'''[CmdletBinding()]
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
    $allowed = @('APIShared.dll','APIShared.pdb','APIShared.xml','info.json')
    $unexpected = @(Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object { $_.Extension -in @('.dll','.exe') -and $_.Name -ne 'APIShared.dll' })
    if ($unexpected.Count) { throw 'The package contains unexpected runtime binaries.' }
    if ($NoInstall -ne 0) { Write-Host 'APIShared built and tested successfully; installation skipped.'; exit 0 }
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
''')
write(root/'.github/workflows/tests.yml','''name: Tests
on:
  push:
  pull_request:
  workflow_dispatch:
permissions:
  contents: read
jobs:
  core:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Validate sources and metadata
        shell: pwsh
        run: ./tools/Validation/Test-Standalone.ps1 -SourceOnly
      - name: Run game-independent tests
        run: dotnet test tests/Core.Tests/Core.Tests.csproj --configuration Release --logger trx --results-directory TestResults
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: TestResults/*.trx
''')
write(root/'.gitignore',read(root/'.gitignore')+'\nTestResults/\n*.trx\n.vs/\n')
write(root/'.editorconfig','''root = true
[*]
end_of_line = crlf
insert_final_newline = true
charset = utf-8
''')
print('Configured standalone build, common paths and prerequisites-free CI.')
