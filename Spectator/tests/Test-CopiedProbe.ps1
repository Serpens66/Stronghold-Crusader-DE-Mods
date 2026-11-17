$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'SpectatorCopiedProbe'
try {
    & dotnet msbuild (Join-Path $PSScriptRoot 'CopiedProbeTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
    if ($LASTEXITCODE -ne 0) { throw 'Copied probe fixture build failed.' }
    & (Join-Path $output 'SpectatorCopiedProbeTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Copied probe fixture failed.' }
    # The runtime has no live copied-entry probe (it disabled features on unlucky starts): the
    # RedBird pin, candidate checks before Enable and patch checks after it cover the contract.
    $native = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../src/SpectatorRuntime.Native.cs'))
    if ($native -match 'CopiedProbeImage|ProbeAvailabilityBackend|ProbeVisualBackend|AllocHGlobal\(' -or
        [regex]::Matches($native, 'VerifyAvailabilityPatch\(candidate, entry\)').Count -ne 1 -or
        [regex]::Matches($native, 'VerifyVisualPatch\((map|resource), image \+ Visual(Map|Resource)Rva').Count -ne 2) {
        throw 'Spectator detours must be checked on the real hooks without a live copied-entry probe.'
    }
    Write-Host 'PASS: Spectator detours have no live probe and are checked on the real hooks.'
} finally { Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue }
