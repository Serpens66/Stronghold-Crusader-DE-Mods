# Local metadata only: a hit requires unchanged inputs AND the original package still present.
# Live release, Git, dependency, package and publication gates remain the caller's responsibility.
function Get-ModCheckpointHash([string]$Path) {
    $stream=[IO.File]::OpenRead($Path);$sha=[Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose();$stream.Dispose() }
}
function Assert-ModCheckpointPath([string]$Path) {
    for($part=[IO.Path]::GetFullPath($Path);$part;$part=[IO.Path]::GetDirectoryName($part)) {
        if((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part)-band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked build checkpoint path.' }
    }
}
function Get-ModCheckpointFiles([string]$Directory) {
    if(-not [IO.Directory]::Exists($Directory)){return}
    Assert-ModCheckpointPath $Directory
    foreach($entry in Get-ChildItem -LiteralPath $Directory -Force) {
        if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build input.'}
        if($entry.PSIsContainer) {
            if($entry.Name -notin @('.git','.work','.local','.release-output','.vs','bin','obj','BepInEx','TestResults')) { Get-ModCheckpointFiles $entry.FullName }
        } else { $entry }
    }
}
function Get-ModCheckpointPackage([string]$Directory) {
    if(-not [IO.Directory]::Exists($Directory)){throw 'Build package missing.'}
    Assert-ModCheckpointPath $Directory
    $records=@(foreach($file in Get-ChildItem -LiteralPath $Directory -Force -Recurse) {
        if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build package.'}
        if(-not $file.PSIsContainer){[ordered]@{Path=$file.FullName.Substring($Directory.TrimEnd('\').Length+1).Replace('\','/');Size=$file.Length;Sha256=(Get-ModCheckpointHash $file.FullName)}}
    })
    if(-not $records.Count){throw 'Build package empty.'}
    return @($records|Sort-Object {$_.Path})
}
function Get-ModCheckpointFingerprint($Context) {
    $inputs=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$inputs.Add($PSCommandPath)
    $roots=@($Context.ModDirectory)
    foreach($relative in @('Shared/Runtime','Shared/Adapters','Shared/Tools/Validation','Shared/Tools/ScriptExtenderUpdate',
        'Shared/Tools/ApiSharedRepository','Shared/Tools/BranchBuild','Shared/Tools/Paths','Shared/Tools/BuildCache')) { $roots+=Join-Path $Context.Workspace $relative }
    foreach($root in $roots){foreach($file in Get-ModCheckpointFiles $root){[void]$inputs.Add($file.FullName)}}
    foreach($relative in @('AGENTS.md','Directory.Build.props','Directory.Build.targets','global.json','NuGet.config')) {
        $file=Join-Path $Context.Workspace $relative;if([IO.File]::Exists($file)){[void]$inputs.Add($file)}
    }
    foreach($file in Get-ModCheckpointFiles (Join-Path $Context.Workspace 'AGENTS')){[void]$inputs.Add($file.FullName)}
    $visitedProjects=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    while($true) {
        $projects=@($inputs|Where-Object {[IO.Path]::GetExtension($_) -eq '.csproj' -and -not $visitedProjects.Contains($_)})
        if(-not $projects.Count){break}
        foreach($project in $projects) {
            [void]$visitedProjects.Add($project);[xml]$xml=[IO.File]::ReadAllText($project)
            $properties=@{MSBuildThisFileDirectory=((Split-Path -Parent $project)+'\');GameDir=$Context.GameDirectory;
                ExtenderDir=$Context.ExtenderDirectory;ApiSharedDir=$Context.ApiSharedDirectory;
                ScriptExtenderSourceRoot=(Join-Path $Context.Coordinator 'shcde-script-extender')}
            foreach($property in $xml.SelectNodes('//*[local-name()="PropertyGroup"]/*')) {
                if(-not $properties.ContainsKey($property.LocalName)){$properties[$property.LocalName]=[string]$property.InnerText}
            }
            foreach($node in $xml.SelectNodes('//*[local-name()="ProjectReference" or local-name()="Compile"][@Include]')) {
                $include=[string]$node.Include
                for($pass=0;$pass -lt 8 -and $include.Contains('$(');$pass++) {
                    $before=$include
                    $include=[regex]::Replace($include,'\$\((?<Name>[A-Za-z0-9_]+)\)',{param($match)if($properties.ContainsKey($match.Groups['Name'].Value)){$properties[$match.Groups['Name'].Value]}else{$match.Value}})
                    if($include -ceq $before){break}
                }
                if($include.Contains('$(') -and [string]$node.Condition -match "^'\`$\([A-Za-z0-9_]+\)'\s*!=\s*''$"){continue}
                if($include.Contains('$(')){throw "Unresolved checkpoint source input: $include"}
                $targetPath=if([IO.Path]::IsPathRooted($include)){$include}else{Join-Path (Split-Path -Parent $project) $include}
                if($targetPath.IndexOfAny([char[]]'*?') -ge 0) {
                    $prefix=[IO.Path]::GetFullPath($targetPath.Substring(0,$targetPath.IndexOfAny([char[]]'*?')));$directory=Split-Path -Parent $prefix.TrimEnd('\','/')
                    if([IO.Directory]::Exists($prefix.TrimEnd('\','/'))){$directory=$prefix.TrimEnd('\','/')}
                    foreach($file in Get-ModCheckpointFiles $directory){[void]$inputs.Add($file.FullName)}
                    continue
                }
                $target=[IO.Path]::GetFullPath($targetPath)
                if($node.LocalName -eq 'ProjectReference') {
                    if(-not [IO.File]::Exists($target)){throw "Checkpoint project reference missing: $target"}
                    foreach($file in Get-ModCheckpointFiles (Split-Path -Parent $target)){[void]$inputs.Add($file.FullName)}
                } else {
                    if(-not [IO.File]::Exists($target)){throw "Checkpoint source link missing: $target"}
                    [void]$inputs.Add($target)
                }
            }
        }
    }
    # Include maintained analysis-hosted suites explicitly referenced by the mod or its tests.
    $inspectNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $sharedTestNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in @($inputs)) {
        if([IO.Path]::GetExtension($file) -notin @('.cs','.csproj','.ps1','.bat','.cmd','.props','.targets')){continue}
        foreach($match in [regex]::Matches([IO.File]::ReadAllText($file),'(?i)_inspect[\\/]+(?<Name>[A-Za-z0-9_.-]+)')) {
            if($match.Groups['Name'].Value -ne 'CrusaderDE-Native-Baseline'){[void]$inspectNames.Add($match.Groups['Name'].Value)}
        }
        foreach($match in [regex]::Matches([IO.File]::ReadAllText($file),'(?i)Shared[\\/]+Tests[\\/]+(?<Name>[A-Za-z0-9_.-]+)')){[void]$sharedTestNames.Add($match.Groups['Name'].Value)}
    }
    foreach($name in $inspectNames){foreach($file in Get-ModCheckpointFiles (Join-Path $Context.Workspace ('_inspect/'+$name))){[void]$inputs.Add($file.FullName)}}
    foreach($name in $sharedTestNames){foreach($file in Get-ModCheckpointFiles (Join-Path $Context.Workspace ('Shared/Tests/'+$name))){[void]$inputs.Add($file.FullName)}}
    # Consumer preflight reads the selected SDK sources in addition to its compiled package.
    foreach($relative in @('src','Content','Properties','Patches')) {
        foreach($file in Get-ModCheckpointFiles (Join-Path $Context.ApiSourceDirectory $relative)){[void]$inputs.Add($file.FullName)}
    }
    foreach($file in Get-ChildItem -LiteralPath $Context.ApiSourceDirectory -File | Where-Object {$_.Extension -in @('.csproj','.props','.targets') -or $_.Name -eq 'info.json'}){[void]$inputs.Add($file.FullName)}
    # These are the real references, including native and test-only game dependencies.
    foreach($root in @((Join-Path $Context.GameDirectory 'Stronghold Crusader Definitive Edition_Data/Managed'),
        (Join-Path $Context.GameDirectory 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64'),
        (Join-Path $Context.GameDirectory 'BepInEx/core'),$Context.ExtenderDirectory,$Context.ApiSharedDirectory) +
        $(if($env:SHCDESE_EXTENDER_DIR){@([IO.Path]::GetFullPath($env:SHCDESE_EXTENDER_DIR))}else{@()})) {
        Assert-ModCheckpointPath $root
        if(-not [IO.Directory]::Exists($root)){throw "Build input directory missing: $root"}
        foreach($file in Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {$_.Extension -in @('.dll','.exe','.config','.json','.xml','.ps1','.sh','.bat','.txt')}){[void]$inputs.Add($file.FullName)}
    }
    foreach($name in @('dotnet','powershell.exe','pwsh.exe','git','gh')) {
        $command=Get-Command $name -CommandType Application -ErrorAction SilentlyContinue|Select-Object -First 1
        if($command){[void]$inputs.Add($command.Source)}
    }
    if(-not [IO.File]::Exists($Context.Compiler)){throw 'Build compiler missing.'}
    [void]$inputs.Add($Context.Compiler)
    $compilerRoot=Split-Path -Parent $Context.Compiler
    foreach($file in Get-ChildItem -LiteralPath $compilerRoot -File | Where-Object {$_.Name -match '^(MSBuild|Microsoft\.Build).*\.(dll|exe|config)$'}){[void]$inputs.Add($file.FullName)}
    foreach($file in Get-ModCheckpointFiles (Join-Path $compilerRoot 'Roslyn')){[void]$inputs.Add($file.FullName)}
    $python='D:/CDesktopLink/Portable/Python/WinPy64/python/python.exe'
    if([IO.File]::Exists($python)){[void]$inputs.Add([IO.Path]::GetFullPath($python))}
    $rows=[Collections.Generic.List[string]]::new()
    foreach($file in $inputs){Assert-ModCheckpointPath $file;$rows.Add([IO.Path]::GetFullPath($file)+':'+(Get-ModCheckpointHash $file))}
    # The native baseline's manifest binds the database digest. Record its stat rather
    # than hashing gigabytes on each retry; the existing native audits validate its identity.
    $baseline=$env:SHCDE_NATIVE_BASELINE_ROOT
    if(-not $baseline){$baseline=Join-Path $Context.Workspace '_inspect/CrusaderDE-Native-Baseline'}
    $current=Join-Path $baseline 'CURRENT.json'
    if([IO.File]::Exists($current)) {
        Assert-ModCheckpointPath $current;$rows.Add('baseline-current:'+(Get-ModCheckpointHash $current))
        $selection=[IO.File]::ReadAllText($current)|ConvertFrom-Json
        $manifestPath=[IO.Path]::GetFullPath((Join-Path $baseline $selection.databaseManifest))
        if(-not $manifestPath.StartsWith([IO.Path]::GetFullPath($baseline).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Baseline manifest escapes root.'}
        Assert-ModCheckpointPath $manifestPath;$rows.Add('baseline-manifest:'+(Get-ModCheckpointHash $manifestPath))
        $manifest=[IO.File]::ReadAllText($manifestPath)|ConvertFrom-Json
        $database=[IO.Path]::GetFullPath((Join-Path $baseline $manifest.database.path))
        if(-not $database.StartsWith([IO.Path]::GetFullPath($baseline).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Baseline database escapes root.'}
        Assert-ModCheckpointPath $database;$stat=Get-Item -LiteralPath $database
        $rows.Add('baseline-db:'+ $database+':'+$manifest.database.referenceSha256+':'+$stat.Length+':'+$stat.LastWriteTimeUtc.Ticks)
        $semantic=[IO.Path]::GetFullPath((Join-Path $baseline $selection.semanticDirectory))
        if(-not $semantic.StartsWith([IO.Path]::GetFullPath($baseline).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Baseline semantic inputs escape root.'}
        foreach($directory in @($semantic,(Join-Path $baseline 'tools'))) {
            foreach($file in Get-ModCheckpointFiles $directory | Where-Object {
                $_.FullName -notmatch '[\\/](analysis|logs|archive)[\\/]' -and $_.Extension -in @('.json','.csv','.tsv','.sql','.py','.ps1','.cs','.h','.md','.txt','.xaml')
            }) {$rows.Add('baseline-input:'+ $file.FullName+':'+(Get-ModCheckpointHash $file.FullName))}
        }
    }
    # Keep no environment values in metadata (credentials can be present). Hash the
    # effective inherited environment; temporary build records are not part of the inputs.
    foreach($entry in [Environment]::GetEnvironmentVariables().GetEnumerator()){$rows.Add('env:'+ $entry.Key+'='+$entry.Value)}
    $rows.Add('context:'+($Context|ConvertTo-Json -Depth 4 -Compress))
    $rows.Add('host:'+ $PSVersionTable.PSVersion+':'+[Environment]::OSVersion.VersionString+':'+[Globalization.CultureInfo]::CurrentCulture.Name)
    $sdkInfo=@(& dotnet --info 2>&1);if($LASTEXITCODE -ne 0){throw 'Cannot identify the .NET toolchain.'};$rows.Add('dotnet:'+($sdkInfo -join "`n"))
    $rows.Sort([StringComparer]::Ordinal);$sha=[Security.Cryptography.SHA256]::Create()
    try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($rows -join "`n")))).Replace('-','').ToLowerInvariant()}
    finally{$sha.Dispose()}
}
function Invoke-ModBuildCheckpointCore {
    param([Parameter(Mandatory)][string]$Workspace,[Parameter(Mandatory)][string]$Coordinator,[Parameter(Mandatory)][string]$ModName,
        [Parameter(Mandatory)][string]$ModDirectory,[Parameter(Mandatory)][string]$PackageDirectory,
        [Parameter(Mandatory)][string]$Driver,[Parameter(Mandatory)][string[]]$Arguments,[Parameter(Mandatory)][string]$Commit,
        [Parameter(Mandatory)][string]$GameDirectory,[Parameter(Mandatory)][string]$ExtenderDirectory,
        [Parameter(Mandatory)][string]$ApiSharedDirectory,[Parameter(Mandatory)][string]$ApiSourceDirectory,
        [Parameter(Mandatory)][string]$Compiler,[string]$RetainedPackageDirectory,[Parameter(Mandatory)][scriptblock]$Action)
    if($ModName -cnotmatch '^[A-Za-z][A-Za-z0-9_.-]*$' -or $Commit -cnotmatch '^[a-f0-9]{40}$'){throw 'Invalid build checkpoint identity.'}
    $context=[ordered]@{Workspace=[IO.Path]::GetFullPath($Workspace);Coordinator=[IO.Path]::GetFullPath($Coordinator);ModName=$ModName;ModDirectory=[IO.Path]::GetFullPath($ModDirectory);
        PackageDirectory=[IO.Path]::GetFullPath($PackageDirectory);Driver=[IO.Path]::GetFullPath($Driver);Arguments=@($Arguments);Commit=$Commit;
        GameDirectory=[IO.Path]::GetFullPath($GameDirectory);ExtenderDirectory=[IO.Path]::GetFullPath($ExtenderDirectory);
        ApiSharedDirectory=[IO.Path]::GetFullPath($ApiSharedDirectory);ApiSourceDirectory=[IO.Path]::GetFullPath($ApiSourceDirectory);Compiler=[IO.Path]::GetFullPath($Compiler)}
    if($RetainedPackageDirectory) {
        $retained=[IO.Path]::GetFullPath($RetainedPackageDirectory)
        if(-not $retained.StartsWith($context.Workspace.TrimEnd('\')+'\.release-output\',[StringComparison]::OrdinalIgnoreCase)){throw 'Retained build package escapes release output.'}
        Assert-ModCheckpointPath $retained
        $context.RetainedPackageDirectory=$retained
    }
    $cache=Join-Path ([IO.Path]::GetFullPath($Coordinator)) '.release-output/checkpoints/mod-builds'
    $path=Join-Path $cache ($ModName+'.json');$fingerprint=$null;$cacheAvailable=$false;$cacheWritable=$false
    try {
        Assert-ModCheckpointPath $cache;[void][IO.Directory]::CreateDirectory($cache)
        Assert-ModCheckpointPath $path
        $cacheWritable=$true
        $fingerprint=Get-ModCheckpointFingerprint $context;$cacheAvailable=$true
        if([IO.File]::Exists($path) -and (Get-Item -LiteralPath $path).Length -le 1MB) {
            $proof=[IO.File]::ReadAllText($path)|ConvertFrom-Json
            if($proof.SchemaVersion -eq 1 -and $proof.Fingerprint -ceq $fingerprint -and $proof.BuildCommit -ceq $Commit -and $proof.Status -ceq 'Passed') {
                foreach($candidate in @($context.PackageDirectory,$RetainedPackageDirectory)) {
                    if(-not $candidate){continue}
                    try {$actual=@(Get-ModCheckpointPackage $candidate)}catch{continue}
                    if(($actual|ConvertTo-Json -Depth 4 -Compress) -ceq (@($proof.PackageFiles)|ConvertTo-Json -Depth 4 -Compress)) {
                        Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] Reusing successful {1} build/checks from {2}; package hashes verified.' -f [DateTime]::Now,$ModName,$proof.BuildCompletedUtc)
                        return [pscustomobject]@{ExitCode=0;Output=@();PackageDirectory=[IO.Path]::GetFullPath($candidate);Checkpoint=[pscustomobject]@{Reused=$true;Fingerprint=$fingerprint;BuildCommit=$proof.BuildCommit;BuildStartedUtc=$proof.BuildStartedUtc;BuildCompletedUtc=$proof.BuildCompletedUtc}}
                    }
                }
            }
        }
    } catch {Write-Host ('Build checkpoint unavailable for '+$ModName+': '+$_.Exception.Message+'; running the build. '+$_.ScriptStackTrace)}
    # Remove a former success before attempting work: failure can never resurrect it.
    if($cacheWritable -and [IO.File]::Exists($path)){Assert-ModCheckpointPath $path;[IO.File]::Delete($path)}
    $started=[DateTime]::UtcNow.ToString('o')
    $result=& $Action
    if($null -eq $result -or -not $result.PSObject.Properties['ExitCode']){throw 'Build checkpoint action must return its exit code and output.'}
    if($result.ExitCode -ne 0){return $result}
    $completed=[DateTime]::UtcNow.ToString('o')
    if($cacheAvailable) {
        try {
            $after=Get-ModCheckpointFingerprint $context
            if($after -cne $fingerprint){throw 'Build inputs changed during execution.'}
            $files=@(Get-ModCheckpointPackage $context.PackageDirectory)
            $proof=[ordered]@{SchemaVersion=1;Status='Passed';Mod=$ModName;Fingerprint=$fingerprint;BuildCommit=$Commit;BuildStartedUtc=$started;BuildCompletedUtc=$completed;PackageFiles=$files}
            $text=($proof|ConvertTo-Json -Depth 6).Replace("`r`n","`n").Replace("`n","`r`n")+"`r`n"
            if([Text.Encoding]::UTF8.GetByteCount($text) -gt 1MB){throw 'Checkpoint metadata exceeds 1 MiB; no cache entry written.'}
            $temporary=$path+'.tmp';[IO.File]::WriteAllText($temporary,$text,[Text.UTF8Encoding]::new($false))
            if([IO.File]::ReadAllText($temporary) -cne $text){throw 'Checkpoint write verification failed.'}
            if([IO.File]::Exists($path)){[IO.File]::Replace($temporary,$path,$null)}else{[IO.File]::Move($temporary,$path)}
            # At most 32 one-MiB records; no binaries, logs, source copies or package copies.
            $entries=@(Get-ChildItem -LiteralPath $cache -File -Filter '*.json'|Sort-Object LastWriteTimeUtc -Descending)
            foreach($old in @($entries|Select-Object -Skip 32)){Assert-ModCheckpointPath $old.FullName;[IO.File]::Delete($old.FullName)}
            $result|Add-Member NoteProperty Checkpoint ([pscustomobject]@{Reused=$false;Fingerprint=$fingerprint;BuildCommit=$Commit;BuildStartedUtc=$started;BuildCompletedUtc=$completed}) -Force
        } catch {Write-Host ('Successful '+$ModName+' build was not cached: '+$_.Exception.Message)}
    }
    return $result
}
function Invoke-ModBuildCheckpoint {
    param([Parameter(Mandatory)][string]$Workspace,[Parameter(Mandatory)][string]$Coordinator,[Parameter(Mandatory)][string]$ModName,
        [Parameter(Mandatory)][string]$ModDirectory,[Parameter(Mandatory)][string]$PackageDirectory,
        [Parameter(Mandatory)][string]$Driver,[Parameter(Mandatory)][string[]]$Arguments,[Parameter(Mandatory)][string]$Commit,
        [Parameter(Mandatory)][string]$GameDirectory,[Parameter(Mandatory)][string]$ExtenderDirectory,
        [Parameter(Mandatory)][string]$ApiSharedDirectory,[Parameter(Mandatory)][string]$ApiSourceDirectory,
        [Parameter(Mandatory)][string]$Compiler,[string]$RetainedPackageDirectory,[Parameter(Mandatory)][scriptblock]$Action)
    # Installation is an externally visible side effect, not proved by package hashes.
    # Only the explicit non-installing build contract is eligible for whole-build reuse.
    if($Arguments -notcontains '/noinstall'){return (& $Action)}
    if($ModName -cnotmatch '^[A-Za-z][A-Za-z0-9_.-]*$'){throw 'Invalid build checkpoint identity.'}
    $directory=Join-Path ([IO.Path]::GetFullPath($Coordinator)) '.release-output/checkpoints/mod-builds'
    try {Assert-ModCheckpointPath $directory;[void][IO.Directory]::CreateDirectory($directory)}
    catch {Write-Host ('Build checkpoint disabled: '+$_.Exception.Message);return (& $Action)}
    $lock=$null
    try {
        $lockPath=Join-Path $directory ($ModName+'.lock');Assert-ModCheckpointPath $lockPath
        $lock=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        Invoke-ModBuildCheckpointCore @PSBoundParameters
    } finally {if($lock){$lock.Dispose()}}
}
