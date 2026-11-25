# Runs on the multiplayer test client only while the user has started it for pending tests
# (Start-ClientStartWatcher.bat); nothing registers it for logon. Its only action is starting
# SHCDE through Steam when the host drops a start request into this folder of the client's
# shared game directory. A request carries a run label only.
$ErrorActionPreference = 'Stop'
$folder = $PSScriptRoot
$gameDirectory = Split-Path -Parent $folder
$requestPath = Join-Path $folder 'start.request'
$resultPath = Join-Path $folder 'start.result'
$alivePath = Join-Path $folder 'watcher.alive'
$logPath = Join-Path $folder 'watcher.log'
$gameProcess = 'Stronghold Crusader Definitive Edition'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Write-WatcherLog([string]$Message) {
    $line = '[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}' -f (Get-Date), $Message
    Write-Host $line
    try {
        if ((Test-Path -LiteralPath $logPath) -and (Get-Item -LiteralPath $logPath).Length -gt 262144) {
            Remove-Item -LiteralPath $logPath -Force
        }
        [IO.File]::AppendAllText($logPath, $line + [char]13 + [char]10, $utf8)
    } catch { }
}

function Write-WatcherFile([string]$Path, [string]$Text) {
    $temporary = $Path + '.tmp'
    [IO.File]::WriteAllText($temporary, $Text, $utf8)
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Force }
    Move-Item -LiteralPath $temporary -Destination $Path
}

function Complete-StartRequest([string]$Label, [string]$Outcome) {
    Write-WatcherFile $resultPath ($Label + ' ' + $Outcome)
    Write-WatcherLog ('request ' + $Label + ' ' + $Outcome)
}

function Invoke-StartRequest([string]$Text) {
    if ($Text -notmatch '^[A-Za-z0-9-]{1,64}$') { Complete-StartRequest 'invalid' 'REFUSED invalid-label'; return }
    if (-not (Test-Path -LiteralPath (Join-Path $gameDirectory 'winhttp.dll'))) {
        Complete-StartRequest $Text 'REFUSED mods-disabled'
        return
    }
    if (Get-Process -Name $gameProcess -ErrorAction SilentlyContinue) {
        Complete-StartRequest $Text 'REFUSED already-running'
        return
    }
    # Same launch path as the client's modded launcher: Steam starts the final game process.
    Start-Process 'steam://rungameid/3024040'
    Complete-StartRequest $Text 'STARTED'
}

$created = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\SHCDE.ClientStartWatcher', [ref]$created)
if (-not $created) { Write-Host 'The client start watcher is already running.'; exit 0 }
try {
    Write-WatcherLog ('watcher started; game directory ' + $gameDirectory + '; close this window to stop it')
    $lastBeat = [DateTime]::MinValue
    while ($true) {
        try {
            if (([DateTime]::UtcNow - $lastBeat).TotalSeconds -ge 5) {
                $lastBeat = [DateTime]::UtcNow
                Write-WatcherFile $alivePath $lastBeat.ToString('o')
            }
            if (Test-Path -LiteralPath $requestPath) {
                $text = ''
                try { $text = [IO.File]::ReadAllText($requestPath, $utf8).Trim() }
                finally { Remove-Item -LiteralPath $requestPath -Force -ErrorAction SilentlyContinue }
                Invoke-StartRequest $text
            }
        } catch {
            Write-WatcherLog ('ERROR ' + $_.Exception.Message)
        }
        Start-Sleep -Milliseconds 1000
    }
} finally {
    Remove-Item -LiteralPath $alivePath -Force -ErrorAction SilentlyContinue
    $mutex.ReleaseMutex()
}
