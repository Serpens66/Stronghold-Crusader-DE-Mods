$ErrorActionPreference = 'Stop'
$modDirectory = Split-Path -Parent $PSScriptRoot
$policy = [IO.File]::ReadAllText((Join-Path $modDirectory 'src\TestActionPolicy.cs'))
$toolPolicy = [IO.File]::ReadAllText((Join-Path $modDirectory 'src\TestToolPolicies.cs'))
$toolPolicy += [IO.File]::ReadAllText((Join-Path $modDirectory 'src\GateHudLifecycle.cs'))
$tests = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ActionPolicyTests.cs'))
Add-Type -TypeDefinition ($toolPolicy + [Environment]::NewLine + $policy.Replace('using System;', '').Replace('using System.Globalization;', '') + [Environment]::NewLine + $tests).Replace('CultureInfo.', 'System.Globalization.CultureInfo.').Replace('NumberStyles.', 'System.Globalization.NumberStyles.') -Language CSharp
$count = [SpectatorEditorBuildTest.ActionPolicyTests]::Run()
Write-Host "PASS: $count action policy checks (amounts, overflow, exactly once, sessions, force isolation)."
