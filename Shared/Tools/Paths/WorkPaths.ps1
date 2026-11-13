# Applies only to this development PowerShell process, never the Unity host.
[AppContext]::SetSwitch('Switch.System.IO.UseLegacyPathHandling', $false)
[AppContext]::SetSwitch('Switch.System.IO.BlockLongPaths', $false)
# Development tooling only. APIShared maintains its own standalone copy.
function New-ShortWorkDirectory {
    param([Parameter(Mandatory)][string]$Root, [ValidateSet('b','t','a')][string]$Kind = 't',
        [Parameter(Mandatory)][string]$Name, [string]$Identity = '', [switch]$Transient)
    if ($Transient -and $Kind -ne 't') { throw 'Only disposable test fixtures can be transient.' }
    $label = ($Name -replace '[^A-Za-z0-9_-]', '-').Trim('-')
    if (-not $label) { throw 'A work directory needs a meaningful name.' }
    if ($label.Length -gt 24) { $label = $label.Substring(0,24) }
    $base = [IO.Path]::GetFullPath((Join-Path $Root ('.work/' + $Kind + '/' + $label)))
    [void][IO.Directory]::CreateDirectory($base)
    $lock = $null
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not $lock) {
        try { $lock = [IO.File]::Open((Join-Path $base '.allocate.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] { if ($watch.Elapsed.TotalSeconds -ge 30) { throw }; Start-Sleep -Milliseconds 20 }
    }
    try {
        $number = 1
        do { $path = Join-Path $base ($number.ToString('D3')); $number++ } while ([IO.Directory]::Exists($path) -or [IO.File]::Exists($path) -or [IO.File]::Exists($path + '.run.json'))
        [void][IO.Directory]::CreateDirectory($path)
        $record = [ordered]@{ SchemaVersion=1; Kind=$Kind; Name=$Name; Identity=$Identity; Root=$path; CreatedUtc=[DateTime]::UtcNow.ToString('o') }
        [IO.File]::WriteAllText(($path + '.run.json'), (($record | ConvertTo-Json) + "`r`n"), [Text.UTF8Encoding]::new($false))
        if ($Transient) { Register-TransientWorkDirectory $path }
        return $path
    } finally { $lock.Dispose() }
}

# Call Complete-TransientWorkDirectories from a standalone test's finally block.
# Owner scoping keeps a nested test from deleting its caller's live fixture.
function Register-TransientWorkDirectory([string]$Path) {
    if (-not (Get-Variable -Scope Global -Name ShcdeTransientFixtures -ErrorAction SilentlyContinue)) {
        $global:ShcdeTransientFixtures = [Collections.Generic.List[object]]::new()
    }
    $helper=$PSCommandPath
    $owner=@(Get-PSCallStack | Where-Object { $_.ScriptName -and $_.ScriptName -ine $helper } | Select-Object -First 1)
    if(-not $owner.Count){throw 'A transient fixture needs a calling test script.'}
    $global:ShcdeTransientFixtures.Add([pscustomobject]@{Path=$Path;Owner=$owner[0].ScriptName})
}
function Complete-TransientWorkDirectories([Parameter(Mandatory)][string]$Owner) {
    if (-not (Get-Variable -Scope Global -Name ShcdeTransientFixtures -ErrorAction SilentlyContinue)) { return }
    for($i=$global:ShcdeTransientFixtures.Count-1;$i -ge 0;$i--) {
        $fixture=$global:ShcdeTransientFixtures[$i]
        if($fixture.Owner -ine $Owner){continue}
        try { Remove-ShortWorkDirectory $fixture.Path; $global:ShcdeTransientFixtures.RemoveAt($i) }
        catch { Write-Warning ('Test fixture retained: '+$fixture.Path+': '+$_.Exception.Message) }
    }
}
function Remove-ShortWorkDirectory {
    param([Parameter(Mandatory)][string]$Path)
    # Validate the exact allocation before recursively touching fixture contents.
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    if ($full -notmatch '[\\/]\.work[\\/]t[\\/][A-Za-z0-9_-]+[\\/][0-9]+$') { throw 'Only allocated test fixtures can be removed.' }
    if (-not [IO.Directory]::Exists($full) -and -not [IO.File]::Exists($full + '.run.json')) { return }
    if ([IO.File]::GetAttributes($full + '.run.json') -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked fixture ownership record.' }
    $record = [IO.File]::ReadAllText($full + '.run.json') | ConvertFrom-Json
    if ($record.SchemaVersion -ne 1 -or $record.Root -ine $full -or
        ($record.PSObject.Properties['Kind'] -and $record.Kind -cne 't')) { throw 'Test fixture ownership mismatch.' }
    for ($part=$full; $part; $part=[IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked fixture root.' }
    }
    function Find-FixtureWorktrees([IO.DirectoryInfo]$Directory) {
        foreach ($entry in $Directory.GetFileSystemInfos()) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($entry.Name -ceq '.git') {
                if ($entry -is [IO.FileInfo]) { $entry.DirectoryName }
            } elseif ($entry -is [IO.DirectoryInfo]) { Find-FixtureWorktrees $entry }
        }
    }
    function Remove-FixtureTree([IO.DirectoryInfo]$Directory) {
        foreach ($entry in $Directory.GetFileSystemInfos()) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                # Remove the link itself, never its target (including directory junctions).
                if ($entry -is [IO.DirectoryInfo]) { [IO.Directory]::Delete($entry.FullName, $false) }
                else { [IO.File]::Delete($entry.FullName) }
            } elseif ($entry -is [IO.DirectoryInfo]) { Remove-FixtureTree $entry }
            else { $entry.IsReadOnly=$false; $entry.Delete() }
        }
        $Directory.Delete()
    }
    if ([IO.Directory]::Exists($full)) {
        # Fixture scripts often define a Git helper; cleanup must call the actual executable.
        $gitExecutable=$null
        foreach ($tree in @(Find-FixtureWorktrees ([IO.DirectoryInfo]::new($full)) | Sort-Object Length -Descending)) {
            if(-not $gitExecutable){$gitExecutable=(Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source}
            $common = @(& $gitExecutable -C $tree rev-parse --path-format=absolute --git-common-dir 2>&1)
            if ($LASTEXITCODE -ne 0) { throw "Cannot classify fixture Git pointer: $tree" }
            $common = [IO.Path]::GetFullPath(($common -join '').Trim()).TrimEnd('\','/')
            # Never unregister work belonging to an unrelated development repository.
            if (-not $common.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)) { throw "Fixture uses an external Git repository: $tree" }
            for ($part=$common; $part.Length -gt $full.Length; $part=[IO.Path]::GetDirectoryName($part)) {
                if ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked fixture Git repository.' }
            }
            $gitDir=@(& $gitExecutable -C $tree rev-parse --absolute-git-dir 2>&1)
            if($LASTEXITCODE -ne 0){throw "Cannot identify fixture Git directory: $tree"}
            # Absorbed submodules also have .git files, but no linked-worktree registration.
            if([IO.Path]::GetFullPath(($gitDir -join '').Trim()).TrimEnd('\','/') -ieq $common){continue}
            & $gitExecutable --git-dir=$common worktree remove --force $tree
            if ($LASTEXITCODE -ne 0) { throw "Cannot unregister fixture worktree: $tree" }
        }
        Remove-FixtureTree ([IO.DirectoryInfo]::new($full))
    }
    [IO.File]::Delete($full + '.run.json')
}
function Assert-ToolPathBudget {
    param([Parameter(Mandatory)][string]$Path, [int]$Maximum = 239)
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.Length -gt $Maximum) { throw "Tool path exceeds its $Maximum character budget ($($full.Length)): $full" }
}
function Assert-ShortWorkDirectory {
    param([Parameter(Mandatory)][string]$Path)
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    if ($full -notmatch '[\\/]\.work[\\/][bta][\\/][A-Za-z0-9_-]+[\\/][0-9]+$') { throw "Not an allocated work directory: $full" }
    $record = [IO.File]::ReadAllText($full + '.run.json') | ConvertFrom-Json
    if ($record.SchemaVersion -ne 1 -or $record.Root -ine $full) { throw 'Work allocation record mismatch.' }
    for ($part=$full; $part; $part=[IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked work path.' }
    }
}
