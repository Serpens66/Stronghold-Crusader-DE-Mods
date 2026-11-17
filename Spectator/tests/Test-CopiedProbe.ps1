$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'SpectatorCopiedProbe'
try {
    # The runtime has no RedBird version pin (it would lock players out after a harmless Extender update);
    # an unreviewed backend fails here at build time instead.
    $game = if ($env:SHCDE_GAME_DIR) { $env:SHCDE_GAME_DIR } else { 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
    $extender = Join-Path $game 'BepInEx\plugins\000shcdese'
    if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $extender 'RedBird.Backends.NativeX64.dll')).Version -ne [version]'1.5.0.0') {
        throw 'Unreviewed RedBird NativeX64 backend: re-audit the Spectator detours (Indirect, displaced bytes, trampolines) and update this gate.'
    }
    & dotnet msbuild (Join-Path $PSScriptRoot 'CopiedProbeTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
    if ($LASTEXITCODE -ne 0) { throw 'Copied probe fixture build failed.' }
    & (Join-Path $output 'SpectatorCopiedProbeTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Copied probe fixture failed.' }
    # The runtime has no live copied-entry probe (it disabled features on unlucky starts): candidate and
    # trampoline checks before Enable and patch checks after it cover the contract.
    $native = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../src/SpectatorRuntime.Native.cs'))
    if ($native -match 'CopiedProbeImage|ProbeAvailabilityBackend|ProbeVisualBackend|AllocHGlobal\(|GetName\(\)\.Version' -or
        [regex]::Matches($native, 'VerifyTrampoline\(candidate\.TrampolineAddress, candidate\.TrampolineSize, entry, (AvailabilityEntry|expected)\)').Count -ne 2 -or
        [regex]::Matches($native, 'VerifyAvailabilityPatch\(candidate, entry\)').Count -ne 1 -or
        [regex]::Matches($native, 'VerifyVisualPatch\((map|resource), image \+ Visual(Map|Resource)Rva').Count -ne 2) {
        throw 'Spectator detours must be checked on the real hooks without a live copied-entry probe.'
    }
    Write-Host 'PASS: Spectator detours have no live probe and are checked on the real hooks.'
} finally { Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue }
