# T1 duplicate case: puts the payload package into BepInEx/plugins as well (-Present),
# or removes exactly that copy again (-Absent). Only files identical to the package are deleted.
param([Parameter(Mandatory)][ValidateSet('Present', 'Absent')][string]$State)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
$package = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\payload\package\' + $ProbePayloadGuid)))
$target = Join-Path (Get-ProbeGame) ('BepInEx\plugins\' + $ProbePayloadGuid)
$files = @(Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object { $_.FullName.Substring($package.Length + 1) })
if ($State -eq 'Present') {
    if (Test-Path -LiteralPath $target) { throw "Already present: $target" }
    Copy-Item -LiteralPath $package -Destination $target -Recurse
    Write-Output "Local copy installed: $target"
    return
}
if (-not (Test-Path -LiteralPath $target)) { Write-Output 'No local copy present.'; return }
$present = @(Get-ChildItem -LiteralPath $target -Recurse -File | ForEach-Object { $_.FullName.Substring($target.Length + 1) })
$unknown = @($present | Where-Object { $files -notcontains $_ })
if ($unknown.Count) { throw ('Local copy contains unknown files; left untouched: ' + ($unknown -join ', ')) }
foreach ($relative in $present) {
    if ((Get-ProbeSha256 (Join-Path $target $relative)) -ne (Get-ProbeSha256 (Join-Path $package $relative))) { throw "Changed file, left untouched: $relative" }
}
Remove-Item -LiteralPath $target -Recurse -Force
Write-Output "Local copy removed: $target"
