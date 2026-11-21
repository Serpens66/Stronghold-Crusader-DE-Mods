# Shared helpers for the WorkshopLoadProbe tools. Development only.
$ProbePayloadGuid = 'WorkshopLoadProbePayload_Serp'

function Get-ProbeWorkspace { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')) }
function Get-ProbeGame { 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
function Get-ProbeLibrary { [IO.Path]::GetFullPath((Join-Path (Get-ProbeGame) '..\..')) }
function Get-ProbeEvidence([string]$Kind) {
    $path = Join-Path (Get-ProbeWorkspace) ('_inspect\WorkshopLoadProbe\' + $Kind)
    [void][IO.Directory]::CreateDirectory($path)
    return $path
}
function Assert-ProbeGameClosed {
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game first.' }
}
function Get-ProbeSha256([string]$Path) {
    $stream = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}
function Write-ProbeText([string]$Path, [string[]]$Lines) {
    [IO.File]::WriteAllText($Path, (($Lines -join "`r`n") + "`r`n"), [Text.UTF8Encoding]::new($false))
}
