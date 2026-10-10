#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $PSScriptRoot 'tests\TrailDeletion.Tests'
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'src\TrailDeletionPolicy.cs'),
    (Join-Path $PSScriptRoot 'src\TrailDeletionViewModel.cs'),
    (Join-Path $testRoot 'Stubs.cs'),
    (Join-Path $testRoot 'Program.cs'))
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('BugfixesAndQoL-DeleteTrail-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
try {
    [TrailDeletionTests]::Run($fixture)
    $root = Join-Path $fixture 'CustomTrails'
    $outside = Join-Path $fixture 'Outside'
    [IO.Directory]::CreateDirectory($outside) | Out-Null
    $junction = Join-Path $root 'Link'
    New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
    try {
        if ([TrailDeletionTests]::Resolve('Link', $junction, $root)) { throw 'Junction target was accepted.' }
        $package = Join-Path $root 'package'
        [IO.Directory]::CreateDirectory($package) | Out-Null
        $nestedLink = Join-Path $package 'Link'
        New-Item -ItemType Junction -Path $nestedLink -Target $outside | Out-Null
        try {
            if ([TrailDeletionTests]::Resolve('package', $package, $root)) { throw 'Nested junction was accepted.' }
        } finally { [IO.Directory]::Delete($nestedLink) }
        Write-Output 'Direct and nested junction rejection passed.'
    } finally { [IO.Directory]::Delete($junction) }
} finally {
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixture)) -cne $expectedParent) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
