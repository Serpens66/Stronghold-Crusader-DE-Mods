using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Windows;

namespace MoveMoatTest
{
    internal sealed unsafe class MoveMoatPathTest : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DetectCompletedMoatModeDelegate(IntPtr unitManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RegionReachabilityDelegate(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int TribeFloodFillMembershipDelegate(
            IntPtr tribeManager, int tribeId, int floodFillStamp);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorReachabilityDelegate(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorSpecialModeDelegate(IntPtr selectionState);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorRegionPrecheckDelegate(IntPtr pathManager, int nativeUnitIndex);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CentralMovementPlanDelegate(
            IntPtr unitManager, int unitId, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathBuilderDelegate(
            IntPtr pathManager, int movementClass, int movementProfile);

        private const int CentralMovementPlanRva = 0x18E1E0;
        private const int TribeFloodFillMembershipRva = 0x124740;
        private const int DetectCompletedMoatModeRva = 0x196840;
        private const int RegionReachabilityRva = 0xE7C40;
        private const int CursorReachabilityRva = 0xE9FF0;
        private const int CursorSpecialModeRva = 0x196870;
        private const int CursorRegionPrecheckRva = 0xE9D90;
        private const int PathBuilderRva = 0xF4930;
        private const int CursorCurrentTileFlagGateRva = 0x8F388;
        private const int CursorCurrentTileFlagGateJumpRva = 0x8F393;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int CursorTargetXRva = 0x3A11E2C;
        private const int CursorTargetYRva = 0x3A11E30;
        private const int PathRegionGridRva = 0x50EC690;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int NativeUnitManagerRva = 0x67E8400;

        private const int MaximumRegionId = short.MaxValue;
        private const int MaximumFloodFillStamp = 0x7D00;
        private const int MaximumCursorDecisionLogs = 32;
        private const int MaximumMovementDecisionLogs = 48;
        private const int MapWidth = 800;
        private const int MapCellCount = MapWidth * MapWidth;
        private const int MoatStateBit = 1 << 20;
        private const uint CompletedMoatTileFlag = 0x40000000;
        private const uint OrdinaryWalkableTileFlag = 0x00008000;

        private const string TribeFloodFillMembershipPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 20 48 63 F2 33 DB 4C 69 CE 88 06 00 00 45 8B F0 48 8B E9";

        private const string CentralMovementPlanPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 38 04 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 20 04 00 00 4C 63 FA " +
            "4C 8D 35 ?? ?? ?? ?? 49 69 DF 90 04 00 00 49 63 E8 48 03 D9 49 63 F1";

        private const string DetectCompletedMoatModePattern =
            "48 63 C2 48 69 D0 90 04 00 00 48 63 84 0A 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 8B 04 81 C1 E8 1E 83 E0 01 C3";

        private const string RegionReachabilityPattern =
            "44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 38 45 33 D2 49 63 F9 4C 89 51 48 48 8B D9";

        private const string PathBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 " +
            "48 63 41 0C 48 8B D9 41 8B F0 44 8B D2";

        private const string CursorReachabilityFunctionPattern =
            "44 89 4C 24 20 44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 50 48 63 F2 45 33 ED 33 D2 49 63 E8 49 63 C1 48 8B D9";

        private const string CursorSpecialModePattern =
            "83 B9 BC 05 00 00 00 74 27 33 C0 48 81 C1 64 05 00 00 48 83 F8 16 " +
            "74 05 83 39 00 75 13 48 FF C0 48 83 C1 04 48 83 F8 23 7C E8 B8 01 00 00 00 C3";

        private const string CursorRegionPrecheckPattern =
            "40 53 55 57 41 54 41 56 48 83 EC 20 FF 41 04 48 8B D9 81 79 04 00 7D 00 00 " +
            "41 BC 01 00 00 00 48 63 FA 7E 1F 44 89 61 04";

        private const string CursorCurrentTileFlagGatePattern =
            "F7 84 97 00 84 89 00 00 01 00 10 74 45 41 8B D6";

        private static readonly byte[] CursorGateJumpOriginal = { 0x74, 0x45 };
        private static readonly byte[] CursorGateJumpReplacement = { 0x90, 0x90 };

        private readonly ManualLogSource log;
        private readonly int* moatPathMode;
        private readonly int* cursorTargetX;
        private readonly int* cursorTargetY;
        private readonly byte* nativeUnitManager;
        private readonly uint* tileFlags;
        private readonly byte* movementTargetAvailability;
        private readonly short* pathRegionGrid;

        [ThreadStatic]
        private static MoveCommandScope activeMoveCommand;
        [ThreadStatic]
        private static PlanScope activePlan;
        [ThreadStatic]
        private static PlanScope pendingPlan;

        private CentralMovementPlanDelegate originalCentralMovementPlan;
        private CentralMovementPlanDelegate rootedCentralMovementPlan;
        private PathBuilderDelegate originalPathBuilder;
        private PathBuilderDelegate rootedPathBuilder;
        private DetectCompletedMoatModeDelegate originalDetectCompletedMoatMode;
        private DetectCompletedMoatModeDelegate rootedDetectCompletedMoatMode;
        private RegionReachabilityDelegate originalRegionReachability;
        private RegionReachabilityDelegate rootedRegionReachability;
        private TribeFloodFillMembershipDelegate originalTribeFloodFillMembership;
        private TribeFloodFillMembershipDelegate rootedTribeFloodFillMembership;
        private CursorReachabilityDelegate originalCursorReachability;
        private CursorReachabilityDelegate rootedCursorReachability;
        private CursorSpecialModeDelegate originalCursorSpecialMode;
        private CursorSpecialModeDelegate rootedCursorSpecialMode;
        private CursorRegionPrecheckDelegate originalCursorRegionPrecheck;
        private CursorRegionPrecheckDelegate rootedCursorRegionPrecheck;

        private NativeDetour centralMovementPlanDetour;
        private NativeDetour pathBuilderDetour;
        private NativeDetour detectCompletedMoatModeDetour;
        private NativeDetour regionReachabilityDetour;
        private NativeDetour tribeFloodFillMembershipDetour;
        private NativeDetour cursorReachabilityDetour;
        private NativeDetour cursorSpecialModeDetour;
        private NativeDetour cursorRegionPrecheckDetour;
        private NativeCodePatch cursorGateJumpPatch;
        private IDisposable tribeMoveSubscription;
        private IDisposable mapLoadSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;

        private int[] visitedWithoutMoat;
        private int[] visitedWithMoat;
        private int[] queue;
        private int gridGeneration;
        private int mapEpoch;
        private int cacheMapEpoch = -1;
        private int cacheUnitIndex = -1;
        private int cacheStartX = -1;
        private int cacheStartY = -1;
        private int cacheTargetRegion = -1;
        private bool cursorChecksArmed;
        private int cursorDecisionLogCount;
        private int movementDecisionLogCount;
        private bool cursorDecisionLogLimitReported;
        private bool movementDecisionLogLimitReported;
        private bool callbackFailureReported;
        private bool disposed;

        public MoveMoatPathTest(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The completed-moat movement test requires the validated CrusaderDE.dll layout.");
            }

            Shared.NativeResolution floodResolution = Resolve(
                memory, TribeFloodFillMembershipPattern, TribeFloodFillMembershipRva,
                "Tribe flood-fill membership helper");
            Shared.NativeResolution planResolution = Resolve(
                memory, CentralMovementPlanPattern, CentralMovementPlanRva,
                "central ordinary-movement planner");
            Shared.NativeResolution modeResolution = Resolve(
                memory, DetectCompletedMoatModePattern, DetectCompletedMoatModeRva,
                "completed-moat path-mode detector");
            Shared.NativeResolution regionResolution = Resolve(
                memory, RegionReachabilityPattern, RegionReachabilityRva,
                "moat-aware region reachability");
            Shared.NativeResolution builderResolution = Resolve(
                memory, PathBuilderPattern, PathBuilderRva,
                "central tile path builder");
            Shared.NativeResolution cursorResolution = Resolve(
                memory, CursorReachabilityFunctionPattern, CursorReachabilityRva,
                "ordinary-movement cursor reachability function");
            Shared.NativeResolution cursorModeResolution = Resolve(
                memory, CursorSpecialModePattern, CursorSpecialModeRva,
                "ordinary-movement cursor selection precheck");
            Shared.NativeResolution cursorRegionResolution = Resolve(
                memory, CursorRegionPrecheckPattern, CursorRegionPrecheckRva,
                "ordinary-movement cursor region precheck");
            Shared.NativeResolution cursorGateResolution = Resolve(
                memory, CursorCurrentTileFlagGatePattern, CursorCurrentTileFlagGateRva,
                "ordinary-movement current-tile cursor gate");

            ValidateExactBytes(
                memory,
                CursorCurrentTileFlagGateRva,
                new byte[] { 0xF7, 0x84, 0x97, 0x00, 0x84, 0x89, 0x00, 0x00, 0x01, 0x00, 0x10 },
                "ordinary-movement current-tile cursor gate");
            ValidateExactBytes(
                memory,
                CursorCurrentTileFlagGateJumpRva,
                CursorGateJumpOriginal,
                "ordinary-movement current-tile cursor-gate jump");

            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            cursorTargetX = (int*)(libraryBase + CursorTargetXRva);
            cursorTargetY = (int*)(libraryBase + CursorTargetYRva);
            nativeUnitManager = (byte*)(libraryBase + NativeUnitManagerRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            pathRegionGrid = (short*)(libraryBase + PathRegionGridRva);
            cursorGateJumpPatch = new NativeCodePatch(
                "ordinary-movement current-tile cursor-gate jump",
                libraryBase + CursorCurrentTileFlagGateJumpRva,
                CursorGateJumpOriginal,
                CursorGateJumpReplacement);

            rootedCentralMovementPlan = RunCentralMovementPlanWithContext;
            rootedPathBuilder = BuildPathWithCompletedMoatRouteVariant;
            rootedTribeFloodFillMembership = AllowTribeFloodFillForMoveOrder;
            rootedDetectCompletedMoatMode = ForceCompletedMoatMode;
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedCursorReachability = AllowCursorReachabilityThroughCompletedMoat;
            rootedCursorSpecialMode = ArmCursorChecksForSelection;
            rootedCursorRegionPrecheck = AllowCursorRegionThroughCompletedMoat;

            NativeDetour pendingPlanDetour = null;
            NativeDetour pendingBuilder = null;
            NativeDetour pendingFlood = null;
            NativeDetour pendingMode = null;
            NativeDetour pendingRegion = null;
            NativeDetour pendingCursor = null;
            NativeDetour pendingCursorMode = null;
            NativeDetour pendingCursorRegion = null;
            bool planApplied = false;
            bool builderApplied = false;
            bool floodApplied = false;
            bool modeApplied = false;
            bool regionApplied = false;
            bool cursorApplied = false;
            bool cursorModeApplied = false;
            bool cursorRegionApplied = false;
            try
            {
                pendingPlanDetour = CreateDetour(
                    libraryBase + unchecked((ulong)planResolution.Rva),
                    rootedCentralMovementPlan);
                originalCentralMovementPlan =
                    pendingPlanDetour.GenerateTrampoline<CentralMovementPlanDelegate>();
                pendingBuilder = CreateDetour(
                    libraryBase + unchecked((ulong)builderResolution.Rva),
                    rootedPathBuilder);
                originalPathBuilder = pendingBuilder.GenerateTrampoline<PathBuilderDelegate>();
                pendingFlood = CreateDetour(libraryBase + unchecked((ulong)floodResolution.Rva), rootedTribeFloodFillMembership);
                originalTribeFloodFillMembership = pendingFlood.GenerateTrampoline<TribeFloodFillMembershipDelegate>();
                pendingMode = CreateDetour(libraryBase + unchecked((ulong)modeResolution.Rva), rootedDetectCompletedMoatMode);
                originalDetectCompletedMoatMode = pendingMode.GenerateTrampoline<DetectCompletedMoatModeDelegate>();
                pendingRegion = CreateDetour(libraryBase + unchecked((ulong)regionResolution.Rva), rootedRegionReachability);
                originalRegionReachability = pendingRegion.GenerateTrampoline<RegionReachabilityDelegate>();
                pendingCursor = CreateDetour(libraryBase + unchecked((ulong)cursorResolution.Rva), rootedCursorReachability);
                originalCursorReachability = pendingCursor.GenerateTrampoline<CursorReachabilityDelegate>();
                pendingCursorMode = CreateDetour(libraryBase + unchecked((ulong)cursorModeResolution.Rva), rootedCursorSpecialMode);
                originalCursorSpecialMode = pendingCursorMode.GenerateTrampoline<CursorSpecialModeDelegate>();
                pendingCursorRegion = CreateDetour(libraryBase + unchecked((ulong)cursorRegionResolution.Rva), rootedCursorRegionPrecheck);
                originalCursorRegionPrecheck = pendingCursorRegion.GenerateTrampoline<CursorRegionPrecheckDelegate>();

                pendingPlanDetour.Apply();
                planApplied = true;
                pendingBuilder.Apply();
                builderApplied = true;
                pendingFlood.Apply();
                floodApplied = true;
                pendingMode.Apply();
                modeApplied = true;
                pendingRegion.Apply();
                regionApplied = true;
                pendingCursor.Apply();
                cursorApplied = true;
                pendingCursorMode.Apply();
                cursorModeApplied = true;
                pendingCursorRegion.Apply();
                cursorRegionApplied = true;

                centralMovementPlanDetour = pendingPlanDetour;
                pathBuilderDetour = pendingBuilder;
                tribeFloodFillMembershipDetour = pendingFlood;
                detectCompletedMoatModeDetour = pendingMode;
                regionReachabilityDetour = pendingRegion;
                cursorReachabilityDetour = pendingCursor;
                cursorSpecialModeDetour = pendingCursorMode;
                cursorRegionPrecheckDetour = pendingCursorRegion;

                tribeMoveSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(ObserveTribeMoveOrder);
                mapLoadSubscription = MapLoaderR3EventHooks.OnLoadMap.Observable.Subscribe(_ => ResetMapState());
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(_ => ResetMapState());
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => ResetMapState());

                // Vanilla skips both real cursor reachability functions for ordinary ground.
                // Falling through is constrained by the conservative completed-moat route test.
                cursorGateJumpPatch.Apply();

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Move Moat Test final candidate installed: " +
                    $"cursorGate=0x{cursorGateResolution.Rva:X}/jump=0x{CursorCurrentTileFlagGateJumpRva:X}, " +
                    $"cursorRegion=0x{cursorRegionResolution.Rva:X}, cursorDirect=0x{cursorResolution.Rva:X}, " +
                    $"plan=0x{planResolution.Rva:X}, mode=0x{modeResolution.Rva:X}, " +
                    $"region=0x{regionResolution.Rva:X}, builder=0x{builderResolution.Rva:X}, " +
                    $"tribeFloodFill=0x{floodResolution.Rva:X}; allCompletedMoats=true, ownerFiltering=false.");
            }
            catch
            {
                TryRestoreCursorPatch();
                tribeMoveSubscription?.Dispose();
                mapLoadSubscription?.Dispose();
                mapStartSubscription?.Dispose();
                mapUnloadSubscription?.Dispose();
                UndoAndDispose(pendingCursorRegion, cursorRegionApplied);
                UndoAndDispose(pendingCursorMode, cursorModeApplied);
                UndoAndDispose(pendingCursor, cursorApplied);
                UndoAndDispose(pendingRegion, regionApplied);
                UndoAndDispose(pendingMode, modeApplied);
                UndoAndDispose(pendingFlood, floodApplied);
                UndoAndDispose(pendingBuilder, builderApplied);
                UndoAndDispose(pendingPlanDetour, planApplied);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            TryRestoreCursorPatch();
            tribeMoveSubscription?.Dispose();
            mapLoadSubscription?.Dispose();
            mapStartSubscription?.Dispose();
            mapUnloadSubscription?.Dispose();
            cursorRegionPrecheckDetour?.Dispose();
            cursorSpecialModeDetour?.Dispose();
            cursorReachabilityDetour?.Dispose();
            regionReachabilityDetour?.Dispose();
            detectCompletedMoatModeDetour?.Dispose();
            tribeFloodFillMembershipDetour?.Dispose();
            pathBuilderDetour?.Dispose();
            centralMovementPlanDetour?.Dispose();
            activeMoveCommand = null;
            activePlan = null;
            pendingPlan = null;
        }

        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (disposed)
                return;

            if (args.Phase == EventHookPhase.Pre)
                activeMoveCommand = new MoveCommandScope(args.TribeId);
            else if (args.Phase == EventHookPhase.Post)
            {
                activeMoveCommand = null;
                activePlan = null;
                pendingPlan = null;
            }
        }

        private int RunCentralMovementPlanWithContext(
            IntPtr unitManager, int unitId, int targetX, int targetY)
        {
            if (disposed || activeMoveCommand == null || unitManager == IntPtr.Zero || unitId <= 0)
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);

            PlanScope previous = activePlan;
            PlanScope plan = new PlanScope(unitId);
            activePlan = plan;
            try
            {
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
            }
            finally
            {
                activePlan = previous;
                if (ReferenceEquals(pendingPlan, plan))
                    pendingPlan = null;
            }
        }

        private int ForceCompletedMoatMode(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalDetectCompletedMoatMode(unitManager, unitId);
            if (disposed || activeMoveCommand == null || unitManager == IntPtr.Zero || unitId <= 0)
                return vanillaResult;

            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
                    return vanillaResult;

                PlanScope plan = activePlan;
                if (plan == null)
                {
                    plan = new PlanScope(unitId);
                    pendingPlan = plan;
                }
                plan.VanillaModeDetected = vanillaResult != 0;
                if (vanillaResult == 0)
                    LogMovementDecision($"stage=mode unit={unitId} vanilla=0 effective=1");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("mode", ex);
                return vanillaResult;
            }
        }

        private int AllowBuilderAfterFailedRegionSearch(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY)
        {
            int vanillaResult = originalRegionReachability(pathManager, movementClass, targetRegion, startX, startY);
            if (disposed || activeMoveCommand == null)
                return vanillaResult;

            try
            {
                bool bypass = vanillaResult == 0 && *moatPathMode == 1 &&
                    targetRegion > 0 && targetRegion <= MaximumRegionId;
                if (!bypass)
                    return vanillaResult;

                LogMovementDecision(
                    $"stage=region movementClass={movementClass} start=({startX},{startY}) " +
                    $"targetRegion={targetRegion} vanilla=0 effective={targetRegion}");
                return targetRegion;
            }
            catch (Exception ex)
            {
                LogFailure("region", ex);
                return vanillaResult;
            }
        }

        private int AllowTribeFloodFillForMoveOrder(IntPtr tribeManager, int tribeId, int floodFillStamp)
        {
            int vanillaResult = originalTribeFloodFillMembership(tribeManager, tribeId, floodFillStamp);
            if (disposed)
                return vanillaResult;

            try
            {
                MoveCommandScope command = activeMoveCommand;
                bool bypass = vanillaResult == 0 && command != null && tribeManager != IntPtr.Zero &&
                    tribeId == command.TribeId && floodFillStamp > 0 && floodFillStamp <= MaximumFloodFillStamp;
                if (!bypass)
                    return vanillaResult;

                command.FloodFillBypasses++;
                LogMovementDecision($"stage=tribe-flood-fill tribe={tribeId} stamp={floodFillStamp} vanilla=0 effective=1");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("tribe-flood-fill", ex);
                return vanillaResult;
            }
        }

