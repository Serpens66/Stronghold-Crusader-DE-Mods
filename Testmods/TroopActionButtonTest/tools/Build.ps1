param([string]$ApiSharedDir=$env:SHCDE_API_SHARED_DIR,
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$workspace=[IO.Path]::GetFullPath((Join-Path $project '../..'))
if (!$ApiSharedDir) { $ApiSharedDir=Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp' }
$ApiSharedDir=[IO.Path]::GetFullPath($ApiSharedDir)
$sdk=Join-Path $ApiSharedDir 'APIShared.dll'
if (!(Test-Path -LiteralPath $sdk)) { throw 'Select a compiled APIShared 0.31.0+ SDK with SHCDE_API_SHARED_DIR.' }
if ([Reflection.AssemblyName]::GetAssemblyName($sdk).Version -lt [version]'0.31.0') { throw 'This test needs APIShared 0.31.0+. Build its current sources without installation and select that SDK.' }
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
$before=Get-ApiRepositoryHash $sdk
$sourceInputs=@(Get-ChildItem -LiteralPath $project -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx|\.local)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md')
} | Sort-Object FullName | ForEach-Object { [ordered]@{Path=$_.FullName;Sha256=(Get-ApiRepositoryHash $_.FullName)} })
$local=Join-Path $project '.local'
[void][IO.Directory]::CreateDirectory($local)
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] TroopActionButtonTest: current source, SDK {1}, no installation' -f [DateTime]::Now,$sdk)
& dotnet run --project (Join-Path $project 'tests/TroopActionButtonTest.Tests.csproj') --configuration Release
if ($LASTEXITCODE) { throw 'Button command regression tests failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods/TroopActionButtonTest' -ConsumerOnly
if ($LASTEXITCODE) { throw 'Scoped source/dependency validation failed.' }
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild=(& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe'|Select-Object -First 1)
if (!$msbuild) { throw 'MSBuild was not found.' }
$output=Join-Path $project 'BepInEx/plugins/TroopActionButtonTest_Serp'
# Like the other local compile-only testmods: validate this explicit SDK, without claiming
# the complete release-package proof required by the normal release consumer driver.
& $msbuild (Join-Path $project 'TroopActionButtonTest.csproj') /t:Rebuild /nologo /v:minimal /p:Configuration=Release /p:ImportDirectoryBuildTargets=false "/p:GameDir=$GameDir" "/p:ExtenderDir=$GameDir\BepInEx\plugins\000shcdese" "/p:ApiSharedDir=$ApiSharedDir" *> (Join-Path $local 'compile.log')
if ($LASTEXITCODE) { throw "Compilation failed; see $local\compile.log" }
if ((Get-ApiRepositoryHash $sdk) -ne $before) { throw 'The selected SDK changed during compilation.' }
foreach($source in $sourceInputs) { if((Get-ApiRepositoryHash $source.Path) -ne $source.Sha256) { throw 'Testmod sources changed during compilation; rebuild the package.' } }
foreach($name in @('APIShared.dll','SHCDESE.dll')) { if(Test-Path -LiteralPath (Join-Path $output $name)) { throw "Dependency must not be shipped: $name" } }
$zip=Join-Path $local 'TroopActionButtonTest.zip'
Compress-Archive -LiteralPath (Join-Path $project 'BepInEx') -DestinationPath $zip -Force
[ordered]@{CreatedUtc=[DateTime]::UtcNow.ToString('o');Installed=$false;Sdk=$sdk;SdkSha256=$before;
    Package=$zip;PackageSha256=(Get-ApiRepositoryHash $zip);Sources=$sourceInputs;
    GameAcceptance='Pending: actual Noesis rendering, mixed selection, circular action pages, split navigation hit areas, world-click shielding, editor and save/map transitions.'} |
    ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $local 'build.json') -Encoding UTF8
Write-Host "PASS: $zip (not installed)"
