[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'

foreach ($name in @('SerpsModsHost.dll','info.json','serps-modpack.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PackageDirectory $name) -PathType Leaf)) {
        throw "Missing host package file: $name"
    }
}
$info = [IO.File]::ReadAllText((Join-Path $PackageDirectory 'info.json')) | ConvertFrom-Json
$pack = [IO.File]::ReadAllText((Join-Path $PackageDirectory 'serps-modpack.json')) | ConvertFrom-Json
if ($info -isnot [pscustomobject] -or $pack -isnot [pscustomobject]) {
    throw 'Host info and pack manifest must be JSON objects.'
}
if ($pack.SchemaVersion -isnot [int] -or $pack.SchemaVersion -ne 2) {
    throw 'Host pack manifest must use schema 2.'
}
if ($info.GUID -cne 'SerpsMods_Serp' -or $pack.PackGuid -cne 'SerpsMods_Serp') {
    throw 'Host info and pack manifest must identify SerpsMods_Serp.'
}
if ($info.Version -isnot [string] -or $info.Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw 'Host info.json must contain a valid numeric Version.'
}
foreach ($field in @('HostVersion','PackVersion')) {
    if ($pack.$field -isnot [string] -or $pack.$field -cne $info.Version) {
        throw "Host pack $field '$($pack.$field)' differs from info.json Version '$($info.Version)'."
    }
}
$version = [version]$info.Version
# CLR assembly versions always have four components; a missing revision means zero.
$expected = [version]::new($version.Major, $version.Minor, $version.Build, [Math]::Max(0, $version.Revision))
$assembly = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $PackageDirectory 'SerpsModsHost.dll'))
if ($assembly.Name -cne 'SerpsModsHost' -or $assembly.Version -ne $expected) {
    throw "Host DLL '$($assembly.FullName)' differs from info.json Version '$($info.Version)'."
}
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] PASS: host package identity and version {1}.' -f [DateTime]::Now,$info.Version)
