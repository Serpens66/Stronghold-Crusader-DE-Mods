$ErrorActionPreference = 'Stop'
$modDirectory = Split-Path -Parent $PSScriptRoot
$policy = [IO.File]::ReadAllText((Join-Path $modDirectory 'src\TestActionPolicy.cs'))
$toolPolicy = [IO.File]::ReadAllText((Join-Path $modDirectory 'src\TestToolPolicies.cs'))
$toolPolicy += [IO.File]::ReadAllText((Join-Path $modDirectory 'src\GateHudLifecycle.cs'))
$toolPolicy += [IO.File]::ReadAllText((Join-Path $modDirectory 'src\ObserverBuildPolicy.cs'))
$tests = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ActionPolicyTests.cs'))
Add-Type -TypeDefinition ($toolPolicy + [Environment]::NewLine + $policy.Replace('using System;', '').Replace('using System.Globalization;', '') + [Environment]::NewLine + $tests).Replace('CultureInfo.', 'System.Globalization.CultureInfo.').Replace('NumberStyles.', 'System.Globalization.NumberStyles.') -Language CSharp
$count = [Spectator.ActionPolicyTests]::Run()
# Locale argument names are a user-facing diagnostic contract, independent of sentence wording.
$detailArguments = @('Player','Good','Current','Stored','Incoming','Amount','Expected','Actual','Limit') | Sort-Object
foreach ($locale in Get-ChildItem -LiteralPath (Join-Path $modDirectory 'Locales') -Filter '*.txt' -File) {
    $entries = @{}
    foreach ($line in [IO.File]::ReadAllLines($locale.FullName)) {
        if ($line -match '^(Tools\.[^=]+)=(.*)$') { $entries[$Matches[1]] = $Matches[2] }
    }
    foreach ($key in @('Tools.Overflow','Tools.NegativeBalance','Tools.BalancePostcondition','Tools.BalanceOperationFailed','Tools.BalanceUnknown','Tools.BalanceDetails')) {
        if (-not $entries[$key]) { throw "Missing credit diagnostic locale: $($locale.Name) $key" }
    }
    $actualArguments = @([regex]::Matches($entries['Tools.BalanceDetails'],'\{(\w+)\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
    if (@(Compare-Object $detailArguments $actualArguments).Count) { throw "Credit diagnostic placeholders changed: $($locale.Name)" }
}
Write-Host "PASS: $count action policy checks (amounts, overflow, exactly once, sessions, force isolation)."
Write-Host 'PASS: credit diagnostic locale arguments in every language.'
