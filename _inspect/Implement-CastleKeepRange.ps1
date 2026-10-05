$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
function Edit([string]$path, [string]$old, [string]$new) {
    $target = Join-Path $workspace $path
    $source = [IO.File]::ReadAllText($target).Replace("`r`n", "`n")
    $old = $old.Replace("`r`n", "`n"); $new = $new.Replace("`r`n", "`n")
    if (-not $source.Contains($old)) { throw "Missing edit anchor: $path : $old" }
    $expected = $source.Replace($old, $new).Replace("`n", "`r`n")
    [IO.File]::WriteAllText($target, $expected, [Text.UTF8Encoding]::new($false))
    if (-not [string]::Equals($expected, [IO.File]::ReadAllText($target), [StringComparison]::Ordinal)) { throw "Write mismatch $path" }
}
Edit 'CastlePlanner/AIVPlacement.Core/KeepRangePolicy.cs' 'using SHCDESE.Interop.Enums;' 'using SHCDESE.Interop;'
Edit 'CastlePlanner/AIVPlacement.Core/CastlePlanner.AIVPlacement.Core.csproj' '  <ItemGroup>' @'
  <PropertyGroup>
    <ExtenderDir Condition="'$(ExtenderDir)' == ''">E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese</ExtenderDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="SHCDESE"><HintPath>$(ExtenderDir)\SHCDESE.dll</HintPath><Private>false</Private></Reference>
'@
Edit 'ExtraFeatures/src/KeepBuildRangeOverride.cs' '        internal void Reconcile(' @'
        // Simulate the next lobby-start reconciliation without changing ownership or native state.
        internal int Preview(int requested, int current)
        {
            lock (sync)
                return requested > 0 ? Math.Min(500, requested) :
                    ownsValue && current == writtenValue ? previousValue : current;
        }

        internal void Reconcile(
'@
Edit 'ExtraFeatures/src/KeepBuildRangeRuntime.cs' '        internal void Refresh()' @'
        internal int PreviewLobby(int currentOverride) =>
            range.Preview(settings.EnableMod ? settings.KeepBuildRange : -1, currentOverride);

        internal void Refresh()
'@
Edit 'ExtraFeatures/src/ExtraFeaturesRuntime.cs' '        private static KeepBuildRangeRuntime processKeepBuildRangeRuntime;' @'
        private static KeepBuildRangeRuntime processKeepBuildRangeRuntime;

        internal static bool TryGetLobbyKeepRangeOverride(int currentOverride, out int range)
        {
            range = currentOverride;
            if (processKeepBuildRangeRuntime == null) return false;
            range = processKeepBuildRangeRuntime.PreviewLobby(currentOverride);
            return true;
        }
'@
Edit 'ExtraFeatures/src/ExtraFeaturesPlugin.cs' '        private void Awake()' @'
        /// <summary>Read-only prediction for a regular/customized skirmish lobby, before its mode gate opens.</summary>
        public static bool TryGetLobbyKeepRangeOverride(int currentOverride, out int range) =>
            ExtraFeaturesRuntime.TryGetLobbyKeepRangeOverride(currentOverride, out range);

        private void Awake()
'@
Edit 'BugfixesAndQoL/src/AIKeepRangeRuntime.cs' '        internal void Refresh() =>' @'
        // Mission end clears the live mask, not the capability for the next lobby start.
        internal bool LobbyBypass => installed && Volatile.Read(ref faulted) == 0 &&
            settings.EnableMod && settings.RemoveAIKeepRangeLimit;

        internal void Refresh() =>
'@
Edit 'BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs' '        private static AIKeepRangeRuntime processAIKeepRangeRuntime;' @'
        private static AIKeepRangeRuntime processAIKeepRangeRuntime;

        internal static bool TryGetLobbyAIKeepRangeBypass(out bool bypass)
        {
            bypass = processAIKeepRangeRuntime?.LobbyBypass == true;
            return processAIKeepRangeRuntime != null;
        }
'@
Edit 'BugfixesAndQoL/src/BugfixesAndQoLPlugin.cs' '        private void Awake()' @'
        /// <summary>Read-only next-game capability, including installation and classification failures.</summary>
        public static bool TryGetLobbyAIKeepRangeBypass(out bool bypass) =>
            BugfixesAndQoLRuntime.TryGetLobbyAIKeepRangeBypass(out bypass);

        private void Awake()
'@
Edit 'CastlePlanner/src/CastlePlannerPlugin.cs' '    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]' @'
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
'@
Edit 'CastlePlanner/CastlePlanner.csproj' '    <Compile Include="src\AIVPlacement\BugfixAivStatusBridge.cs" />' @'
    <Compile Include="src\AIVPlacement\BugfixAivStatusBridge.cs" />
    <Compile Include="src\AIVPlacement\KeepRangeSettingsBridge.cs" />
'@
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestModels.cs' '            IEnumerable<int> humanPlayerIds = null)' @'
            IEnumerable<int> humanPlayerIds = null,
            KeepRangeSnapshot keepRange = null)
'@
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestModels.cs' '            MapPath = mapPath ?? string.Empty;' '            MapPath = mapPath ?? string.Empty;'
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestModels.cs' '            HumanPlayerIds = new ReadOnlyCollection<int>(' @'
            KeepRange = keepRange ?? KeepRangeSnapshot.Vanilla;
            HumanPlayerIds = new ReadOnlyCollection<int>(
'@
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestModels.cs' '        public IReadOnlyList<int> HumanPlayerIds { get; }' @'
        public IReadOnlyList<int> HumanPlayerIds { get; }
        public KeepRangeSnapshot KeepRange { get; }
'@
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestModels.cs' '            IEnumerable<AivPlacementCheckRequest> requests)' @'
            IEnumerable<AivPlacementCheckRequest> requests,
            KeepRangeSnapshot keepRange = null)
'@
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestModels.cs' '            Requests = new ReadOnlyCollection<AivPlacementCheckRequest>(' @'
            KeepRange = keepRange ?? KeepRangeSnapshot.Vanilla;
            Requests = new ReadOnlyCollection<AivPlacementCheckRequest>(
'@
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestModels.cs' '        public IReadOnlyList<AivPlacementCheckRequest> Requests { get; }' @'
        public IReadOnlyList<AivPlacementCheckRequest> Requests { get; }
        public KeepRangeSnapshot KeepRange { get; }
'@
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestBuilder.cs' 'return new AivPlacementRequestBatch(generation, requests);' 'return new AivPlacementRequestBatch(generation, requests, capture.KeepRange);'
Edit 'CastlePlanner/AIVPlacement.Core/LobbyRequestBuilder.cs' '            Append(result, capture.MapPath);' @'
            Append(result, capture.MapPath);
            Append(result, capture.KeepRange.Fingerprint);
'@
Edit 'CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs' '            var humanPlayerIds = new List<int>();' @'
            var humanPlayerIds = new List<int>();
            var teams = new Dictionary<int, int>();
'@
Edit 'CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs' '                    if (member.SkirmishHumanMember)' @'
                    teams[playerId] = lobby.getTeam(member);
                    if (member.SkirmishHumanMember)
'@
Edit 'CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs' '                humanPlayerIds);' '                humanPlayerIds, KeepRangeSettingsBridge.Capture(teams, log));'
