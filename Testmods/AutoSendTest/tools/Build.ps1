param([string]$ApiSharedDir = $env:SHCDE_AUTOSEND_API_DIR,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition', [switch]$DevDiag)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
Remove-Item Env:SHCDE_DEVDIAG -ErrorAction SilentlyContinue
if ($DevDiag -and (& git -C $workspace branch --show-current) -eq 'main') { throw 'Diagnostic builds are not permitted on main.' }
if ($DevDiag) { $env:SHCDE_DEVDIAG = '1' }
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
$run = New-ShortWorkDirectory -Root $workspace -Kind a -Name 'autosend-build' -Identity 'current source; explicit APIShared SDK; no install'
if (!$ApiSharedDir) { $ApiSharedDir = Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp' }
. (Join-Path $workspace 'APIShared\tools\BuildProof.ps1')
$sdkFile = Join-Path $ApiSharedDir 'APIShared.dll'
$sdkHash = Get-ApiFileHash $sdkFile
$extender = Join-Path $GameDir 'BepInEx\plugins\000shcdese'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$output = Join-Path $run 'package\BepInEx\plugins\AutoSendTest_Serp'
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] AutoSend build: $run"
& dotnet run --project (Join-Path $project 'tests\AutoSendPolicy.Tests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'AutoSend policy regressions failed.' }
& $msbuild (Join-Path $project 'AutoSendTest.csproj') /t:Rebuild /nologo /v:minimal /p:ImportDirectoryBuildTargets=false "/p:GameDir=$GameDir" "/p:ExtenderDir=$extender" "/p:ApiSharedDir=$ApiSharedDir" "/p:OutputPath=$output" ("/p:SerpDevDiagnostics=" + $DevDiag.IsPresent.ToString().ToLowerInvariant()) *> (Join-Path $run 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "AutoSend compilation failed: $run\compile.log" }
$zip = Join-Path $run 'AutoSendTest.zip'
Compress-Archive -LiteralPath (Join-Path $run 'package\BepInEx') -DestinationPath $zip
if ((Get-ApiFileHash $sdkFile) -ne $sdkHash) { throw 'The selected APIShared SDK changed during this build.' }
$sourceInputs = @(Get-ChildItem -LiteralPath $project -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.txt')
} | Sort-Object FullName | ForEach-Object { [ordered]@{Path=$_.FullName;Sha256=(Get-ApiFileHash $_.FullName)} })
[ordered]@{CreatedUtc=[DateTime]::UtcNow.ToString('o');Head=(& git -C $workspace rev-parse HEAD);
    Installed=$false;DevDiag=$DevDiag.IsPresent;SdkPath=$sdkFile;SdkSha256=$sdkHash;Package=$zip;PackageSha256=(Get-ApiFileHash $zip);
    Sources=$sourceInputs;Files=@(Get-ChildItem -LiteralPath (Join-Path $run 'package') -Recurse -File | Sort-Object FullName |
        ForEach-Object { [ordered]@{Path=$_.FullName;Sha256=(Get-ApiFileHash $_.FullName)} });
    GameAcceptance='Pending: game UI, editor, save/load and real host/client; machine tests and compilation do not establish these.'} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding UTF8
Write-Host "PASS: $zip (no installation; use matching APIShared build)"
