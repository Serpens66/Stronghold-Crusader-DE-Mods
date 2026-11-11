[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Driver,
    [switch]$Api,
    [Parameter(ValueFromRemainingArguments)][string[]]$DriverArguments
)
. (Join-Path $PSScriptRoot 'BranchBuild.Common.ps1')
$source = $null
$run = $null
$exitCode = 0
try {
    $Repository = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $null = Get-BranchTestSelection $Driver $DriverArguments
    $branch = Get-BranchChoice $DriverArguments
    # The workspace package folders may only receive the build of the workspace's own sources.
    $syncPackage = $DriverArguments -icontains '/syncpackage'
    if ($syncPackage -and ($branch -ne 'working' -or $Api)) { throw '/syncpackage requires a mod build of the current working tree (/source:working or the automated default).' }
    Repair-AbandonedBranchRuns $Repository $branch
    $run = Start-BranchRun $Repository $branch $Driver ($DriverArguments -icontains '/keepsource') ($DriverArguments -icontains '/noinstall')
    $source = if ($branch -eq 'working') { New-WorkingSource $Repository $run.Root } else { New-BranchSource $Repository $branch $run.Root }
    $run.State.Commit = $source.Commit
    Set-BranchRunPhase $run 'building'
    $null = Invoke-SelectedBranchBuild -Repository $Repository -Driver $Driver -Branch $branch -DriverArguments $DriverArguments -ControllerRoot $Repository -Api:$Api -SourceSelection $source
    # Reached only after a successful build; a failure throws before and mirrors nothing.
    if ($syncPackage) { $null = Sync-BranchPackages $Repository $source.Root $Driver }
} catch {
    Write-BranchMessage ('ERROR: ' + $_.Exception.Message)
    $exitCode = if ($_.Exception.Data.Contains('BuildExitCode')) { [int]$_.Exception.Data['BuildExitCode'] } else { 1 }
} finally {
    if ($run) {
        try { Complete-BranchRun $Repository $run }
        catch { Write-BranchMessage ("WARNING: Could not finish owned-run cleanup: $($run.Root): " + $_.Exception.Message) }
        finally { $run.Lease.Dispose() }
    }
}
exit $exitCode
