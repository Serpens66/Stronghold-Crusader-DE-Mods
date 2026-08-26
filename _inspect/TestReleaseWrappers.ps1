[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$wrappers = @(Get-ChildItem -LiteralPath $workspaceRoot -Filter 'release.bat' -File -Recurse |
    Where-Object { $_.FullName -notlike (Join-Path $workspaceRoot '_inspect\*') } |
    Sort-Object FullName)

Assert-True ($wrappers.Count -gt 0) 'No release wrappers found.'

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = [IO.Path]::GetFullPath((Join-Path $tempBase ('SHCDE-ReleaseWrapperTests-' + [Guid]::NewGuid().ToString('N'))))
Assert-True ($tempRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and $tempRoot.Length -gt $tempBase.Length) 'Unsafe temporary test directory.'
$sharedReleaseDirectory = Join-Path $tempRoot 'Shared\Release'
$argumentLog = Join-Path $tempRoot 'arguments.txt'
$expectedExitCode = 37

try {
    [IO.Directory]::CreateDirectory($sharedReleaseDirectory) | Out-Null

    $mock = @"
@echo off
>"$argumentLog" echo %*
exit /b $expectedExitCode
"@
    [IO.File]::WriteAllText(
        (Join-Path $sharedReleaseDirectory 'Invoke-Release.bat'),
        ($mock -replace "`r?`n", "`r`n"),
        [Text.Encoding]::ASCII)

    foreach ($wrapper in $wrappers) {
        $modName = Split-Path -Leaf $wrapper.DirectoryName
        $content = [IO.File]::ReadAllText($wrapper.FullName)

        Assert-True ($content -match [regex]::Escape("Invoke-Release.bat`" $modName /called %*")) "$modName does not forward all arguments."
        Assert-True ($content -match 'findstr\s+/I\s+/C:"/nopause"\s+>nul\s+\|\|\s+pause') "$modName does not suppress pause for /nopause."

        $isolatedModDirectory = Join-Path $tempRoot $modName
        [IO.Directory]::CreateDirectory($isolatedModDirectory) | Out-Null
        Copy-Item -LiteralPath $wrapper.FullName -Destination (Join-Path $isolatedModDirectory 'release.bat')
        Remove-Item -LiteralPath $argumentLog -Force -ErrorAction SilentlyContinue

        $wrapperOutput = @(& (Join-Path $isolatedModDirectory 'release.bat') /nopause /noprompt 2>&1)
        $actualExitCode = $LASTEXITCODE
        Assert-True ($actualExitCode -eq $expectedExitCode) "$modName returned $actualExitCode instead of $expectedExitCode."
        Assert-True (-not ($wrapperOutput -match 'Press any key|Drücken Sie eine beliebige Taste')) "$modName paused despite /nopause."

        $arguments = [IO.File]::ReadAllText($argumentLog).Trim()
        Assert-True ($arguments -ceq "$modName /called /nopause /noprompt") "$modName forwarded unexpected arguments: $arguments"

        $manualOutput = @('' | & (Join-Path $isolatedModDirectory 'release.bat') 2>&1)
        $manualExitCode = $LASTEXITCODE
        Assert-True ($manualExitCode -eq $expectedExitCode) "$modName changed the exit code after a manual pause."
        Assert-True (@($manualOutput -match 'Press any key|beliebige Taste').Count -gt 0) "$modName did not pause for a manual invocation."
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

$global:LASTEXITCODE = 0
Write-Host "PASS: $($wrappers.Count) release wrappers forward arguments, preserve exit codes, and honor /nopause."
