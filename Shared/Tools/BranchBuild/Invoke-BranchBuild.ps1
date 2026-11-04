[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Driver,
    [switch]$Api,
    [Parameter(ValueFromRemainingArguments)][string[]]$DriverArguments
)
. (Join-Path $PSScriptRoot 'BranchBuild.Common.ps1')
$source = $null
$exitCode = 0
try {
    $Repository = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $null = Get-BranchTestSelection $Driver $DriverArguments
    $branch = Get-BranchChoice $DriverArguments
    # The workspace package folders may only receive the build of the workspace's own sources.
    $syncPackage = $DriverArguments -icontains '/syncpackage'
    if ($syncPackage -and ($branch -ne 'working' -or $Api)) { throw '/syncpackage requires a mod build of the current working tree (/source:working or the automated default).' }
    $source = if ($branch -eq 'working') { New-WorkingSource $Repository } else { New-BranchSource $Repository $branch }
    $null = Invoke-SelectedBranchBuild -Repository $Repository -Driver $Driver -Branch $branch -DriverArguments $DriverArguments -ControllerRoot $Repository -Api:$Api -SourceSelection $source
    # Reached only after a successful build; a failure throws before and mirrors nothing.
    if ($syncPackage) { $null = Sync-BranchPackages $Repository $source.Root $Driver }
} catch {
    Write-BranchMessage ('ERROR: ' + $_.Exception.Message)
    $exitCode = if ($_.Exception.Data.Contains('BuildExitCode')) { [int]$_.Exception.Data['BuildExitCode'] } else { 1 }
} finally {
    # Build copies are ~5 GB each; keep one only on explicit request (/keepsource).
    if ($source -and (Test-Path -LiteralPath $source.Root)) {
        if ($DriverArguments -icontains '/keepsource') {
            Write-BranchMessage "Build copy kept on request: $($source.Root)"
        } else {
            try {
                # Local build provenance is written inside the copy; keep it beside the build record.
                foreach ($provenance in @(Get-ChildItem -LiteralPath (Join-Path $source.Root '.release-output\local') -Filter 'latest.provenance.json' -File -Recurse -ErrorAction SilentlyContinue)) {
                    Copy-Item -LiteralPath $provenance.FullName -Destination ($source.Root + '.' + $provenance.Directory.Name + '.provenance.json') -Force
                }
                Remove-BranchSource $Repository $source.Root
                Write-BranchMessage "Build copy removed: $($source.Root) (use /keepsource to keep it)."
            } catch {
                # Cleanup never changes the build result; report what is left behind.
                Write-BranchMessage ("WARNING: Build copy could not be removed: $($source.Root): " + $_.Exception.Message)
            }
        }
    }
}
exit $exitCode
