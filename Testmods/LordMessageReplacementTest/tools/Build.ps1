[CmdletBinding()]
param([string]$ApiSharedDir = $env:SHCDE_API_SHARED_DIR, [switch]$NoInstall,
    [string]$GameDir = $(if ($env:SHCDE_GAME_DIR) { $env:SHCDE_GAME_DIR } else {
        'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
    }))
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $project '../..'))
$GameDir = [IO.Path]::GetFullPath($GameDir)
$installSuffix = [string]$env:SHCDE_BRANCH_INSTALL_SUFFIX
if ($installSuffix -notin @('', '_dev')) { throw 'Unsupported branch installation suffix.' }
$NoInstall = $NoInstall -or $env:SHCDE_BRANCH_NOINSTALL -eq '1'
if (-not $ApiSharedDir) { $ApiSharedDir = Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp' }
$ApiSharedDir = [IO.Path]::GetFullPath($ApiSharedDir)
$extender = if ($env:SHCDESE_EXTENDER_DIR) { $env:SHCDESE_EXTENDER_DIR } else { Join-Path $GameDir 'BepInEx/plugins/000shcdese' }
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
$sdk = Join-Path $ApiSharedDir 'APIShared.dll'
if (-not (Test-Path -LiteralPath $sdk -PathType Leaf)) { throw 'Select the freshly built APIShared package.' }
$sdkHash = Get-ApiRepositoryHash $sdk
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] LordMessageReplacementTest: production policy tests, scoped validation and selected public SDK.' -f [DateTime]::Now)
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory $ApiSharedDir
if ($LASTEXITCODE) { throw 'The selected APIShared SDK proof failed.' }
& dotnet run --project (Join-Path $project 'tests/LordMessageReplacementTest.Tests.csproj') --configuration Release "-p:ExtenderDir=$extender"
if ($LASTEXITCODE) { throw 'Lord message replacement policy tests failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods/LordMessageReplacementTest' -ConsumerOnly
if ($LASTEXITCODE) { throw 'Scoped runtime validation failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/Validation/Test-DependencyMetadata.ps1') -Workspace $workspace -ProjectFiles (Join-Path $project 'LordMessageReplacementTest.csproj')
if ($LASTEXITCODE) { throw 'Dependency metadata validation failed.' }
foreach ($file in Get-ChildItem -LiteralPath $project -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx|\.local)[\\/]' -and $_.Extension -in '.cs','.csproj','.ps1','.bat','.json'
}) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF source: $($file.FullName)" }
}
$msbuild = $env:SHCDE_MSBUILD
if (-not $msbuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $msbuild -or -not (Test-Path -LiteralPath $msbuild -PathType Leaf)) { throw 'MSBuild was not found.' }
$local = Join-Path $project '.local'
[void][IO.Directory]::CreateDirectory($local)
& $msbuild (Join-Path $project 'LordMessageReplacementTest.csproj') /t:Rebuild /nologo /v:minimal /p:Configuration=Release /p:ImportDirectoryBuildTargets=false "/p:GameDir=$GameDir" "/p:ExtenderDir=$extender" "/p:ApiSharedDir=$ApiSharedDir" *> (Join-Path $local 'compile.log')
if ($LASTEXITCODE) { throw "Compilation failed; see $local\compile.log" }
if ((Get-ApiRepositoryHash $sdk) -ne $sdkHash) { throw 'The selected APIShared SDK changed during compilation.' }
$output = Join-Path $project 'BepInEx/plugins/LordMessageReplacementTest_Serp'
foreach ($dependency in 'APIShared.dll','SHCDESE.dll','Assembly-CSharp.dll') {
    if (Test-Path -LiteralPath (Join-Path $output $dependency)) { throw "Runtime dependency must not be shipped: $dependency" }
}
$files = @('LordMessageReplacementTest.dll','LordMessageReplacementTest.pdb','info.json')
if (-not $NoInstall) {
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before installing.' }
    $destination = Join-Path $GameDir ('BepInEx/plugins/LordMessageReplacementTest_Serp' + $installSuffix)
    [void][IO.Directory]::CreateDirectory($destination)
    foreach ($name in $files) {
        $source = Join-Path $output $name
        $installed = Join-Path $destination $name
        Copy-Item -LiteralPath $source -Destination $installed -Force
        if ((Get-ApiRepositoryHash $source) -ne (Get-ApiRepositoryHash $installed)) { throw "Installed hash mismatch: $name" }
    }
}
[ordered]@{CreatedUtc=[DateTime]::UtcNow.ToString('o');Installed=(-not $NoInstall);InstallSuffix=$installSuffix;Sdk=$sdk;SdkSha256=$sdkHash;
    Package=$output;Files=@($files | ForEach-Object { [ordered]@{Name=$_;Sha256=(Get-ApiRepositoryHash (Join-Path $output $_))} });
    GameAcceptance='Pending: actual allied messages for all four Lords, individual counterexamples, queue, save-load, editor and host/client playback.'} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $local 'build.json') -Encoding UTF8
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] PASS: LordMessageReplacementTest compiled; installed={1}.' -f [DateTime]::Now,(-not $NoInstall))
