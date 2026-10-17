param([string]$ArtifactRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
$receipt=Get-Content -Raw -LiteralPath (Join-Path $ArtifactRoot 'trial\expectations.json')|ConvertFrom-Json
$path=Join-Path $ArtifactRoot 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp\info.json'
$stream=[IO.File]::OpenRead($path);$sha=[Security.Cryptography.SHA256]::Create()
try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
if($hash -cne $receipt.ManifestSHA256){throw 'Control manifest hash mismatch.'}
$manifest=Get-Content -Raw -LiteralPath $path|ConvertFrom-Json
if($manifest.GUID -cne 'DependencyBootstrapProbeControl_Serp' -or $receipt.ControlGuid -cne $manifest.GUID -or $manifest.Dependencies.Count -ne 3 -or $manifest.Manifest -ne 0 -or $manifest.NetworkMode -ne 0){throw 'Invalid control manifest identity/shape.'}
$deps=@($manifest.Dependencies)
if($deps[0].GUID -cne $receipt.MissingGuid -or $deps[1].GUID -cne $receipt.Provider -or $deps[2].GUID -cne $receipt.Provider){throw 'Control dependencies do not match receipt.'}
if([version]$deps[1].MinimumVersion -le [version]$receipt.ProviderVersion -or [version]$deps[2].MaximumVersion -ge [version]$receipt.ProviderVersion){throw 'Control bounds are not strictly impossible.'}
if(@($receipt.ResolvedPairs).Count -ne 2){throw 'Missing positive control requirements.'}
Write-Host 'PASS: control manifest identity and all three negative requirements.'
