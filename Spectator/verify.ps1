[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
function Check([string]$file, [string[]]$arguments = @()) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $file @arguments
    if ($LASTEXITCODE -ne 0) { throw "Check failed: $file" }
}
$workspace = Split-Path -Parent $PSScriptRoot
Check (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') @('-ModDirectory','Spectator','-ConsumerOnly')
Check (Join-Path $workspace 'Shared/Tools/Validation/Test-IngameTooltips.ps1') @('-Workspace',$workspace,'-ModDirectories',$PSScriptRoot)
foreach ($name in @('ActionPolicy','SpectatorSelection','SpectatorControllers','SavePolicy','CreditFocus','OptionalGates','OptionalPowers','CompactLayout','NativeActions','NativeOrderContract','NativePowerContext','InstalledContracts')) {
    Write-Host "[Spectator] $name"
    Check (Join-Path $PSScriptRoot "tests/Test-$name.ps1")
}
$files = @(Get-ChildItem (Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'Patches'),(Join-Path $PSScriptRoot 'Override'),(Join-Path $PSScriptRoot 'Locales') -Recurse -File)
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "Non-CRLF source: $($file.FullName)" }
    if ($file.Extension -eq '.xaml') { [xml]$xml = $text }
    if ($file.Extension -eq '.cs' -and $text -match 'Newtonsoft\.Json|System\.Text\.Json|JavaScriptSerializer|UnpatchSelf\(') { throw "Forbidden runtime dependency/teardown: $($file.FullName)" }
}
$locales = @(Get-ChildItem (Join-Path $PSScriptRoot 'Locales') -Filter '*.txt' -File)
$reference = $null
foreach ($locale in $locales) {
    $keys = @([IO.File]::ReadAllLines($locale.FullName) | Where-Object { $_ -match '^([^#=]+)=' } | ForEach-Object { ($_ -split '=',2)[0].Trim() } | Sort-Object)
    if (-not $keys.Count -or @($keys | Group-Object | Where-Object Count -gt 1).Count) { throw "Missing/duplicate locale keys: $locale" }
    if ($null -eq $reference) { $reference = $keys }
    elseif (@(Compare-Object $reference $keys).Count) { throw "Locale parity: $locale" }
}
Write-Host 'PASS: relevant Spectator runtime/controller, native, integration, XAML, tooltip, locale and CRLF checks.'
