$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $projectRoot 'SaveExtractor.csproj'
$distRoot = Join-Path $projectRoot 'dist'
$stage = Join-Path $distRoot 'win-x64'
$archive = Join-Path $distRoot 'SaveExtractor-portable-win-x64.zip'

$resolvedDist = [IO.Path]::GetFullPath($distRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$resolvedStage = [IO.Path]::GetFullPath($stage)
if (-not $resolvedStage.StartsWith($resolvedDist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe staging path: $resolvedStage"
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

& dotnet publish $project -c Release -r win-x64 --self-contained true -o $stage -m:1 `
    -p:PublishSingleFile=true -p:PublishTrimmed=false -p:RuntimeFrameworkVersion=10.0.11 `
    -p:NuGetAudit=false -p:UseSharedCompilation=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $stage 'SaveExtractor.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Portable executable missing: $exe"
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'SaveExtractor-Start.cmd') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'ANLEITUNG.txt') -Destination $stage

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -CompressionLevel Optimal -Force
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
    throw "Distribution archive missing: $archive"
}
Write-Host "Distribution erstellt: $archive"
