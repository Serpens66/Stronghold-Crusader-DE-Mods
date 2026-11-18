# T2/T3: records what Steam keeps for one Workshop item at this moment. Read-only.
# Output: _inspect\WorkshopLoadProbe\snapshots\<time>-<label>.json
param([Parameter(Mandatory)][string]$ItemId, [Parameter(Mandatory)][string]$Label)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if ($ItemId -notmatch '^[1-9][0-9]*$') { throw 'ItemId must be a Workshop ID.' }
$library = Get-ProbeLibrary
$workshop = Join-Path $library 'workshop'

function Get-Inventory([string]$Directory) {
    if (-not (Test-Path -LiteralPath $Directory)) { return $null }
    return @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Force | Sort-Object FullName | ForEach-Object {
        $hash = try { Get-ProbeSha256 $_.FullName } catch { 'unreadable: ' + $_.Exception.Message }
        [ordered]@{ Path = $_.FullName.Substring($Directory.Length + 1); Size = $_.Length; LastWriteUtc = $_.LastWriteTimeUtc.ToString('o'); Sha256 = $hash }
    })
}

$acfPath = Join-Path $workshop "appworkshop_$ProbeAppId.acf"
$acf = $null
if (Test-Path -LiteralPath $acfPath) {
    $parsed = Read-ProbeAcf $acfPath
    $app = $parsed[0]
    $scalars = [ordered]@{}; foreach ($key in $app.Keys) { if ($app[$key] -is [string]) { $scalars[$key] = $app[$key] } }
    $blocks = [ordered]@{}; foreach ($key in $app.Keys) { if ($app[$key] -isnot [string] -and $app[$key].Contains($ItemId)) { $blocks[$key] = $app[$key][$ItemId] } }
    $acf = [ordered]@{ LastWriteUtc = (Get-Item -LiteralPath $acfPath).LastWriteTimeUtc.ToString('o'); App = $scalars; Item = $blocks }
}
$snapshot = [ordered]@{
    TimeLocal = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'); Label = $Label; ItemId = $ItemId
    SteamRunning = [bool](Get-Process -Name 'steam' -ErrorAction SilentlyContinue)
    GameRunning = [bool](Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue)
    Acf = $acf
    Content = Get-Inventory (Join-Path $workshop "content\$ProbeAppId\$ItemId")
    Downloads = Get-Inventory (Join-Path $workshop "downloads\$ProbeAppId\$ItemId")
    Temp = Get-Inventory (Join-Path $workshop "temp\$ProbeAppId\$ItemId")
}
$name = (Get-Date).ToString('yyyyMMdd-HHmmss') + '-' + ($Label -replace '[^A-Za-z0-9_-]', '-') + '.json'
$path = Join-Path (Get-ProbeEvidence 'snapshots') $name
Write-ProbeText $path @($snapshot | ConvertTo-Json -Depth 8)
Write-Output "Snapshot: $path"
Write-Output ("ACF item fields: " + $(if ($acf) { ($acf.Item.Keys | ForEach-Object { $_ + '{' + (($acf.Item[$_].Keys | ForEach-Object { $k = $_; "$k" }) -join ',') + '}' }) -join ' ' } else { 'no acf' }))
Write-Output ("Content files: " + $(if ($null -eq $snapshot.Content) { 'folder missing' } else { $snapshot.Content.Count }))
