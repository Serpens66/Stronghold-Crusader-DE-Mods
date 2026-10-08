[CmdletBinding()]
param([Parameter(Mandatory)][string]$Branch)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
if (-not $Branch.StartsWith('codex/', [StringComparison]::Ordinal)) { throw 'Use a codex/ contribution branch.' }
& git check-ref-format --branch $Branch
if ($LASTEXITCODE -ne 0) { throw 'Invalid contribution branch.' }
$status = @(& git -C $workspace status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the workspace Git state.' }
if ($status.Count -ne 0) { throw 'Commit or otherwise preserve working changes before exporting. This tool never stashes or resets them.' }
$split = (& git -C $workspace subtree split --prefix=APIShared --rejoin).Trim()
if ($LASTEXITCODE -ne 0 -or $split -notmatch '^[0-9a-f]{40}$') { throw 'APIShared-only subtree export failed.' }
$unexpected = @(& git -C $workspace ls-tree -r --name-only $split | Where-Object { $_ -match '(^|/)(Shared|BugfixesAndQoL|Testmods|BepInEx|bin|obj)/|\.(dll|exe|pdb|log)$' })
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the exported source tree.' }
if ($unexpected.Count -ne 0) { throw 'Export includes workspace or generated files; do not push it.' }
& git -C $workspace push apishared "${split}:refs/heads/$Branch"
if ($LASTEXITCODE -ne 0) { throw 'Contribution push failed; the split commit remains available locally.' }
Write-Host "Pushed only APIShared to $Branch. Open a pull request in SHCDE-APIShared/APIShared for review."
