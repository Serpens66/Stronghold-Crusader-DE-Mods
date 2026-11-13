[CmdletBinding()]
param([switch]$DevDiag)
$ErrorActionPreference = 'Stop'
$relayRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$relayWorkspace = [IO.Path]::GetFullPath((Join-Path $relayRoot '../..'))
. (Join-Path $relayWorkspace 'Shared/Tools/BranchBuild/BranchBuild.Common.ps1')
function Step([string]$label) { Write-Host ('[{0:HH:mm:ss.fff}] {1}' -f [DateTime]::Now,$label) }
Step 'PlayerControlRelayTest: working sources, public develop SDK, no installation.'
if ($DevDiag -and ((git -C $relayWorkspace branch --show-current) -join '').Trim() -eq 'main') { throw 'DEVDIAG is not permitted on main.' }
$sdk = Get-CachedApiSdk -Repository $relayWorkspace -ApiRepository (Join-Path $relayWorkspace 'APIShared') -Branch develop
$apiPackage = Join-Path $sdk.Root 'BepInEx/plugins/APIShared_Serp'
$proof = Get-Content -LiteralPath (Join-Path $sdk.Root '.local/build-proof.json') -Raw | ConvertFrom-Json
$game = $proof.GameDir
$msbuild = $proof.MSBuild
if (-not $game -or -not (Test-Path -LiteralPath $msbuild)) { throw 'The verified SDK must provide its game/MSBuild references.' }
Step 'Scoped runtime and dependency checks'
& (Join-Path $relayWorkspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -Workspace $relayWorkspace -ModDirectory 'Testmods/PlayerControlRelayTest' -ConsumerOnly
& (Join-Path $relayWorkspace 'Shared/Tools/Validation/Test-DependencyMetadata.ps1') -ProjectFiles (Join-Path $relayRoot 'PlayerControlRelayTest.csproj')
foreach ($file in Get-ChildItem -LiteralPath $relayRoot -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj','.ps1','.bat','.json','.md' -and $_.FullName -notmatch '[\\/](obj|bin|BepInEx|\.local)[\\/]' }) {
    $content = [IO.File]::ReadAllText($file.FullName)
    if ($content -match '(?<!\r)\n|\r(?!\n)') { throw "CRLF required: $($file.FullName)" }
}
Step 'Behavior and actual MessagePack tests'
& dotnet run --project (Join-Path $relayRoot 'tests/PlayerControlRelayCoreTests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Relay core tests failed.' }
& dotnet run --project (Join-Path $relayRoot 'tests/PlayerControlRelayWireTests.csproj') --configuration Release -p:BaseIntermediateOutputPath=obj/Wire/
if ($LASTEXITCODE -ne 0) { throw 'Relay wire tests failed.' }
Step 'Compile against actual game assembly and verified develop APIShared'
& $msbuild (Join-Path $relayRoot 'PlayerControlRelayTest.csproj') /nologo /verbosity:minimal /p:Configuration=Release /p:ImportDirectoryBuildTargets=false ("/p:ApiSharedDir="+$apiPackage) ("/p:GameDir="+$game) ("/p:SerpDevDiagnostics="+$DevDiag.IsPresent.ToString().ToLowerInvariant())
if ($LASTEXITCODE -ne 0) { throw 'Relay runtime compilation failed.' }
$output = Join-Path $relayRoot 'BepInEx/plugins/PlayerControlRelayTest_Serp'
$local = Join-Path $relayRoot '.local'
[void][IO.Directory]::CreateDirectory($local)
Copy-Item -LiteralPath (Join-Path $relayRoot 'info.json') -Destination $output -Force
# Zip only the testmod; dependencies remain supplied by the chosen public SDK.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = Join-Path $local 'PlayerControlRelayTest.zip'
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package }
$archive = [IO.Compression.ZipFile]::Open($package,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $output -File) {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,('BepInEx/plugins/PlayerControlRelayTest_Serp/'+$file.Name),[IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $archive.Dispose() }
$records = @(Get-ChildItem -LiteralPath $output -File | ForEach-Object { [ordered]@{ Name=$_.Name; Sha256=(Get-BranchHash $_.FullName) } })
$inputs = @(Get-ChildItem -LiteralPath $relayRoot -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj','.ps1','.bat','.json','.md' -and $_.FullName -notmatch '[\\/](obj|bin|BepInEx|\.local)[\\/]' } | ForEach-Object { [ordered]@{ Path=$_.FullName.Substring($relayRoot.Length+1); Sha256=(Get-BranchHash $_.FullName) } })
$record = [ordered]@{ BuiltUtc=[DateTime]::UtcNow.ToString('o'); WorkspaceCommit=((git -C $relayWorkspace rev-parse HEAD)-join ''); ApiBranch='develop'; ApiCommit=$sdk.Commit; DevDiag=$DevDiag.IsPresent; Installed=$false; Inputs=$inputs; Files=$records; Package=$package; PackageSha256=(Get-BranchHash $package) }
[IO.File]::WriteAllText((Join-Path $local 'build.json'),(($record|ConvertTo-Json -Depth 6)+"`r`n"),[Text.UTF8Encoding]::new($false))
Step ('PASS: '+$package+'; not installed.')
