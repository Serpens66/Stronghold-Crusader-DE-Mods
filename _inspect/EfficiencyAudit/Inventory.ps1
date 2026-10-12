param([string]$Workspace = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$ErrorActionPreference = 'Stop'
$workspacePath = [IO.Path]::GetFullPath($Workspace)
$roots = @('APIShared/src','BugfixesAndQoL/src','BuildingCosts/src','BuildingLimit/src',
    'ExtraFeatures/src','RandomEvents/src','CastlePlanner/src','StartConditions/src',
    'UnitCosts/src','UnitLimit/src','ExtendedData/src','SerpsModsHost/src',
    'Shared/Runtime','Shared/Adapters/APIShared')
$patterns = [ordered]@{
    Publisher = 'OnTick\s*\+=|onBeforeRender\s*\+=|\.Observable\s*$|\.Subscribe\s*\('
    FullScan = 'GetUnitsAsSpan\s*\(|GetBuildingsAsSpan\s*\(|GetUnitArray\s*\(|GetBuildingsArray\s*\('
    Materialization = '\.ToArray\s*\(|\.ToList\s*\(|\.OrderBy(?:Descending)?\s*\('
    Reflection = '\.GetProperties\s*\(|\.GetFields\s*\(|\.GetMethods\s*\(|\.GetCustomAttributes\s*\('
    FileIO = '\b(?:File|Directory)\.(?:Read|Write|GetFiles|GetDirectories|Enumerate|Exists)|SHA256\.Create'
    Lifecycle = '\b(?:OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|InvokeRepeating)\s*\('
}
$files = @(foreach ($root in $roots) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspacePath $root) -Recurse -File -Filter '*.cs') {
        $text = [IO.File]::ReadAllText($file.FullName)
        $lines = $text -split '\r?\n'
        $hits = @(for ($index = 0; $index -lt $lines.Length; $index++) {
            foreach ($pattern in $patterns.GetEnumerator()) {
                if ($lines[$index] -match $pattern.Value) {
                    [pscustomobject]@{ Kind = $pattern.Key; Line = $index + 1; Text = $lines[$index].Trim() }
                }
            }
        })
        [pscustomobject]@{ Root = $root; Path = $file.FullName.Substring($workspacePath.Length + 1);
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;
            Lines = $lines.Length; Hits = $hits }
    }
})
$inventory = [ordered]@{
    CreatedUtc = [DateTime]::UtcNow.ToString('o'); Scope = 'Current source; pack 1.0.23 membership';
    Note = 'Search inventory, not proof of execution frequency or completed manual review.';
    Roots = $roots; Files = $files
}
$json = ($inventory | ConvertTo-Json -Depth 7) -replace '\r?\n', "`r`n"
$target = Join-Path $PSScriptRoot 'source-inventory.json'
[IO.File]::WriteAllText($target, $json + "`r`n", [Text.UTF8Encoding]::new($false))
if ([IO.File]::ReadAllText($target) -cne ($json + "`r`n")) { throw 'Inventory readback failed.' }
$files | Group-Object Root | ForEach-Object {
    [pscustomobject]@{ Root = $_.Name; Files = $_.Count;
        Lines = ($_.Group | Measure-Object Lines -Sum).Sum;
        Publishers = @($_.Group.Hits | Where-Object Kind -eq Publisher).Count;
        FullScans = @($_.Group.Hits | Where-Object Kind -eq FullScan).Count }
} | Format-Table -AutoSize
