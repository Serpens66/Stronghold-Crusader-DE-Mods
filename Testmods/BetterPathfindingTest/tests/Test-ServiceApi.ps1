$ErrorActionPreference = 'Stop'
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$hostAssembly = Join-Path $PSScriptRoot 'bin/Release/net10.0/BetterPathfindingTest.Tests.dll'
& dotnet build (Join-Path $PSScriptRoot 'client/ClientProbe.csproj') -c Release --nologo "/p:HostAssembly=$hostAssembly"
if ($LASTEXITCODE) { throw 'Independent API consumer compilation failed.' }
& dotnet run --no-build --project (Join-Path $PSScriptRoot 'BetterPathfindingTest.Tests.csproj') -c Release -- --external-client (Join-Path $PSScriptRoot 'client/bin/Release/net10.0/NavigationClientProbe.dll')
if ($LASTEXITCODE) { throw 'External API consumer behavior failed.' }
