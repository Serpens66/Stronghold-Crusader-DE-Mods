# Owned build runs: external records survive preparation and cleanup failures.
function Assert-BranchRunRoot([string]$Repository, [string]$Root) {
    $repositoryPath = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $runPath = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $prefix = $repositoryPath + '\.work\b\'
    if (-not $runPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        $runPath.Substring($prefix.Length) -notmatch '^[A-Za-z0-9_-]+\\[0-9]+$') { throw "Invalid owned build root: $runPath" }
    Assert-BranchUnlinkedPath $runPath
    return $runPath
}
function Set-BranchRunPhase($Run, [string]$Phase, [string]$Detail = '') {
    $Run.State.Phase = $Phase
    $Run.State.Detail = $Detail
    $Run.State.UpdatedUtc = [DateTime]::UtcNow.ToString('o')
    Write-BranchText ($Run.Root + '.lifecycle.json') (($Run.State | ConvertTo-Json -Depth 10) + "`r`n")
}
function Start-BranchRun([string]$Repository, [string]$Branch, [string]$Driver, [bool]$KeepSource, [bool]$NoInstall = $false) {
    $repositoryPath = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $reference = if ($Branch -eq 'working') { 'HEAD^{commit}' } else { "refs/heads/${Branch}^{commit}" }
    $commit = ((Invoke-BranchGit $repositoryPath @('rev-parse','--verify',$reference)) -join '').Trim()
    $root = New-ShortWorkDirectory -Root $repositoryPath -Kind b -Name $Branch -Identity $commit
    $root = Assert-BranchRunRoot $repositoryPath $root
    $lease = [IO.File]::Open($root + '.lease', 'OpenOrCreate', 'ReadWrite', 'None')
    $state = [ordered]@{ SchemaVersion=1; Protocol='ModBuildRun'; Repository=$repositoryPath; Root=$root; Commit=$commit; Branch=$Branch;
        Driver=$Driver; OwnerPid=$PID; OwnerStartedUtc=(Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('o');
        KeepSource=$KeepSource; NoInstall=$NoInstall; Phase='preparing'; Detail=''; UpdatedUtc=''; RetentionManifest='' }
    $run = [pscustomobject]@{ Root=$root; Commit=$commit; Branch=$Branch; Lease=$lease; State=$state }
    try { Set-BranchRunPhase $run 'preparing'; return $run } catch { $lease.Dispose(); throw }
}
function Test-BranchRunProtected([string]$Repository, [string]$Root) {
    $index = Join-Path $Repository '.work/INDEX.json'
    if (-not (Test-Path -LiteralPath $index -PathType Leaf)) { return $false }
    $document = [IO.File]::ReadAllText($index) | ConvertFrom-Json
    $relative = $Root.Substring($Repository.Length + 1).Replace('\','/')
    foreach ($property in $document.PSObject.Properties) {
        $value = $property.Value
        if ($value -and $value.PSObject.Properties['Preserve'] -and $value.Preserve) {
            $text = ($value | ConvertTo-Json -Depth 100).Replace('\\','/').Replace('\','/')
            if ($text.IndexOf($relative, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                $text.IndexOf($Root.Replace('\','/'), [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
        }
    }
    return $false
}
function Get-BranchCrlfBlobHash([string]$Root, [string]$ObjectId) {
    if ($ObjectId -cnotmatch '^[0-9a-f]{40,64}$') { throw 'Invalid Git restoration request.' }
    $size = [long](((Invoke-BranchGit $Root @('cat-file','-s',$ObjectId)) -join '').Trim())
    if ($size -gt 16777216) { return '' }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command git -CommandType Application | Select-Object -First 1).Source
    $start.WorkingDirectory = $Root
    $start.Arguments = 'cat-file blob ' + $ObjectId
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $bytes = [IO.MemoryStream]::new(); $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $errors = $process.StandardError.ReadToEndAsync()
        $process.StandardOutput.BaseStream.CopyTo($bytes); $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw ('Git restoration failed: ' + $errors.Result) }
        # Latin-1 is a reversible byte mapping. The sole restoration operation is
        # LF->CRLF (existing CRLF preserved), and its exact raw hash must match.
        $encoding = [Text.Encoding]::GetEncoding(28591)
        $text = $encoding.GetString($bytes.ToArray()).Replace("`r`n","`n").Replace("`n","`r`n")
        return [BitConverter]::ToString($sha.ComputeHash($encoding.GetBytes($text))).Replace('-','').ToLowerInvariant()
    } finally { $bytes.Dispose(); $sha.Dispose(); $process.Dispose() }
}
function Test-BranchOwnedFixtureFile([string]$Root, [string]$Relative) {
    if ($Relative -notmatch '^\.work/t/([A-Za-z0-9_-]+)/([0-9]+)(/|\.run\.json$)') { return $false }
    $fixture = Join-Path $Root ('.work/t/' + $Matches[1] + '/' + $Matches[2])
    $record = $fixture + '.run.json'
    Assert-BranchUnlinkedPath $fixture
    Assert-BranchUnlinkedPath (Join-Path $Root $Relative)
    if (-not [IO.File]::Exists($record)) { return $false }
    $metadata = [IO.File]::ReadAllText($record) | ConvertFrom-Json
    if ($metadata.SchemaVersion -ne 1 -or $metadata.Root -ine $fixture) { return $false }
    if ($metadata.PSObject.Properties['Kind']) {
        if ($metadata.Kind -cne 't') { return $false }
    } else {
        # TestWorkDirectory.cs deliberately emits only SchemaVersion + Root.
        # Require its exact canonical record, not arbitrary metadata without a kind.
        $expected = '{"SchemaVersion":1,"Root":"' + $fixture.Replace('\','\\').Replace('"','\"') + '"}'
        if ([IO.File]::ReadAllText($record) -cne $expected) { return $false }
    }
    for ($part = [IO.Path]::GetDirectoryName((Join-Path $Root $Relative)); $part -and $part.Length -ge $fixture.Length; $part = [IO.Path]::GetDirectoryName($part)) {
        if (Test-Path -LiteralPath (Join-Path $part '.git')) { throw 'Test fixture contains a nested repository; manual review required.' }
    }
    return $true
}
function Test-BranchGeneratedCacheFile([string]$Relative) {
    if ($Relative -notmatch '(^|/)(bin|obj|packages|node_modules)/') { return $false }
    if ($Relative -match '\.(cs|cpp|c|h|ps1|py|bat|sh)$' -and
        $Relative -notmatch '(\.g\.cs|\.AssemblyInfo\.cs|\.AssemblyAttributes\.cs)$') {
        throw "Unclassified source inside build cache: $Relative"
    }
    return $true
}
function Save-BranchRunContents([string]$Repository, $Run) {
    $root = Assert-BranchRunRoot $Repository $Run.Root
    $top = ((Invoke-BranchGit $root @('rev-parse','--show-toplevel')) -join '').Trim()
    if ([IO.Path]::GetFullPath($top).TrimEnd('\','/') -ine $root) { throw 'Build source is not its own worktree.' }
    if (@(Invoke-BranchGit $root @('diff','--cached','--name-only','HEAD')).Count) { throw 'Build contains staged changes; retain its index for explicit review.' }
    $head = ((Invoke-BranchGit $root @('rev-parse','HEAD')) -join '').Trim()
    $reachableCommits = @(Invoke-BranchGit $Repository @('rev-list','--all'))
    if ($head -notin $reachableCommits) { throw "Build has an unreferenced commit: $head" }
    $reachable = @{}
    foreach ($line in @(Invoke-BranchGit $Repository @('rev-list','--objects','--all'))) { $reachable[($line -split ' ',2)[0]] = $true }
    $initialIgnored = @(Invoke-BranchGit $root @('-c','core.quotepath=false','ls-files','--others','--ignored','--exclude-standard'))
    $nestedEvidence = [Collections.Generic.List[string]]::new()
    $nestedSurvivors = [Collections.Generic.List[object]]::new()
    $nestedIgnored = @()
    $nestedDifferences = @()
    $nested = Join-Path $root 'APIShared'
    if (Test-Path -LiteralPath (Join-Path $nested '.git') -PathType Leaf) {
        Assert-BranchUnlinkedPath $nested
        $common = ((Invoke-BranchGit $nested @('rev-parse','--path-format=absolute','--git-common-dir')) -join '').Trim()
        $owner = Split-Path -Parent $common
        if ([IO.Path]::GetFullPath($owner).TrimEnd('\','/') -ine (Join-Path $Repository 'APIShared')) { throw 'Nested SDK has an unexpected owner.' }
        $nestedHead = ((Invoke-BranchGit $nested @('rev-parse','HEAD')) -join '').Trim()
        if ($nestedHead -notin @(Invoke-BranchGit $owner @('rev-list','--all'))) { throw 'Nested SDK has an unreferenced commit.' }
        if (@(Invoke-BranchGit $nested @('diff','--cached','--name-only','HEAD')).Count) { throw 'Nested SDK contains staged changes; explicit review required.' }
        # A dirty SDK inspection copy is removable only while the real SDK still has identical bytes.
        $nestedDifferences = @(Get-BranchSourceDifferences $nested)
        foreach ($difference in $nestedDifferences) {
            $survivor = Join-Path $owner $difference.Path
            Assert-BranchUnlinkedPath $survivor
            if ($difference.Deleted) {
                if ([IO.File]::Exists($survivor)) { throw 'Nested SDK contains an unmatched deletion.' }
            } elseif (-not [IO.File]::Exists($survivor) -or (Get-BranchHash $survivor) -cne $difference.Sha256) {
                throw "Nested SDK has unique changes; retaining source: $($difference.Path)"
            }
        }
    }
    if (Test-Path -LiteralPath (Join-Path $nested '.git') -PathType Leaf) {
        $nestedIgnored = @(Invoke-BranchGit $nested @('-c','core.quotepath=false','ls-files','--others','--ignored','--exclude-standard'))
        foreach ($relative in $nestedIgnored) {
            $relative = $relative.Replace('\','/')
            $file = Join-Path $nested $relative
            $survivor = Join-Path $owner $relative
            Assert-BranchUnlinkedPath $file; Assert-BranchUnlinkedPath $survivor
            if ([IO.Directory]::Exists($file)) { throw 'Nested SDK contains another repository or unknown directory.' }
            if ([IO.File]::Exists($survivor) -and (Get-BranchHash $survivor) -ceq (Get-BranchHash $file)) {
                $nestedSurvivors.Add([pscustomobject]@{Path=$relative;Sha256=(Get-BranchHash $file)})
                continue
            }
            if (Test-BranchGeneratedCacheFile $relative) { continue }
            if ($relative -match '\.(log|trx|json)$' -or (Test-BranchOwnedFixtureFile $nested $relative)) {
                $nestedEvidence.Add('APIShared/' + $relative)
            } else { throw "Nested SDK has unique ignored contents: $relative" }
        }
    }
    foreach ($relative in @(Invoke-BranchGit $root @('ls-files','--others','--exclude-standard'))) {
        $candidate = Join-Path $root $relative
        if ($relative.TrimEnd('/','\') -ieq 'APIShared' -and (Test-Path -LiteralPath (Join-Path $nested '.git') -PathType Leaf)) { continue }
        if ([IO.Directory]::Exists($candidate)) { throw "Unclassified nested repository/directory: $relative" }
    }
    $differences = @(Get-BranchSourceDifferences $root | Where-Object { $_.Path.TrimEnd('/','\') -ine 'APIShared' })
    $records = [Collections.Generic.List[object]]::new()
    $snapshot = $root + '.retained'
    $objects = Join-Path $Repository '.work/b/.retained-objects'
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($difference in $differences) { [void]$paths.Add([string]$difference.Path) }
    foreach ($relative in $nestedEvidence) { [void]$paths.Add($relative) }
    # The final package and provenance are results of this run, not the disposable compiler cache.
    $project = Split-Path -Parent ($Run.State.Driver.Replace('/', '\'))
    foreach ($relative in @('.release-output/local', "$project/BepInEx/plugins")) {
        $directory = Join-Path $root $relative
        Assert-BranchUnlinkedPath $directory
        if (Test-Path -LiteralPath $directory -PathType Container) {
            foreach ($file in @(Get-ChildItem -LiteralPath $directory -Recurse -File)) {
                Assert-BranchUnlinkedPath $file.FullName
                [void]$paths.Add($file.FullName.Substring($root.Length + 1).Replace('\','/'))
            }
        }
    }
    foreach ($relative in $initialIgnored) {
        $relative = $relative.Replace('\','/')
        if ($relative -match '^\.work/t/[A-Za-z0-9_-]+/\.allocate\.lock$') { continue }
        if (Test-BranchOwnedFixtureFile $root $relative) { [void]$paths.Add($relative); continue }
        if (Test-BranchGeneratedCacheFile $relative) { continue }
        if ($relative -match '^APIShared/' -or $relative -match '^\.release-output/checkpoints/') { continue }
        if ($relative.StartsWith('.release-output/local/') -or $relative.StartsWith($project.Replace('\','/') + '/BepInEx/plugins/')) { continue }
        if ($relative -match '\.(log|trx)$') { [void]$paths.Add($relative); continue }
        throw "Unclassified ignored build content; retaining full source: $relative"
    }
    foreach ($relative in $paths) {
        # Explicit no-install builds need no exact compiler binary history. Installed
        # or unknown runs still preserve sole delivered bytes (including MVID/PDB).
        $normalized = $relative.Replace('\','/')
        if ($Run.State.NoInstall -eq $true -and $normalized -match '\.(dll|pdb)$' -and
            ($normalized.StartsWith($project.Replace('\','/') + '/BepInEx/plugins/', [StringComparison]::OrdinalIgnoreCase) -or
             $normalized -match '(^|/)(bin|obj)/')) { continue }
        $file = [IO.Path]::GetFullPath((Join-Path $root $relative))
        if (-not $file.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Retained source escapes build root.' }
        Assert-BranchUnlinkedPath $file
        if (-not [IO.File]::Exists($file)) { $records.Add([ordered]@{Path=$relative;Deleted=$true}); continue }
        $hash = Get-BranchHash $file
        $current = Join-Path $Repository $relative
        Assert-BranchUnlinkedPath $current
        if ([IO.File]::Exists($current) -and (Get-BranchHash $current) -ceq $hash) {
            # A surviving uncommitted file can change later; retain its content unless Git also owns it.
        }
        $gitHash = ((Invoke-BranchGit $root @('hash-object','--no-filters','--',$file)) -join '').Trim()
        if ($reachable.ContainsKey($gitHash)) { $records.Add([ordered]@{Path=$relative;Sha256=$hash;GitBlob=$gitHash}); continue }
        # Resolve a possible stored blob with Git's clean conversion, but verify the
        # simple recorded byte restoration independently of attributes or filters.
        $filteredHash = ((Invoke-BranchGit $root @('hash-object',('--path=' + $relative),'--',$file)) -join '').Trim()
        if ($reachable.ContainsKey($filteredHash) -and (Get-BranchCrlfBlobHash $root $filteredHash) -ceq $hash) {
            $records.Add([ordered]@{Path=$relative;Sha256=$hash;GitBlob=$filteredHash;RestoreKind='lf-to-crlf'})
            continue
        }
        $object = Join-Path $objects ($hash + '.gz')
        Assert-BranchUnlinkedPath $object
        [void][IO.Directory]::CreateDirectory($objects)
        if (-not [IO.File]::Exists($object)) {
            $input = [IO.File]::OpenRead($file)
            $temporary = $object + '.' + $PID + '.tmp'
            $output = [IO.File]::Open($temporary,'CreateNew','Write','None')
            try { $gzip = [IO.Compression.GZipStream]::new($output,[IO.Compression.CompressionMode]::Compress,$true); try { $input.CopyTo($gzip) } finally { $gzip.Dispose() } }
            finally { $output.Dispose(); $input.Dispose() }
            try { [IO.File]::Move($temporary,$object) } catch { if ([IO.File]::Exists($object)) { [IO.File]::Delete($temporary) } else { throw } }
        }
        $input = [IO.File]::OpenRead($object)
        $gzip = [IO.Compression.GZipStream]::new($input,[IO.Compression.CompressionMode]::Decompress)
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $verified = [BitConverter]::ToString($sha.ComputeHash($gzip)).Replace('-','').ToLowerInvariant() }
        finally { $sha.Dispose(); $gzip.Dispose(); $input.Dispose() }
        if ($verified -cne $hash -or (Get-BranchHash $file) -cne $hash) { throw "Retained content changed or failed verification: $relative" }
        $records.Add([ordered]@{Path=$relative;Sha256=$hash;Object=$object})
    }
    # A record preserves deletions and exact raw bytes, without duplicating committed files.
    $manifest = [ordered]@{SchemaVersion=1;Repository=$Repository;BaseCommit=$head;Root=$root;CreatedUtc=[DateTime]::UtcNow.ToString('o');Files=$records.ToArray()}
    Write-BranchText ($snapshot + '.json') (($manifest | ConvertTo-Json -Depth 10) + "`r`n")
    $Run.State.RetentionManifest = $snapshot + '.json'
    $after = @(Get-BranchSourceDifferences $root | Where-Object { $_.Path.TrimEnd('/','\') -ine 'APIShared' })
    if (($differences | ConvertTo-Json -Depth 5 -Compress) -cne ($after | ConvertTo-Json -Depth 5 -Compress)) { throw 'Build source changed while exporting retained contents.' }
    $afterIgnored = @(Invoke-BranchGit $root @('-c','core.quotepath=false','ls-files','--others','--ignored','--exclude-standard'))
    if (($initialIgnored -join "`n") -cne ($afterIgnored -join "`n")) { throw 'Ignored content set changed during preservation.' }
    foreach ($record in $records) {
        if (-not $record.Deleted -and (Get-BranchHash (Join-Path $root $record.Path)) -cne $record.Sha256) { throw 'Preserved content changed before cleanup.' }
    }
    if (@(Invoke-BranchGit $root @('diff','--cached','--name-only','HEAD')).Count) { throw 'Index changed during preservation.' }
    if (((Invoke-BranchGit $root @('rev-parse','HEAD')) -join '').Trim() -cne $head) { throw 'Build HEAD changed during preservation.' }
    if (Test-Path -LiteralPath (Join-Path $nested '.git') -PathType Leaf) {
        foreach ($difference in @($nestedDifferences) + @($nestedSurvivors)) {
            $survivor = Join-Path $owner $difference.Path
            if ($difference.Deleted) {
                if ([IO.File]::Exists($survivor)) { throw 'Surviving SDK source changed during preservation.' }
            } elseif (-not [IO.File]::Exists($survivor) -or (Get-BranchHash $survivor) -cne $difference.Sha256) {
                throw 'Surviving SDK source/evidence changed during preservation.'
            }
        }
        $afterNested = @(Get-BranchSourceDifferences $nested)
        $afterNestedIgnored = @(Invoke-BranchGit $nested @('-c','core.quotepath=false','ls-files','--others','--ignored','--exclude-standard'))
        if (($nestedDifferences | ConvertTo-Json -Depth 5 -Compress) -cne ($afterNested | ConvertTo-Json -Depth 5 -Compress) -or
            ($nestedIgnored -join "`n") -cne ($afterNestedIgnored -join "`n") -or
            @(Invoke-BranchGit $nested @('diff','--cached','--name-only','HEAD')).Count -or
            ((Invoke-BranchGit $nested @('rev-parse','HEAD')) -join '').Trim() -cne $nestedHead) { throw 'Nested SDK changed during preservation.' }
    }

}
function Complete-BranchRun([string]$Repository, $Run) {
    if (Test-BranchRunProtected $Repository $Run.Root) { Set-BranchRunPhase $Run 'retaining' 'Protected by .work/INDEX.json'; return }
    if ($Run.State.KeepSource) { Set-BranchRunPhase $Run 'retaining' 'Explicit /keepsource'; return }
    if (-not (Test-Path -LiteralPath $Run.Root)) { Set-BranchRunPhase $Run 'cleaned'; return }
    try {
        Set-BranchRunPhase $Run 'cleaning'
        if (Test-Path -LiteralPath (Join-Path $Run.Root '.git') -PathType Leaf) {
            Save-BranchRunContents $Repository $Run
            Set-BranchRunPhase $Run 'cleaning' 'Required contents verified outside worktree'
            Remove-BranchSource $Repository $Run.Root
        } elseif (@(Get-ChildItem -LiteralPath $Run.Root -Force).Count -eq 0) {
            [IO.Directory]::Delete((Assert-BranchRunRoot $Repository $Run.Root),$false)
        } else { throw 'Preparation left an unregistered nonempty directory; manual review required.' }
        Set-BranchRunPhase $Run 'cleaned'
        Write-BranchMessage "Build copy removed: $($Run.Root) (use /keepsource to keep it)."
    } catch {
        $failure = $_.Exception.Message
        try { Set-BranchRunPhase $Run 'cleanup-failed' $failure } catch { Write-BranchMessage ('WARNING: Could not update cleanup record: ' + $_.Exception.Message) }
        Write-BranchMessage "WARNING: Build copy retained: $($Run.Root): $failure"
    }
}
# External process inspection is a safety boundary; unavailable inspection retains the run.
function Get-BranchRunProcesses { @(Get-CimInstance Win32_Process -ErrorAction Stop) }
function Repair-AbandonedBranchRuns([string]$Repository, [string]$Branch) {
    $directory = Join-Path $Repository ('.work/b/' + $Branch)
    if (-not (Test-Path -LiteralPath $directory)) { return }
    Assert-BranchUnlinkedPath $directory
    foreach ($file in @(Get-ChildItem -LiteralPath $directory -Filter '*.lifecycle.json' -File)) {
        $lease = $null
        try {
            $state = [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
            if ($state.SchemaVersion -ne 1 -or $state.Protocol -cne 'ModBuildRun' -or $state.Repository -ine $Repository -or
                $state.Phase -notin @('preparing','building','cleaning','cleanup-failed') -or $state.KeepSource) { continue }
            $root = Assert-BranchRunRoot $Repository $state.Root
            if ($file.FullName -ine ($root + '.lifecycle.json')) { throw 'Mismatched lifecycle filename.' }
            if (-not [IO.File]::Exists($root + '.lease')) { throw 'Owned run lease file missing; manual review required.' }
            try { $lease = [IO.File]::Open($root + '.lease','Open','ReadWrite','None') }
            catch [IO.IOException] { continue }
            $owner = Get-Process -Id $state.OwnerPid -ErrorAction SilentlyContinue
            if ($owner -and $owner.StartTime.ToUniversalTime().ToString('o') -ceq $state.OwnerStartedUtc) { continue }
            # Children may outlive the controller. Unavailable process inspection fails closed.
            if ($state.Phase -eq 'building') {
                $buildPath = $root + '.build.json'
                if (-not [IO.File]::Exists($buildPath)) { continue }
                $completed = [IO.File]::ReadAllText($buildPath) | ConvertFrom-Json
                if (-not $completed.PSObject.Properties['ExitCode'] -or $completed.SourceRoot -ine $root) { continue }
            }
            $processes = @(Get-BranchRunProcesses)
            if (@($processes | Where-Object { $_.ParentProcessId -eq $state.OwnerPid }).Count) { continue }
            if (@($processes | Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($root,[StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { continue }
            $run = [pscustomobject]@{Root=$root;Lease=$lease;State=$state}
            Complete-BranchRun $Repository $run
        } catch { Write-BranchMessage "Retained run requires review: $($file.Name): $($_.Exception.Message)" }
        finally { if ($lease) { $lease.Dispose() } }
    }
    $legacy = @(Get-ChildItem -LiteralPath $directory -Directory | Where-Object { -not [IO.File]::Exists($_.FullName + '.lifecycle.json') }).Count
    if ($legacy) { Write-BranchMessage "$legacy legacy build copies require explicit inventory; no automatic deletion." }
}
