$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'SpectatorCopiedProbe'
try {
    & dotnet msbuild (Join-Path $PSScriptRoot 'CopiedProbeTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
    if ($LASTEXITCODE -ne 0) { throw 'Copied probe fixture build failed.' }
    & (Join-Path $output 'SpectatorCopiedProbeTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Copied probe fixture failed.' }
    # Both runtime probes must use the full scan-window image, never a short heap copy.
    $native = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../src/SpectatorRuntime.Native.cs'))
    if ([regex]::Matches($native, 'Shared\.CopiedProbeImage\.Create\(entry, NativeDetourBackend\.Instance\.Options\)').Count -ne 2 -or $native -match 'AllocHGlobal\(64\)') {
        throw 'Spectator copied-entry probes must allocate the CopiedProbeImage scan window.'
    }
    Write-Host 'PASS: both Spectator copied-entry probes use the bounded scan-window image.'
} finally { Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue }
