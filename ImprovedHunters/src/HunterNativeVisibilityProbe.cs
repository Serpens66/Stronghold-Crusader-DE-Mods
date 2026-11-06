using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using RedBird.Core.Memory;

namespace ImprovedHunters
{
    internal readonly struct HunterNearVisibilityGeometry
    {
        public readonly int HunterTileX;
        public readonly int HunterTileY;
        public readonly int PreyTileX;
        public readonly int PreyTileY;
        public readonly int HunterWorldX;
        public readonly int HunterWorldY;
        public readonly int HunterHeight;
        public readonly int PreyWorldX;
        public readonly int PreyWorldY;
        public readonly int PreyHeight;

        public HunterNearVisibilityGeometry(
            int hunterTileX,
            int hunterTileY,
            int preyTileX,
            int preyTileY,
            int hunterWorldX,
            int hunterWorldY,
            int hunterHeight,
            int preyWorldX,
            int preyWorldY,
            int preyHeight)
        {
            HunterTileX = hunterTileX;
            HunterTileY = hunterTileY;
            PreyTileX = preyTileX;
            PreyTileY = preyTileY;
            HunterWorldX = hunterWorldX;
            HunterWorldY = hunterWorldY;
            HunterHeight = hunterHeight;
            PreyWorldX = preyWorldX;
            PreyWorldY = preyWorldY;
            PreyHeight = preyHeight;
        }

        public int TileManhattanDistance =>
            Math.Abs(PreyTileX - HunterTileX) + Math.Abs(PreyTileY - HunterTileY);

        public int WorldChebyshevDistance =>
            Math.Max(Math.Abs(PreyWorldX - HunterWorldX), Math.Abs(PreyWorldY - HunterWorldY));
    }

    /// <summary>
    /// Temporary, behavior-neutral runtime validation of Vanilla's native Hunter
    /// visibility helper. It installs no hook and mutates neither units nor orders.
    /// Keep this file separate so the complete probe can be removed after sign-off.
    /// </summary>
    internal sealed unsafe class HunterNativeVisibilityProbe : IDisposable
    {
        private const string ReferenceDllSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int VisibilityWrapperRva = 0xA06F0;
        private const int VisibilityCoreRva = 0x9E350;
        private const int HunterQueryCallRva = 0x18B0A2;
        private const int HunterDirectOrderCallRva = 0x18ED6A;
        private const int FirstCoreCallDisplacementRva = VisibilityWrapperRva + 0x54;
        private const int FirstCoreCallNextInstructionRva = VisibilityWrapperRva + 0x58;
        private const int ReverseCoreCallDisplacementRva = VisibilityWrapperRva + 0x79;
        private const int ReverseCoreCallNextInstructionRva = VisibilityWrapperRva + 0x7D;

        private const string WrapperEntryPattern =
            "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 54 41 56 41 57 48 83 EC 40";
        private const string CoreEntryPattern =
            "44 89 44 24 18 89 54 24 10 48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 68";
        private const string FirstCoreCallPattern =
            "C7 40 E0 00 00 00 00 48 8B E9 44 89 70 D8 44 89 78 D0 44 89 60 C8 E8 ? ? ? ? 85 C0 75 21";
        private const string ReverseCoreCallPattern =
            "89 44 24 38 45 8B CE 89 5C 24 30 45 8B C7 89 7C 24 28 41 8B D4 48 8B CD 89 74 24 20 E8 ? ? ? ?";
        private const string HunterQueryCallPattern =
            "E8 ? ? ? ? FF C8 3D AF 01 00 00 77 1F";
        private const string HunterDirectOrderCallPattern =
            "E8 ? ? ? ? 8B D0 85 C0 0F 8E EB 00 00 00";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NativeVisibilityDelegate(
            IntPtr context,
            int startX,
            int startY,
            int startHeight,
            int endX,
            int endY,
            int endHeight);

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private NativeVisibilityDelegate visibility;
        private NativeVisibilityDelegate visibilityCore;
        private int probeInProgress;
        private bool reentrancyLogged;
        private bool captureFailureLogged;
        private bool invocationFailureLogged;
        private bool disabled;
        private bool disposed;

        public HunterNativeVisibilityProbe(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters native visibility probe unavailable: " +
                    $"DLL hash differs from audited SHA-256 {ReferenceDllSha256}; behavior remains unchanged.");
                return;
            }

