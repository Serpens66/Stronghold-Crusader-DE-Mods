[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$mod = Split-Path -Parent $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $mod '..'))
. (Join-Path $mod 'tools/Package.Common.ps1')
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root $workspace -Kind t -Name pop-package -Identity 'Timestamp-independent asset copying and install gates'
$output = Join-Path $fixture 'package'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$sdk = [pscustomobject]@{Directory=$env:SHCDE_API_SHARED_DIR}
$layout = Get-ExtraFeaturesPackageLayout -ModDirectory $mod -OutputDirectory $output -ApiSharedDir $sdk.Directory
$checks = 0

function Assert-Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Assert-RejectedBeforeCopy($Layout, [string]$Destination, [string]$ExpectedError) {
    $rejected = $false
    try { Install-ExtraFeaturesPackage $Layout -Destination $Destination }
    catch {
        if ($_.Exception.Message -notlike $ExpectedError) { throw }
        $rejected = $true
    }
    Assert-Check $rejected 'Invalid package was accepted.'
    Assert-Check (-not [IO.Directory]::Exists($Destination)) 'Installation wrote files before rejecting an invalid package.'
}

# Exercise the real project and MSBuild copy tasks, not a reimplementation of
# PreserveNewest/Always. Equal-length stale files reproduce timestamp-only skips.
foreach ($offset in @(0, 1)) {
    foreach ($asset in $layout.Assets) {
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($asset.Package))
        $bytes = [IO.File]::ReadAllBytes($asset.Source)
        Assert-Check ($bytes.Length -gt 0) 'Fixture needs nonempty actual assets.'
        $bytes[0] = $bytes[0] -bxor 1
        [IO.File]::WriteAllBytes($asset.Package, $bytes)
        [IO.File]::SetLastWriteTimeUtc($asset.Package, ([IO.File]::GetLastWriteTimeUtc($asset.Source)).AddDays($offset))
    }
    $arguments = @((Join-Path $mod 'ExtraFeatures.csproj'), '/t:Build', '/nologo', '/verbosity:minimal',
        '/p:Configuration=Debug', "/p:ApiSharedDir=$($sdk.Directory)", "/p:OutputPath=$output",
        "/p:BaseIntermediateOutputPath=$(Join-Path $fixture 'obj')/", "/p:IntermediateOutputPath=$(Join-Path $fixture 'obj/Debug')/")
    & $msbuild @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Asset regression fixture build failed.' }
    Assert-ExtraFeaturesPackage $layout
    $checks++
    Write-Host "PASS: stale equal-length assets overwritten; output timestamp source+$offset day(s)."
}

# Each evaluated asset must be guarded, including future added/localized files.
$case = 0
foreach ($asset in $layout.Assets) {
    $saved = [IO.File]::ReadAllBytes($asset.Package)
    try {
        [IO.File]::Delete($asset.Package)
        Assert-RejectedBeforeCopy $layout (Join-Path $fixture "missing-$case") 'Missing package content:*'
        $changed = [byte[]]$saved.Clone()
        $changed[0] = $changed[0] -bxor 1
        [IO.File]::WriteAllBytes($asset.Package, $changed)
        Assert-RejectedBeforeCopy $layout (Join-Path $fixture "damaged-$case") 'Source/package mismatch:*'
    } finally { [IO.File]::WriteAllBytes($asset.Package, $saved) }
    $case++
}

$destination = Join-Path $fixture 'installed'
[void][IO.Directory]::CreateDirectory($destination)
$foreign = Join-Path $destination 'foreign-file.txt'
[IO.File]::WriteAllText($foreign, 'Preserve unrelated installed files.')
$foreignHash = Get-Sha256Hex $foreign
Install-ExtraFeaturesPackage $layout -Destination $destination
Assert-Check ((Get-Sha256Hex $foreign) -ceq $foreignHash) 'Valid installation changed an unrelated file.'
$asset = $layout.Assets[0]
$installedAsset = Resolve-ExtraFeaturesPackagePath $destination $asset.RelativePath
[IO.File]::WriteAllText($installedAsset, 'Damaged after installation.')
$rejected = $false
try { Assert-ExtraFeaturesPackage $layout -InstalledDirectory $destination }
catch {
    if ($_.Exception.Message -notlike 'Source/installation mismatch:*') { throw }
    $rejected = $true
}
Assert-Check $rejected 'Post-install source/hash verification missed damaged content.'
$null = & (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory $sdk.Directory
Write-Host "PASS: $checks package regression checks; fixture retained at $fixture"
