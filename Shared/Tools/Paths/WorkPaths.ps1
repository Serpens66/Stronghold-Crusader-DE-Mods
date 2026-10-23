# Applies only to this development PowerShell process, never the Unity host.
[AppContext]::SetSwitch('Switch.System.IO.UseLegacyPathHandling', $false)
[AppContext]::SetSwitch('Switch.System.IO.BlockLongPaths', $false)
# Development tooling only. APIShared maintains its own standalone copy.
function New-ShortWorkDirectory {
    param([Parameter(Mandatory)][string]$Root, [ValidateSet('b','t','a')][string]$Kind = 't',
        [Parameter(Mandatory)][string]$Name, [string]$Identity = '')
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
        return $path
    } finally { $lock.Dispose() }
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