        private int BuildPathWithCompletedMoatRouteVariant(
            IntPtr pathManager, int movementClass, int movementProfile)
        {
            MoveCommandScope command = activeMoveCommand;
            PlanScope plan = activePlan ?? pendingPlan;
            if (disposed || pathManager == IntPtr.Zero || command == null || plan == null ||
                plan.VanillaModeDetected || command.FloodFillBypasses <= 0 || *moatPathMode != 1)
            {
                return originalPathBuilder(pathManager, movementClass, movementProfile);
            }

            int* routeVariant = (int*)((byte*)pathManager.ToPointer() + 0x80);
            int originalRouteVariant = *routeVariant;
            bool overrideApplied = originalRouteVariant == 1;
            if (overrideApplied)
                *routeVariant = 0;

            int result;
            try
            {
                result = originalPathBuilder(pathManager, movementClass, movementProfile);
            }
            catch
            {
                if (overrideApplied)
                    *routeVariant = originalRouteVariant;
                throw;
            }

            bool retained = overrideApplied && result > 0;
            if (overrideApplied && !retained)
                *routeVariant = originalRouteVariant;

            if (overrideApplied)
            {
                try
                {
                    LogMovementDecision(
                        $"stage=builder-route80 unit={plan.UnitId} movementClass={movementClass} " +
                        $"movementProfile={movementProfile} original=1 " +
                        $"effective={(retained ? 0 : originalRouteVariant)} " +
                        $"result={result} retained={retained}");
                }
                catch
                {
                    // Logging must never change a successfully produced native path.
                }
            }

            return result;
        }