            ValidateNativeCallChain(memory);
            if (imageBase == 0)
                throw new InvalidOperationException("native image base is zero.");
            if (IntPtr.Size != sizeof(long))
                throw new PlatformNotSupportedException("native visibility probe requires the 64-bit game process.");

            ulong wrapperAddress = checked(imageBase + (ulong)VisibilityWrapperRva);
            visibility = Marshal.GetDelegateForFunctionPointer<NativeVisibilityDelegate>(
                new IntPtr(unchecked((long)wrapperAddress)));
            ulong coreAddress = checked(imageBase + (ulong)VisibilityCoreRva);
            visibilityCore = Marshal.GetDelegateForFunctionPointer<NativeVisibilityDelegate>(
                new IntPtr(unchecked((long)coreAddress)));

            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters native visibility probe initialized: " +
                $"mode=reference-rva, wrapperRva=0x{VisibilityWrapperRva:X}, coreRva=0x{VisibilityCoreRva:X}, " +
                "privateContextBytes=16, contextGuards=True, directionalCoreComparison=True, " +
                "nativeHooks=False, behaviorNeutral=True.");
        }

        public bool IsAvailable => visibility != null && visibilityCore != null && !disabled && !disposed;

        public bool TryEvaluateDirectVisibility(
            int hunterUnitId,
            uint hunterGlobalId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out int result)
        {
            result = 0;
            if (!IsAvailable ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) ||
                !settings.ImprovedPathfinding ||
                !settings.IsHuntingEnabled(preyType) ||
                hunterUnitId <= 0 ||
                preyUnitId <= 0 ||
                hunterGlobalId == 0 ||
                preyGlobalId == 0)
            {
                return false;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out GameUnit* hunter, out _) ||
                !APIShared.UnitAccess.TryGetById(unitApi, preyUnitId, out GameUnit* prey, out _) ||
                hunter == null ||
                prey == null ||
                hunter->r_GlobalId != hunterGlobalId ||
                prey->r_GlobalId != preyGlobalId ||
                hunter->r_AliveState != AliveState.IsAlive ||
                prey->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                prey->r_UnitChimp != preyType)
            {
                return false;
            }

            return TryInvokeVisibility(hunter, prey, out result, out _);
        }

        public bool TryEvaluateNearVisibility(
            int hunterUnitId,
            uint hunterGlobalId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out int wrapperResult,
            out int hunterToPreyResult,
            out int preyToHunterResult,
            out HunterNearVisibilityGeometry geometry)
        {
            wrapperResult = 0;
            hunterToPreyResult = -1;
            preyToHunterResult = -1;
            geometry = default;
            if (!IsAvailable ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) ||
                !settings.ImprovedPathfinding ||
                !settings.IsHuntingEnabled(preyType) ||
                hunterUnitId <= 0 ||
                preyUnitId <= 0 ||
                hunterGlobalId == 0 ||
                preyGlobalId == 0)
            {
                return false;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out GameUnit* hunter, out _) ||
                !APIShared.UnitAccess.TryGetById(unitApi, preyUnitId, out GameUnit* prey, out _) ||
                hunter == null ||
                prey == null ||
                hunter->r_GlobalId != hunterGlobalId ||
                prey->r_GlobalId != preyGlobalId ||
                hunter->r_AliveState != AliveState.IsAlive ||
                prey->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                prey->r_UnitChimp != preyType)
            {
                return false;
            }

            return TryInvokeNearVisibility(
                hunter,
                prey,
                out wrapperResult,
                out hunterToPreyResult,
                out preyToHunterResult,
                out geometry);
        }

        public void ResetForMap()
        {
            reentrancyLogged = false;
            captureFailureLogged = false;
            invocationFailureLogged = false;
            probeInProgress = 0;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            visibility = null;
            visibilityCore = null;
        }

        private static void ValidateNativeCallChain(ReadOnlySpan<byte> memory)
        {
            RequirePattern(memory, VisibilityWrapperRva, WrapperEntryPattern, "visibility wrapper entry");
            RequirePattern(memory, VisibilityCoreRva, CoreEntryPattern, "visibility core entry");
            RequirePattern(
                memory,
                VisibilityWrapperRva + 0x3D,
                FirstCoreCallPattern,
                "visibility wrapper first core call and private-context write setup");
            RequirePattern(
                memory,
                VisibilityWrapperRva + 0x5C,
                ReverseCoreCallPattern,
                "visibility wrapper reverse core call");
            RequirePattern(memory, HunterQueryCallRva, HunterQueryCallPattern, "Hunter query visibility call");
            RequirePattern(
                memory,
                HunterDirectOrderCallRva,
                HunterDirectOrderCallPattern,
                "Hunter direct-order visibility call");

            RequireRelativeTarget(
                memory,
                FirstCoreCallDisplacementRva,
                FirstCoreCallNextInstructionRva,
                VisibilityCoreRva,
                "visibility wrapper first core target");
            RequireRelativeTarget(
                memory,
                ReverseCoreCallDisplacementRva,
                ReverseCoreCallNextInstructionRva,
                VisibilityCoreRva,
                "visibility wrapper reverse core target");
            RequireRelativeTarget(
                memory,
                HunterQueryCallRva + 1,
                HunterQueryCallRva + 5,
                VisibilityWrapperRva,
                "Hunter query wrapper target");
            RequireRelativeTarget(
                memory,
                HunterDirectOrderCallRva + 1,
                HunterDirectOrderCallRva + 5,
                VisibilityWrapperRva,
                "Hunter direct-order wrapper target");
        }

        private static void RequirePattern(
            ReadOnlySpan<byte> memory,
            int rva,
            string pattern,
            string name)
        {
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, rva, pattern))
                throw new InvalidOperationException($"{name} failed byte validation at RVA 0x{rva:X}.");
        }

        private static void RequireRelativeTarget(
            ReadOnlySpan<byte> memory,
            int displacementRva,
            int nextInstructionRva,
            int expectedTargetRva,
            string name)
        {
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                displacementRva,
                nextInstructionRva);
            if (targetRva != expectedTargetRva)
            {
                throw new InvalidOperationException(
                    $"{name} resolved RVA 0x{targetRva:X}, expected 0x{expectedTargetRva:X}.");
            }
        }

        private bool TryInvokeVisibility(
            GameUnit* hunter,
            GameUnit* prey,
            out int result,
            out int contextScratch)
        {
            result = 0;
            contextScratch = 0;
            if (Interlocked.CompareExchange(ref probeInProgress, 1, 0) != 0)
            {
                if (!reentrancyLogged)
                {
                    reentrancyLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Improved Hunters native visibility probe skipped a reentrant invocation; behavior remains unchanged.");
                }
                return false;
            }

            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter(
                    "HunterVisibilityNative",
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    prey->r_CurrentTilePositionX,
                    prey->r_CurrentTilePositionY))
            {
                try
                {
                    if (TryInvokeGuardedVisibility(
                        visibility,
                        hunter->r_CurrentWorldPositionX,
                        hunter->r_CurrentWorldPositionY,
                        hunter->r_HeightElevation + hunter->N0000006A + 30,
                        prey->r_CurrentWorldPositionX,
                        prey->r_CurrentWorldPositionY,
                        prey->r_HeightElevation + prey->N0000006A + 26,
                        out result,
                        out contextScratch))
                    {
                        diagnostic.Complete(result);
                        return true;
                    }

                    DisableForContextGuardChange();
                    return false;
                }
                catch (Exception exception)
                {
                    LogFailureOnce("native invocation", exception, disableProbe: true);
                    return false;
                }
                finally
                {
                    Volatile.Write(ref probeInProgress, 0);
                }
            }
        }

        private bool TryInvokeNearVisibility(
            GameUnit* hunter,
            GameUnit* prey,
            out int wrapperResult,
            out int hunterToPreyResult,
            out int preyToHunterResult,
            out HunterNearVisibilityGeometry geometry)
        {
            wrapperResult = 0;
            hunterToPreyResult = -1;
            preyToHunterResult = -1;
            geometry = default;
            if (Interlocked.CompareExchange(ref probeInProgress, 1, 0) != 0)
            {
                if (!reentrancyLogged)
                {
                    reentrancyLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Improved Hunters native visibility probe skipped a reentrant near-visibility invocation; " +
                        "behavior remains unchanged.");
                }
                return false;
            }

            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter(
                    "HunterNearVisibilityNative",
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    prey->r_CurrentTilePositionX,
                    prey->r_CurrentTilePositionY))
            {
                try
                {
                    int hunterX = hunter->r_CurrentWorldPositionX;
                    int hunterY = hunter->r_CurrentWorldPositionY;
                    int hunterHeight = hunter->r_HeightElevation + hunter->N0000006A + 30;
                    int preyX = prey->r_CurrentWorldPositionX;
                    int preyY = prey->r_CurrentWorldPositionY;
                    int preyHeight = prey->r_HeightElevation + prey->N0000006A + 26;
                    geometry = new HunterNearVisibilityGeometry(
                        hunter->r_CurrentTilePositionX,
                        hunter->r_CurrentTilePositionY,
                        prey->r_CurrentTilePositionX,
                        prey->r_CurrentTilePositionY,
                        hunterX,
                        hunterY,
                        hunterHeight,
                        preyX,
                        preyY,
                        preyHeight);

                    if (!TryInvokeGuardedVisibility(
                            visibility,
                            hunterX,
                            hunterY,
                            hunterHeight,
                            preyX,
                            preyY,
                            preyHeight,
                            out wrapperResult,
                            out _))
                    {
                        DisableForContextGuardChange();
                        return false;
                    }

                    // The validated wrapper calls both core directions before it
                    // can return zero. Preserve those exact results without two
                    // redundant native calls on the new 250-ms near-target path.
                    if (wrapperResult == 0)
                    {
                        hunterToPreyResult = 0;
                        preyToHunterResult = 0;
                        diagnostic.Complete(0);
                        return true;
                    }

                    if (!TryInvokeGuardedVisibility(
                            visibilityCore,
                            hunterX,
                            hunterY,
                            hunterHeight,
                            preyX,
                            preyY,
                            preyHeight,
                            out hunterToPreyResult,
                            out _) ||
                        !TryInvokeGuardedVisibility(
                            visibilityCore,
                            preyX,
                            preyY,
                            preyHeight,
                            hunterX,
                            hunterY,
                            hunterHeight,
                            out preyToHunterResult,
                            out _))
                    {
                        DisableForContextGuardChange();
                        return false;
                    }

                    diagnostic.Complete(wrapperResult);
                    return true;
                }
                catch (Exception exception)
                {
                    LogFailureOnce("near-visibility native invocation", exception, disableProbe: true);
                    return false;
                }
                finally
                {
                    Volatile.Write(ref probeInProgress, 0);
                }
            }
        }

        private static bool TryInvokeGuardedVisibility(
            NativeVisibilityDelegate function,
            int startX,
            int startY,
            int startHeight,
            int endX,
            int endY,
            int endHeight,
            out int result,
            out int contextScratch)
        {
            int* guardedBuffer = stackalloc int[8];
            guardedBuffer[0] = unchecked((int)0x13579BDF);
            guardedBuffer[1] = unchecked((int)0x2468ACE0);
            guardedBuffer[2] = 0;
            guardedBuffer[3] = 0;
            guardedBuffer[4] = 0;
            guardedBuffer[5] = 0;
            guardedBuffer[6] = unchecked((int)0x55AA33CC);
            guardedBuffer[7] = unchecked((int)0xAA55CC33);

            result = function(
                (IntPtr)(guardedBuffer + 2),
                startX,
                startY,
                startHeight,
                endX,
                endY,
                endHeight);
            contextScratch = guardedBuffer[5];
            return guardedBuffer[0] == unchecked((int)0x13579BDF) &&
                guardedBuffer[1] == unchecked((int)0x2468ACE0) &&
                guardedBuffer[2] == 0 &&
                guardedBuffer[3] == 0 &&
                guardedBuffer[4] == 0 &&
                guardedBuffer[6] == unchecked((int)0x55AA33CC) &&
                guardedBuffer[7] == unchecked((int)0xAA55CC33);
        }

        private void DisableForContextGuardChange()
        {
            disabled = true;
            Shared.CrashBreadcrumbDiagnostics.Record("HunterVisibilityGuardChanged", outcome: -1);
            Shared.DebugLogHelper.LogError(
                log,
                "Improved Hunters native visibility probe disabled: the private context guard changed outside context+0xC; " +
                "no Hunter state or order was modified.");
        }

        private void LogFailureOnce(string operation, Exception exception, bool disableProbe)
        {
            Shared.CrashBreadcrumbDiagnostics.Record("HunterVisibilityFailure", disableProbe ? 1 : 0, outcome: -1);
            if (disableProbe)
            {
                disabled = true;
                if (invocationFailureLogged)
                    return;
                invocationFailureLogged = true;
            }
            else
            {
                if (captureFailureLogged)
                    return;
                captureFailureLogged = true;
            }

            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters native visibility probe {operation} failed; " +
                $"disabled={disabled}, Hunter behavior remains unchanged: {exception}");
        }
    }
}
