[CmdletBinding()]
param([Parameter(Mandatory)][string]$Revision)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$status = @(& git -C $workspace status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the workspace Git state.' }
if ($status.Count -ne 0) { throw 'Commit or otherwise preserve working changes before importing. This tool never stashes or resets them.' }
$remote = 'apishared'
& git -C $workspace fetch $remote $Revision
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch the explicitly selected APIShared revision.' }
$commit = (& git -C $workspace rev-parse 'FETCH_HEAD^{commit}').Trim()
if ($LASTEXITCODE -ne 0) { throw 'The selected revision is not a commit.' }
& git -C $workspace subtree merge --prefix=APIShared --squash $commit --message "Import reviewed APIShared $commit"
if ($LASTEXITCODE -ne 0) { throw 'Subtree import needs attention. Resolve the reported state; no automatic reset or fallback is performed.' }
& (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace
Write-Host "Imported explicit APIShared revision $commit. Review the diff and run the affected build.bat drivers before installing or publishing."
