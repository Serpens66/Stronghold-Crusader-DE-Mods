[CmdletBinding()]
param(
    [string]$GameDir = $env:SHCDE_GAME_DIR,
    [string]$ApiSharedDir = $env:SHCDE_PITCH_MOAT_API_DIR,
    [string]$MsBuild = $env:SHCDE_MSBUILD,
    [switch]$DevDiag
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspace 'APIShared\tools\BuildProof.ps1')

Remove-Item Env:SHCDE_DEVDIAG -ErrorAction SilentlyContinue
if ($DevDiag) {
    $branch = & git -C $workspace branch --show-current
    if ($LASTEXITCODE -ne 0 -or $branch -eq 'main') { throw 'Diagnostic builds require a verified non-main source.' }
}

if (!$GameDir) { $GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$extender = Join-Path $GameDir 'BepInEx\plugins\000shcdese'
if (!$ApiSharedDir) { $ApiSharedDir = Join-Path $GameDir 'BepInEx\plugins\APIShared_Serp' }
$ApiSharedDir = (Resolve-Path -LiteralPath $ApiSharedDir).Path
if (!$MsBuild) { $MsBuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' }
$MsBuild = (Resolve-Path -LiteralPath $MsBuild).Path

$csproj = Join-Path $project 'PitchMoatLineTest.csproj'
$manifestPath = Join-Path $project 'info.json'
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
$apiDependency = @($manifest.Dependencies | Where-Object GUID -eq 'APIShared_Serp')
if ($apiDependency.Count -ne 1) { throw 'Expected one hard APIShared dependency.' }
foreach ($dependency in @(
    @{ Directory = $ApiSharedDir; Guid = $apiDependency[0].GUID; Minimum = $apiDependency[0].MinimumVersion },
    @{ Directory = $extender; Guid = '000shcdese'; Minimum = $manifest.MinimumScriptExtenderVersion }
)) {
    $selected = [IO.File]::ReadAllText((Join-Path $dependency.Directory 'info.json')) | ConvertFrom-Json
    if ($selected.GUID -ne $dependency.Guid -or [version]$selected.Version -lt [version]$dependency.Minimum) {
        throw "Incompatible SDK selection: expected $($dependency.Guid) >= $($dependency.Minimum) in $($dependency.Directory)."
    }
}

$head = & git -C $workspace rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source checkout.' }
$inputs = @(
    (Join-Path $ApiSharedDir 'APIShared.dll'),
    (Join-Path $extender 'SHCDESE.dll'),
    (Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'),
    (Join-Path $workspace 'Shared\Runtime\Diagnostics\DebugLogHelper.cs'),
    (Join-Path $workspace 'Shared\Runtime\Diagnostics\DevDiag.cs')
)
$source = Get-ChildItem -LiteralPath $project -Recurse -File | Where-Object {
    $_.FullName.Substring($project.Length) -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and
    $_.Extension -in @('.bat', '.cs', '.csproj', '.json', '.md', '.ps1', '.txt')
}
$inputs += $source.FullName
$inputRecords = @($inputs | Sort-Object -Unique | ForEach-Object {
    [ordered]@{ Path = $_; Sha256 = Get-ApiFileHash $_ }
})

$run = New-ShortWorkDirectory -Root $workspace -Kind a -Name 'pitch-moat-build' -Identity "PitchMoatLineTest working source at $head, no installation"
$package = Join-Path $run 'package'
$output = Join-Path $package ('BepInEx\plugins\' + $manifest.GUID)
$testsLog = Join-Path $run 'tests.log'
$compileLog = Join-Path $run 'compile.log'
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] Pitch and moat line test build: $run"

$validation = Join-Path $workspace 'Shared\Tools\Validation'
& (Join-Path $validation 'Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods\PitchMoatLineTest' -ConsumerOnly
& dotnet run --project (Join-Path $project 'tests\PitchMoatLineTest.Tests.csproj') --configuration Release *> $testsLog
if ($LASTEXITCODE -ne 0) { throw "Policy tests failed: $testsLog" }

if ($DevDiag) { $env:SHCDE_DEVDIAG = '1' }
& $MsBuild $csproj /t:Build /nologo /v:minimal "/p:GameDir=$GameDir" "/p:ExtenderDir=$extender" "/p:ApiSharedDir=$ApiSharedDir" "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$(Join-Path $run 'obj')/" *> $compileLog
if ($LASTEXITCODE -ne 0) { throw "Testmod compilation failed: $compileLog" }

$builtDll = Join-Path $output 'PitchMoatLineTest.dll'
if (-not (Test-Path -LiteralPath $builtDll -PathType Leaf)) { throw "The build did not produce $builtDll." }
if ([Reflection.AssemblyName]::GetAssemblyName($builtDll).Version.ToString(3) -ne $manifest.Version) {
    throw 'Built assembly version differs from the source manifest.'
}
if ((Get-ApiFileHash (Join-Path $output 'info.json')) -ne (Get-ApiFileHash $manifestPath)) { throw 'Packaged manifest differs from the source manifest.' }
$unexpected = @(Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
    $_.DirectoryName -ne $output -or $_.Name -notin @('PitchMoatLineTest.dll', 'PitchMoatLineTest.pdb', 'info.json', 'TESTPLAN.txt')
})
if ($unexpected.Count) { throw "Unexpected package output: $($unexpected.FullName -join ', ')" }
foreach ($inputRecord in $inputRecords) {
    if ((Get-ApiFileHash $inputRecord.Path) -ne $inputRecord.Sha256) { throw "Build input changed: $($inputRecord.Path)" }
}

$name = if ($DevDiag) { 'PitchMoatLineTest-devdiag.zip' } else { 'PitchMoatLineTest.zip' }
$zip = Join-Path $run $name
Compress-Archive -LiteralPath (Join-Path $package 'BepInEx') -DestinationPath $zip
$files = @(Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ Path = $_.FullName.Substring($package.Length + 1); Sha256 = Get-ApiFileHash $_.FullName }
})
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | Where-Object Name | ForEach-Object { $_.FullName.Replace('/', '\') })
    if (Compare-Object @($files.Path) $entries -CaseSensitive) { throw 'Archive contents differ from the package staging directory.' }
} finally {
    $archive.Dispose()
}
[ordered]@{
    CreatedUtc = [DateTime]::UtcNow.ToString('o')
    Head = $head
    Installed = $false
    DevDiag = $DevDiag.IsPresent
    Package = $zip
    PackageSha256 = Get-ApiFileHash $zip
    Inputs = $inputRecords
    Files = $files
    GameAcceptance = 'Pending: single-player, editor and real host/client acceptance.'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding UTF8
Write-Host "PASS: $zip (no installation)"
