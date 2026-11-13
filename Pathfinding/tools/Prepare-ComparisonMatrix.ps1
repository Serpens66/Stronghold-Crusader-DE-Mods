[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputFile,
    [string]$StressSave = 'test_stresstest5.sav',
    [ValidateRange(10, 1800)][int]$RunSeconds = 120
)
# Preparation only: emits runner argument objects and prerequisites. Never starts the game,
# installs a package, changes live settings or claims a prepared scenario has been executed.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$settingsSource = [IO.File]::ReadAllText((Join-Path $root 'Pathfinding/src/Settings/PathfindingViewModel.cs'))
$variants = @(
    @{ Key = 'moat'; Setting = 'EnableFriendlyMoatTraversal'; Scenario = 'friendly-moat-crossing'; Note = 'Ground soldiers, finished friendly moat, same move selection and target.' },
    @{ Key = 'assassin-human'; Setting = 'EnableImprovedAssassinPathfinding'; Scenario = 'human-assassin-mass-move'; Note = 'Human-controlled Assassins, same selection/order; hold AI option fixed.' },
    @{ Key = 'assassin-ai'; Setting = 'EnableImprovedAssassinPathfindingAI'; Scenario = 'ai-assassin-siege'; Note = 'AI-owned Assassin army; hold human option fixed.' }
)
$rows = @()
foreach ($variant in $variants) {
    if ($settingsSource -notmatch ('\b' + [regex]::Escape($variant.Setting) + '\b')) { throw "Setting no longer exists: $($variant.Setting)" }
    # A/B then B/A avoids assigning all early/late runs to one option state.
    for ($repeat = 0; $repeat -lt 4; $repeat++) {
        $enabled = $repeat -eq 1 -or $repeat -eq 2
        $label = 'pf-compare-' + $variant.Key + '-' + [int]$enabled + '-' + ($repeat + 1)
        $required = [ordered]@{ EnableMod = $true; EnablePathfindingCore = $true; AvoidFire = $false; AvoidPlague = $false }
        $required[$variant.Setting] = $enabled
        $rows += [ordered]@{
            Label = $label
            Scenario = $variant.Scenario
            RequiredHostSettings = $required
            ScenarioRequirements = $variant.Note
            Runner = 'Pathfinding/tools/Run-PathfindingIteration.ps1'
            RunnerParameters = [ordered]@{
                Label = $label; SavePath = $StressSave; RunSeconds = $RunSeconds
                ExtraConfig = 'Automation.GameSpeed=90;Automation.TargetZoom=0.5;Automation.WarmupTicks=80;Automation.PauseSeconds=12;Automation.SettleSeconds=2;Spikes.RunPhaseTiming=false;Extras.EngineCalls=false;Sampler.SimOutsideRun=true'
                RouteStats = $true
            }
            ExecutionStatus = 'prepared-not-run'
        }
    }
}
$plan = [ordered]@{
    SchemaVersion = 1
    PreparedUtc = [DateTime]::UtcNow.ToString('o')
    Scope = 'G4-G7/G9 comparison preparation; no game execution and no benchmark result'
    Preconditions = @(
        'Verify selected source/SDK/native/package identities before executing any row.',
        'Use a save actually containing the named scenario; StressSave is only a placeholder until scenario validation.',
        'Apply RequiredHostSettings through the existing preset/test API before mission start and verify effective values.',
        'Preserve all other settings, save bytes, zoom, resolution, speed and plugin selection across each pair.',
        'The existing iteration runner does not apply RequiredHostSettings; do not mistake an unchanged configuration for an A/B test.',
        'Use external testmod/public test API for human orders, editor actions and controlled terrain changes; no global inputs without explicit permission.',
        'Run an uninstrumented control separately; this route-stat/sampler configuration is diagnostic and has observer overhead.',
        'Keep startup/loading/first-tick metrics separate from steady-state measurement; restore settings and plugin selection after each series.'
    )
    Measurements = @('Render FPS/p95/p99/frame tails', 'Simulation ticks/s and tick gaps', 'Core work vs Vanilla fallbacks vs replanning', 'Field builds/queries/unreached starts/cache invalidations', 'Cursor rebuild count/reason/build time', 'Independent Assassin requests outside group fields', 'Allocations and GC', 'Command acceptance, real movement and identity preservation')
    AdditionalScenarios = @(
        @{ Key = 'ai110'; Task = 'Q5'; Setup = 'Replay the same historical save with Pathfinding off/on in A/B and B/A order. Before the first AI110 transition record tick, unit/global/group identity, owner/type, current/next/target tile, path/PCL, tile flags, selection +0x8FC, field +0x6EC, retry +0xABE, creation age and native caller. A full-health AI110 observation alone does not establish a cause. Use a separate testmod/public observer; do not add an unaudited production hook.' },
        @{ Key = 'rules-burst'; Task = 'G4'; Setup = 'Map-wide rule band with 1 and 5 openings; simultaneous reachable/unreachable moves; count core work and Vanilla fallback separately.' },
        @{ Key = 'regions'; Task = 'G5'; Setup = 'Repeated wall/gate/moat changes in live game and editor; correlate RegionGeneration with each rebuild and first following cursor query.' },
        @{ Key = 'independent-assassins'; Task = 'G6'; Setup = 'Independent non-group human/AI orders, repeated and fresh endpoints; attribute cold/warm/invalidation costs separately.' },
        @{ Key = 'group-fields'; Task = 'G7'; Setup = 'Multiple simultaneous large groups, enclosed start components of size16 and17, larger disconnected and reachable components; preserve batch/thread equality.' },
        @{ Key = 'editor'; Task = 'G9'; Setup = 'Repeat moat, terrain and mass-order cases in editor with explicit mode evidence.' },
        @{ Key = 'host-client'; Task = 'G9'; Setup = 'Two actual human peers; same authoritative options, commands, decisions, routes and final identities.' }
    )
    Rows = $rows
}
$target = [IO.Path]::GetFullPath($OutputFile)
$parent = [IO.Path]::GetDirectoryName($target)
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Output parent must already exist; allocate development evidence with WorkPaths first.' }
$json = $plan | ConvertTo-Json -Depth 12
$expected = $json.Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n"
[IO.File]::WriteAllText($target, $expected, [Text.UTF8Encoding]::new($false))
if (-not [string]::Equals([IO.File]::ReadAllText($target), $expected, [StringComparison]::Ordinal)) { throw 'Comparison-plan write mismatch.' }
Write-Output "Prepared $($rows.Count) paired comparison rows: $target. No game/settings/install action performed."
