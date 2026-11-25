# Host side of the on-demand client start watcher (ClientStart\Watch-ClientStartRequests.ps1).
# The user starts the watcher on the client before paired tests; these helpers only exchange
# files in <client game share>\_Automation and never change anything else on the client.

function Get-ClientStartFolder([string]$ClientRoot) { Join-Path $ClientRoot '_Automation' }

function Install-ClientStartWatcher([string]$ClientRoot) {
    $source = Join-Path $PSScriptRoot 'ClientStart'
    $target = Get-ClientStartFolder $ClientRoot
    [void][IO.Directory]::CreateDirectory($target)
    foreach ($name in 'Watch-ClientStartRequests.ps1', 'Start-ClientStartWatcher.bat') {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $target $name) -Force
    }
    $target
}

function Test-ClientStartWatcher([string]$ClientRoot, [int]$MaximumAgeSeconds = 20) {
    $alive = Join-Path (Get-ClientStartFolder $ClientRoot) 'watcher.alive'
    try {
        $stamp = [DateTime]::Parse([IO.File]::ReadAllText($alive).Trim(), [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind)
        return ([DateTime]::UtcNow - $stamp.ToUniversalTime()).TotalSeconds -le $MaximumAgeSeconds
    } catch { return $false }
}

# Returns Started=$true only after the watcher confirmed the Steam launch for exactly this label.
function Request-ClientGameStart([string]$ClientRoot, [string]$Label, [int]$TimeoutSeconds = 15) {
    if ($Label -notmatch '^[A-Za-z0-9-]{1,64}$') { throw 'Client start labels use 1-64 letters, digits or hyphens.' }
    if (-not (Test-ClientStartWatcher $ClientRoot)) {
        return [pscustomobject]@{ Started = $false; Detail = 'watcher not running on the client' }
    }
    $folder = Get-ClientStartFolder $ClientRoot
    $request = Join-Path $folder 'start.request'
    $result = Join-Path $folder 'start.result'
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    Remove-Item -LiteralPath $result -Force -ErrorAction SilentlyContinue
    $temporary = $request + '.tmp'
    [IO.File]::WriteAllText($temporary, $Label, $utf8)
    Move-Item -LiteralPath $temporary -Destination $request -Force
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            $line = [IO.File]::ReadAllText($result, $utf8).Trim()
            if ($line.StartsWith($Label + ' ')) {
                $outcome = $line.Substring($Label.Length + 1)
                return [pscustomobject]@{ Started = $outcome -eq 'STARTED'; Detail = $outcome }
            }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    Remove-Item -LiteralPath $request -Force -ErrorAction SilentlyContinue
    [pscustomobject]@{ Started = $false; Detail = 'no watcher answer within ' + $TimeoutSeconds + ' s' }
}
