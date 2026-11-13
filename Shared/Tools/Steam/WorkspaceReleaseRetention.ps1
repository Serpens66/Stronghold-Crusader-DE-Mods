# Generated release payloads are temporary. Current prepared-package identities,
# publisher deliveries and compact upload/recovery history are the durable state.
function Get-WorkspaceReleaseProcesses { @(Get-CimInstance Win32_Process -ErrorAction Stop) }
function Clear-WorkspaceReleaseOutputs {
    param([Parameter(Mandatory)][string]$Workspace,[IO.FileStream]$WorkflowLease)
    $workspace=Assert-WorkshopPublisherScopePath $Workspace
    $output=Join-Path $workspace '.release-output'
    $state=Join-Path $output 'SteamWorkshop'
    $result=[ordered]@{ObservedUtc=[DateTime]::UtcNow.ToString('o');Removed=@();Retained=@();RemovedBytes=[long]0;Skipped=''}
    if(-not [IO.Directory]::Exists($output)){return [pscustomobject]$result}
    $leases=[Collections.Generic.List[IDisposable]]::new()
    try{
        $null=Assert-WorkshopPublisherScopePath $output
        foreach($lockPath in @((Join-Path $state 'workflow.lock'),(Join-Path $state 'test/workflow.lock'))){
            $null=Assert-WorkshopPublisherScopePath $lockPath
            if($WorkflowLease -and $WorkflowLease.Name -ieq $lockPath){continue}
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($lockPath))
            $leases.Add([IO.File]::Open($lockPath,'OpenOrCreate','ReadWrite','None'))
        }
        $processes=@(Get-WorkspaceReleaseProcesses | Where-Object {$_.ProcessId -ne $PID})
        # Direct historical release entrypoints do not acquire workflow.lock.
        if(@($processes|Where-Object {$_.CommandLine -and $_.CommandLine -match '(?i)(Release-Mod|Create-SteamModPack)\.ps1'}).Count){throw 'A release producer is still active.'}
        $protected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        function Add-ReleaseReferences($Value){
            if($null -eq $Value){return}
            if($Value -is [string]){
                if([IO.Path]::IsPathRooted($Value)){
                    $full=[IO.Path]::GetFullPath($Value).TrimEnd('\','/')
                    if($full.StartsWith($output+'\',[StringComparison]::OrdinalIgnoreCase)){[void]$protected.Add($full)}
                }
            }elseif($Value -is [Collections.IEnumerable] -and $Value -isnot [pscustomobject]){foreach($item in $Value){Add-ReleaseReferences $item}}
            else{foreach($property in $Value.PSObject.Properties){Add-ReleaseReferences $property.Value}}
        }
        foreach($mode in @('','test/')){
            $prepared=Join-Path $state ($mode+'prepared')
            foreach($proofPath in @(Get-ChildItem -LiteralPath $prepared -File -Filter '*.json' -ErrorAction SilentlyContinue)){
                $null=Assert-WorkshopPublisherScopePath $proofPath.FullName
                $proof=[IO.File]::ReadAllText($proofPath.FullName)|ConvertFrom-Json
                Add-ReleaseReferences $proof
                if($proof.PSObject.Properties['PublishedRelease']){
                    [void]$protected.Add((Join-Path $state ('releases/v'+$proof.Version)))
                }
            }
        }
        $candidates=[Collections.Generic.List[string]]::new()
        foreach($project in @(Get-ChildItem -LiteralPath $output -Directory)){
            if($project.Name -in @('SteamWorkshop','checkpoints','local')){continue}
            foreach($version in @(Get-ChildItem -LiteralPath $project.FullName -Directory)){
                if($version.Name -cmatch '^v\d+\.\d+\.\d+$' -or $version.Name -ceq 'cache'){$candidates.Add($version.FullName)}
            }
        }
        $apiLegacy=Join-Path $output 'APIShared/main'
        if([IO.Directory]::Exists($apiLegacy)){$candidates.Add($apiLegacy)}
        foreach($mode in @('','test/')){
            foreach($relative in @('validation','preparation','staging','releases','publisher/Uploads')){
                $path=Join-Path $state ($mode+$relative)
                if([IO.Directory]::Exists($path)){$candidates.Add($path)}
            }
        }
        function Remove-GeneratedReleaseTree([IO.DirectoryInfo]$Directory){
            foreach($entry in $Directory.GetFileSystemInfos()){
                if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint -or $entry.Name -ceq '.git'){throw 'Linked or repository contents appeared during release cleanup.'}
                if($entry -is [IO.DirectoryInfo]){Remove-GeneratedReleaseTree $entry}else{$entry.IsReadOnly=$false;$entry.Delete()}
            }
            $Directory.Delete()
        }
        function Remove-ReleaseCandidate([string]$Path){
            $path=Assert-WorkshopPublisherScopePath $Path
            if(-not $path.StartsWith($output+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Release cleanup escapes its workspace output.'}
            if(-not (Test-Path -LiteralPath $path)){return}
            $item=Get-Item -LiteralPath $path -Force
            # Refuse repositories before descending around referenced assets. A
            # worktree's .git can be a file and must never itself be a candidate.
            if($item.Name -ieq '.git' -or ($item -is [IO.DirectoryInfo] -and (Test-Path -LiteralPath (Join-Path $path '.git')))){
                throw 'Release output contains a Git repository; retained.'
            }
            foreach($pin in $protected){
                if($path -ieq $pin -or $path.StartsWith($pin+'\',[StringComparison]::OrdinalIgnoreCase)){
                    $result.Retained+=@{Path=$path;Reason='Referenced by a current preparation'};return
                }
            }
            $hasReferences=@($protected|Where-Object {$_.StartsWith($path+'\',[StringComparison]::OrdinalIgnoreCase)}).Count -gt 0
            if($hasReferences){
                foreach($entry in ([IO.DirectoryInfo]::new($path)).GetFileSystemInfos()){Remove-ReleaseCandidate $entry.FullName}
                return
            }
            if(@($processes|Where-Object {$_.CommandLine -and ($_.CommandLine.IndexOf($path,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                ($path -match '[\\/]publisher[\\/]Uploads$' -and $_.Name -ieq 'WorkshopUploader.exe'))}).Count){
                $result.Retained+=@{Path=$path;Reason='Active process'};return
            }
            $bytes=[long]0
            if($item -is [IO.DirectoryInfo]){
                $stack=[Collections.Generic.Stack[IO.DirectoryInfo]]::new();$stack.Push($item)
                while($stack.Count){
                    foreach($entry in $stack.Pop().GetFileSystemInfos()){
                        if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint -or $entry.Name -ceq '.git'){throw 'Release output contains links or a Git repository; retained.'}
                        if($entry -is [IO.DirectoryInfo]){$stack.Push($entry)}else{$bytes+=$entry.Length}
                    }
                }
                Remove-GeneratedReleaseTree $item
            }else{$bytes=$item.Length;$item.IsReadOnly=$false;$item.Delete()}
            $result.Removed+=@{Path=$path;Bytes=$bytes};$result.RemovedBytes+=$bytes
        }
        foreach($path in $candidates){
            try{Remove-ReleaseCandidate $path}
            catch{$result.Retained+=@{Path=$path;Reason=$_.Exception.Message};Write-Warning ('Generated release output retained: '+$path+': '+$_.Exception.Message)}
        }
    }catch{$result.Skipped=$_.Exception.Message;Write-Warning ('Generated release cleanup deferred: '+$_.Exception.Message)}
    finally{foreach($lease in $leases){$lease.Dispose()}}
    $report=Join-Path $state 'retention/artifact-cleanup-latest.json'
    $null=Assert-WorkshopPublisherScopePath $report
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($report))
    [IO.File]::WriteAllText($report,(($result|ConvertTo-Json -Depth 8)+"`r`n"),[Text.UTF8Encoding]::new($false))
    return [pscustomobject]$result
}
