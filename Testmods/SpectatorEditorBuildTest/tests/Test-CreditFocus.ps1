$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent (Split-Path -Parent $root)
$output = Join-Path $workspace ('_inspect\SpectatorVanillaToolbar-20261016\focus\' + [guid]::NewGuid().ToString('N'))
& dotnet msbuild (Join-Path $PSScriptRoot 'CreditFocusTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Credit focus fixture build failed.' }
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $output 'CreditFocusTests.dll')))
$count = [SpectatorEditorBuildTest.CreditFocusTests]::Run()
Write-Host "PASS: $count production input adapter checks (focus, typing, paste, overflow, hiding and detach)."