        private int ArmCursorChecksForSelection(IntPtr selectionState)
        {
            int vanillaResult = originalCursorSpecialMode(selectionState);
            if (disposed || selectionState == IntPtr.Zero)
                return vanillaResult;

            try
            {
                byte* state = (byte*)selectionState.ToPointer();
                int* slots = (int*)(state + 0x564);
                for (int index = 0; index < 35; index++)
                {
                    if (index != 22 && slots[index] != 0)
                    {
                        cursorChecksArmed = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                cursorChecksArmed = false;
                LogFailure("cursor-selection", ex);
            }

            return vanillaResult;
        }

        private int AllowCursorRegionThroughCompletedMoat(IntPtr pathManager, int nativeUnitIndex)
        {
            int vanillaResult = originalCursorRegionPrecheck(pathManager, nativeUnitIndex);
            if (disposed || vanillaResult != 0)
                return vanillaResult;

            try
            {
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                if (!HasConservativeCompletedMoatRoute(nativeUnitIndex, targetX, targetY))
                    return vanillaResult;

                LogCursorDecision($"stage=cursor-region unitIndex={nativeUnitIndex} target=({targetX},{targetY}) vanilla=0 effective=1");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("cursor-region", ex);
                return vanillaResult;
            }
        }

        private int AllowCursorReachabilityThroughCompletedMoat(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY)
        {
            int vanillaResult = originalCursorReachability(pathManager, nativeUnitIndex, targetX, targetY);
            if (disposed || vanillaResult != 0)
                return vanillaResult;

            try
            {
                if (!HasConservativeCompletedMoatRoute(nativeUnitIndex, targetX, targetY))
                    return vanillaResult;

                LogCursorDecision($"stage=cursor-direct unitIndex={nativeUnitIndex} target=({targetX},{targetY}) vanilla=0 effective=1");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("cursor-direct", ex);
                return vanillaResult;
            }
        }

        private bool HasConservativeCompletedMoatRoute(int nativeUnitIndex, int targetX, int targetY)
        {
            if (!cursorChecksArmed || targetX < 0 || targetX >= MapWidth ||
                targetY < 0 || targetY >= MapWidth ||
                movementTargetAvailability[(targetY * MapWidth) + targetX] == 0)
            {
                return false;
            }

            int nextUnitId = *(int*)nativeUnitManager;
            if (nativeUnitIndex <= 0 || nativeUnitIndex >= nextUnitId)
                return false;

            byte* nativeUnit = nativeUnitManager + (nativeUnitIndex * 0x490);
            int startX = *(ushort*)(nativeUnit + 0x71C);
            int startY = *(ushort*)(nativeUnit + 0x71E);
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;

            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            if (!IsValidTileId(targetTileId))
                return false;

            int targetRegion = pathRegionGrid[targetTileId];
            if (targetRegion <= 0 || targetRegion > MaximumRegionId)
                return false;

            EnsureReachabilityMap(nativeUnitIndex, startX, startY, targetRegion);
            return visitedWithMoat[(targetY * MapWidth) + targetX] == gridGeneration;
        }

        private void EnsureReachabilityMap(int nativeUnitIndex, int startX, int startY, int targetRegion)
        {
            if (visitedWithoutMoat != null && cacheMapEpoch == mapEpoch &&
                cacheUnitIndex == nativeUnitIndex && cacheStartX == startX &&
                cacheStartY == startY && cacheTargetRegion == targetRegion)
            {
                return;
            }

            if (visitedWithoutMoat == null)
            {
                visitedWithoutMoat = new int[MapCellCount];
                visitedWithMoat = new int[MapCellCount];
                queue = new int[MapCellCount * 2];
            }

            if (gridGeneration == int.MaxValue)
            {
                Array.Clear(visitedWithoutMoat, 0, visitedWithoutMoat.Length);
                Array.Clear(visitedWithMoat, 0, visitedWithMoat.Length);
                gridGeneration = 1;
            }
            else
            {
                gridGeneration++;
            }

            cacheMapEpoch = mapEpoch;
            cacheUnitIndex = nativeUnitIndex;
            cacheStartX = startX;
            cacheStartY = startY;
            cacheTargetRegion = targetRegion;

            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId))
                return;

            int startRegion = pathRegionGrid[startTileId];
            int startCell = (startY * MapWidth) + startX;
            bool startIsMoat = (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
            if (startIsMoat)
                visitedWithMoat[startCell] = gridGeneration;
            else
                visitedWithoutMoat[startCell] = gridGeneration;

            int head = 0;
            int tail = 0;
            queue[tail++] = startCell | (startIsMoat ? MoatStateBit : 0);
            while (head < tail)
            {
                int encoded = queue[head++];
                bool usedMoat = (encoded & MoatStateBit) != 0;
                int cell = encoded & (MoatStateBit - 1);
                int y = cell / MapWidth;
                int x = cell - (y * MapWidth);

                VisitNeighbour(x - 1, y, usedMoat, startRegion, targetRegion, ref tail);
                VisitNeighbour(x + 1, y, usedMoat, startRegion, targetRegion, ref tail);
                VisitNeighbour(x, y - 1, usedMoat, startRegion, targetRegion, ref tail);
                VisitNeighbour(x, y + 1, usedMoat, startRegion, targetRegion, ref tail);
            }
        }

        private void VisitNeighbour(
            int x, int y, bool usedMoat, int startRegion, int targetRegion, ref int queueTail)
        {
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth)
                return;

            int cell = (y * MapWidth) + x;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(tileId))
                return;

