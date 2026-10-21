$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent (Split-Path -Parent $root)
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'CreditFocus'
& dotnet msbuild (Join-Path $PSScriptRoot 'CreditFocusTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Credit focus fixture build failed.' }
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $output 'CreditFocusTests.dll')))
$count = [Spectator.CreditFocusTests]::Run()
Write-Host "PASS: $count production input adapter checks (focus, typing, paste, overflow, hiding and detach)."
