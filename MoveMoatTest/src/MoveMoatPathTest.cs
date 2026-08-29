using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace MoveMoatTest
{
    internal sealed unsafe class MoveMoatPathTest : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DetectCompletedMoatModeDelegate(IntPtr unitManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RegionReachabilityDelegate(
            IntPtr pathManager,
            int movementClass,
            int targetRegion,
            int startX,
            int startY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathBuilderDelegate(
            IntPtr pathManager,
            int movementClass,
            int movementProfile);

        private const int DetectCompletedMoatModeRva = 0x196840;
        private const int RegionReachabilityRva = 0xE7C40;
        private const int PathBuilderRva = 0xF4930;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int PathStartXRva = 0x60AD668;
        private const int PathStartYRva = 0x60AD66C;
        private const int PathTargetXRva = 0x60AD670;
        private const int PathTargetYRva = 0x60AD674;
        private const int MaximumRegionId = short.MaxValue;
        private const int MaximumModeLogs = 24;
        private const int MaximumReachabilityLogs = 96;
        private const int MaximumBuilderLogs = 96;

        private const string DetectCompletedMoatModePattern =
            "48 63 C2 48 69 D0 90 04 00 00 48 63 84 0A 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 8B 04 81 C1 E8 1E 83 E0 01 C3";

        private const string RegionReachabilityPattern =
            "44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 38 45 33 D2 49 63 F9 4C 89 51 48 48 8B D9";

        private const string PathBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 " +
            "48 63 41 0C 48 8B D9 41 8B F0 44 8B D2";

        private readonly ManualLogSource log;
        private readonly int* moatPathMode;
        private readonly int* pathStartX;
        private readonly int* pathStartY;
        private readonly int* pathTargetX;
        private readonly int* pathTargetY;
        private DetectCompletedMoatModeDelegate originalDetectCompletedMoatMode;
        private DetectCompletedMoatModeDelegate rootedDetectCompletedMoatMode;
        private RegionReachabilityDelegate originalRegionReachability;
        private RegionReachabilityDelegate rootedRegionReachability;
        private PathBuilderDelegate originalPathBuilder;
        private PathBuilderDelegate rootedPathBuilder;
        private NativeDetour detectCompletedMoatModeDetour;
        private NativeDetour regionReachabilityDetour;
        private NativeDetour pathBuilderDetour;
        private int modeLogCount;
        private int reachabilityLogCount;
        private int builderLogCount;
        private bool modeLogLimitReported;
        private bool reachabilityLogLimitReported;
        private bool builderLogLimitReported;
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
                    "The central moat-path test requires the validated CrusaderDE.dll layout.");
            }

            Shared.NativeResolution modeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DetectCompletedMoatModePattern,
                DetectCompletedMoatModeRva,
                referenceHashMatches,
                "completed-moat path-mode detector",
                log: null);
            Shared.NativeResolution reachabilityResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                RegionReachabilityPattern,
                RegionReachabilityRva,
                referenceHashMatches,
                "moat-aware region reachability",
                log: null);
            Shared.NativeResolution builderResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                PathBuilderPattern,
                PathBuilderRva,
                referenceHashMatches,
                "central tile path builder",
                log: null);

            RequireValidatedRva(modeResolution, DetectCompletedMoatModeRva, "completed-moat path-mode detector");
            RequireValidatedRva(reachabilityResolution, RegionReachabilityRva, "moat-aware region reachability");
            RequireValidatedRva(builderResolution, PathBuilderRva, "central tile path builder");
            ValidatePatternSpans(memory);

            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            pathStartX = (int*)(libraryBase + PathStartXRva);
            pathStartY = (int*)(libraryBase + PathStartYRva);
            pathTargetX = (int*)(libraryBase + PathTargetXRva);
            pathTargetY = (int*)(libraryBase + PathTargetYRva);

            rootedDetectCompletedMoatMode = ForceCompletedMoatMode;
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedPathBuilder = ObservePathBuilder;

            NativeDetour pendingModeDetour = null;
            NativeDetour pendingReachabilityDetour = null;
            NativeDetour pendingBuilderDetour = null;
            bool modeApplied = false;
            bool reachabilityApplied = false;
            bool builderApplied = false;
            try
            {
                pendingModeDetour = CreateDetour(
                    libraryBase + unchecked((ulong)modeResolution.Rva),
                    rootedDetectCompletedMoatMode);
                originalDetectCompletedMoatMode =
                    pendingModeDetour.GenerateTrampoline<DetectCompletedMoatModeDelegate>();

                pendingReachabilityDetour = CreateDetour(
                    libraryBase + unchecked((ulong)reachabilityResolution.Rva),
                    rootedRegionReachability);
                originalRegionReachability =
                    pendingReachabilityDetour.GenerateTrampoline<RegionReachabilityDelegate>();

                pendingBuilderDetour = CreateDetour(
                    libraryBase + unchecked((ulong)builderResolution.Rva),
                    rootedPathBuilder);
                originalPathBuilder = pendingBuilderDetour.GenerateTrampoline<PathBuilderDelegate>();

                pendingModeDetour.Apply();
                modeApplied = true;
                pendingReachabilityDetour.Apply();
                reachabilityApplied = true;
                pendingBuilderDetour.Apply();
                builderApplied = true;

                detectCompletedMoatModeDetour = pendingModeDetour;
                regionReachabilityDetour = pendingReachabilityDetour;
                pathBuilderDetour = pendingBuilderDetour;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Move Moat Test installed: modeRva=0x{modeResolution.Rva:X}/method={modeResolution.Method}, " +
                    $"reachabilityRva=0x{reachabilityResolution.Rva:X}/method={reachabilityResolution.Method}, " +
                    $"builderRva=0x{builderResolution.Rva:X}/method={builderResolution.Method}; " +
                    "allCompletedMoats=true, ownerFiltering=false, realBuilderResultUnchanged=true.");
            }
            catch
            {
                if (builderApplied)
                    pendingBuilderDetour?.Undo();
                pendingBuilderDetour?.Dispose();
                if (reachabilityApplied)
                    pendingReachabilityDetour?.Undo();
                pendingReachabilityDetour?.Dispose();
                if (modeApplied)
                    pendingModeDetour?.Undo();
                pendingModeDetour?.Dispose();
                originalDetectCompletedMoatMode = null;
                originalRegionReachability = null;
                originalPathBuilder = null;
                rootedDetectCompletedMoatMode = null;
                rootedRegionReachability = null;
                rootedPathBuilder = null;
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            pathBuilderDetour?.Dispose();
            regionReachabilityDetour?.Dispose();
            detectCompletedMoatModeDetour?.Dispose();
            pathBuilderDetour = null;
            regionReachabilityDetour = null;
            detectCompletedMoatModeDetour = null;
            originalPathBuilder = null;
            originalRegionReachability = null;
            originalDetectCompletedMoatMode = null;
            rootedPathBuilder = null;
            rootedRegionReachability = null;
            rootedDetectCompletedMoatMode = null;
        }

        private int ForceCompletedMoatMode(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalDetectCompletedMoatMode(unitManager, unitId);
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return vanillaResult;

            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null)
                {
                    return vanillaResult;
                }

                LogModeActivation(unitId, unit, vanillaResult);
                return 1;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test mode callback failed; Vanilla result {vanillaResult} remains active: {ex}");
                return vanillaResult;
            }
        }

        private int AllowBuilderAfterFailedRegionSearch(
            IntPtr pathManager,
            int movementClass,
            int targetRegion,
            int startX,
            int startY)
        {
            int vanillaResult = originalRegionReachability(
                pathManager,
                movementClass,
                targetRegion,
                startX,
                startY);
            int effectiveResult = vanillaResult;

            try
            {
                bool bypassApplied = !disposed &&
                    vanillaResult == 0 &&
                    *moatPathMode == 1 &&
                    targetRegion > 0 &&
                    targetRegion <= MaximumRegionId;
                if (bypassApplied)
                    effectiveResult = targetRegion;

                LogReachability(
                    movementClass,
                    targetRegion,
                    startX,
                    startY,
                    vanillaResult,
                    effectiveResult,
                    bypassApplied);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test reachability callback failed; Vanilla result {vanillaResult} remains active: {ex}");
                return vanillaResult;
            }

            return effectiveResult;
        }

        private int ObservePathBuilder(
            IntPtr pathManager,
            int movementClass,
            int movementProfile)
        {
            int result = originalPathBuilder(pathManager, movementClass, movementProfile);
            try
            {
                if (!disposed && *moatPathMode == 1)
                    LogBuilderResult(movementClass, movementProfile, result);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test builder observer failed; real builder result {result} remains unchanged: {ex}");
            }

            return result;
        }

        private void LogModeActivation(int unitId, GameUnit* unit, int vanillaResult)
        {
            if (modeLogCount < MaximumModeLogs)
            {
                modeLogCount++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=mode unit={unitId} player={unit->r_ControllableForPlayerId} " +
                    $"unitType={(int)unit->r_UnitChimp} tile=({unit->r_CurrentTilePositionX}," +
                    $"{unit->r_CurrentTilePositionY}) target=({unit->r_TargetTilePositionX}," +
                    $"{unit->r_TargetTilePositionY}) vanilla={vanillaResult} effective=1.");
                return;
            }

            if (modeLogLimitReported)
                return;
            modeLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat mode diagnostics reached their {MaximumModeLogs}-entry limit.");
        }

        private void LogReachability(
            int movementClass,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            int effectiveResult,
            bool bypassApplied)
        {
            if (reachabilityLogCount < MaximumReachabilityLogs)
            {
                reachabilityLogCount++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=region movementClass={movementClass} start=({startX},{startY}) " +
                    $"targetRegion={targetRegion} vanilla={vanillaResult} effective={effectiveResult} " +
                    $"bypass={bypassApplied} pathMode={*moatPathMode}.");
                return;
            }

            if (reachabilityLogLimitReported)
                return;
            reachabilityLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat region diagnostics reached their {MaximumReachabilityLogs}-entry limit.");
        }

        private void LogBuilderResult(int movementClass, int movementProfile, int result)
        {
            if (builderLogCount < MaximumBuilderLogs)
            {
                builderLogCount++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=builder movementClass={movementClass} movementProfile={movementProfile} " +
                    $"start=({*pathStartX},{*pathStartY}) target=({*pathTargetX},{*pathTargetY}) " +
                    $"result={result} accepted={result > 0} pathMode={*moatPathMode}.");
                return;
            }

            if (builderLogLimitReported)
                return;
            builderLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat builder diagnostics reached their {MaximumBuilderLogs}-entry limit.");
        }

        private static NativeDetour CreateDetour<TDelegate>(ulong targetAddress, TDelegate callback)
            where TDelegate : Delegate =>
            new NativeDetour(
                (IntPtr)unchecked((long)targetAddress),
                Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });

        private static void RequireValidatedRva(
            Shared.NativeResolution resolution,
            int expectedRva,
            string label)
        {
            if (resolution.Rva != expectedRva)
            {
                throw new InvalidOperationException(
                    $"The native {label} resolved to 0x{resolution.Rva:X} instead of validated RVA 0x{expectedRva:X}.");
            }
        }

        private static void ValidatePatternSpans(ReadOnlySpan<byte> memory)
        {
            ValidatePatternSpan(
                memory,
                DetectCompletedMoatModeRva,
                DetectCompletedMoatModePattern,
                "completed-moat path-mode detector");
            ValidatePatternSpan(
                memory,
                RegionReachabilityRva,
                RegionReachabilityPattern,
                "moat-aware region reachability");
            ValidatePatternSpan(
                memory,
                PathBuilderRva,
                PathBuilderPattern,
                "central tile path builder");
        }

        private static void ValidatePatternSpan(
            ReadOnlySpan<byte> memory,
            int rva,
            string pattern,
            string label)
        {
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, rva, pattern))
            {
                throw new InvalidOperationException(
                    $"The complete validated pattern span for {label} did not match CrusaderDE.dll.");
            }
        }
    }
}
