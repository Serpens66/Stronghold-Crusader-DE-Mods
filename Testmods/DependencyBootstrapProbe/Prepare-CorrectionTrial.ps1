param([Parameter(Mandatory=$true)][string]$AcceptedShadowRoot)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Check-Run.ps1') -Mode Active -RequireShadow -LogPath (Join-Path $AcceptedShadowRoot 'DependencyBootstrapProbe.trace.log') -ExpectedArtifactRoot $AcceptedShadowRoot -GameDir $AcceptedShadowRoot
$lines = @(Get-Content -LiteralPath (Join-Path $AcceptedShadowRoot 'DependencyBootstrapProbe.trace.log'))
$start = -1
for ($i=0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '\[DBP:SESSION\]') { $start=$i } }
if ($start -lt 0) { throw 'Missing session.' }
$events = @(foreach ($line in $lines[$start..($lines.Count-1)]) { if ($line -match '\[DBP:(SHADOW_CANDIDATE|SHADOW_DEPENDENCY|SHADOW_INDEX)\]\s*(.*)$') { [pscustomobject]@{ Kind=$Matches[1]; Data=($Matches[2] | ConvertFrom-Json) } } })
$resolved = @($events | Where-Object { $_.Kind -eq 'SHADOW_DEPENDENCY' -and $_.Data.originalIssue -and -not $_.Data.shadowIssue } | ForEach-Object { $_.Data })
$providers = @($resolved.dependency | Sort-Object -Unique)
if ($resolved.Count -ne 2 -or $providers.Count -ne 1) { throw 'This combined trial expects exactly two resolved requirements for one provider.' }
$provider = @($events | Where-Object { $_.Kind -eq 'SHADOW_CANDIDATE' -and $_.Data.guid -ceq $providers[0] -and $_.Data.state -eq 'Eligible' })
if ($provider.Count -ne 1) { throw 'Ambiguous eligible provider.' }
$version = [version]$provider[0].Data.version
if ($version.Major -eq [int]::MaxValue -or $version -le [version]'0.0.0.0') { throw 'Cannot construct strict control bounds.' }
$missing = 'DependencyBootstrapProbe_IntentionallyMissing'
if (@($events | Where-Object { $_.Kind -in @('SHADOW_CANDIDATE','SHADOW_INDEX') -and $_.Data.guid -ieq $missing }).Count) { throw 'Missing fixture GUID is actually present.' }
$guid = 'DependencyBootstrapProbeControl_Serp'
if (@($events | Where-Object { $_.Kind -in @('SHADOW_CANDIDATE','SHADOW_INDEX') -and $_.Data.guid -ieq $guid }).Count) { throw 'Control fixture already present in baseline.' }
$manifest = [ordered]@{
    GUID=$guid; Name='TEST ONLY - Dependency correction controls (3 expected errors)'; Author='Serpens66'; Version='0.1.0'; Manifest=0; NetworkMode=0
    Description='Temporary empty asset test: missing provider and two deliberately impossible version bounds.'
    Dependencies=@(
        [ordered]@{GUID=$missing},
        [ordered]@{GUID=$providers[0]; MinimumVersion=([version]::new($version.Major+1,0,0,0).ToString())},
        [ordered]@{GUID=$providers[0]; MaximumVersion='0.0.0.0'}
    )
}
$directory=Join-Path $PSScriptRoot 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp'
[IO.Directory]::CreateDirectory($directory)|Out-Null
$json=($manifest|ConvertTo-Json -Depth 6)
$json=[regex]::Replace($json,'\r?\n',"`r`n")+"`r`n"
[IO.File]::WriteAllText((Join-Path $directory 'info.json'),$json,([Text.UTF8Encoding]::new($false)))
$trial=Join-Path $PSScriptRoot 'trial'
[IO.Directory]::CreateDirectory($trial)|Out-Null
$sha=[Security.Cryptography.SHA256]::Create()
try { $hash=[BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes((Join-Path $directory 'info.json')))).Replace('-','') } finally { $sha.Dispose() }
$expectations=[ordered]@{ManifestSHA256=$hash; ControlGuid=$guid; MissingGuid=$missing; Provider=$providers[0]; ProviderVersion=$version.ToString(); ResolvedPairs=@($resolved|ForEach-Object{[ordered]@{consumer=$_.consumer;dependency=$_.dependency}}); Baseline=[IO.Path]::GetFullPath($AcceptedShadowRoot)}
[IO.File]::WriteAllText((Join-Path $trial 'expectations.json'),([regex]::Replace(($expectations|ConvertTo-Json -Depth 6),'\r?\n',"`r`n")+"`r`n"))
Write-Host "Prepared controls only in workspace: missing=$missing; provider=$($providers[0]) $version; min=$($manifest.Dependencies[1].MinimumVersion); max=$($manifest.Dependencies[2].MaximumVersion)"
