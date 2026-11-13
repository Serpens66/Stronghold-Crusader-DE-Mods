# Workspace-only publisher lifetime. The frozen CLI bytes remain immutable.
function Assert-WorkshopPublisherScopePath([string]$Path) {
    $full=[IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    for($part=$full;$part;$part=[IO.Path]::GetDirectoryName($part)){
        if((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked publisher invocation path.'}
    }
    return $full
}
function Get-WorkshopPublisherScopeHash([string]$Path) {
    if(-not [IO.File]::Exists($Path)){return 'missing'}
    $stream=[IO.File]::OpenRead($Path);$sha=[Security.Cryptography.SHA256]::Create()
    try{return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}
    finally{$sha.Dispose();$stream.Dispose()}
}
function Assert-WorkshopPublisherState([string]$Path) {
    $null=Assert-WorkshopPublisherScopePath $Path
    $state=[IO.File]::ReadAllText($Path)|ConvertFrom-Json
    if([string]$state.WorkshopId -cnotmatch '^[1-9][0-9]*$'){throw 'Invalid publisher recovery item ID.'}
    if($state.PSObject.Properties['Version'] -and [string]$state.Version -and [string]$state.Version -cnotmatch '^\d+\.\d+\.\d+$'){throw 'Invalid publisher recovery version.'}
    if($state.PSObject.Properties['Fingerprint'] -and [string]$state.Fingerprint -and [string]$state.Fingerprint -cnotmatch '^[0-9a-f]{64}$'){throw 'Invalid publisher recovery fingerprint.'}
}
function Complete-WorkshopPublisherScope([string]$StateRoot,[string]$CallRoot) {
    $state=Assert-WorkshopPublisherScopePath $StateRoot
    $call=Assert-WorkshopPublisherScopePath $CallRoot
    if($call -notmatch ('^'+[regex]::Escape($state)+'\\calls\\[0-9a-f]{32}$')){throw 'Publisher invocation escapes its owned state directory.'}
    $ownerPath=Join-Path $call 'owner.json'
    $null=Assert-WorkshopPublisherScopePath $ownerPath
    $owner=[IO.File]::ReadAllText($ownerPath)|ConvertFrom-Json
    if($owner.SchemaVersion -ne 1 -or $owner.Protocol -cne 'WorkspacePublisherCall' -or $owner.Root -ine $call -or
        $owner.StateRoot -ine $state -or $owner.PackageId -cnotmatch '^[A-Za-z0-9_.-]+$' -or
        $owner.InitialStateHash -cnotmatch '^(missing|[0-9a-f]{64})$'){throw 'Unrecognized publisher invocation ownership.'}
    if($owner.ChildPid -gt 0){
        $child=Get-Process -Id $owner.ChildPid -ErrorAction SilentlyContinue
        if($child){
            try{if($child.StartTime.ToUniversalTime().ToString('o') -ceq $owner.ChildStartedUtc){throw 'Publisher child is still using its snapshot.'}}
            finally{$child.Dispose()}
        }
    }elseif($owner.Phase -ceq 'Starting'){throw 'Publisher start was interrupted before its child identity was recorded; retained.'}
    if($owner.Phase -ceq 'OutputDescendant'){throw 'Publisher descendant ownership is unresolved; invocation retained.'}
    # Verify every descendant before recursion. Unknown roots, Git repositories and
    # links are retained, not followed or treated as disposable package data.
    $unknownData=$false
    $stack=[Collections.Generic.Stack[IO.DirectoryInfo]]::new();$stack.Push([IO.DirectoryInfo]::new($call))
    while($stack.Count){
        $directory=$stack.Pop()
        foreach($entry in $directory.GetFileSystemInfos()){
            if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint -or $entry.Name -ceq '.git'){throw 'Linked or repository data in publisher invocation; retained.'}
            if($directory.FullName -ieq $call){
                if($entry -is [IO.DirectoryInfo]){if($entry.Name -cnotin @('Uploads','Publishing')){$unknownData=$true}}
                elseif($entry.Name -cnotin @('owner.json','lease','request.json')){$unknownData=$true}
            }
            if($directory.FullName -ieq (Join-Path $call 'Publishing') -and
                ($entry -is [IO.DirectoryInfo] -or $entry.Name -cnotin @(($owner.PackageId+'.json'),($owner.PackageId+'.json.tmp')))){$unknownData=$true}
            if($directory.FullName -ieq (Join-Path $call 'Uploads') -and
                ($entry -isnot [IO.DirectoryInfo] -or $entry.Name -cnotmatch '^[0-9a-f]{32}$')){throw 'Unknown publisher snapshot data; retained.'}
            if($entry -is [IO.DirectoryInfo]){$stack.Push($entry)}
        }
    }
    $source=Join-Path $call ('Publishing/'+$owner.PackageId+'.json')
    $pending=$source+'.tmp'
    $destination=Join-Path $state ('Publishing/'+$owner.PackageId+'.json')
    $null=Assert-WorkshopPublisherScopePath $source;$null=Assert-WorkshopPublisherScopePath $destination
    if([IO.File]::Exists($pending)){
        # The real publisher writes this file after CreateItem, before atomic
        # replacement. Its item ID must survive interruption at that exact point.
        try{Assert-WorkshopPublisherState $pending;$source=$pending}
        catch{$unknownData=$true}
    }
    if([IO.File]::Exists($source)){
        Assert-WorkshopPublisherState $source
        $sourceHash=Get-WorkshopPublisherScopeHash $source
        $current=Get-WorkshopPublisherScopeHash $destination
        if($sourceHash -cne $owner.InitialStateHash -and $sourceHash -cne $current){
            if($current -cne $owner.InitialStateHash){$unknownData=$true}
            else{
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
            $temporary=$destination+'.scope-'+[guid]::NewGuid().ToString('N')
            $backup=$temporary+'.previous'
            try{
                [IO.File]::Copy($source,$temporary)
                if([IO.File]::Exists($destination)){[IO.File]::Replace($temporary,$destination,$backup)}else{[IO.File]::Move($temporary,$destination)}
                if((Get-WorkshopPublisherScopeHash $destination) -cne $sourceHash){throw 'Publisher recovery state write verification failed.'}
                if([IO.File]::Exists($backup)){[IO.File]::Delete($backup)}
            }finally{if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)}}
            }
        }
    }
    function Remove-PublisherCallTree([IO.DirectoryInfo]$Directory){
        foreach($entry in $Directory.GetFileSystemInfos()){
            if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Publisher invocation changed to a link during cleanup.'}
            if($entry -is [IO.DirectoryInfo]){Remove-PublisherCallTree $entry}else{$entry.IsReadOnly=$false;$entry.Delete()}
        }
        $Directory.Delete()
    }
    $uploads=Join-Path $call 'Uploads'
    if([IO.Directory]::Exists($uploads)){Remove-PublisherCallTree ([IO.DirectoryInfo]::new($uploads))}
    if($unknownData){throw 'Unrecognized compact recovery data retained; reproducible publisher payload removed.'}
    Remove-PublisherCallTree ([IO.DirectoryInfo]::new($call))
}
function Invoke-ScopedWorkshopPublisher {
    param([Parameter(Mandatory)][string]$Publisher,[Parameter(Mandatory)][ValidateSet('validate','inspect','publish')][string]$Action,
        [Parameter(Mandatory)][string]$RequestPath,[Parameter(Mandatory)][string]$ResultPath)
    $request=[IO.File]::ReadAllText($RequestPath)|ConvertFrom-Json
    if([string]$request.PackageId -cnotmatch '^[A-Za-z0-9_.-]+$'){throw 'Invalid workspace publisher package identity.'}
    $state=Assert-WorkshopPublisherScopePath ([string]$request.StateRoot)
    [void][IO.Directory]::CreateDirectory($state)
    $lock=$null;$lease=$null;$process=$null;$call=$null;$owner=$null;$code=1
    try{
        # Also protects direct workspace script calls; the normal workflow holds its
        # broader workflow lock. Recovery state can never be overwritten by a peer.
        $lock=[IO.File]::Open((Join-Path $state 'invocations.lock'),'OpenOrCreate','ReadWrite','None')
        $calls=Join-Path $state 'calls';$null=Assert-WorkshopPublisherScopePath $calls
        [void][IO.Directory]::CreateDirectory($calls)
        foreach($old in @(Get-ChildItem -LiteralPath $calls -Directory)){
            $oldLease=$null
            try{
                if($old.Name -cnotmatch '^[0-9a-f]{32}$' -or -not [IO.File]::Exists((Join-Path $old.FullName 'lease'))){continue}
                $oldLease=[IO.File]::Open((Join-Path $old.FullName 'lease'),'Open','ReadWrite','None');$oldLease.Dispose();$oldLease=$null
                Complete-WorkshopPublisherScope $state $old.FullName
            }catch{
                Write-Warning ('Publisher invocation retained: '+$old.FullName+': '+$_.Exception.Message)
                if($Action -cne 'validate'){
                    $pendingOwner=$null
                    try{$pendingOwner=[IO.File]::ReadAllText((Join-Path $old.FullName 'owner.json'))|ConvertFrom-Json}catch{}
                    if(-not $pendingOwner -or $pendingOwner.PackageId -ceq $request.PackageId){throw 'Resolve the previous publisher invocation/recovery ID before another Steam operation.'}
                }
            }
            finally{if($oldLease){$oldLease.Dispose()}}
        }
        $call=Join-Path $calls ([guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($call)
        $lease=[IO.File]::Open((Join-Path $call 'lease'),'CreateNew','ReadWrite','None')
        $publishing=Join-Path $state ('Publishing/'+$request.PackageId+'.json');$null=Assert-WorkshopPublisherScopePath $publishing
        $owner=[ordered]@{SchemaVersion=1;Protocol='WorkspacePublisherCall';Root=$call;StateRoot=$state;PackageId=[string]$request.PackageId;
            InitialStateHash=(Get-WorkshopPublisherScopeHash $publishing);Phase='Allocated';ChildPid=0;ChildStartedUtc=''}
        if([IO.File]::Exists($publishing)){
            Assert-WorkshopPublisherState $publishing
            $copy=Join-Path $call ('Publishing/'+$request.PackageId+'.json');[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($copy));[IO.File]::Copy($publishing,$copy)
        }
        $ownerPath=Join-Path $call 'owner.json'
        [IO.File]::WriteAllText($ownerPath,($owner|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
        $request.StateRoot=$call
        $privateRequest=Join-Path $call 'request.json'
        [IO.File]::WriteAllText($privateRequest,($request|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
        $publisherPath=[IO.Path]::GetFullPath($Publisher);$resultFile=[IO.Path]::GetFullPath($ResultPath)
        foreach($argument in @($privateRequest,$resultFile)){if($argument.Contains('"')){throw 'Invalid publisher argument path.'}}
        $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$publisherPath
        $start.Arguments='--'+$Action+' "'+$privateRequest+'" "'+$resultFile+'"'
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $owner.Phase='Starting';[IO.File]::WriteAllText($ownerPath,($owner|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
        $process=[Diagnostics.Process]::Start($start)
        $owner.ChildPid=$process.Id;$owner.ChildStartedUtc=$process.StartTime.ToUniversalTime().ToString('o');$owner.Phase='Running'
        [IO.File]::WriteAllText($ownerPath,($owner|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
        $stdout=$process.StandardOutput.ReadLineAsync();$stderr=$process.StandardError.ReadLineAsync();$outDone=$false;$errorDone=$false
        $exitDrain=[Diagnostics.Stopwatch]::new()
        while(-not $outDone -or -not $errorDone){
            if(-not $outDone -and $stdout.IsCompleted){$line=$stdout.GetAwaiter().GetResult();if($null -eq $line){$outDone=$true}else{Write-Host $line;$stdout=$process.StandardOutput.ReadLineAsync()}}
            if(-not $errorDone -and $stderr.IsCompleted){$line=$stderr.GetAwaiter().GetResult();if($null -eq $line){$errorDone=$true}else{Write-Host $line;$stderr=$process.StandardError.ReadLineAsync()}}
            if($process.HasExited -and (-not $outDone -or -not $errorDone)){
                if(-not $exitDrain.IsRunning){$exitDrain.Start()}
                if($exitDrain.Elapsed.TotalSeconds -ge 2){
                    # A descendant may have inherited a pipe. Never wait forever
                    # or delete files it could still read; retain this rare scope.
                    $owner.Phase='OutputDescendant'
                    [IO.File]::WriteAllText($ownerPath,($owner|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
                    throw 'Publisher exited with inherited output handles still open; invocation retained for review.'
                }
            }
            if(-not $outDone -or -not $errorDone){[Threading.Thread]::Sleep(20)}
        }
        $process.WaitForExit();$code=$process.ExitCode
        $owner.Phase='Complete';[IO.File]::WriteAllText($ownerPath,($owner|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    }finally{
        if($lease){$lease.Dispose()}
        if($call -and $owner){
            try{Complete-WorkshopPublisherScope $state $call}
            catch{Write-Warning ('Publisher invocation retained: '+$call+': '+$_.Exception.Message);$code=1}
        }
        if($process){$process.Dispose()}
        if($lock){$lock.Dispose()}
    }
    return $code
}
