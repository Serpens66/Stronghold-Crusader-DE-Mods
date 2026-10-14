$ErrorActionPreference = 'Stop'
# Builds only the isolated numeric test harness. Never invokes the mod build or installer.
$project = Join-Path $PSScriptRoot 'Tests.csproj'
$resultLog = Join-Path $PSScriptRoot 'results.log'
& dotnet run --project $project --configuration Release 2>&1 | Tee-Object -FilePath $resultLog
if ($LASTEXITCODE -ne 0) { throw "Market observation tests failed (exit $LASTEXITCODE)." }
