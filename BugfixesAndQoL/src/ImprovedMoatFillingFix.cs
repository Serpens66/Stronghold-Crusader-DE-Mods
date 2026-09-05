using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe class ImprovedMoatFillingFix
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FindMoatWorkTargetDelegate(
            IntPtr tileManager, int playerId, int unitId, int relationshipMode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ResolveMoatWorkTileDelegate(
            IntPtr tileManager, int moatId, int mode, uint sourceX, uint sourceY);

        private const int FindMoatWorkTargetRva =
            ImprovedMoatFillingNativeContractPolicy.FindMoatWorkTargetRva;
        private const int ResolveMoatWorkTileRva =
            ImprovedMoatFillingNativeContractPolicy.ResolveMoatWorkTileRva;
        private const int StateDispatcherRva = 0x13F540;
        private const int StateDispatcherSize = 10069;
        private const int MovementPlannerRva =
            ImprovedMoatFillingNativeContractPolicy.MovementPlannerRva;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int NativeHeightLayerRva = 0x4DDD350;
        private const int PathRegionGridRva = 0x50EC690;
        private const int MapWidth = 800;
        private const int NativeTileCount = 0x4E520;
        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatRecordTileIdOffset = 0x00;
        private const int MoatRecordXOffset = 0x04;
        private const int MoatRecordYOffset = 0x06;
        private const int MoatRecordReservationOffset = 0x0F;
        private const int SelectedMoatApproachXOffset = 0x2038E38;
        private const int SelectedMoatApproachYOffset = 0x2038E3C;
        private const int SelectedMoatTileIdOffset = 0x2038E40;
        private const int MaximumMoatRecordId = 0x31F;
        private const int VanillaApproachHeightTolerance = 0x10;

        private static readonly int[] NeighbourX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] NeighbourY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private const string FindMoatWorkTargetPattern =
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 " +
            "48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4";

        private const string ResolveMoatWorkTilePattern =
            "44 89 4C 24 20 53 57 41 57 48 83 EC 20 48 63 44 24 60 " +
            "45 8B D0 49 63 D9 4C 63 DA 81 FB 1F 03 00 00 " +
            "0F 87 ?? ?? ?? ?? 3D 1F 03 00 00 0F 87 ?? ?? ?? ??";

        [ThreadStatic]
        private static PendingApproach pendingApproach;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ulong libraryBase;
        private readonly byte* movementTargetAvailability;
        private readonly byte* nativeHeightLayer;
        private readonly short* pathRegionGrid;
        private readonly uint* tileFlags;
        private readonly ScanRegion region;
        private HookTransaction transaction;
        private readonly DetourHandle<FindMoatWorkTargetDelegate> findMoatWorkTargetDetour =
            new DetourHandle<FindMoatWorkTargetDelegate>();
        private readonly DetourHandle<ResolveMoatWorkTileDelegate> resolveMoatWorkTileDetour =
            new DetourHandle<ResolveMoatWorkTileDelegate>();
        private bool reservationWarningLogged;
        private bool selectorErrorLogged;
        private bool resolverErrorLogged;
        private bool revalidationWarningLogged;

        internal ImprovedMoatFillingFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.region = region ?? throw new ArgumentNullException(nameof(region));
            this.libraryBase = libraryBase;
            ValidateNativeContracts(memory);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            nativeHeightLayer = (byte*)(libraryBase + NativeHeightLayerRva);
            pathRegionGrid = (short*)(libraryBase + PathRegionGridRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
        }

        internal void Apply()
        {
            transaction = new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
            {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    // Runtime ownership is process-long and startup teardown must not remove these hooks.
                    OwnsHooks = false
                });
            transaction.AddDetour(
                findMoatWorkTargetDetour,
                HookTarget.FromAddress(libraryBase + FindMoatWorkTargetRva),
                FindMoatWorkTargetWithFreeApproach);
            transaction.AddDetour(
                resolveMoatWorkTileDetour,
                HookTarget.FromAddress(libraryBase + ResolveMoatWorkTileRva),
                ResolveMoatWorkTileWithSelectedApproach);
            CommitResult commitResult = transaction.Commit();
            if (!commitResult.IsCompleteSuccess ||
                !findMoatWorkTargetDetour.Success || !resolveMoatWorkTileDetour.Success)
                throw new InvalidOperationException("The standalone moat detours were not installed atomically.");
        }

        private bool IsEnabled => settings.EnableMod && settings.EnableImprovedMoatFilling;

        private int FindMoatWorkTargetWithFreeApproach(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode)
        {
            pendingApproach = null;
            if (!ImprovedMoatFillingPolicy.ShouldInspectSelection(IsEnabled, relationshipMode) ||
                !TryCaptureUnit(unitId, playerId, out int sourceX, out int sourceY) ||
                tileManager == IntPtr.Zero ||
                tileManager != GameTileManagerAPI.Instance.GetTileManager())
            {
                return findMoatWorkTargetDetour.Original(tileManager, playerId, unitId, relationshipMode);
            }

            List<ExcludedReservation> exclusions = null;
            int currentMoatId = -1;
            bool currentReservationRetained = false;
            try
            {
                int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
                int maximumAttempts = Math.Min(Math.Max(moatCount, 1), MaximumMoatRecordId + 1);
                for (int attempt = 0; attempt < maximumAttempts; attempt++)
                {
                    currentReservationRetained = false;
                    currentMoatId = findMoatWorkTargetDetour.Original(
                        tileManager, playerId, unitId, relationshipMode);
                    if (currentMoatId <= 0)
                        break;
                    currentReservationRetained = true;

                    if (!TryReadMoatRecord(
                            tileManager, currentMoatId, out byte* record,
                            out int moatTileId, out int moatX, out int moatY))
                    {
                        return currentMoatId;
                    }

                    if (TryChooseApproach(unitId, sourceX, sourceY, moatX, moatY, out Approach selected))
                    {
                        pendingApproach = new PendingApproach(
                            tileManager, unitId, playerId, currentMoatId, moatTileId,
                            sourceX, sourceY, selected);
                        return currentMoatId;
                    }

                    byte reservationAfterSelection = record[MoatRecordReservationOffset];
                    if (!ImprovedMoatFillingPolicy.TryUndoVanillaReservation(
                            reservationAfterSelection, out byte reservationBeforeSelection))
                    {
                        if (!reservationWarningLogged)
                        {
                            reservationWarningLogged = true;
                            Shared.DebugLogHelper.LogWarning(
                                log,
                                "Improved moat filling encountered an unsafe Vanilla reservation; " +
                                "the current Vanilla selection is retained.");
                        }
                        return currentMoatId;
                    }

                    if (exclusions == null)
                        exclusions = new List<ExcludedReservation>();
                    exclusions.Add(new ExcludedReservation(record, reservationBeforeSelection));
                    currentReservationRetained = false;
                    record[MoatRecordReservationOffset] =
                        ImprovedMoatFillingPolicy.TemporarilyExcludedReservation;
                }
                return currentMoatId;
            }
            catch (Exception ex)
            {
                RestoreExclusions(exclusions);
                exclusions?.Clear();
                LogOnce(ref selectorErrorLogged,
                    "Improved moat filling selector failed once; Vanilla behavior remains active: " + ex);
                if (currentMoatId > 0 && currentReservationRetained)
                    return currentMoatId;
                return findMoatWorkTargetDetour.Original(tileManager, playerId, unitId, relationshipMode);
            }
            finally
            {
                RestoreExclusions(exclusions);
            }
        }

        private int ResolveMoatWorkTileWithSelectedApproach(
            IntPtr tileManager,
            int moatId,
            int mode,
            uint sourceX,
            uint sourceY)
        {
            PendingApproach pending = pendingApproach;
            bool matches = pending != null && pending.Matches(tileManager, moatId, sourceX, sourceY);
            int vanillaResult = resolveMoatWorkTileDetour.Original(tileManager, moatId, mode, sourceX, sourceY);

            if (mode == ImprovedMoatFillingPolicy.PublishMoatTileMode)
            {
                if (!matches || vanillaResult <= 0)
                    pendingApproach = null;
                return vanillaResult;
            }

            pendingApproach = null;
            if (!ImprovedMoatFillingPolicy.ShouldReplaceResolverResult(mode, matches))
                return vanillaResult;

            byte* manager = null;
            int oldTileId = 0;
            int oldX = 0;
            int oldY = 0;
            bool captured = false;
            try
            {
                if (!IsPendingApproachValid(tileManager, moatId, pending))
                {
                    if (!revalidationWarningLogged)
                    {
                        revalidationWarningLogged = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            "Improved moat filling rejected a changed approach during synchronous revalidation; " +
                            "the Vanilla resolver result is retained.");
                    }
                    return vanillaResult;
                }

                manager = (byte*)tileManager.ToPointer();
                oldTileId = *(int*)(manager + SelectedMoatTileIdOffset);
                oldX = *(int*)(manager + SelectedMoatApproachXOffset);
                oldY = *(int*)(manager + SelectedMoatApproachYOffset);
                captured = true;
                *(int*)(manager + SelectedMoatTileIdOffset) = pending.MoatTileId;
                *(int*)(manager + SelectedMoatApproachXOffset) = pending.Approach.X;
                *(int*)(manager + SelectedMoatApproachYOffset) = pending.Approach.Y;
                return pending.Approach.TileId;
            }
            catch (Exception ex)
            {
                if (captured)
                {
                    *(int*)(manager + SelectedMoatTileIdOffset) = oldTileId;
                    *(int*)(manager + SelectedMoatApproachXOffset) = oldX;
                    *(int*)(manager + SelectedMoatApproachYOffset) = oldY;
                }
                LogOnce(ref resolverErrorLogged,
                    "Improved moat filling resolver failed once; the Vanilla result is retained: " + ex);
                return vanillaResult;
            }
        }

        private bool TryChooseApproach(
            int unitId,
            int sourceX,
            int sourceY,
            int moatX,
            int moatY,
            out Approach selected)
        {
            selected = default;
            int sourceTileId = GameTileManagerAPI.Instance.GetTileId(sourceX, sourceY);
            if (!IsValidTileId(sourceTileId))
                return false;
            short sourceRegion = pathRegionGrid[sourceTileId];
            byte sourceHeight = nativeHeightLayer[sourceTileId];
            bool found = false;
            long bestDistance = long.MaxValue;

            for (int order = 0; order < NeighbourX.Length; order++)
            {
                int x = moatX + NeighbourX[order];
                int y = moatY + NeighbourY[order];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth)
                    continue;
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (!IsEligibleApproach(unitId, sourceRegion, sourceHeight, x, y, tileId))
                    continue;
                long dx = sourceX - x;
                long dy = sourceY - y;
                long distance = dx * dx + dy * dy;
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    selected = new Approach(order, x, y, tileId);
                }
            }
            return found;
        }

        private bool IsEligibleApproach(
            int unitId,
            short sourceRegion,
            byte sourceHeight,
            int x,
            int y,
            int tileId)
        {
            if (!IsValidTileId(tileId) || movementTargetAvailability[y * MapWidth + x] == 0 ||
                nativeHeightLayer[tileId] > sourceHeight + VanillaApproachHeightTolerance ||
                !ImprovedMoatFillingPolicy.IsSameNativeRegion(sourceRegion, pathRegionGrid[tileId]))
            {
                return false;
            }
            uint flags = tileFlags[tileId];
            return !ImprovedMoatFillingPolicy.IsCompletedMoat(flags) &&
                !ImprovedMoatFillingPolicy.HasDownstreamMovementBlockingFlags(flags) &&
                !IsOccupiedByOtherLivingUnit(tileId, unitId);
        }

        private bool IsPendingApproachValid(IntPtr tileManager, int moatId, PendingApproach pending)
        {
            if (pending == null || tileManager == IntPtr.Zero ||
                tileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                !TryCaptureUnit(pending.UnitId, pending.PlayerId, out int currentX, out int currentY) ||
                currentX != pending.SourceX || currentY != pending.SourceY ||
                !TryReadMoatRecord(
                    tileManager, moatId, out _, out int moatTileId, out int moatX, out int moatY) ||
                moatTileId != pending.MoatTileId ||
                moatX + NeighbourX[pending.Approach.Order] != pending.Approach.X ||
                moatY + NeighbourY[pending.Approach.Order] != pending.Approach.Y)
            {
                return false;
            }
            int sourceTileId = GameTileManagerAPI.Instance.GetTileId(currentX, currentY);
            return IsValidTileId(sourceTileId) && IsEligibleApproach(
                pending.UnitId,
                pathRegionGrid[sourceTileId],
                nativeHeightLayer[sourceTileId],
                pending.Approach.X,
                pending.Approach.Y,
                pending.Approach.TileId);
        }

        private static bool TryCaptureUnit(
            int unitId,
            int playerId,
            out int sourceX,
            out int sourceY)
        {
            sourceX = -1;
            sourceY = -1;
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_ControllableForPlayerId != playerId || !CanDigMoat(unit->r_UnitChimp))
            {
                return false;
            }
            sourceX = unit->r_CurrentTilePositionX;
            sourceY = unit->r_CurrentTilePositionY;
            return (uint)sourceX < MapWidth && (uint)sourceY < MapWidth;
        }

        private bool IsOccupiedByOtherLivingUnit(int tileId, int currentUnitId)
        {
            int occupantUnitId = GameTileManagerAPI.Instance.GetTileUnitId(tileId);
            if (occupantUnitId == 0 || occupantUnitId == currentUnitId)
                return false;
            return GameUnitManagerAPI.Instance.TryGetUnitById(occupantUnitId, out GameUnit* occupant) &&
                occupant != null && occupant->r_AliveState == AliveState.IsAlive;
        }

        private static bool CanDigMoat(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadMoatRecord(
            IntPtr tileManager,
            int moatId,
            out byte* record,
            out int tileId,
            out int x,
            out int y)
        {
            record = null;
            tileId = -1;
            x = -1;
            y = -1;
            if (tileManager == IntPtr.Zero)
                return false;
            byte* manager = (byte*)tileManager.ToPointer();
            int count = *(int*)(manager + MoatRecordCountOffset);
            if (moatId <= 0 || moatId > MaximumMoatRecordId || moatId >= count)
                return false;
            record = manager + MoatRecordArrayOffset + moatId * MoatRecordSize;
            tileId = *(int*)(record + MoatRecordTileIdOffset);
            x = *(short*)(record + MoatRecordXOffset);
            y = *(short*)(record + MoatRecordYOffset);
            return IsValidTileId(tileId) && (uint)x < MapWidth && (uint)y < MapWidth &&
                GameTileManagerAPI.Instance.GetTileId(x, y) == tileId;
        }

        private void LogOnce(ref bool logged, string message)
        {
            if (logged)
                return;
            logged = true;
            Shared.DebugLogHelper.LogError(log, message);
        }

        private static void RestoreExclusions(List<ExcludedReservation> exclusions)
        {
            if (exclusions == null)
                return;
            for (int index = 0; index < exclusions.Count; index++)
                exclusions[index].Restore();
        }

        private static bool IsValidTileId(int tileId) => tileId >= 0 && tileId < NativeTileCount;

        private static void ValidateNativeContracts(ReadOnlySpan<byte> memory)
        {
            ResolveExact(memory, FindMoatWorkTargetPattern, FindMoatWorkTargetRva, "moat work-target selector");
            ResolveExact(memory, ResolveMoatWorkTilePattern, ResolveMoatWorkTileRva, "moat work-tile resolver");
            ValidateOwnedHookEntry(memory, FindMoatWorkTargetRva, new byte[]
            {
                0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                0x10, 0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55,
                0x41, 0x56, 0x48, 0x83, 0xEC, 0x68, 0x48, 0x8B, 0xE9
            }, "moat work-target selector entry");
            ValidateOwnedHookEntry(memory, ResolveMoatWorkTileRva, new byte[]
            {
                0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x57, 0x41,
                0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63, 0x44,
                0x24, 0x60, 0x45, 0x8B, 0xD0, 0x49, 0x63, 0xD9,
                0x4C, 0x63, 0xDA
            }, "moat work-tile resolver entry");
            // Do not require pristine live bytes inside the downstream planner. Script Extender
            // owns its entry detour, and our mounted-stockpile fix legitimately hooks its
            // structure-flag gate before this feature initializes. The canonical DLL tests
            // still validate the original planner entry and both flag gates.
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490)
                throw new InvalidOperationException("GameUnit no longer matches the audited 0x490-byte layout.");
            ValidateField(nameof(GameUnit.r_AliveState), 0x88);
            ValidateField(nameof(GameUnit.r_ControllableForPlayerId), 0x92);
            ValidateField(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
            ValidateField(nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
            int selectorCalls = CountNearCalls(memory, StateDispatcherRva, StateDispatcherSize, FindMoatWorkTargetRva);
            int resolverCalls = CountNearCalls(memory, StateDispatcherRva, StateDispatcherSize, ResolveMoatWorkTileRva);
            int plannerCalls = CountNearCalls(memory, StateDispatcherRva, StateDispatcherSize, MovementPlannerRva);
            if (selectorCalls < 2 || resolverCalls < 3 || plannerCalls < 1)
                throw new InvalidOperationException("The state-dispatcher moat callgraph changed.");
        }

        private static void ResolveExact(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string name)
        {
            Shared.NativeResolution result = Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, expectedRva, true, name, null);
            if (result.Rva != expectedRva)
                throw new InvalidOperationException($"{name} resolved to 0x{result.Rva:X}.");
        }

        private static void ValidateExactBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string name)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException($"Native bytes changed for {name}.");
            }
        }

        private static void ValidateOwnedHookEntry(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string name)
        {
            if (!ImprovedMoatFillingNativeContractPolicy.RequiresPristineLiveBytes(rva))
                throw new InvalidOperationException($"{name} is not owned by the moat fix.");
            ValidateExactBytes(memory, rva, expected, name);
        }

        private static void ValidateField(string fieldName, int expectedOffset)
        {
            int actual = Marshal.OffsetOf(typeof(GameUnit), fieldName).ToInt32();
            if (actual != expectedOffset)
                throw new InvalidOperationException($"GameUnit.{fieldName} offset changed.");
        }

        private static int CountNearCalls(
            ReadOnlySpan<byte> memory,
            int startRva,
            int length,
            int targetRva)
        {
            int count = 0;
            int end = Math.Min(memory.Length - 5, checked(startRva + length));
            for (int rva = startRva; rva <= end; rva++)
            {
                if (memory[rva] == 0xE8 &&
                    Shared.NativePatternResolver.ResolveRelativeTarget(memory, rva + 1, rva + 5) == targetRva)
                {
                    count++;
                }
            }
            return count;
        }

        private sealed class PendingApproach
        {
            internal PendingApproach(
                IntPtr tileManager,
                int unitId,
                int playerId,
                int moatId,
                int moatTileId,
                int sourceX,
                int sourceY,
                Approach approach)
            {
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                MoatId = moatId;
                MoatTileId = moatTileId;
                SourceX = sourceX;
                SourceY = sourceY;
                Approach = approach;
            }

            internal IntPtr TileManager { get; }
            internal int UnitId { get; }
            internal int PlayerId { get; }
            internal int MoatId { get; }
            internal int MoatTileId { get; }
            internal int SourceX { get; }
            internal int SourceY { get; }
            internal Approach Approach { get; }

            internal bool Matches(IntPtr tileManager, int moatId, uint sourceX, uint sourceY) =>
                TileManager == tileManager && MoatId == moatId &&
                sourceX == unchecked((uint)SourceX) && sourceY == unchecked((uint)SourceY);
        }

        private readonly struct Approach
        {
            internal Approach(int order, int x, int y, int tileId)
            {
                Order = order;
                X = x;
                Y = y;
                TileId = tileId;
            }

            internal int Order { get; }
            internal int X { get; }
            internal int Y { get; }
            internal int TileId { get; }
        }

        private readonly struct ExcludedReservation
        {
            internal ExcludedReservation(byte* record, byte originalReservation)
            {
                Record = record;
                OriginalReservation = originalReservation;
            }

            private byte* Record { get; }
            private byte OriginalReservation { get; }
            internal void Restore() => Record[MoatRecordReservationOffset] = OriginalReservation;
        }
    }
}
