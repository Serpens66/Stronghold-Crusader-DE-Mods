# Shared helpers for the WorkshopLoadProbe tools. Development only.
$ProbePayloadGuid = 'WorkshopLoadProbePayload_Serp'
$ProbeAppId = '3024040'

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

# Minimal Valve KeyValues reader (quoted strings and braces), as in the patcher.
function Read-ProbeAcf([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $tokens = [regex]::Matches($text, '"((?:\\.|[^"\\])*)"|[{}]') | ForEach-Object { if ($_.Value -eq '{' -or $_.Value -eq '}') { $_.Value } else { [regex]::Unescape($_.Groups[1].Value) } }
    $stack = [Collections.Generic.Stack[object]]::new()
    $current = [ordered]@{}
    $key = $null
    foreach ($token in $tokens) {
        if ($token -eq '{') { $child = [ordered]@{}; $current[$key] = $child; $stack.Push($current); $current = $child; $key = $null }
        elseif ($token -eq '}') { $current = $stack.Pop() }
        elseif ($null -eq $key) { $key = $token }
        else { $current[$key] = $token; $key = $null }
    }
    return $current
}