            uint flags = tileFlags[tileId];
            bool isMoat = (flags & CompletedMoatTileFlag) != 0;
            if (!isMoat && ((flags & OrdinaryWalkableTileFlag) == 0 || movementTargetAvailability[cell] == 0))
                return;

            if (!isMoat)
            {
                int region = pathRegionGrid[tileId];
                if ((!usedMoat && region != startRegion) ||
                    (usedMoat && region != startRegion && region != targetRegion))
                {
                    return;
                }
            }

            bool nextUsedMoat = usedMoat || isMoat;
            int[] visited = nextUsedMoat ? visitedWithMoat : visitedWithoutMoat;
            if (visited[cell] == gridGeneration)
                return;

            visited[cell] = gridGeneration;
            queue[queueTail++] = cell | (nextUsedMoat ? MoatStateBit : 0);
        }

        private void ResetMapState()
        {
            mapEpoch++;
            cacheMapEpoch = -1;
            cacheUnitIndex = -1;
            cacheStartX = -1;
            cacheStartY = -1;
            cacheTargetRegion = -1;
            cursorChecksArmed = false;
            activeMoveCommand = null;
            activePlan = null;
            pendingPlan = null;
        }

        private void LogCursorDecision(string message)
        {
            if (cursorDecisionLogCount < MaximumCursorDecisionLogs)
            {
                cursorDecisionLogCount++;
                Shared.DebugLogHelper.LogInfo(log, $"MoveMoat {message}.");
                return;
            }

            if (cursorDecisionLogLimitReported)
                return;
            cursorDecisionLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat cursor diagnostics reached their {MaximumCursorDecisionLogs}-entry limit.");
        }

        private void LogMovementDecision(string message)
        {
            if (movementDecisionLogCount < MaximumMovementDecisionLogs)
            {
                movementDecisionLogCount++;
                Shared.DebugLogHelper.LogInfo(log, $"MoveMoat {message}.");
                return;
            }

            if (movementDecisionLogLimitReported)
                return;
            movementDecisionLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat movement diagnostics reached their {MaximumMovementDecisionLogs}-entry limit.");
        }

        private void LogFailure(string stage, Exception ex)
        {
            if (callbackFailureReported)
                return;
            callbackFailureReported = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"MoveMoat {stage} callback failed once; Vanilla behavior remains active: {ex}");
        }

        private void TryRestoreCursorPatch()
        {
            NativeCodePatch patch = cursorGateJumpPatch;
            if (patch == null)
                return;

            try
            {
                patch.Restore();
                cursorGateJumpPatch = null;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"MoveMoat could not restore cursor patch: {ex}");
            }
        }

        private Shared.NativeResolution Resolve(
            ReadOnlySpan<byte> memory, string pattern, int expectedRva, string label)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                expectedRva,
                referenceHashMatches: true,
                name: label,
                log: null);
            if (resolution.Rva != expectedRva)
            {
                throw new InvalidOperationException(
                    $"The native {label} resolved to 0x{resolution.Rva:X} instead of 0x{expectedRva:X}.");
            }
            return resolution;
        }

        private static void ValidateExactBytes(
            ReadOnlySpan<byte> memory, int rva, byte[] expected, string label)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"The validated instruction bytes for {label} did not match CrusaderDE.dll.");
            }
        }

        private static bool IsValidTileId(int tileId) => tileId >= 0 && tileId < 320800;

        private static NativeDetour CreateDetour<TDelegate>(ulong targetAddress, TDelegate callback)
            where TDelegate : Delegate =>
            new NativeDetour(
                (IntPtr)unchecked((long)targetAddress),
                Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });

        private static void UndoAndDispose(NativeDetour detour, bool applied)
        {
            if (applied)
                detour?.Undo();
            detour?.Dispose();
        }

        private sealed class MoveCommandScope
        {
            public MoveCommandScope(int tribeId)
            {
                TribeId = tribeId;
            }

            public int TribeId { get; }
            public int FloodFillBypasses { get; set; }
        }

        private sealed class PlanScope
        {
            public PlanScope(int unitId)
            {
                UnitId = unitId;
            }

            public int UnitId { get; }
            public bool VanillaModeDetected { get; set; }
        }

        private sealed class NativeCodePatch
        {
            private readonly string label;
            private readonly IntPtr address;
            private readonly byte[] originalBytes;
            private readonly byte[] replacementBytes;

            public NativeCodePatch(string label, ulong address, byte[] originalBytes, byte[] replacementBytes)
            {
                this.label = label ?? throw new ArgumentNullException(nameof(label));
                this.address = unchecked((IntPtr)(long)address);
                this.originalBytes = (byte[])originalBytes.Clone();
                this.replacementBytes = (byte[])replacementBytes.Clone();
                if (this.originalBytes.Length == 0 ||
                    this.originalBytes.Length != this.replacementBytes.Length)
                {
                    throw new ArgumentException("Native patch byte arrays must have equal non-zero lengths.");
                }

                VerifyCurrentBytes(this.originalBytes, "initialize");
            }

            public void Apply()
            {
                VerifyCurrentBytes(originalBytes, "apply");
                WriteBytes(replacementBytes);
            }

            public void Restore()
            {
                byte[] current = ReadBytes(originalBytes.Length);
                if (current.AsSpan().SequenceEqual(originalBytes))
                    return;
                if (!current.AsSpan().SequenceEqual(replacementBytes))
                {
                    throw new InvalidOperationException(
                        $"Cannot restore native patch '{label}' because its bytes changed.");
                }
                WriteBytes(originalBytes);
            }

            private byte[] ReadBytes(int length)
            {
                byte[] bytes = new byte[length];
                Marshal.Copy(address, bytes, 0, length);
                return bytes;
            }

            private void VerifyCurrentBytes(byte[] expected, string operation)
            {
                byte[] current = ReadBytes(expected.Length);
                if (!current.AsSpan().SequenceEqual(expected))
                {
                    throw new InvalidOperationException(
                        $"Cannot {operation} native patch '{label}' because its bytes do not match.");
                }
            }

            private void WriteBytes(byte[] bytes)
            {
                UIntPtr size = unchecked((UIntPtr)(uint)bytes.Length);
                if (!Kernel32.VirtualProtect(
                        address,
                        size,
                        Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                        out Kernel32.MemoryPermissions oldProtection))
                {
                    throw new InvalidOperationException($"VirtualProtect failed for native patch '{label}'.");
                }

                try
                {
                    Marshal.Copy(bytes, 0, address, bytes.Length);
                }
                finally
                {
                    if (!Kernel32.VirtualProtect(address, size, oldProtection, out _))
                    {
                        throw new InvalidOperationException(
                            $"Restoring memory protection failed for native patch '{label}'.");
                    }
                }

                if (!MinWinAPI.FlushInstructionCache(Process.GetCurrentProcess().Handle, address, size))
                {
                    throw new InvalidOperationException(
                        $"Flushing the instruction cache failed for native patch '{label}'.");
                }

                VerifyCurrentBytes(bytes, "verify");
            }
        }
    }
}
