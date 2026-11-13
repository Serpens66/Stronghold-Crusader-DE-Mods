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
$workingLock = $null
try {
    $Repository = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $null = Get-BranchTestSelection $Driver $DriverArguments
    $branch = Get-BranchChoice $DriverArguments
    # The workspace package folders may only receive the build of the workspace's own sources.
    $syncPackage = $DriverArguments -icontains '/syncpackage'
    if ($syncPackage -and ($branch -ne 'working' -or $Api)) { throw '/syncpackage requires a mod build of the current working tree (/source:working or the automated default).' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $key = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Repository.ToLowerInvariant()))).Replace('-','') }
    finally { $sha.Dispose() }
    # Builds share current package folders, project references and latest-output
    # records. Hold the lease through source cleanup and export verification.
    $workingLock = [Threading.Mutex]::new($false, 'Local\SHCDE-WorkspaceBuild-' + $key)
    try { if (-not $workingLock.WaitOne(0)) { Write-BranchMessage 'Waiting for another build in this workspace...'; [void]$workingLock.WaitOne() } }
    catch [Threading.AbandonedMutexException] { }
    if ($branch -eq 'working') {
        $latestRecord = Get-WorkingBuildRecordPath $Repository $Driver
        if ([IO.File]::Exists($latestRecord)) { [IO.File]::Delete($latestRecord) }
        # The working tree is already the requested source. Do not clone it, overlay
        # its build scripts or create nested SDK inspection repositories.
        $source = [pscustomobject]@{ Root=$Repository; Branch='working'; Commit=((Invoke-BranchGit $Repository @('rev-parse','HEAD')) -join ''); Direct=$true }
    } else {
        Repair-AbandonedBranchRuns $Repository $branch
        $run = Start-BranchRun $Repository $branch $Driver ($DriverArguments -icontains '/keepsource') ($DriverArguments -icontains '/noinstall')
        $source = New-BranchSource $Repository $branch $run.Root
        $run.State.Commit = $source.Commit
        Set-BranchRunPhase $run 'building'
    }
    $null = Invoke-SelectedBranchBuild -Repository $Repository -Driver $Driver -Branch $branch -DriverArguments $DriverArguments -ControllerRoot $Repository -Api:$Api -SourceSelection $source
    # /syncpackage remains accepted for existing callers: working builds already
    # produce the current workspace package, so no separate mirror is needed.
} catch {
    Write-BranchMessage ('ERROR: ' + $_.Exception.Message)
    $exitCode = if ($_.Exception.Data.Contains('BuildExitCode')) { [int]$_.Exception.Data['BuildExitCode'] } else { 1 }
} finally {
    if ($run) {
        try { Complete-BranchRun $Repository $run }
        catch { Write-BranchMessage ("WARNING: Could not finish owned-run cleanup: $($run.Root): " + $_.Exception.Message) }
        finally { $run.Lease.Dispose() }
    }
    if ($workingLock) { $workingLock.ReleaseMutex(); $workingLock.Dispose() }
}
exit $exitCode
