// Feature: Weighted replacement for Vanilla's Assassin-only path-cost expansion.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AssassinPathfindingRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AssassinPathBuilderDelegate(
            IntPtr context,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int maximumNodes,
            int continuation);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ResumeOldOrderDelegate(IntPtr tribeManager, int unitId, int internalCommand);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CommonPathRequestDelegate(
            IntPtr unitBase,
            int nativeUnitIndex,
            int targetX,
            int targetY,
            int pathOption);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate byte SpecialTilePredicateDelegate(IntPtr context, int tileId);

        private const int MapWidth = 800;
        private const int CoordinateCount = MapWidth * MapWidth;
        private const int TileCount = 320800;
        private const int MaximumCommittedPathLength = 2000;
        private const int AssassinBuilderRva = AssassinCombatResumeNativeDefinition.AssassinPathBuilderRva;
        private const int SpecialTilePredicateRva = 0x107160;
        private const int SpecialTilePredicateContextRva = 0x32DE440;
        private const int ValidCoordinateGridRva = 0x3A11EA4;
        private const int RowLookupRva = 0x402FF2C;
        private const int TileFlagsRva = 0x48F71B0;
        private const int BuildingLayerRva = 0x4B6AA50;
        private const int HeightLayerRva = 0x4DDD350;
        private const int OccupancyLayerRva = 0x51890D0;
        private const int NativeDistanceLayerRva = 0x5225B10;
        private const int NativeVisitStampLayerRva = 0x52C2550;
        private const int DirectionMaskRva = 0x312620;
        private const uint AssassinFallbackBlockingMask = 0x4A5014B1u;
        private const uint NativeSpecialTileFlag = 1u << 12;
        private const uint IsWallFlag = 1u << 8;
        private const uint IsStairsFlag = 1u << 11;
        private const uint IsLowWallFlag = 1u << 16;
        private const string AssassinBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 18 48 89 74 24 20 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 EA 48 8B D9 49 63 F9";

        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly AssassinClimbRuntime climbRuntime;
        private readonly int[] costs = new int[CoordinateCount];
        private readonly int[] parents = new int[CoordinateCount];
        private readonly int[] insertionOrder = new int[CoordinateCount];
        private readonly int[] heap = new int[CoordinateCount];
        private readonly int[] heapPositions = new int[CoordinateCount];
        private readonly int[] touched = new int[CoordinateCount];
        private readonly int[] route = new int[MaximumCommittedPathLength + 1];
        private readonly byte[] seenTiles = new byte[TileCount];
        private IntPtr libraryHandle;
        private AssassinPathBuilderDelegate original;
        private AssassinPathBuilderDelegate rootedDetour;
        private ResumeOldOrderDelegate originalResumeOldOrder;
        private ResumeOldOrderDelegate rootedResumeOldOrderDetour;
        private CommonPathRequestDelegate originalCommonPathRequest;
        private CommonPathRequestDelegate rootedCommonPathRequestDetour;
        private SpecialTilePredicateDelegate specialTilePredicate;
        private NativeDetour detour;
        private NativeDetour resumeOldOrderDetour;
        private NativeDetour commonPathRequestDetour;
        private AssassinPathReconstructionPatch reconstructionPatch;
        private int* assassinPathContextFlag;
        private byte* validCoordinates;
        private int* rowLookup;
        private uint* tileFlags;
        private ushort* buildingLayer;
        private byte* heightLayer;
        private byte* occupancyLayer;
        private short* nativeDistances;
        private short* nativeVisitStamps;
        private byte* directionMasks;
        private int heapCount;
        private int touchedCount;
        private int nextInsertionOrder;
        private bool fallbackLogged;
        private bool coordinateMapValidated;
        private bool coordinateValidationFailureLogged;

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private const int MaximumCombatResumeDiagnosticEventsPerMap = 64;
        private int combatResumeDiagnosticEventCount;
        private int activeCombatResumeDiagnosticId;
        private int activeCombatResumeBuilderCalls;
        #endregion

        public AssassinPathfindingRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings, AssassinClimbRuntime climbRuntime)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.climbRuntime = climbRuntime ?? throw new ArgumentNullException(nameof(climbRuntime));
            for (int node = 0; node < CoordinateCount; node++)
            {
                costs[node] = int.MaxValue;
                parents[node] = -1;
                heapPositions[node] = -1;
            }
        }

        public bool IsInstalled =>
            detour != null &&
            resumeOldOrderDetour != null &&
            commonPathRequestDetour != null;

        public void InitializeNative(IntPtr newLibraryHandle, ReadOnlySpan<byte> memory, bool fixedLayoutHashValidated)
        {
            if (detour != null)
                return;
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (newLibraryHandle == IntPtr.Zero ||
                memory.Length <= NativeVisitStampLayerRva + TileCount * sizeof(short) ||
                memory.Length <= AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva + sizeof(int))
                throw new InvalidOperationException("native module memory does not cover the required Assassin pathfinding layers");

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinBuilderPattern,
                AssassinBuilderRva,
                referenceHashMatches: true,
                "Assassin path-cost builder",
                log);
            IntPtr resolved = IntPtr.Add(newLibraryHandle, resolution.Rva);
            if (resolved != IntPtr.Add(newLibraryHandle, AssassinBuilderRva))
                throw new InvalidOperationException("Assassin path-cost builder resolved outside its validated RVA");

            Shared.NativeResolution resumeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.ResumeOldOrderPattern,
                AssassinCombatResumeNativeDefinition.ResumeOldOrderRva,
                referenceHashMatches: true,
                "Assassin post-combat movement-order resume",
                log);
            IntPtr resolvedResumeOldOrder = IntPtr.Add(newLibraryHandle, resumeResolution.Rva);
            if (resumeResolution.Rva != AssassinCombatResumeNativeDefinition.ResumeOldOrderRva)
                throw new InvalidOperationException("Assassin movement-order resume resolved outside its validated RVA");

            Shared.NativeResolution nativeUnitIndexResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.ResumeNativeUnitIndexAddressingPattern,
                AssassinCombatResumeNativeDefinition.ResumeNativeUnitIndexAddressingRva,
                referenceHashMatches: true,
                "Assassin resume native unit-index addressing",
                log);
            if (nativeUnitIndexResolution.Rva != AssassinCombatResumeNativeDefinition.ResumeNativeUnitIndexAddressingRva)
                throw new InvalidOperationException("Assassin movement-order resume no longer uses its validated native unit-index addressing");

            Shared.NativeResolution commonPathResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathRequestPattern,
                AssassinCombatResumeNativeDefinition.CommonPathRequestRva,
                referenceHashMatches: true,
                "common path request used by Assassin post-combat repathing",
                log);
            if (commonPathResolution.Rva != AssassinCombatResumeNativeDefinition.CommonPathRequestRva)
                throw new InvalidOperationException("common path request resolved outside its validated RVA");

            ValidatePostCombatNativeContracts(memory);
            IntPtr resolvedCommonPathRequest = IntPtr.Add(newLibraryHandle, commonPathResolution.Rva);

            libraryHandle = newLibraryHandle;
            validCoordinates = (byte*)IntPtr.Add(newLibraryHandle, ValidCoordinateGridRva).ToPointer();
            rowLookup = (int*)IntPtr.Add(newLibraryHandle, RowLookupRva).ToPointer();
            tileFlags = (uint*)IntPtr.Add(newLibraryHandle, TileFlagsRva).ToPointer();
            buildingLayer = (ushort*)IntPtr.Add(newLibraryHandle, BuildingLayerRva).ToPointer();
            heightLayer = (byte*)IntPtr.Add(newLibraryHandle, HeightLayerRva).ToPointer();
            occupancyLayer = (byte*)IntPtr.Add(newLibraryHandle, OccupancyLayerRva).ToPointer();
            nativeDistances = (short*)IntPtr.Add(newLibraryHandle, NativeDistanceLayerRva).ToPointer();
            nativeVisitStamps = (short*)IntPtr.Add(newLibraryHandle, NativeVisitStampLayerRva).ToPointer();
            directionMasks = (byte*)IntPtr.Add(newLibraryHandle, DirectionMaskRva).ToPointer();
            assassinPathContextFlag = (int*)IntPtr.Add(
                newLibraryHandle,
                AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva).ToPointer();
            specialTilePredicate = Marshal.GetDelegateForFunctionPointer<SpecialTilePredicateDelegate>(
                IntPtr.Add(newLibraryHandle, SpecialTilePredicateRva));

            rootedDetour = BuildWeightedPath;
            IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(rootedDetour);
            rootedResumeOldOrderDetour = ResumeOldOrderAfterCombat;
            IntPtr resumeDetourAddress = Marshal.GetFunctionPointerForDelegate(rootedResumeOldOrderDetour);
            rootedCommonPathRequestDetour = RequestPathWithAssassinCombatContext;
            IntPtr commonPathDetourAddress = Marshal.GetFunctionPointerForDelegate(rootedCommonPathRequestDetour);
            NativeDetour installed = null;
            NativeDetour installedResumeOldOrder = null;
            NativeDetour installedCommonPathRequest = null;
            AssassinPathReconstructionPatch pendingReconstructionPatch = null;
            bool builderApplied = false;
            bool resumeApplied = false;
            bool commonPathApplied = false;
            try
            {
                pendingReconstructionPatch = new AssassinPathReconstructionPatch(
                    log,
                    newLibraryHandle,
                    memory,
                    referenceHashMatches: true);
                installed = new NativeDetour(resolved, detourAddress, new NativeDetourConfig { ManualApply = true });
                original = installed.GenerateTrampoline<AssassinPathBuilderDelegate>();
                installedResumeOldOrder = new NativeDetour(
                    resolvedResumeOldOrder,
                    resumeDetourAddress,
                    new NativeDetourConfig { ManualApply = true });
                originalResumeOldOrder = installedResumeOldOrder.GenerateTrampoline<ResumeOldOrderDelegate>();
                installedCommonPathRequest = new NativeDetour(
                    resolvedCommonPathRequest,
                    commonPathDetourAddress,
                    new NativeDetourConfig { ManualApply = true });
                originalCommonPathRequest = installedCommonPathRequest.GenerateTrampoline<CommonPathRequestDelegate>();
                installed.Apply();
                builderApplied = true;
                installedResumeOldOrder.Apply();
                resumeApplied = true;
                installedCommonPathRequest.Apply();
                commonPathApplied = true;
                detour = installed;
                resumeOldOrderDetour = installedResumeOldOrder;
                commonPathRequestDetour = installedCommonPathRequest;
                reconstructionPatch = pendingReconstructionPatch;
                ApplySetting();
                LogDebug($"weighted Assassin pathfinding installed at RVA 0x{AssassinBuilderRva:X}, including order resume at RVA 0x{AssassinCombatResumeNativeDefinition.ResumeOldOrderRva:X} and state-{AssassinCombatResumePolicy.PostCombatRepathState} repath via RVA 0x{AssassinCombatResumeNativeDefinition.CommonPathRequestRva:X}; climb costs={AssassinClimbCostPolicy.MinimumClimbTicks}/{AssassinClimbCostPolicy.LowWallClimbTicks}/{AssassinClimbCostPolicy.NormalWallClimbTicks} ticks.");
            }
            catch
            {
                if (pendingReconstructionPatch?.IsApplied == true)
                    pendingReconstructionPatch.SetEnabled(false);
                if (commonPathApplied)
                    installedCommonPathRequest?.Undo();
                installedCommonPathRequest?.Dispose();
                if (resumeApplied)
                    installedResumeOldOrder?.Undo();
                installedResumeOldOrder?.Dispose();
                if (builderApplied)
                    installed?.Undo();
                installed?.Dispose();
                original = null;
                rootedDetour = null;
                originalResumeOldOrder = null;
                rootedResumeOldOrderDetour = null;
                originalCommonPathRequest = null;
                rootedCommonPathRequestDetour = null;
                detour = null;
                resumeOldOrderDetour = null;
                commonPathRequestDetour = null;
                reconstructionPatch = null;
                throw;
            }
        }

        public void ApplySetting()
        {
            AssassinPathReconstructionPatch patch = reconstructionPatch;
            if (patch == null)
                return;

            patch.SetEnabled(AssassinClimbTransitionPolicy.ShouldRelaxPathReconstruction(
                settings.EnableMod,
                settings.EnableImprovedAssassinPathfinding,
                IsInstalled));
        }

        public void BeginMap()
        {
            ResetMapValidation();
            ResetCombatResumeDiagnostics();
        }

        public void EndMap()
        {
            ResetMapValidation();
            ResetCombatResumeDiagnostics();
        }

        private int BuildWeightedPath(IntPtr context, int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation)
        {
            int diagnosticId = activeCombatResumeDiagnosticId;
            if (diagnosticId > 0)
            {
                activeCombatResumeBuilderCalls++;
                LogCombatResumeDiagnostic(
                    diagnosticId,
                    $"builder-enter call={activeCombatResumeBuilderCalls}, context=0x{context.ToInt64():X}, start={startX},{startY}, target={targetX},{targetY}, maximumNodes={maximumNodes}, continuation={continuation}, contextFlag={*assassinPathContextFlag}");
            }

            AssassinPathBuilderDelegate vanilla = original;
            if (vanilla == null)
                return ReturnCombatResumeBuilderDiagnostic(diagnosticId, "missing-vanilla-trampoline", 0, 0);

            // Vanilla initializes internal queue state even when our compact route field replaces it.
            int vanillaResult = vanilla(context, startX, startY, targetX, targetY, maximumNodes, continuation);
            bool improvedPathfindingEnabled = settings.EnableMod && settings.EnableImprovedAssassinPathfinding;
            if (!improvedPathfindingEnabled || continuation != 0)
                return ReturnCombatResumeBuilderDiagnostic(
                    diagnosticId,
                    !improvedPathfindingEnabled ? "vanilla-feature-disabled" : "vanilla-continuation",
                    vanillaResult,
                    vanillaResult);
            if (targetX < 0 || targetY < 0)
                return ReturnCombatResumeBuilderDiagnostic(
                    diagnosticId,
                    "vanilla-negative-target",
                    vanillaResult,
                    vanillaResult);

            try
            {
                if (!TryResolveAssassinRequest(startX, startY, out int playerId, out int speedDelay))
                    return ReturnCombatResumeBuilderDiagnostic(
                        diagnosticId,
                        "vanilla-assassin-request-unresolved",
                        vanillaResult,
                        vanillaResult);
                if (!EnsureCoordinateTileMappingValidated())
                    return ReturnCombatResumeBuilderDiagnostic(
                        diagnosticId,
                        "vanilla-coordinate-map-unavailable",
                        vanillaResult,
                        vanillaResult);

                bool allowClimbing = climbRuntime.IsClimbingAllowed(playerId);
                // Never publish a relaxed route unless Vanilla can reconstruct the same
                // validated reserved climb endpoints. This keeps patch failures fail-closed.
                bool allowWalkableReservedClimbEndpoints = improvedPathfindingEnabled &&
                    reconstructionPatch?.IsApplied == true;
                bool found = TryBuildWeightedRoute(
                    context,
                    startX,
                    startY,
                    targetX,
                    targetY,
                    maximumNodes,
                    speedDelay,
                    allowClimbing,
                    allowWalkableReservedClimbEndpoints: allowWalkableReservedClimbEndpoints);
                return ReturnCombatResumeBuilderDiagnostic(
                    diagnosticId,
                    found ? "weighted-route-found" : "weighted-route-not-found",
                    found ? 1 : 0,
                    vanillaResult);
            }
            catch (Exception ex)
            {
                if (!fallbackLogged)
                {
                    fallbackLogged = true;
                    LogError($"weighted Assassin pathfinding failed and this request fell back to Vanilla: {ex}");
                }
                return ReturnCombatResumeBuilderDiagnostic(
                    diagnosticId,
                    "vanilla-after-weighted-exception",
                    vanillaResult,
                    vanillaResult);
            }
        }

        private int ResumeOldOrderAfterCombat(IntPtr tribeManager, int nativeUnitIndex, int internalCommand)
        {
            ResumeOldOrderDelegate vanilla = originalResumeOldOrder;
            if (vanilla == null)
                return 0;

            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            bool unitResolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(nativeUnitIndex, units.Length);
            AliveState aliveState = unitResolved ? units[nativeUnitIndex].r_AliveState : default;
            eChimps unitType = unitResolved ? units[nativeUnitIndex].r_UnitChimp : default;
            bool eligible = AssassinCombatResumePolicy.ShouldUseAssassinPathContext(
                    settings.EnableMod,
                    settings.EnableImprovedAssassinPathfinding,
                    IsInstalled,
                    unitResolved,
                    aliveState,
                    unitType);

            int diagnosticId = BeginCombatResumeDiagnostic(
                nativeUnitIndex,
                units.Length,
                unitResolved,
                aliveState,
                unitType,
                internalCommand,
                eligible);
            int previousDiagnosticId = activeCombatResumeDiagnosticId;
            int previousBuilderCalls = activeCombatResumeBuilderCalls;
            activeCombatResumeDiagnosticId = diagnosticId;
            activeCombatResumeBuilderCalls = 0;
            if (!eligible)
            {
                try
                {
                    int vanillaResult = vanilla(tribeManager, nativeUnitIndex, internalCommand);
                    LogCombatResumeDiagnostic(
                        diagnosticId,
                        $"resume-exit eligible=False, result={vanillaResult}, builderCalls={activeCombatResumeBuilderCalls}, contextFlag={*assassinPathContextFlag}");
                    return vanillaResult;
                }
                finally
                {
                    activeCombatResumeDiagnosticId = previousDiagnosticId;
                    activeCombatResumeBuilderCalls = previousBuilderCalls;
                }
            }

            // MoveHere sets this flag before creating the path context, but Vanilla's combat
            // resume omits it. Scope the correction to this one Assassin order reissue.
            int previousPathContext = *assassinPathContextFlag;
            *assassinPathContextFlag = 1;
            int result = 0;
            int pathContextAfterVanilla = int.MinValue;
            bool completed = false;
            try
            {
                result = vanilla(tribeManager, nativeUnitIndex, internalCommand);
                pathContextAfterVanilla = *assassinPathContextFlag;
                completed = true;
                return result;
            }
            finally
            {
                *assassinPathContextFlag = previousPathContext;
                LogCombatResumeDiagnostic(
                    diagnosticId,
                    $"resume-exit eligible=True, completed={completed}, result={result}, builderCalls={activeCombatResumeBuilderCalls}, flagBefore={previousPathContext}, flagAfterVanilla={pathContextAfterVanilla}, flagRestored={*assassinPathContextFlag}");
                activeCombatResumeDiagnosticId = previousDiagnosticId;
                activeCombatResumeBuilderCalls = previousBuilderCalls;
            }
        }

        private int RequestPathWithAssassinCombatContext(
            IntPtr unitBase,
            int nativeUnitIndex,
            int targetX,
            int targetY,
            int pathOption)
        {
            CommonPathRequestDelegate vanilla = originalCommonPathRequest;
            if (vanilla == null)
                return 0;
            if (!settings.EnableMod ||
                !settings.EnableImprovedAssassinPathfinding ||
                !IsInstalled)
            {
                return vanilla(unitBase, nativeUnitIndex, targetX, targetY, pathOption);
            }

            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            bool unitResolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(nativeUnitIndex, units.Length);
            AliveState aliveState = unitResolved ? units[nativeUnitIndex].r_AliveState : default;
            eChimps unitType = unitResolved ? units[nativeUnitIndex].r_UnitChimp : default;
            ushort aiState = unitResolved ? units[nativeUnitIndex].r_AIState : (ushort)0;
            int previousPathContext = *assassinPathContextFlag;
            bool injectContext = AssassinCombatResumePolicy.ShouldInjectPostCombatPathContext(
                settings.EnableMod,
                settings.EnableImprovedAssassinPathfinding,
                IsInstalled,
                unitResolved,
                aliveState,
                unitType,
                aiState,
                previousPathContext);

            int diagnosticId = BeginDirectRepathDiagnostic(
                nativeUnitIndex,
                units.Length,
                unitResolved,
                aliveState,
                unitType,
                aiState,
                targetX,
                targetY,
                pathOption,
                injectContext,
                previousPathContext);
            int previousDiagnosticId = activeCombatResumeDiagnosticId;
            int previousBuilderCalls = activeCombatResumeBuilderCalls;
            activeCombatResumeDiagnosticId = diagnosticId;
            activeCombatResumeBuilderCalls = 0;

            if (!injectContext)
            {
                try
                {
                    int vanillaResult = vanilla(unitBase, nativeUnitIndex, targetX, targetY, pathOption);
                    LogCombatResumeDiagnostic(
                        diagnosticId,
                        $"direct-repath-exit injected=False, result={vanillaResult}, builderCalls={activeCombatResumeBuilderCalls}, flagAfterVanilla={*assassinPathContextFlag}");
                    return vanillaResult;
                }
                finally
                {
                    activeCombatResumeDiagnosticId = previousDiagnosticId;
                    activeCombatResumeBuilderCalls = previousBuilderCalls;
                }
            }

            // State 122 directly requests the stored destination but omits MoveHere's Assassin
            // context. Limit the correction to this one path request and preserve nesting.
            *assassinPathContextFlag = 1;
            int result = 0;
            int pathContextAfterVanilla = int.MinValue;
            bool completed = false;
            try
            {
                result = vanilla(unitBase, nativeUnitIndex, targetX, targetY, pathOption);
                pathContextAfterVanilla = *assassinPathContextFlag;
                completed = true;
                return result;
            }
            finally
            {
                *assassinPathContextFlag = previousPathContext;
                LogCombatResumeDiagnostic(
                    diagnosticId,
                    $"direct-repath-exit injected=True, completed={completed}, result={result}, builderCalls={activeCombatResumeBuilderCalls}, flagBefore={previousPathContext}, flagAfterVanilla={pathContextAfterVanilla}, flagRestored={*assassinPathContextFlag}");
                activeCombatResumeDiagnosticId = previousDiagnosticId;
                activeCombatResumeBuilderCalls = previousBuilderCalls;
            }
        }

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private int BeginCombatResumeDiagnostic(
            int nativeUnitIndex,
            int unitCount,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType,
            int internalCommand,
            bool eligible)
        {
            if (!settings.EnableMod ||
                !settings.EnableImprovedAssassinPathfinding ||
                combatResumeDiagnosticEventCount >= MaximumCombatResumeDiagnosticEventsPerMap)
            {
                return 0;
            }

            int diagnosticId = ++combatResumeDiagnosticEventCount;
            LogCombatResumeDiagnostic(
                diagnosticId,
                $"resume-enter nativeUnitIndex={nativeUnitIndex}, unitCount={unitCount}, resolved={unitResolved}, aliveState={aliveState}, unitType={unitType}, internalCommand={internalCommand}, eligible={eligible}, contextFlag={*assassinPathContextFlag}");
            return diagnosticId;
        }

        private int BeginDirectRepathDiagnostic(
            int nativeUnitIndex,
            int unitCount,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType,
            ushort aiState,
            int targetX,
            int targetY,
            int pathOption,
            bool injectContext,
            int pathContext)
        {
            if (!settings.EnableMod ||
                !settings.EnableImprovedAssassinPathfinding ||
                aiState != AssassinCombatResumePolicy.PostCombatRepathState ||
                combatResumeDiagnosticEventCount >= MaximumCombatResumeDiagnosticEventsPerMap)
            {
                return 0;
            }

            int diagnosticId = ++combatResumeDiagnosticEventCount;
            LogCombatResumeDiagnostic(
                diagnosticId,
                $"direct-repath-enter nativeUnitIndex={nativeUnitIndex}, unitCount={unitCount}, resolved={unitResolved}, aliveState={aliveState}, unitType={unitType}, aiState={aiState}, target={targetX},{targetY}, pathOption={pathOption}, eligible={injectContext}, contextFlag={pathContext}");
            return diagnosticId;
        }

        private int ReturnCombatResumeBuilderDiagnostic(
            int diagnosticId,
            string outcome,
            int result,
            int vanillaResult)
        {
            LogCombatResumeDiagnostic(
                diagnosticId,
                $"builder-exit call={activeCombatResumeBuilderCalls}, outcome={outcome}, result={result}, vanillaResult={vanillaResult}, contextFlag={*assassinPathContextFlag}");
            return result;
        }

        private void ResetCombatResumeDiagnostics()
        {
            combatResumeDiagnosticEventCount = 0;
            activeCombatResumeDiagnosticId = 0;
            activeCombatResumeBuilderCalls = 0;
        }

        private void ValidatePostCombatNativeContracts(ReadOnlySpan<byte> memory)
        {
            Shared.NativeResolution remap = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinStateRemapSequence,
                AssassinCombatResumeNativeDefinition.AssassinStateRemapSequenceRva,
                referenceHashMatches: true,
                "Assassin AI-state remap around post-combat state 122",
                log);
            if (memory[remap.Rva + AssassinCombatResumeNativeDefinition.PostCombatStateRemapOffset] !=
                AssassinCombatResumeNativeDefinition.PostCombatStateRemapIndex)
                throw new InvalidOperationException("Assassin state 122 no longer maps to the audited jump-table index");

            Shared.NativeResolution jumpTable = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinStateJumpTableSequence,
                AssassinCombatResumeNativeDefinition.AssassinStateJumpTableSequenceRva,
                referenceHashMatches: true,
                "Assassin AI-state jump table around post-combat state 122",
                log);
            int stateHandlerRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                jumpTable.Rva + AssassinCombatResumeNativeDefinition.PostCombatStateJumpTargetOffset);
            if (stateHandlerRva != AssassinCombatResumeNativeDefinition.PostCombatStateHandlerRva)
                throw new InvalidOperationException("Assassin state 122 no longer targets the audited post-combat handler");

            Shared.NativeResolution directRepath = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequence,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva,
                referenceHashMatches: true,
                "Assassin state-122 direct path request",
                log);
            if (directRepath.Rva != AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva)
                throw new InvalidOperationException("Assassin state-122 direct path request resolved outside its validated RVA");
            int directPathTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                directRepath.Rva + AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset + 1,
                directRepath.Rva + AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset + 5);
            int nextState = Shared.NativePatternResolver.ReadInt32(
                memory,
                directRepath.Rva + AssassinCombatResumeNativeDefinition.PostCombatMovementStateLoadOffset + 1);
            if (directPathTarget != AssassinCombatResumeNativeDefinition.CommonPathRequestRva || nextState != 101)
                throw new InvalidOperationException(
                    "Assassin state 122 no longer directly requests the audited path and then enters movement state 101");
        }

        private void LogCombatResumeDiagnostic(int diagnosticId, string message)
        {
            if (diagnosticId <= 0)
                return;
            LogDebug($"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC event={diagnosticId}] {message}");
        }
        #endregion

        private bool TryBuildWeightedRoute(
            IntPtr context,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int maximumNodes,
            int speedDelay,
            bool allowClimbing,
            bool allowWalkableReservedClimbEndpoints)
        {
            if (!IsValidCoordinate(startX, startY) || !IsValidCoordinate(targetX, targetY))
                return false;

            ResetTouchedNodes();
            int cardinalTicks = AssassinClimbCostPolicy.GetCardinalMovementTicks(speedDelay);
            int diagonalTicks = AssassinClimbCostPolicy.GetDiagonalMovementTicks(speedDelay);
            int startTile = GetTileId(startX, startY);
            int targetTile = GetTileId(targetX, targetY);
            if (!IsNativeTile(startTile) || !IsNativeTile(targetTile))
                return false;

            int startNode = GetCoordinateIndex(startX, startY);
            int targetNode = GetCoordinateIndex(targetX, targetY);
            Touch(startNode, 0, -1);
            Push(startNode);
            int expanded = 0;
            int nodeLimit = Math.Max(1, Math.Min(maximumNodes, TileCount));

            while (heapCount > 0 && expanded < nodeLimit)
            {
                int currentNode = Pop();
                expanded++;
                if (currentNode == targetNode)
                    return CommitRoute(context, startNode, targetNode);

                int currentX = currentNode % MapWidth;
                int currentY = currentNode / MapWidth;
                int currentTile = GetTileId(currentX, currentY);
                if (!IsNativeTile(currentTile))
                    continue;

                uint currentFlags = tileFlags[currentTile];
                for (int direction = 0; direction < DirectionX.Length; direction++)
                {
                    int nextX = currentX + DirectionX[direction];
                    int nextY = currentY + DirectionY[direction];
                    if (!IsValidCoordinate(nextX, nextY))
                        continue;

                    int nextTile = GetTileId(nextX, nextY);
                    if (!IsNativeTile(nextTile))
                        continue;

                    int nextNode = GetCoordinateIndex(nextX, nextY);
                    bool ordinaryEdge = (directionMasks[direction] & occupancyLayer[currentTile]) != 0;
                    bool climbEdge = false;
                    if (!ordinaryEdge)
                    {
                        if ((direction & 1) != 0)
                            continue;
                        bool fallbackAccepted = IsVanillaAssassinFallback(
                            currentTile,
                            nextTile,
                            currentFlags,
                            allowWalkableReservedClimbEndpoints);
                        if (!fallbackAccepted)
                            continue;
                        climbEdge = true;
                        if (!allowClimbing)
                            continue;
                    }

                    int movementTicks = (direction & 1) == 0
                        ? cardinalTicks
                        : diagonalTicks;
                    int climbTicks = climbEdge ? GetClimbTicks(currentTile, nextTile) : 0;
                    int edgeCost = movementTicks > int.MaxValue - climbTicks ? int.MaxValue : movementTicks + climbTicks;
                    int newCost = costs[currentNode] > int.MaxValue - edgeCost ? int.MaxValue : costs[currentNode] + edgeCost;
                    if (newCost >= costs[nextNode])
                        continue;

                    if (costs[nextNode] == int.MaxValue)
                        Touch(nextNode, newCost, currentNode);
                    else
                    {
                        costs[nextNode] = newCost;
                        parents[nextNode] = currentNode;
                    }
                    PushOrDecrease(nextNode);
                }
            }

            return false;
        }

        private bool IsVanillaAssassinFallback(
            int current,
            int target,
            uint currentFlags,
            bool allowWalkableReservedClimbEndpoints)
        {
            uint targetFlags = tileFlags[target];
            bool targetAccepted = (targetFlags & AssassinFallbackBlockingMask) == 0;
            if (!targetAccepted && (targetFlags & NativeSpecialTileFlag) != 0)
            {
                targetAccepted = specialTilePredicate(
                    IntPtr.Add(libraryHandle, SpecialTilePredicateContextRva),
                    target) != 0;
            }

            bool startAccepted = AssassinClimbTransitionPolicy.CanUseStartTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[current],
                occupancyLayer[current]);
            bool targetBuildingAccepted = AssassinClimbTransitionPolicy.CanUseTargetTile(
                allowWalkableReservedClimbEndpoints,
                buildingLayer[target],
                occupancyLayer[target]);
            bool hasWall = ((currentFlags | targetFlags) & IsWallFlag) != 0;
            return targetAccepted && startAccepted && targetBuildingAccepted && hasWall;
        }

        private int GetClimbTicks(int current, int target)
        {
            int heightDifference = heightLayer[target] - heightLayer[current];
            uint targetFlags = tileFlags[target];
            return AssassinClimbCostPolicy.GetAdditionalTicks(
                isClimbEdge: true,
                heightDifference: heightDifference,
                targetIsLowWall: (targetFlags & IsLowWallFlag) != 0,
                targetIsNormalWall: (targetFlags & IsWallFlag) != 0,
                targetIsStairs: (targetFlags & IsStairsFlag) != 0);
        }

        private bool CommitRoute(IntPtr context, int startNode, int targetNode)
        {
            int routeLength = 0;
            int node = targetNode;
            while (node >= 0 && routeLength < route.Length)
            {
                route[routeLength++] = node;
                if (node == startNode)
                    break;
                node = parents[node];
            }

            if (routeLength == 0 || routeLength > MaximumCommittedPathLength || route[routeLength - 1] != startNode)
                return false;

            int generation = *(int*)((byte*)context.ToPointer() + 4) + 1;
            if (generation > 32000)
            {
                new Span<short>(nativeVisitStamps, TileCount).Clear();
                generation = 1;
            }
            *(int*)((byte*)context.ToPointer() + 4) = generation;

            for (int reverseIndex = routeLength - 1, distance = 1;
                 reverseIndex >= 0;
                 reverseIndex--, distance++)
            {
                int routeNode = route[reverseIndex];
                int routeX = routeNode % MapWidth;
                int routeY = routeNode / MapWidth;
                int routeTile = GetTileId(routeX, routeY);
                if (!IsNativeTile(routeTile))
                    return false;
                nativeVisitStamps[routeTile] = (short)generation;
                nativeDistances[routeTile] = (short)distance;
            }

            return true;
        }

        private bool TryResolveAssassinRequest(int startX, int startY, out int playerId, out int speedDelay)
        {
            playerId = -1;
            speedDelay = -1;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int index = 0; index < units.Length; index++)
            {
                ref GameUnit candidate = ref units[index];
                if (candidate.r_AliveState != AliveState.IsAlive ||
                    candidate.r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                    candidate.r_CurrentTilePositionX != startX || candidate.r_CurrentTilePositionY != startY)
                {
                    continue;
                }

                int candidatePlayer = candidate.r_ControllableForPlayerId;
                if (playerId > 0 && candidatePlayer != playerId)
                    return false;
                playerId = candidatePlayer;
                int candidateDelay = candidate.r_CurrentSpeed;
                if (candidateDelay > speedDelay)
                    speedDelay = candidateDelay;
            }
            if (speedDelay < 0)
                speedDelay = GameUnitManagerAPI.Instance.GetDefaultSpeed(eChimps.CHIMP_TYPE_ARAB_ASSASIN);
            return playerId > 0;
        }

        private bool IsValidCoordinate(int x, int y)
        {
            return (uint)x < MapWidth && (uint)y < MapWidth && validCoordinates[y * MapWidth + x] != 0;
        }

        private int GetTileId(int x, int y) => rowLookup[y * 3] + x;

        private static int GetCoordinateIndex(int x, int y) => y * MapWidth + x;

        private static bool IsNativeTile(int tile) => (uint)tile < TileCount;

        private void ValidateCoordinateTileMapping()
        {
            Array.Clear(seenTiles, 0, seenTiles.Length);
            int validCount = 0;
            for (int y = 0; y < MapWidth; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    if (!IsValidCoordinate(x, y))
                        continue;

                    int tile = GetTileId(x, y);
                    if (!IsNativeTile(tile))
                        throw new InvalidOperationException($"valid coordinate {x},{y} maps outside the native tile layers: {tile}");
                    if (seenTiles[tile] != 0)
                        throw new InvalidOperationException($"valid coordinate {x},{y} maps to duplicate native tile {tile}");
                    seenTiles[tile] = 1;
                    validCount++;
                }
            }

            if (validCount <= 0 || validCount > TileCount)
                throw new InvalidOperationException($"native coordinate map exposed an invalid valid-tile count: {validCount}");
            LogDebug($"Assassin coordinate map validated for the current map: validCoordinates={validCount}.");
        }

        private bool EnsureCoordinateTileMappingValidated()
        {
            if (coordinateMapValidated)
                return true;

            try
            {
                // These globals are empty while the DLL loads. A real Assassin request is the
                // first lifecycle point that guarantees that Vanilla has prepared the map.
                ValidateCoordinateTileMapping();
                coordinateMapValidated = true;
                coordinateValidationFailureLogged = false;
                return true;
            }
            catch (Exception ex)
            {
                if (!coordinateValidationFailureLogged)
                {
                    coordinateValidationFailureLogged = true;
                    LogWarning($"Assassin coordinate map is not ready or invalid; this map uses Vanilla pathfinding until validation succeeds: {ex.Message}");
                }
                return false;
            }
        }

        private void ResetMapValidation()
        {
            coordinateMapValidated = false;
            coordinateValidationFailureLogged = false;
            fallbackLogged = false;
        }

        private void Touch(int node, int cost, int parent)
        {
            touched[touchedCount++] = node;
            costs[node] = cost;
            parents[node] = parent;
            insertionOrder[node] = nextInsertionOrder++;
        }

        private void ResetTouchedNodes()
        {
            for (int index = 0; index < touchedCount; index++)
            {
                int node = touched[index];
                costs[node] = int.MaxValue;
                parents[node] = -1;
                heapPositions[node] = -1;
            }
            touchedCount = 0;
            heapCount = 0;
            nextInsertionOrder = 0;
        }

        private void Push(int tile)
        {
            int position = heapCount++;
            heap[position] = tile;
            heapPositions[tile] = position;
            SiftUp(position);
        }

        private void PushOrDecrease(int tile)
        {
            int position = heapPositions[tile];
            if (position < 0)
                Push(tile);
            else
                SiftUp(position);
        }

        private int Pop()
        {
            int result = heap[0];
            int tail = heap[--heapCount];
            heapPositions[result] = -1;
            if (heapCount > 0)
            {
                heap[0] = tail;
                heapPositions[tail] = 0;
                SiftDown(0);
            }
            return result;
        }

        private void SiftUp(int position)
        {
            int tile = heap[position];
            while (position > 0)
            {
                int parent = (position - 1) >> 1;
                if (!ComesBefore(tile, heap[parent]))
                    break;
                heap[position] = heap[parent];
                heapPositions[heap[position]] = position;
                position = parent;
            }
            heap[position] = tile;
            heapPositions[tile] = position;
        }

        private void SiftDown(int position)
        {
            int tile = heap[position];
            while (true)
            {
                int left = position * 2 + 1;
                if (left >= heapCount)
                    break;
                int right = left + 1;
                int best = right < heapCount && ComesBefore(heap[right], heap[left]) ? right : left;
                if (!ComesBefore(heap[best], tile))
                    break;
                heap[position] = heap[best];
                heapPositions[heap[position]] = position;
                position = best;
            }
            heap[position] = tile;
            heapPositions[tile] = position;
        }

        private bool ComesBefore(int left, int right)
        {
            return costs[left] < costs[right] ||
                (costs[left] == costs[right] && insertionOrder[left] < insertionOrder[right]);
        }

        private void LogDebug(string message) => log.LogDebug($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogWarning(string message) => log.LogWarning($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogError(string message) => log.LogError($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private static string TimestampNow() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
