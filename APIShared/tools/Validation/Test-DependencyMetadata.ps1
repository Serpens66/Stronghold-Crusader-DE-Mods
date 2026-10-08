$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DependencyMetadata.Common.ps1')
$source = '[BepInDependency("other.author.api", "1.2.0")][BepInDependency("optional.mod", BepInDependency.DependencyFlags.SoftDependency)]'
$metadata = [pscustomobject]@{ Dependencies = @([pscustomobject]@{ GUID = 'other.author.api'; MinimumVersion = '1.2.0' }) }
Assert-PluginDependencyMetadata $source $metadata 'ArbitraryGuid'
$constantSource = 'const string DependencyGuid = "other.author.api"; const string Minimum = "1.2.0"; [BepInDependency(DependencyGuid, Minimum)]'
Assert-PluginDependencyMetadata $constantSource $metadata 'NamedConstants'
$presence = [pscustomobject]@{ Dependencies = @([pscustomobject]@{ GUID = 'presence.only' }) }
Assert-PluginDependencyMetadata '[BepInDependency("presence.only")]' $presence 'PresenceOnly'
$failures = 0
foreach ($fixture in @(
    @{ Source = $source; Metadata = [pscustomobject]@{ Dependencies = @() } },
    @{ Source = $source; Metadata = [pscustomobject]@{ Dependencies = @([pscustomobject]@{ GUID = 'other.author.api'; MinimumVersion = '9.0.0' }) } },
    @{ Source = $source + $source; Metadata = $metadata },
    @{ Source = $source; Metadata = [pscustomobject]@{ Dependencies = @($metadata.Dependencies[0], $metadata.Dependencies[0]) } },
    @{ Source = '[BepInDependency("broken", "invalid")]'; Metadata = [pscustomobject]@{ Dependencies = @([pscustomobject]@{ GUID = 'broken'; MinimumVersion = 'invalid' }) } },
    @{ Source = $source; Metadata = [pscustomobject]@{ Dependencies = @([pscustomobject]@{ GUID = 'other.author.api'; MinimumVersion = '1.2.0'; MaximumVersion = '1.0.0' }) } }
)) {
    $rejected = $false
    try { Assert-PluginDependencyMetadata $fixture.Source $fixture.Metadata 'InvalidFixture' } catch { $rejected = $true }
    if (-not $rejected) { throw 'An invalid dependency fixture was accepted.' }
    $failures++
}
Write-Host "PASS: generic dependency declarations, arbitrary GUIDs, constants, presence-only and optional dependencies; $failures malformed fixtures rejected."
