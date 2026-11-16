[CmdletBinding()]
param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$restartRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$restartWorkspace = [IO.Path]::GetFullPath((Join-Path $restartRoot '../..'))
$restartLocal = Join-Path $restartRoot '.local'
[void][IO.Directory]::CreateDirectory($restartLocal)
. (Join-Path $restartWorkspace 'Shared/Tools/BranchBuild/BranchBuild.Common.ps1')
function Step([string]$label) { Write-Host ('[{0:HH:mm:ss.fff}] {1}' -f [DateTime]::Now,$label) }
$restartVswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$restartMsbuild = (& $restartVswhere -latest -products '*' -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
if (!$restartMsbuild -or !(Test-Path -LiteralPath $restartMsbuild)) { throw 'MSBuild not found.' }
$restartApi = Join-Path $GameDir 'BepInEx/plugins/APIShared_Serp'
$restartApiBefore = Get-BranchHash (Join-Path $restartApi 'APIShared.dll')
$restartNative = Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
Step 'Working testmod sources; installed dependencies; no installation or dependency rebuild.'
& (Join-Path $restartWorkspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -Workspace $restartWorkspace -ModDirectory 'Testmods/MultiplayerRestartTest' -ConsumerOnly
Step 'Production protocol tests'
& dotnet run --project (Join-Path $restartRoot 'tests/RestartProtocol.Tests.csproj') --configuration Release -p:ImportDirectoryBuildTargets=false
if ($LASTEXITCODE -ne 0) { throw 'Restart protocol tests failed.' }
$restartProject = Join-Path $restartRoot 'MultiplayerRestartTest.csproj'
Step 'Actual game IL, barrier, serializer, snapshot and failure-path fixtures'
& $restartMsbuild $restartProject /nologo /verbosity:quiet /clp:ErrorsOnly /p:Configuration=Release /p:Fixture=true /p:ImportDirectoryBuildTargets=false ("/p:GameDir="+$GameDir) ("/p:ApiSharedDir="+$restartApi) ("/flp:LogFile="+(Join-Path $restartLocal 'fixture-build.log')+';Verbosity=minimal')
if ($LASTEXITCODE -ne 0) { throw 'Runtime fixture compilation failed.' }
& (Join-Path $restartLocal 'fixture/MultiplayerRestartTest.exe') $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Actual-runtime fixtures failed.' }
Step 'Compile experimental runtime against actual installed assemblies'
& $restartMsbuild $restartProject /nologo /verbosity:quiet /clp:ErrorsOnly /p:Configuration=Release /p:ImportDirectoryBuildTargets=false ("/p:GameDir="+$GameDir) ("/p:ApiSharedDir="+$restartApi) ("/flp:LogFile="+(Join-Path $restartLocal 'runtime-build.log')+';Verbosity=minimal')
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
if ((Get-BranchHash (Join-Path $restartApi 'APIShared.dll')) -ne $restartApiBefore) { throw 'Installed APIShared changed during build.' }
$restartOutput = Join-Path $restartRoot 'BepInEx/plugins/MultiplayerRestartTest_Serp'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$restartPackage = Join-Path $restartLocal 'MultiplayerRestartTest.zip'
if (Test-Path -LiteralPath $restartPackage) { Remove-Item -LiteralPath $restartPackage }
$restartArchive = [IO.Compression.ZipFile]::Open($restartPackage,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($restartName in @('MultiplayerRestartTest.dll','MultiplayerRestartTest.pdb','info.json')) {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($restartArchive,(Join-Path $restartOutput $restartName),('BepInEx/plugins/MultiplayerRestartTest_Serp/'+$restartName),[IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $restartArchive.Dispose() }
$restartFiles = @(Get-ChildItem -LiteralPath $restartOutput -File | ForEach-Object { [ordered]@{ Name=$_.Name; Sha256=(Get-BranchHash $_.FullName) } })
$restartInputs = @(Get-ChildItem -LiteralPath $restartRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](obj|bin|\.local|BepInEx)[\\/]' -and $_.Extension -in '.cs','.csproj','.json','.ps1','.bat','.config','.md' } | ForEach-Object { [ordered]@{ Path=$_.FullName.Substring($restartRoot.Length+1); Sha256=(Get-BranchHash $_.FullName) } })
$restartRecord = [ordered]@{ BuiltUtc=[DateTime]::UtcNow.ToString('o'); Version='0.1.1'; Experimental=$true; Installed=$false; MultiplayerAccepted=$false; NativeSha256=(Get-BranchHash $restartNative); ManagedSha256=(Get-BranchHash (Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll')); ApiSharedSha256=$restartApiBefore; Files=$restartFiles; Inputs=$restartInputs; Package=$restartPackage; PackageSha256=(Get-BranchHash $restartPackage) }
[IO.File]::WriteAllText((Join-Path $restartLocal 'build.json'),(($restartRecord|ConvertTo-Json -Depth 6)+"`r`n"),[Text.UTF8Encoding]::new($false))
Step ('PASS: '+$restartPackage+'; not installed.')
