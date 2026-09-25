[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

$modsDocument = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
$mods = @(foreach ($entry in $modsDocument) { $entry })
$mod = @($mods | Where-Object { [string]$_.Name -ceq 'BugfixesAndQoL' })
if ($mod.Count -ne 1) {
    throw "Expected exactly one BugfixesAndQoL Script Extender inventory entry; found $($mod.Count)."
}

Assert-SERuntimeModPreflight $mod[0] $workspace

$waterboyRuntimePath = Join-Path $PSScriptRoot 'src\WaterboyTargetReservationRuntime.cs'
$waterboyRuntime = [System.IO.File]::ReadAllText($waterboyRuntimePath)
$waterboyViewModel = [System.IO.File]::ReadAllText(
    (Join-Path $PSScriptRoot 'src\BugfixesAndQoLViewModel.cs'))
if ($waterboyRuntime -match 'AuditedScriptExtenderAssemblyVersion|AuditedRedBirdAssemblyVersion|Unaudited dependencies') {
    throw 'Exact Script Extender or RedBird version gating was reintroduced for Waterboy targeting.'
}
if ($waterboyRuntime -match '\btargetSearchHook\s*\.\s*(Dispose|Undo|Disable)\s*\(' -or
    $waterboyRuntime -match '\b(Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Waterboy runtime contains a published-hook teardown or MonoBehaviour callback.'
}
if ($waterboyRuntime -match 'MaximumDetailedLogs|LogDetail\(|map modes|reached OnGameTick|dependencies detected' -or
    $waterboyRuntime -match 'WaterboySettings|EnableNearestWaterboyTargetingData|ResolveEffectiveMode') {
    throw 'Waterboy detailed diagnostics or obsolete per-player setting artifacts remain.'
}
if (-not $waterboyViewModel.Contains('private bool enableNearestWaterboyTargeting = true;') -or
    -not $waterboyViewModel.Contains('[SyncHostOnly]' + [Environment]::NewLine +
        '        public bool EnableNearestWaterboyTargeting') -or
    $waterboyViewModel.Contains('EnableNearestWaterboyTargetingData')) {
    throw 'Waterboy host setting contract is incomplete.'
}

$patchRoot = Join-Path $PSScriptRoot 'Patches'
$xamlViolations = @(foreach ($file in Get-ChildItem -LiteralPath $patchRoot -Filter '*.xaml' -Recurse) {
    [xml]$document = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($contentNode in @($document.SelectNodes('/Patch/Operation/Content'))) {
        $directElementCount = @(
            $contentNode.ChildNodes |
                Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element }
        ).Count
        if ($directElementCount -ne 1) {
            [pscustomobject]@{
                Path = $file.FullName
                DirectElementCount = $directElementCount
            }
        }
    }
})
if ($xamlViolations.Count -ne 0) {
    $details = $xamlViolations |
        ForEach-Object { "$($_.Path) (direct elements: $($_.DirectElementCount))" }
    throw "Script Extender XAML patch <Content> contract failed: $($details -join '; ')"
}

Write-Output 'BugfixesAndQoL runtime JSON/lifecycle and XAML patch preflight succeeded.'
