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

$runtimeSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File)
$pluginSources = @($runtimeSources | Where-Object { $_.Name -like '*Plugin.cs' })
$projectText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BugfixesAndQoL.csproj'))
$sourceText = (@($runtimeSources | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n")
$forbiddenRuntimeJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
if ($sourceText -match $forbiddenRuntimeJson -or $projectText -match $forbiddenRuntimeJson) {
    throw 'Forbidden runtime JSON serializer or assembly reference.'
}
if ($projectText -match 'Assembly-CSharp-publicized') {
    throw 'The runtime project must reference the installed real Assembly-CSharp.dll.'
}
foreach ($file in $pluginSources) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '\b(?:public|protected|internal|private)\s+(?:static\s+)?(?:void|IEnumerator)\s+(?:Update|LateUpdate|FixedUpdate)\s*\(' -or
        $text -match '\bStartCoroutine\s*\(' -or
        $text -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit)\s*\(') {
        throw "Long-lived or teardown MonoBehaviour callback in $($file.Name)."
    }
}

$woodScopeSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\AiWoodBuildCallScope.cs'))
$woodWrapperSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\AIPreplacedBuildingFixRuntime.cs'))
if ($woodWrapperSource -notmatch 'AiWoodBuildCallScope\.Enter\(playerId\)' -or
    $woodWrapperSource -notmatch 'AiWoodBuildCallScope\.Leave\(woodScope\)' -or
    $woodScopeSource -notmatch '\[ThreadStatic\]' -or
    $woodScopeSource -match 'AiBuildDiagnostic|CaptureTiles|CodePatch') {
    throw 'Optional wood-call scope is missing or contains diagnostic/native behavior.'
}
Add-Type -TypeDefinition $woodScopeSource -Language CSharp
[int]$scopePlayer = 0
[long]$scopeCall = 0
$inactive = [BugfixesAndQoL.AiWoodBuildCallScope]::Enter(6)
if ([BugfixesAndQoL.AiWoodBuildCallScope]::TryGetCurrent([ref]$scopePlayer, [ref]$scopeCall)) {
    throw 'Wood-call scope activated without a registered consumer.'
}
[BugfixesAndQoL.AiWoodBuildCallScope]::Leave($inactive)
[BugfixesAndQoL.AiWoodBuildCallScope]::RegisterConsumer()
$outer = [BugfixesAndQoL.AiWoodBuildCallScope]::Enter(6)
if (-not [BugfixesAndQoL.AiWoodBuildCallScope]::TryGetCurrent([ref]$scopePlayer, [ref]$scopeCall) -or
    $scopePlayer -ne 6 -or $scopeCall -le 0) { throw 'Wood-call outer context failed.' }
$outerCall = $scopeCall
$inner = [BugfixesAndQoL.AiWoodBuildCallScope]::Enter(2)
if (-not [BugfixesAndQoL.AiWoodBuildCallScope]::TryGetCurrent([ref]$scopePlayer, [ref]$scopeCall) -or
    $scopePlayer -ne 2 -or $scopeCall -le $outerCall) { throw 'Nested wood-call context failed.' }
[BugfixesAndQoL.AiWoodBuildCallScope]::Leave($inner)
if (-not [BugfixesAndQoL.AiWoodBuildCallScope]::TryGetCurrent([ref]$scopePlayer, [ref]$scopeCall) -or
    $scopePlayer -ne 6 -or $scopeCall -ne $outerCall) { throw 'Wood-call restoration failed.' }
[BugfixesAndQoL.AiWoodBuildCallScope]::Leave($outer)
if ([BugfixesAndQoL.AiWoodBuildCallScope]::TryGetCurrent([ref]$scopePlayer, [ref]$scopeCall)) {
    throw 'Wood-call context leaked after leaving the wrapper.'
}

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
