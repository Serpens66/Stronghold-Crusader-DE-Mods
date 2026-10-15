# Local output retention only. Call after successful preparation while workflow.lock is held.
function Get-WorkshopOutputBackups($Config,$Target,[bool]$Test) {
    $folder=[IO.Path]::GetFullPath((Get-WorkshopFolder $Config $Target $Test))
    $parent=[IO.Path]::GetDirectoryName($folder)
    Assert-ApiWorkshopUnlinkedPath $parent
    if(-not [IO.Directory]::Exists($parent)){return}
    $pattern='^'+[regex]::Escape([IO.Path]::GetFileName($folder))+'\.previous-[a-f0-9]{32}$'
    Get-ChildItem -LiteralPath $parent -Force -Directory | Where-Object {$_.Name -cmatch $pattern}
}
function Assert-WorkshopRetentionRelative([string]$Path) {
    if(-not $Path -or $Path -match '[\\:<>"|?*\x00-\x1f\x7f]' -or $Path -match '(^|/)(\.|\.\.|)(/|$)' -or $Path -match '[. ](/|$)' -or @($Path.Split('/')|Where-Object {$_ -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)'}).Count){throw 'Unsafe retention relative path.'}
}
function Get-WorkshopRetentionTree([string]$Root) {
    Assert-ApiWorkshopUnlinkedTree $Root
    $files=@(Get-ChildItem -LiteralPath $Root -Recurse -Force -File|ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($Root.Length+1).Replace('\','/');Size=$_.Length;Sha256=(Get-WorkshopFileHash $_.FullName)}
    })
    $directories=@(Get-ChildItem -LiteralPath $Root -Recurse -Force -Directory|ForEach-Object {$_.FullName.Substring($Root.Length+1).Replace('\','/')})
    [pscustomobject]@{Files=$files;Directories=$directories}
}
function Get-WorkshopRetentionMapInfo([string]$Path) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $bytes=[IO.File]::ReadAllBytes($Path);$offset=-1
    for($i=0;$i -le $bytes.Length-4;$i++){if($bytes[$i] -eq 80 -and $bytes[$i+1] -eq 75 -and $bytes[$i+2] -eq 3 -and $bytes[$i+3] -eq 4){$offset=$i;break}}
    if($offset -lt 0){throw 'Backup map has no ZIP payload.'}
    $stream=[IO.MemoryStream]::new($bytes,$offset,$bytes.Length-$offset,$false)
    $archive=$null
    try{
        $archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Read,$true)
        $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach($entry in $archive.Entries){
            $name=$entry.FullName.TrimEnd('/');Assert-WorkshopRetentionRelative $name
            if(-not $names.Add($name) -or (($entry.ExternalAttributes -shr 16)-band 0xF000) -eq 0xA000){throw 'Duplicate or linked backup map entry.'}
        }
        $entry=$archive.GetEntry('info.json');if(-not $entry -or $entry.Length -gt 1MB){throw 'Missing or oversized map identity.'}
        $reader=[IO.StreamReader]::new($entry.Open())
        try{$info=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
        if([string]$info.Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$' -or [int]$info.Manifest -ne 1){throw 'Invalid backup map version/manifest.'}
        return $info
    }finally{if($archive){$archive.Dispose()};$stream.Dispose()}
}
function Test-WorkshopRetentionFile([string]$Relative,[string]$Key,[string]$Layout) {
    Assert-WorkshopRetentionRelative $Relative
    $map=if($Key -ceq 'APIShared'){'APIShared.map'}else{'SerpsMods.map'}
    if($Relative -cin @($map,'preview.png',($map+'.sha256'))){return $true}
    return $Key -ceq 'SerpsMods' -and $Layout -ceq 'loose' -and
        ($Relative -ceq 'apishared-workshop.json' -or ($Relative.StartsWith('Payload/BepInEx/plugins/SerpsMods_Serp/',[StringComparison]::Ordinal) -and $Relative -notmatch '/(?i:config|LobbyModSettings)(/|$)'))
}
function Test-WorkshopRetentionDirectory([string]$Relative,[string]$Key,[string]$Layout) {
    Assert-WorkshopRetentionRelative $Relative
    if($Key -cne 'SerpsMods' -or $Layout -cne 'loose'){return $false}
    $root='Payload/BepInEx/plugins/SerpsMods_Serp'
    return $Relative -cin @('Payload','Payload/BepInEx','Payload/BepInEx/plugins',$root) -or
        ($Relative.StartsWith($root+'/',[StringComparison]::Ordinal) -and $Relative -notmatch '/(?i:config|LobbyModSettings)(/|$)')
}
function Get-WorkshopOutputBackupInventory([string]$Directory,$Target) {
    $tree=Get-WorkshopRetentionTree $Directory
    $map=if($Target.Key -ceq 'APIShared'){'APIShared.map'}else{'SerpsMods.map'}
    $info=Get-WorkshopRetentionMapInfo (Join-Path $Directory $map)
    $layout='map';$declarationPath=Join-Path $Directory 'apishared-workshop.json'
    $expected=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    [void]$expected.Add($map);[void]$expected.Add('preview.png')
    if($Target.Key -ceq 'SerpsMods' -and [IO.File]::Exists($declarationPath)){
        $layout='loose';$manifest=[IO.File]::ReadAllText($declarationPath)|ConvertFrom-Json
        if($manifest.SchemaVersion -ne 1 -or $manifest.PackageId -cne $Target.Guid -or $manifest.Version -cne $info.Version -or $manifest.Bootstrap.Guid -cne $info.GUID -or $manifest.Bootstrap.MapFile -cne $map -or $manifest.Bootstrap.Version -cne $manifest.Version -or @($manifest.Roots).Count -ne 1 -or $manifest.Roots[0] -cne 'BepInEx/plugins/SerpsMods_Serp'){throw 'Backup declaration/map identity mismatch.'}
        [void]$expected.Add('apishared-workshop.json')
        $actual=@{};foreach($file in $tree.Files){$actual[$file.Path]=$file}
        foreach($file in $manifest.Files){
            $relative='Payload/'+[string]$file.Path
            if(-not (Test-WorkshopRetentionFile $relative $Target.Key $layout) -or -not $expected.Add($relative) -or -not $actual.ContainsKey($relative) -or $actual[$relative].Sha256 -cne [string]$file.Sha256 -or $actual[$relative].Size -ne $file.Size){throw ('Backup payload inventory mismatch: '+$relative)}
        }
    }elseif($info.GUID -cne $Target.Guid){throw 'Backup map belongs to another package.'}
    $checksum=Join-Path $Directory ($map+'.sha256')
    if([IO.File]::Exists($checksum)){
        $text=[IO.File]::ReadAllText($checksum).Trim()
        if($text -cnotmatch ('^([a-f0-9]{64})\s+\*?'+[regex]::Escape($map)+'$') -or $Matches[1] -cne (Get-WorkshopFileHash (Join-Path $Directory $map))){throw 'Backup checksum mismatch.'}
        [void]$expected.Add($map+'.sha256')
    }
    if($tree.Files.Count -ne $expected.Count){throw 'Unknown or missing backup files.'}
    foreach($file in $tree.Files){if(-not $expected.Contains($file.Path)){throw ('Unknown backup file: '+$file.Path)}}
    foreach($directory in $tree.Directories){if(-not (Test-WorkshopRetentionDirectory $directory $Target.Key $layout) -and -not @($tree.Files|Where-Object {$_.Path.StartsWith($directory+'/',[StringComparison]::Ordinal)}).Count){throw ('Unknown empty backup directory: '+$directory)}}
    [pscustomobject]@{Layout=$layout;Files=@($tree.Files);Directories=@($tree.Directories);Version=[string]$info.Version}
}
function Get-WorkshopRetentionJobHash($Job) {
    $rows=@([string]$Job.SchemaVersion,[string]$Job.Target,[string]$Job.PackageId,[string]$Job.Mode,[string]$Job.Destination,[string]$Job.DirectoryName,[string]$Job.Layout)
    $rows+=@($Job.Files|Sort-Object Path|ForEach-Object {[string]$_.Path+"`t"+[string]$_.Size+"`t"+[string]$_.Sha256})
    $rows+=@($Job.Directories|Sort-Object|ForEach-Object {'DIR '+[string]$_})
    $sha=[Security.Cryptography.SHA256]::Create()
    try{return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($rows -join "`n"))).Replace('-','').ToLowerInvariant()}
    finally{$sha.Dispose()}
}
function Write-WorkshopRetentionJob([string]$Path,$Job) {
    Assert-ApiWorkshopUnlinkedPath $Path;Assert-ApiWorkshopUnlinkedPath ($Path+'.tmp')
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    $text=(($Job|ConvertTo-Json -Depth 8)-replace '\r?\n',"`r`n")+"`r`n"
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes($text)
    $stream=[IO.FileStream]::new(($Path+'.tmp'),[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()}
    if([IO.File]::ReadAllText($Path+'.tmp') -cne $text){throw 'Retention job write verification failed.'}
    if([IO.File]::Exists($Path)){[IO.File]::Replace(($Path+'.tmp'),$Path,$null)}else{[IO.File]::Move(($Path+'.tmp'),$Path)}
}
function Assert-WorkshopRetentionJob($Job,[string]$Destination,$Target,[bool]$Test) {
    $pattern='^'+[regex]::Escape([IO.Path]::GetFileName($Destination))+'\.previous-[a-f0-9]{32}$'
    if($Job.SchemaVersion -ne 1 -or $Job.Target -cne $Target.Key -or $Job.PackageId -cne $Target.Guid -or $Job.Mode -cne $(if($Test){'Test'}else{'Production'}) -or $Job.Destination -cne $Destination -or $Job.DirectoryName -cnotmatch $pattern -or $Job.Layout -cnotin @('map','loose') -or ($Job.Layout -ceq 'loose' -and $Target.Key -cne 'SerpsMods') -or $Job.Digest -cne (Get-WorkshopRetentionJobHash $Job)){throw 'Invalid or modified retention job.'}
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in @($Job.Files)){
        if(-not (Test-WorkshopRetentionFile $file.Path $Target.Key $Job.Layout) -or -not $names.Add($file.Path) -or [string]$file.Sha256 -cnotmatch '^[a-f0-9]{64}$' -or [long]$file.Size -lt 0){throw 'Invalid retention file inventory.'}
    }
    $map=if($Target.Key -ceq 'APIShared'){'APIShared.map'}else{'SerpsMods.map'}
    if(-not $names.Contains($map) -or -not $names.Contains('preview.png') -or ($Job.Layout -ceq 'loose' -and -not $names.Contains('apishared-workshop.json'))){throw 'Incomplete retention file inventory.'}
    foreach($directory in @($Job.Directories)){
        Assert-WorkshopRetentionRelative $directory
        if(-not $names.Add($directory+'/') -or (-not (Test-WorkshopRetentionDirectory $directory $Target.Key $Job.Layout) -and -not @($Job.Files|Where-Object {$_.Path.StartsWith($directory+'/',[StringComparison]::Ordinal)}).Count)){throw 'Invalid retention directory inventory.'}
    }
}
function Remove-WorkshopRetentionJob($Job,[string]$JobPath,[string]$Destination,$Target,[bool]$Test) {
    Assert-WorkshopRetentionJob $Job $Destination $Target $Test
    $parent=[IO.Path]::GetDirectoryName($Destination)
    $directory=[IO.Path]::GetFullPath((Join-Path $parent $Job.DirectoryName))
    if([IO.Path]::GetDirectoryName($directory) -cne $parent -or $directory -ceq $Destination){throw 'Retention destination escapes output siblings.'}
    Assert-ApiWorkshopUnlinkedPath $directory
    $freed=[long]0
    try{
    if([IO.Directory]::Exists($directory)){
        $tree=Get-WorkshopRetentionTree $directory
        $files=@{};foreach($file in @($Job.Files)){$files[$file.Path]=$file}
        foreach($file in $tree.Files){if(-not $files.ContainsKey($file.Path) -or $file.Size -ne $files[$file.Path].Size -or $file.Sha256 -cne $files[$file.Path].Sha256){throw ('Backup changed during deletion: '+$file.Path)}}
        foreach($child in $tree.Directories){if($child -cnotin @($Job.Directories)){throw ('Unknown directory during deletion: '+$child)}}
        # No recursive deletion: only individually pinned files and then empty directories.
        foreach($file in @($Job.Files)){
            $path=Join-Path $directory $file.Path;Assert-ApiWorkshopUnlinkedPath $path
            if([IO.File]::Exists($path)){
                if((Get-WorkshopFileHash $path) -cne $file.Sha256){throw ('Backup file changed: '+$file.Path)}
                Remove-Item -LiteralPath $path -Force -ErrorAction Stop
                $freed+=[long]$file.Size
            }
        }
        foreach($child in @($Job.Directories|Sort-Object Length -Descending)){
            $path=Join-Path $directory $child;Assert-ApiWorkshopUnlinkedPath $path
            if([IO.Directory]::Exists($path)){[IO.Directory]::Delete($path,$false)}
        }
        Assert-ApiWorkshopUnlinkedPath $directory;[IO.Directory]::Delete($directory,$false)
    }
    Assert-ApiWorkshopUnlinkedPath $JobPath;[IO.File]::Delete($JobPath)
    return $freed
    }catch{$_.Exception.Data['RetentionFreedBytes']=$freed;throw}
}
function Invoke-WorkshopOutputRetention($Config,$Target,[bool]$Test,[string[]]$Before,$Lock,[string[]]$DeleteOnly) {
    $removed=0;$freed=[long]0;$deferred=0
    try{
        if($Target.Key -cnotin @('APIShared','SerpsMods')){throw 'Unsupported output retention target.'}
        $state=[IO.Path]::GetFullPath((Get-WorkshopStateRoot $Config $Test))
        $lockPath=Join-Path $state 'workflow.lock';Assert-ApiWorkshopUnlinkedPath $lockPath
        if(-not $Lock -or -not $Lock.CanWrite -or $Lock.Name -cne $lockPath){throw 'Retention requires the live workflow lock.'}
        $other=$null
        try{$other=[IO.File]::Open($lockPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::ReadWrite)}catch [IO.IOException]{}
        if($other){$other.Dispose();throw 'Workflow lock is not exclusive.'}
        $null=Assert-WorkshopPreparation $Config $Target $Test
        $destination=[IO.Path]::GetFullPath((Get-WorkshopFolder $Config $Target $Test))
        $workspace=[IO.Path]::GetFullPath($Config.Root).TrimEnd('\')+'\'
        if(-not $destination.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase)){throw 'Retention output escapes workspace.'}
        $backups=@(Get-WorkshopOutputBackups $Config $Target $Test)
        $leaf=[IO.Path]::GetFileName($destination)
        foreach($directory in Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($destination)) -Force -Directory){
            if($directory.Name.StartsWith($leaf+'.ready-',[StringComparison]::Ordinal) -or ($directory.Name.StartsWith($leaf+'.previous-',[StringComparison]::Ordinal) -and $directory.Name -cnotin @($backups|ForEach-Object {$_.Name}))){
                $deferred++;Write-Host ('WARNING: unfinished or unrecognized output retained: '+$directory.FullName)
            }
        }
        $new=@($backups|Where-Object {$_.Name -cnotin @($Before)})
        if($null -eq $DeleteOnly -and $new.Count -gt 1){throw 'Multiple new backup folders; retain all and inspect the preparation.'}
        $pending=Join-Path $state ('retention/'+$Target.Key+'.pending.json')
        Assert-ApiWorkshopUnlinkedPath $pending
        if([IO.File]::Exists($pending)){
            $job=[IO.File]::ReadAllText($pending)|ConvertFrom-Json
            Assert-WorkshopRetentionJob $job $destination $Target $Test
            if($null -ne $DeleteOnly -and $job.DirectoryName -cnotin $DeleteOnly){throw 'Pending deletion is outside the explicit cleanup selection.'}
            if($null -eq $DeleteOnly -and $job.DirectoryName -cin @($new|ForEach-Object {$_.Name})){throw 'Pending deletion conflicts with the new backup.'}
            $freed+=Remove-WorkshopRetentionJob $job $pending $destination $Target $Test;$removed++
        }
        $valid=@()
        foreach($backup in @(Get-WorkshopOutputBackups $Config $Target $Test)){
            try{$inventory=Get-WorkshopOutputBackupInventory $backup.FullName $Target;$valid+=@([pscustomobject]@{Directory=$backup;Inventory=$inventory})}
            catch{$deferred++;Write-Host ('WARNING: backup retained: '+$backup.FullName+'; '+$_.Exception.Message)}
        }
        $keep=$null
        if($null -eq $DeleteOnly){
            if($new.Count -eq 1){
                $keep=$new[0].Name
                if(-not @($valid|Where-Object {$_.Directory.Name -ceq $keep}).Count){throw 'The newest backup is not valid; retain older backups.'}
            }elseif($valid.Count){$keep=($valid|Sort-Object @{Expression={$_.Directory.LastWriteTimeUtc};Descending=$true},@{Expression={$_.Directory.Name};Descending=$false}|Select-Object -First 1).Directory.Name}
        }
        foreach($entry in $valid){
            if($entry.Directory.Name -ceq $keep -or ($null -ne $DeleteOnly -and $entry.Directory.Name -cnotin $DeleteOnly)){continue}
            $job=[ordered]@{SchemaVersion=1;Target=$Target.Key;PackageId=$Target.Guid;Mode=$(if($Test){'Test'}else{'Production'});Destination=$destination;DirectoryName=$entry.Directory.Name;Layout=$entry.Inventory.Layout;Files=@($entry.Inventory.Files);Directories=@($entry.Inventory.Directories)}
            $job.Digest=Get-WorkshopRetentionJobHash $job
            Write-WorkshopRetentionJob $pending $job
            $freed+=Remove-WorkshopRetentionJob $job $pending $destination $Target $Test;$removed++
        }
    }catch{
        if($_.Exception.Data.Contains('RetentionFreedBytes')){$freed+=[long]$_.Exception.Data['RetentionFreedBytes']}
        $deferred++;Write-Host ('WARNING: Workshop output retention deferred for '+$Target.Key+': '+$_.Exception.Message)
    }
    Write-Host ('Workshop output retention '+$Target.Key+': removed='+$removed+'; freedBytes='+$freed+'; deferred='+$deferred)
    [pscustomobject]@{Removed=$removed;FreedBytes=$freed;Deferred=$deferred}
}
