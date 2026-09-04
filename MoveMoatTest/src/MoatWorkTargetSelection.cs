using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FindMoatWorkTargetDelegate(
            IntPtr tileManager, int playerId, int unitId, int relationshipMode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ResolveMoatWorkTileDelegate(
            IntPtr tileManager, int moatId, int mode, uint sourceX, uint sourceY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int HasFillMoatApproachDelegate(
            IntPtr tileManager, int sourceRegion, int moatTileId, int moatY);

        private const int FindMoatWorkTargetRva = 0x69D60;
        private const int ResolveMoatWorkTileRva = 0x6AF60;
        private const int HasFillMoatApproachRva = 0x6C490;
        private const int FillApproachCallRva = 0x69EE6;
        private const int DigStandingOnMoatCallRva = 0x69F91;
        private const int DigRegionSearchCallRva = 0x69FE3;
        private const int DigRegionPairCallRva = 0x6A014;
        private const int DigAlternativeRegionPairCallRva = 0x6A0C2;
        private const int MovementPlannerLowFlagGateRva = 0x196464;
        private const int MovementPlannerStructureFlagGateRva = 0x19648D;

        private const int SelectedMoatTileIdOffset = 0x2038E40;
        private const int SelectedMoatApproachXOffset = 0x2038E38;
        private const int SelectedMoatApproachYOffset = 0x2038E3C;
        private const int MoatRecordTileIdOffset = 0x00;
        private const int MoatRecordXOffset = 0x04;
        private const int MoatRecordYOffset = 0x06;
        private const int MoatRecordReservationOffset = 0x0F;
        private const int MaximumMoatRecordId = 0x31F;
        private const byte MoatReservationStep = 20;
        private const byte TemporarilyExcludedMoatReservation = 100;
        private const uint MovementBlockedLowTileFlagMask = 0x00000030;
        private const uint MovementBlockedStructureTileFlagMask = 0x10000100;

        // Exact order used by 0x6C490/0x6AF60: N, NE, E, SE, S, SW, W, NW.
        private static readonly int[] MoatWorkNeighbourX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] MoatWorkNeighbourY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private const string FindMoatWorkTargetPattern =
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 " +
            "48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4";

        private const string ResolveMoatWorkTilePattern =
            "44 89 4C 24 20 53 57 41 57 48 83 EC 20 48 63 44 24 60 " +
            "45 8B D0 49 63 D9 4C 63 DA 81 FB 1F 03 00 00 " +
            "0F 87 ?? ?? ?? ?? 3D 1F 03 00 00 0F 87 ?? ?? ?? ??";

        private const string HasFillMoatApproachPattern =
            "48 89 5C 24 08 48 89 7C 24 10 49 63 C0 45 33 DB 8B FA " +
            "48 8B D9 44 0F B6 94 08 A0 E5 D7 00 49 63 C1 41 83 C2 10 " +
            "48 C1 E0 05 48 03 C1";

        [ThreadStatic]
        private static MoatWorkSelectionScope activeMoatWorkSelection;
        [ThreadStatic]
        private static PendingFillMoatApproach pendingFillMoatApproach;
        [ThreadStatic]
        private static PendingDigMoatTarget pendingDigMoatTarget;

        private FindMoatWorkTargetDelegate originalFindMoatWorkTarget;
        private FindMoatWorkTargetDelegate rootedFindMoatWorkTarget;
        private ResolveMoatWorkTileDelegate originalResolveMoatWorkTile;
        private ResolveMoatWorkTileDelegate rootedResolveMoatWorkTile;
        private HasFillMoatApproachDelegate originalHasFillMoatApproach;
        private HasFillMoatApproachDelegate rootedHasFillMoatApproach;
        private NativeDetour findMoatWorkTargetDetour;
        private NativeDetour resolveMoatWorkTileDetour;
        private NativeDetour hasFillMoatApproachDetour;
        private readonly Dictionary<int, string> lastMoatWorkSelectionByUnit =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastMoatWorkApproachByUnit =
            new Dictionary<int, string>();
        private Func<bool> improvedMoatFillingEnabledProvider;
        private string improvedMoatFillingProviderOwner;
        private bool improvedMoatFillingProviderErrorLogged;
        private bool improvedMoatFillingProviderFailed;
        private bool improvedMoatFillingReservationWarningLogged;
        private bool improvedMoatFillingSelectionErrorLogged;

        internal int RegisterImprovedMoatFillingProvider(
            string ownerGuid,
            Func<bool> enabledProvider)
        {
            if (findMoatWorkTargetDetour == null || resolveMoatWorkTileDetour == null)
                return 0;
            if (string.IsNullOrWhiteSpace(ownerGuid))
                throw new ArgumentException("An owner GUID is required.", nameof(ownerGuid));
            if (enabledProvider == null)
                throw new ArgumentNullException(nameof(enabledProvider));
            if (improvedMoatFillingEnabledProvider != null &&
                !string.Equals(improvedMoatFillingProviderOwner, ownerGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Improved moat filling is already controlled by {improvedMoatFillingProviderOwner}.");
            }
            improvedMoatFillingProviderOwner = ownerGuid;
            improvedMoatFillingEnabledProvider = enabledProvider;
            improvedMoatFillingProviderFailed = false;
            improvedMoatFillingProviderErrorLogged = false;
            return 1;
        }

        private void TryInstallMoatWorkTargetSelection(
            ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            NativeDetour pendingFind = null;
            NativeDetour pendingResolve = null;
            NativeDetour pendingFillApproach = null;
            bool findApplied = false;
            bool resolveApplied = false;
            bool fillApproachApplied = false;
            try
            {
                if (regionPairReachabilityDetour == null || originalRegionPairReachability == null ||
                    regionReachabilityDetour == null || originalRegionReachability == null)
                {
                    throw new InvalidOperationException(
                        "A shared E2610/E7C40 reachability hook is unavailable.");
                }

                Shared.NativeResolution findResolution = Resolve(
                    memory, FindMoatWorkTargetPattern, FindMoatWorkTargetRva,
                    "shared moat work-target selector");
                Shared.NativeResolution resolveResolution = Resolve(
                    memory, ResolveMoatWorkTilePattern, ResolveMoatWorkTileRva,
                    "shared moat work-tile resolver");
                Shared.NativeResolution fillApproachResolution = Resolve(
                    memory, HasFillMoatApproachPattern, HasFillMoatApproachRva,
                    "fill-moat neighbouring approach check");

                ValidateMoatWorkTargetContracts(memory);

                rootedFindMoatWorkTarget = FindMoatWorkTargetWithOwnerRoute;
                rootedResolveMoatWorkTile = ResolveMoatWorkTileWithOwnerRoute;
                rootedHasFillMoatApproach = AllowFillMoatApproachThroughFriendlyMoat;

                pendingFind = CreateDetour(
                    libraryBase + unchecked((ulong)findResolution.Rva),
                    rootedFindMoatWorkTarget);
                originalFindMoatWorkTarget =
                    pendingFind.GenerateTrampoline<FindMoatWorkTargetDelegate>();
                pendingResolve = CreateDetour(
                    libraryBase + unchecked((ulong)resolveResolution.Rva),
                    rootedResolveMoatWorkTile);
                originalResolveMoatWorkTile =
                    pendingResolve.GenerateTrampoline<ResolveMoatWorkTileDelegate>();
                pendingFillApproach = CreateDetour(
                    libraryBase + unchecked((ulong)fillApproachResolution.Rva),
                    rootedHasFillMoatApproach);
                originalHasFillMoatApproach =
                    pendingFillApproach.GenerateTrampoline<HasFillMoatApproachDelegate>();

                pendingFind.Apply();
                findApplied = true;
                pendingResolve.Apply();
                resolveApplied = true;
                pendingFillApproach.Apply();
                fillApproachApplied = true;

                findMoatWorkTargetDetour = pendingFind;
                resolveMoatWorkTileDetour = pendingResolve;
                hasFillMoatApproachDetour = pendingFillApproach;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "MoveMoat moat-work target selection installed: " +
                    $"selector=0x{findResolution.Rva:X}, resolver=0x{resolveResolution.Rva:X}, " +
                    $"fillApproach=0x{fillApproachResolution.Rva:X}, " +
                    $"regionPair=0x{RegionPairReachabilityRva:X}, " +
                    $"regionSearch=0x{RegionReachabilityRva:X}.");
            }
            catch (Exception ex)
            {
                UndoAndDispose(pendingFillApproach, fillApproachApplied);
                UndoAndDispose(pendingResolve, resolveApplied);
                UndoAndDispose(pendingFind, findApplied);
                hasFillMoatApproachDetour = null;
                resolveMoatWorkTileDetour = null;
                findMoatWorkTargetDetour = null;
                rootedFindMoatWorkTarget = null;
                rootedResolveMoatWorkTile = null;
                rootedHasFillMoatApproach = null;
                originalFindMoatWorkTarget = null;
                originalResolveMoatWorkTile = null;
                originalHasFillMoatApproach = null;
                ResetMoatWorkTargetSelection();
                Shared.DebugLogHelper.LogError(
                    log,
                    "MoveMoat moat-work target selection was not installed; " +
                    $"existing movement remains active and work-target selection stays Vanilla: {ex}");
            }
        }

        private static void ValidateMoatWorkTargetContracts(ReadOnlySpan<byte> memory)
        {
            ValidateExactBytes(
                memory, FindMoatWorkTargetRva,
                new byte[]
                {
                    0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                    0x10, 0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55,
                    0x41, 0x56, 0x48, 0x83, 0xEC, 0x68, 0x48, 0x8B,
                    0xE9
                },
                "shared moat work-target selector entry");
            ValidateExactBytes(
                memory, ResolveMoatWorkTileRva,
                new byte[]
                {
                    0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x57, 0x41,
                    0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63, 0x44,
                    0x24, 0x60, 0x45, 0x8B, 0xD0, 0x49, 0x63, 0xD9,
                    0x4C, 0x63, 0xDA
                },
                "shared moat work-tile resolver entry");
            ValidateExactBytes(
                memory, HasFillMoatApproachRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x7C,
                    0x24, 0x10, 0x49, 0x63, 0xC0, 0x45, 0x33, 0xDB,
                    0x8B, 0xFA, 0x48, 0x8B, 0xD9
                },
                "fill-moat neighbouring approach check entry");
            ValidateCallTarget(
                memory, FillApproachCallRva, HasFillMoatApproachRva,
                new byte[] { 0xE8, 0xA5, 0x25, 0x00, 0x00 },
                "work-target fill-approach call");
            ValidateCallTarget(
                memory, DigStandingOnMoatCallRva, UnitStandingOnCompletedMoatRva,
                new byte[] { 0xE8, 0xAA, 0xC8, 0x12, 0x00 },
                "work-target standing-on-moat call");
            ValidateCallTarget(
                memory, DigRegionSearchCallRva, RegionReachabilityRva,
                new byte[] { 0xE8, 0x58, 0xDC, 0x07, 0x00 },
                "work-target moat-aware region call");
            ValidateCallTarget(
                memory, DigRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0xF7, 0x85, 0x07, 0x00 },
                "work-target direct region-pair call");
            ValidateCallTarget(
                memory, DigAlternativeRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x49, 0x85, 0x07, 0x00 },
                "work-target alternative region-pair call");
            ValidateExactBytes(
                memory, MovementPlannerLowFlagGateRva,
                new byte[] { 0xF6, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04, 0x30 },
                "moat-work downstream movement low-flag gate");
            ValidateExactBytes(
                memory, MovementPlannerStructureFlagGateRva,
                new byte[]
                {
                    0xF7, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04,
                    0x00, 0x01, 0x00, 0x10
                },
                "moat-work downstream movement structure-flag gate");
        }

        private int FindMoatWorkTargetWithOwnerRoute(
            IntPtr tileManager, int playerId, int unitId, int relationshipMode)
        {
            // A work hand-off may only survive the exact 0x6AF60 -> 0x196280 chain.
            // A new selector invocation proves that an older hand-off was not consumed.
            if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                pendingPlan = null;
            pendingFillMoatApproach = null;
            pendingDigMoatTarget = null;
            MoatWorkSelectionScope scope;
            try
            {
                if (!TryCreateMoatWorkSelectionScope(
                        tileManager, playerId, unitId, relationshipMode, out scope))
                {
                    return originalFindMoatWorkTarget(
                        tileManager, playerId, unitId, relationshipMode);
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("moat-work-selection-capture", ex);
                return originalFindMoatWorkTarget(
                    tileManager, playerId, unitId, relationshipMode);
            }

            int result = SelectMoatWorkTarget(scope, tileManager, playerId, unitId, relationshipMode);

            try
            {
                scope.SelectedMoatId = result;
                if (relationshipMode == 2 && result > 0 &&
                    scope.FillApproaches.TryGetValue(result, out MoatWorkApproach approach))
                {
                    pendingFillMoatApproach = new PendingFillMoatApproach(
                        mapEpoch, tileManager, unitId, playerId, result,
                        scope.StartX, scope.StartY, approach,
                        scope.ImprovedFillSelection);
                }
                else if (relationshipMode == 1 && result > 0 &&
                    TryCreatePendingDigMoatTarget(scope, result, out PendingDigMoatTarget digTarget))
                {
                    pendingDigMoatTarget = digTarget;
                }
                LogMoatWorkSelection(scope);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("moat-work-selection-post", ex);
                pendingFillMoatApproach = null;
                pendingDigMoatTarget = null;
            }
            return result;
        }

        private int SelectMoatWorkTarget(
            MoatWorkSelectionScope scope,
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode)
        {
            bool improveFill = relationshipMode == 2 && IsImprovedMoatFillingEnabled();
            scope.ImprovedFillSelection = improveFill;
            List<ExcludedMoatReservation> exclusions = null;
            MoatWorkSelectionScope previous = activeMoatWorkSelection;
            int result = -1;
            bool currentReservationRetained = false;
            activeMoatWorkSelection = scope;
            try
            {
                int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
                int maximumAttempts = improveFill
                    ? Math.Min(Math.Max(moatCount, 1), MaximumMoatRecordId + 1)
                    : 1;
                for (int attempt = 0; attempt < maximumAttempts; attempt++)
                {
                    currentReservationRetained = false;
                    result = originalFindMoatWorkTarget(
                        tileManager, playerId, unitId, relationshipMode);
                    if (!improveFill || result <= 0)
                        break;
                    currentReservationRetained = true;

                    if (!TryReadMoatRecord(
                            tileManager, result, out byte* record,
                            out int moatTileId, out int moatX, out int moatY))
                    {
                        break;
                    }
                    if (TryFindBestFillMoatApproach(
                            scope, result, moatTileId, moatX, moatY,
                            out MoatWorkApproach approach))
                    {
                        scope.FillApproaches[result] = approach;
                        break;
                    }

                    byte afterSelection = record[MoatRecordReservationOffset];
                    if (afterSelection < MoatReservationStep)
                    {
                        if (!improvedMoatFillingReservationWarningLogged)
                        {
                            improvedMoatFillingReservationWarningLogged = true;
                            Shared.DebugLogHelper.LogWarning(
                                log,
                                "MoveMoat improved moat filling encountered an unsafe Vanilla reservation once; " +
                                "the current Vanilla selection is retained.");
                        }
                        break;
                    }
                    byte beforeSelection = (byte)(afterSelection - MoatReservationStep);
                    if (exclusions == null)
                        exclusions = new List<ExcludedMoatReservation>();
                    exclusions.Add(new ExcludedMoatReservation(record, beforeSelection));
                    currentReservationRetained = false;
                    record[MoatRecordReservationOffset] = TemporarilyExcludedMoatReservation;
                }
                return result;
            }
            catch (Exception ex)
            {
                RestoreExcludedMoatReservations(exclusions);
                exclusions?.Clear();
                if (!improvedMoatFillingSelectionErrorLogged)
                {
                    improvedMoatFillingSelectionErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "MoveMoat improved moat filling selection failed once; " +
                        "Vanilla behavior is retained: " + ex);
                }
                if (result > 0 && currentReservationRetained)
                    return result;
                return originalFindMoatWorkTarget(
                    tileManager, playerId, unitId, relationshipMode);
            }
            finally
            {
                RestoreExcludedMoatReservations(exclusions);
                activeMoatWorkSelection = previous;
            }
        }

        private bool IsImprovedMoatFillingEnabled()
        {
            if (improvedMoatFillingProviderFailed)
                return false;
            Func<bool> provider = improvedMoatFillingEnabledProvider;
            if (provider == null)
                return true;
            try
            {
                return provider();
            }
            catch (Exception ex)
            {
                // A broken optional bridge must not keep throwing in every unit's search hotpath.
                improvedMoatFillingProviderFailed = true;
                if (!improvedMoatFillingProviderErrorLogged)
                {
                    improvedMoatFillingProviderErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "MoveMoat improved-moat-filling provider failed once; the optional fix is disabled: " + ex);
                }
                return false;
            }
        }

        private bool TryCreatePendingDigMoatTarget(
            MoatWorkSelectionScope scope,
            int moatId,
            out PendingDigMoatTarget pending)
        {
            pending = null;
            if (scope == null || scope.RelationshipMode != 1 ||
                !scope.Matches(mapEpoch, scope.TileManager) ||
                !TryReadMoatRecordTile(
                    scope.TileManager, moatId, out int tileId, out int x, out int y))
            {
                return false;
            }

            int targetRegion = pathRegionGrid[tileId];
            if (targetRegion <= 0 || targetRegion > MaximumRegionId ||
                !scope.RegionDecisions.TryGetValue(targetRegion, out bool selectedByFallback) ||
                !selectedByFallback)
            {
                return false;
            }

            var plan = new PlanScope(scope.UnitId, x, y)
            {
                PlayerId = scope.PlayerId
            };
            if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                    plan, out RouteProbeSummary summary))
            {
                return false;
            }

            pending = new PendingDigMoatTarget(
                mapEpoch,
                scope.TileManager,
                scope.UnitId,
                scope.PlayerId,
                moatId,
                scope.StartX,
                scope.StartY,
                tileId,
                x,
                y,
                targetRegion,
                summary);
            return true;
        }

        private bool TryCreateMoatWorkSelectionScope(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode,
            out MoatWorkSelectionScope scope)
        {
            scope = null;
            if (disposed || tileManager == IntPtr.Zero ||
                tileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                (relationshipMode != 1 && relationshipMode != 2) ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_ControllableForPlayerId != playerId || !CanDigMoat(unit))
            {
                return false;
            }

            int startX = unit->r_CurrentTilePositionX;
            int startY = unit->r_CurrentTilePositionY;
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;
            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId))
                return false;

            scope = new MoatWorkSelectionScope(
                mapEpoch, tileManager, unitId, playerId, relationshipMode,
                startX, startY, startTileId, pathRegionGrid[startTileId]);
            return true;
        }

        private int AllowFillMoatApproachThroughFriendlyMoat(
            IntPtr tileManager, int sourceRegion, int moatTileId, int moatY)
        {
            int vanillaResult = originalHasFillMoatApproach(
                tileManager, sourceRegion, moatTileId, moatY);
            MoatWorkSelectionScope scope = activeMoatWorkSelection;
            if (vanillaResult != 0 || scope == null || scope.RelationshipMode != 2 ||
                !scope.Matches(mapEpoch, tileManager) || moatY < 0 || moatY >= MapWidth)
            {
                return vanillaResult;
            }

            try
            {
                scope.FillFallbackEvaluations++;
                if (!TryGetMoatRecord(
                        tileManager, moatTileId, moatY,
                        out int moatId, out int moatX, out int recordY))
                {
                    return vanillaResult;
                }
                if (!TryFindBestFillMoatApproach(
                        scope, moatId, moatTileId, moatX, recordY,
                        out MoatWorkApproach approach))
                {
                    return vanillaResult;
                }

                scope.FillApproaches[moatId] = approach;
                scope.MergeRoute(approach.Summary);
                scope.FillFallbackAllowed++;
                return 1;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("fill-moat-work-approach", ex);
                return vanillaResult;
            }
        }

        private bool TryGetMoatRecord(
            IntPtr tileManager,
            int moatTileId,
            int expectedY,
            out int moatId,
            out int moatX,
            out int moatY)
        {
            moatId = 0;
            moatX = -1;
            moatY = -1;
            if (!IsValidTileId(moatTileId) || getMoatIdAtTile == null)
                return false;
            moatId = getMoatIdAtTile(tileManager, moatTileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId > MaximumMoatRecordId || moatId >= moatCount)
                return false;
            byte* record = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            if (*(int*)(record + MoatRecordTileIdOffset) != moatTileId)
                return false;
            moatX = *(short*)(record + MoatRecordXOffset);
            moatY = *(short*)(record + MoatRecordYOffset);
            if (moatX < 0 || moatX >= MapWidth || moatY != expectedY)
                return false;
            UnmanagedVector2<ushort> tilePosition =
                GameTileManagerAPI.Instance.GetTileVectorFromId(moatTileId);
            return tilePosition.X == moatX && tilePosition.Y == moatY;
        }

        private bool TryFindBestFillMoatApproach(
            MoatWorkSelectionScope scope,
            int moatId,
            int moatTileId,
            int moatX,
            int moatY,
            out MoatWorkApproach best)
        {
            best = default;
            byte sourceHeight = nativeHeightLayer[scope.StartTileId];
            bool found = false;
            long bestDistance = long.MaxValue;
            RouteProbeSummary observed = new RouteProbeSummary(scope.PlayerId);
            int checkedTiles = 0;
            for (int index = 0; index < MoatWorkNeighbourX.Length; index++)
            {
                int x = moatX + MoatWorkNeighbourX[index];
                int y = moatY + MoatWorkNeighbourY[index];
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth)
                {
                    scope.FillInvalidTileRejected++;
                    continue;
                }
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (!IsValidTileId(tileId))
                {
                    scope.FillInvalidTileRejected++;
                    continue;
                }
                if ((int)nativeHeightLayer[tileId] > sourceHeight + 0x10)
                {
                    scope.FillHeightRejected++;
                    continue;
                }

                bool completedMoat = IsCompletedMoatTile(tileId);
                bool friendlyMoatEndpoint = completedMoat &&
                    IsFriendlyCompletedMoatForWeightedShadow(scope.PlayerId, tileId);
                if (completedMoat && !friendlyMoatEndpoint)
                {
                    scope.FillEnemyOrInvalidMoatRejected++;
                    continue;
                }
                uint flags = tileFlags[tileId];
                if (!completedMoat &&
                    (movementTargetAvailability[y * MapWidth + x] == 0 ||
                     (scope.ImprovedFillSelection
                         ? HasDownstreamMovementBlockingFlags(flags)
                         : (flags & OrdinaryWalkableTileFlag) == 0 ||
                           (flags & CursorSpecialStructureTileFlagMask) != 0)))
                {
                    scope.FillGroundBlockedRejected++;
                    continue;
                }
                if (scope.ImprovedFillSelection &&
                    IsOccupiedByOtherLivingUnit(tileId, scope.UnitId))
                {
                    scope.FillOccupiedRejected++;
                    continue;
                }

                checkedTiles++;
                if (friendlyMoatEndpoint)
                    scope.FillFriendlyMoatEndpoints++;
                RouteProbeSummary summary = new RouteProbeSummary(scope.PlayerId);
                bool vanillaRegionApproach = scope.ImprovedFillSelection && !completedMoat &&
                    pathRegionGrid[tileId] == scope.StartRegion;
                if (!vanillaRegionApproach &&
                    !TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint(
                        scope.UnitId, scope.PlayerId, scope.StartX, scope.StartY,
                        x, y, tileId, out summary))
                {
                    observed.MergeObservations(summary);
                    scope.FillOwnerRouteRejected++;
                    continue;
                }
                observed.MergeObservations(summary);
                long dx = scope.StartX - x;
                long dy = scope.StartY - y;
                long distance = dx * dx + dy * dy;
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    best = new MoatWorkApproach(
                        moatId, moatTileId, x, y, tileId, index,
                        summary);
                }
            }
            scope.CheckedApproachTiles += checkedTiles;
            scope.MergeRoute(observed);
            return found;
        }

        private int ResolveMoatWorkTileWithOwnerRoute(
            IntPtr tileManager, int moatId, int mode, uint sourceX, uint sourceY)
        {
            PendingDigMoatTarget pendingDig = pendingDigMoatTarget;
            bool pendingDigMatches = pendingDig != null && pendingDig.Matches(
                mapEpoch, tileManager, moatId, sourceX, sourceY);
            if (pendingDig != null && (!pendingDigMatches || mode != 1))
                pendingDigMoatTarget = null;
            PendingFillMoatApproach pending = pendingFillMoatApproach;
            bool pendingMatches = pending != null && pending.Matches(
                mapEpoch, tileManager, moatId, sourceX, sourceY);
            if (pending != null && !pendingMatches)
                pendingFillMoatApproach = null;
            try
            {
                if (tileManager == IntPtr.Zero ||
                    tileManager != GameTileManagerAPI.Instance.GetTileManager())
                {
                    pendingFillMoatApproach = null;
                    return originalResolveMoatWorkTile(
                        tileManager, moatId, mode, sourceX, sourceY);
                }
            }
            catch (Exception ex)
            {
                pendingFillMoatApproach = null;
                TryLogDiagnosticFailure("fill-moat-work-resolver-capture", ex);
                return originalResolveMoatWorkTile(
                    tileManager, moatId, mode, sourceX, sourceY);
            }
            byte* manager = (byte*)tileManager.ToPointer();
            int oldTileId = *(int*)(manager + SelectedMoatTileIdOffset);
            int oldX = *(int*)(manager + SelectedMoatApproachXOffset);
            int oldY = *(int*)(manager + SelectedMoatApproachYOffset);
            int vanillaResult = originalResolveMoatWorkTile(
                tileManager, moatId, mode, sourceX, sourceY);
            // Command 7 first resolves mode 1 to publish the target moat itself and then
            // immediately resolves mode 2 for the approach tile. Preserve an exact hand-off
            // across that mode-1 call, but discard it on every other deviation.
            if (mode == 1)
            {
                if (!pendingMatches || vanillaResult == 0)
                    pendingFillMoatApproach = null;
                if (pendingDigMatches)
                {
                    try
                    {
                        if (vanillaResult == pendingDig.TileId &&
                            *(int*)(manager + SelectedMoatApproachXOffset) == pendingDig.X &&
                            *(int*)(manager + SelectedMoatApproachYOffset) == pendingDig.Y &&
                            ValidatePendingDigTarget(pendingDig))
                        {
                            var plan = new PlanScope(pendingDig.UnitId, pendingDig.X, pendingDig.Y)
                            {
                                PlayerId = pendingDig.PlayerId,
                                FriendlyRouteQualified = true,
                                OwnerRouteProbeCompleted = true,
                                MoatWorkMovement = true,
                                MoatWorkTargetTileId = pendingDig.TileId
                            };
                            pendingPlan = plan;
                            LogResolvedDigMoatTarget(pendingDig);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                            pendingPlan = null;
                        TryLogDiagnosticFailure("dig-moat-work-resolver", ex);
                    }
                    finally
                    {
                        pendingDigMoatTarget = null;
                    }
                }
                return vanillaResult;
            }
            pendingFillMoatApproach = null;
            if (mode != 2)
                return vanillaResult;

            if (!pendingMatches)
            {
                return vanillaResult;
            }

            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        pending.UnitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_ControllableForPlayerId != pending.PlayerId || !CanDigMoat(unit) ||
                    unit->r_CurrentTilePositionX != pending.StartX ||
                    unit->r_CurrentTilePositionY != pending.StartY ||
                    !ValidatePendingFillApproach(pending))
                {
                    return vanillaResult;
                }

                *(int*)(manager + SelectedMoatTileIdOffset) = pending.Approach.MoatTileId;
                *(int*)(manager + SelectedMoatApproachXOffset) = pending.Approach.X;
                *(int*)(manager + SelectedMoatApproachYOffset) = pending.Approach.Y;
                pendingPlan = new PlanScope(
                    pending.UnitId, pending.Approach.X, pending.Approach.Y)
                {
                    PlayerId = pending.PlayerId,
                    FriendlyRouteQualified = true,
                    OwnerRouteProbeCompleted = true,
                    MoatWorkMovement = true,
                    MoatWorkTargetTileId = pending.Approach.MoatTileId
                };
                if (pending.Approach.Summary.RouteFound)
                    LogResolvedFillMoatApproach(pending);
                return pending.Approach.TileId;
            }
            catch (Exception ex)
            {
                *(int*)(manager + SelectedMoatTileIdOffset) = oldTileId;
                *(int*)(manager + SelectedMoatApproachXOffset) = oldX;
                *(int*)(manager + SelectedMoatApproachYOffset) = oldY;
                if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                    pendingPlan = null;
                TryLogDiagnosticFailure("fill-moat-work-resolver", ex);
                return vanillaResult;
            }
        }

        private bool ValidatePendingDigTarget(PendingDigMoatTarget pending)
        {
            if (pending == null || pending.MapEpoch != mapEpoch ||
                pending.TileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(
                    pending.UnitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_ControllableForPlayerId != pending.PlayerId || !CanDigMoat(unit) ||
                unit->r_CurrentTilePositionX != pending.StartX ||
                unit->r_CurrentTilePositionY != pending.StartY ||
                !TryReadMoatRecordTile(
                    pending.TileManager, pending.MoatId,
                    out int tileId, out int x, out int y) ||
                tileId != pending.TileId || x != pending.X || y != pending.Y ||
                pathRegionGrid[tileId] != pending.TargetRegion)
            {
                return false;
            }

            var plan = new PlanScope(pending.UnitId, pending.X, pending.Y)
            {
                PlayerId = pending.PlayerId
            };
            return TryFindRequiredFriendlyCompletedMoatRouteForPlan(plan, out _);
        }

        private bool ValidatePendingFillApproach(PendingFillMoatApproach pending)
        {
            MoatWorkApproach approach = pending.Approach;
            int sourceTileId = GameTileManagerAPI.Instance.GetTileId(
                pending.StartX, pending.StartY);
            if (approach.MoatId != pending.MoatId ||
                approach.NativeOrder < 0 || approach.NativeOrder >= MoatWorkNeighbourX.Length ||
                !IsValidTileId(sourceTileId) || !IsValidTileId(approach.MoatTileId) ||
                !IsValidTileId(approach.TileId) ||
                approach.X < 0 || approach.X >= MapWidth ||
                approach.Y < 0 || approach.Y >= MapWidth ||
                GameTileManagerAPI.Instance.GetTileId(approach.X, approach.Y) != approach.TileId ||
                (int)nativeHeightLayer[approach.TileId] >
                    nativeHeightLayer[sourceTileId] + 0x10)
            {
                return false;
            }
            bool completedMoat = IsCompletedMoatTile(approach.TileId);
            if (completedMoat)
            {
                if (!IsFriendlyCompletedMoatForWeightedShadow(
                        pending.PlayerId, approach.TileId))
                {
                    return false;
                }
            }
            else
            {
                uint flags = tileFlags[approach.TileId];
                if (movementTargetAvailability[approach.Y * MapWidth + approach.X] == 0 ||
                    (pending.ImprovedFillSelection
                        ? HasDownstreamMovementBlockingFlags(flags)
                        : (flags & OrdinaryWalkableTileFlag) == 0 ||
                          (flags & CursorSpecialStructureTileFlagMask) != 0))
                {
                    return false;
                }
            }
            if (pending.ImprovedFillSelection &&
                IsOccupiedByOtherLivingUnit(approach.TileId, pending.UnitId))
                return false;
            if (!TryReadMoatRecordTile(
                    pending.TileManager, pending.MoatId,
                    out int moatTileId, out int moatX, out int moatY) ||
                moatTileId != approach.MoatTileId ||
                moatX + MoatWorkNeighbourX[approach.NativeOrder] != approach.X ||
                moatY + MoatWorkNeighbourY[approach.NativeOrder] != approach.Y)
            {
                return false;
            }
            if (pending.ImprovedFillSelection && !completedMoat &&
                pathRegionGrid[approach.TileId] == pathRegionGrid[sourceTileId])
                return true;
            return TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint(
                pending.UnitId,
                pending.PlayerId,
                pending.StartX,
                pending.StartY,
                approach.X,
                approach.Y,
                approach.TileId,
                out _);
        }

        private bool TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint(
            int unitId,
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int targetTileId,
            out RouteProbeSummary summary)
        {
            summary = new RouteProbeSummary(playerId);
            if (!IsCompletedMoatTile(targetTileId))
            {
                var plan = new PlanScope(unitId, targetX, targetY)
                {
                    PlayerId = playerId
                };
                return TryFindRequiredFriendlyCompletedMoatRouteForPlan(plan, out summary);
            }
            if (!IsFriendlyCompletedMoatForWeightedShadow(playerId, targetTileId))
                return false;

            if (GameTileManagerAPI.Instance.GetTileManager() == IntPtr.Zero ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                return false;

            // A completed moat endpoint is regionless. The shared tile graph is now
            // region-independent, so the exact endpoint can be inspected directly.
            int targetCell = targetY * MapWidth + targetX;
            EnsureReachabilityMap(playerId, startX, startY);
            summary = GetCachedRouteSummaryForTarget(targetX, targetY);
            bool reached = gridGeneration > 0 && visitedWithMoat != null &&
                visitedWithMoat[targetCell] == gridGeneration;
            summary.AttackProbeEvaluated = true;
            summary.ReachedWithMoat = reached;
            summary.ReachedWithoutMoat = false;
            summary.RouteFound = reached && summary.FriendlyMoatTiles > 0;
            return summary.RouteFound;
        }

        private bool TryAllowDigWorkRegionPair(
            IntPtr pathManager,
            int playerId,
            int sourceRegion,
            int targetRegion,
            int movementProfile,
            int vanillaResult)
        {
            try
            {
                MoatWorkSelectionScope scope = activeMoatWorkSelection;
                if (vanillaResult != 0 || scope == null || scope.RelationshipMode != 1 ||
                    pathManager != nativePathManager || playerId != scope.PlayerId ||
                    sourceRegion < 0 || sourceRegion > MaximumRegionId ||
                    targetRegion <= 0 || targetRegion > MaximumRegionId)
                {
                    return false;
                }
                return EvaluateDigWorkRegion(scope, targetRegion, "E2610", movementProfile);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("dig-moat-work-region-pair", ex);
                return false;
            }
        }

        private bool TryAllowDigWorkRegionSearch(
            IntPtr pathManager,
            int playerId,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            out int effectiveResult)
        {
            effectiveResult = vanillaResult;
            try
            {
                MoatWorkSelectionScope scope = activeMoatWorkSelection;
                if (scope == null || scope.RelationshipMode != 1 ||
                    pathManager != nativePathManager || playerId != scope.PlayerId ||
                    targetRegion <= 0 || targetRegion > MaximumRegionId ||
                    startX != scope.StartX || startY != scope.StartY ||
                    vanillaResult != 0)
                {
                    return false;
                }
                if (!EvaluateDigWorkRegion(scope, targetRegion, "E7C40", 0))
                    return false;
                effectiveResult = targetRegion;
                return true;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("dig-moat-work-region-search", ex);
                effectiveResult = vanillaResult;
                return false;
            }
        }

        private bool EvaluateDigWorkRegion(
            MoatWorkSelectionScope scope,
            int targetRegion,
            string helper,
            int movementProfile)
        {
            if (!scope.Matches(mapEpoch, scope.TileManager) || disposed ||
                scope.TileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(
                    scope.UnitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_ControllableForPlayerId != scope.PlayerId || !CanDigMoat(unit) ||
                unit->r_CurrentTilePositionX != scope.StartX ||
                unit->r_CurrentTilePositionY != scope.StartY)
            {
                return false;
            }
            if (scope.RegionDecisions.TryGetValue(targetRegion, out bool cached))
                return cached;

            scope.DigFallbackEvaluations++;
            EnsureReachabilityMap(scope.PlayerId, scope.StartX, scope.StartY);
            RouteProbeSummary summary = GetCachedRouteSummaryForRegion(targetRegion);
            bool allowed = summary.ReachedWithMoat && !summary.ReachedWithoutMoat &&
                summary.FriendlyMoatTiles > 0;
            summary.RouteFound = allowed;
            summary.AttackProbeEvaluated = true;
            scope.RegionDecisions[targetRegion] = allowed;
            scope.RegionSummaries[targetRegion] = summary;
            scope.LastRegionHelper = helper;
            scope.LastMovementProfile = movementProfile;
            scope.MergeRoute(summary);
            if (allowed)
                scope.DigFallbackAllowed++;
            return allowed;
        }

        private void LogMoatWorkSelection(MoatWorkSelectionScope scope)
        {
            if (scope.DigFallbackEvaluations == 0 && scope.FillFallbackEvaluations == 0)
                return;
            bool selectedByFallback = false;
            int selectedRegion = 0;
            RouteProbeSummary selectedRoute = default;
            if (scope.SelectedMoatId > 0 && TryReadMoatRecordTile(
                    scope.TileManager, scope.SelectedMoatId,
                    out int selectedTileId, out _, out _))
            {
                selectedRegion = pathRegionGrid[selectedTileId];
                selectedByFallback = scope.RelationshipMode == 1
                    ? scope.RegionDecisions.TryGetValue(selectedRegion, out bool allowed) && allowed
                    : scope.FillApproaches.ContainsKey(scope.SelectedMoatId);
                if (scope.RelationshipMode == 1)
                    scope.RegionSummaries.TryGetValue(selectedRegion, out selectedRoute);
                else if (scope.FillApproaches.TryGetValue(
                    scope.SelectedMoatId, out MoatWorkApproach selectedApproach))
                {
                    selectedRoute = selectedApproach.Summary;
                }
            }
            string kind = scope.RelationshipMode == 1 ? "dig" : "fill";
            string signature =
                $"{mapEpoch}:{kind}:{scope.StartTileId}:{scope.SelectedMoatId}:" +
                $"{scope.DigFallbackEvaluations}:{scope.DigFallbackAllowed}:" +
                $"{scope.FillFallbackEvaluations}:{scope.FillFallbackAllowed}:" +
                $"{scope.CheckedApproachTiles}:{scope.FillFriendlyMoatEndpoints}:" +
                $"{scope.FillInvalidTileRejected}:{scope.FillHeightRejected}:" +
                $"{scope.FillEnemyOrInvalidMoatRejected}:{scope.FillGroundBlockedRejected}:" +
                $"{scope.FillOccupiedRejected}:{scope.FillOwnerRouteRejected}:" +
                $"{selectedByFallback}:{selectedRegion}:" +
                $"{scope.Route.FriendlyMoatTiles}:{scope.Route.EnemyMoatTiles}";
            if (lastMoatWorkSelectionByUnit.TryGetValue(scope.UnitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            lastMoatWorkSelectionByUnit[scope.UnitId] = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=work-target-selection-fallback kind={kind} unit={scope.UnitId} " +
                $"player={scope.PlayerId} start=({scope.StartX},{scope.StartY})/" +
                $"region={scope.StartRegion} selectedMoat={scope.SelectedMoatId} " +
                $"selectedRegion={selectedRegion} selectedByFallback={selectedByFallback} " +
                $"digRegions={scope.DigFallbackAllowed}/{scope.DigFallbackEvaluations} " +
                $"fillApproaches={scope.FillFallbackAllowed}/{scope.FillFallbackEvaluations} " +
                $"regionHelper={scope.LastRegionHelper ?? "none"} " +
                $"movementProfile={scope.LastMovementProfile} " +
                $"checkedApproachTiles={scope.CheckedApproachTiles} " +
                $"friendlyMoatEndpoints={scope.FillFriendlyMoatEndpoints} " +
                $"rejectedInvalid={scope.FillInvalidTileRejected} " +
                $"rejectedHeight={scope.FillHeightRejected} " +
                $"rejectedEnemyOrInvalidMoat={scope.FillEnemyOrInvalidMoatRejected} " +
                $"rejectedGroundBlocked={scope.FillGroundBlockedRejected} " +
                $"rejectedOccupied={scope.FillOccupiedRejected} " +
                $"rejectedOwnerRoute={scope.FillOwnerRouteRejected} " +
                $"selectedRoute=[{selectedRoute.ToLogFields()}] " +
                $"observedRoutes=[{scope.Route.ToLogFields()}].");
        }

        private void LogResolvedFillMoatApproach(PendingFillMoatApproach pending)
        {
            MoatWorkApproach approach = pending.Approach;
            string signature =
                $"{mapEpoch}:{pending.MoatId}:{approach.MoatTileId}:{approach.TileId}:" +
                $"{approach.X}:{approach.Y}:{approach.NativeOrder}:" +
                $"{approach.Summary.ObservedOwnerMask}";
            if (lastMoatWorkApproachByUnit.TryGetValue(
                    pending.UnitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            lastMoatWorkApproachByUnit[pending.UnitId] = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=work-approach-tile kind=fill unit={pending.UnitId} " +
                $"player={pending.PlayerId} moat={pending.MoatId}/tile={approach.MoatTileId} " +
                $"approach=({approach.X},{approach.Y})/{approach.TileId} " +
                $"nativeOrder={approach.NativeOrder} handoff=owner-qualified-plan " +
                $"{approach.Summary.ToLogFields()}.");
        }

        private void LogResolvedDigMoatTarget(PendingDigMoatTarget pending)
        {
            string signature =
                $"{mapEpoch}:dig:{pending.MoatId}:{pending.TileId}:" +
                $"{pending.X}:{pending.Y}:{pending.Summary.ObservedOwnerMask}";
            if (lastMoatWorkApproachByUnit.TryGetValue(
                    pending.UnitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            lastMoatWorkApproachByUnit[pending.UnitId] = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=work-approach-tile kind=dig unit={pending.UnitId} " +
                $"player={pending.PlayerId} moat={pending.MoatId}/tile={pending.TileId} " +
                $"approach=({pending.X},{pending.Y})/{pending.TileId} " +
                $"selectedRegion={pending.TargetRegion} handoff=owner-qualified-plan " +
                $"{pending.Summary.ToLogFields()}.");
        }

        private bool TryReadMoatRecordTile(
            IntPtr tileManager, int moatId, out int tileId, out int x, out int y)
        {
            tileId = -1;
            x = -1;
            y = -1;
            if (tileManager == IntPtr.Zero)
                return false;
            int count = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId > MaximumMoatRecordId || moatId >= count)
                return false;
            byte* record = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            tileId = *(int*)(record + MoatRecordTileIdOffset);
            x = *(short*)(record + MoatRecordXOffset);
            y = *(short*)(record + MoatRecordYOffset);
            return IsValidTileId(tileId) && x >= 0 && x < MapWidth &&
                y >= 0 && y < MapWidth &&
                GameTileManagerAPI.Instance.GetTileId(x, y) == tileId;
        }

        private static bool HasDownstreamMovementBlockingFlags(uint flags) =>
            (flags & MovementBlockedLowTileFlagMask) != 0 ||
            (flags & MovementBlockedStructureTileFlagMask) != 0;

        private bool IsOccupiedByOtherLivingUnit(int tileId, int currentUnitId)
        {
            int occupantUnitId = GameTileManagerAPI.Instance.GetTileUnitId(tileId);
            if (occupantUnitId == 0 || occupantUnitId == currentUnitId)
                return false;
            return GameUnitManagerAPI.Instance.TryGetUnitById(
                    occupantUnitId, out GameUnit* occupant) &&
                occupant != null && occupant->r_AliveState == AliveState.IsAlive;
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
            return IsValidTileId(tileId) && x >= 0 && x < MapWidth && y >= 0 && y < MapWidth &&
                GameTileManagerAPI.Instance.GetTileId(x, y) == tileId;
        }

        private static void RestoreExcludedMoatReservations(List<ExcludedMoatReservation> exclusions)
        {
            if (exclusions == null)
                return;
            for (int index = 0; index < exclusions.Count; index++)
                exclusions[index].Restore();
        }

        private void DisposeMoatWorkTargetSelection()
        {
            hasFillMoatApproachDetour?.Dispose();
            resolveMoatWorkTileDetour?.Dispose();
            findMoatWorkTargetDetour?.Dispose();
            hasFillMoatApproachDetour = null;
            resolveMoatWorkTileDetour = null;
            findMoatWorkTargetDetour = null;
            originalHasFillMoatApproach = null;
            originalResolveMoatWorkTile = null;
            originalFindMoatWorkTarget = null;
            rootedHasFillMoatApproach = null;
            rootedResolveMoatWorkTile = null;
            rootedFindMoatWorkTarget = null;
            ResetMoatWorkTargetSelection();
        }

        private void ResetMoatWorkTargetSelection()
        {
            activeMoatWorkSelection = null;
            pendingFillMoatApproach = null;
            pendingDigMoatTarget = null;
            lastMoatWorkSelectionByUnit.Clear();
            lastMoatWorkApproachByUnit.Clear();
        }

        private sealed class MoatWorkSelectionScope
        {
            public MoatWorkSelectionScope(
                int mapEpoch,
                IntPtr tileManager,
                int unitId,
                int playerId,
                int relationshipMode,
                int startX,
                int startY,
                int startTileId,
                int startRegion)
            {
                MapEpoch = mapEpoch;
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                RelationshipMode = relationshipMode;
                StartX = startX;
                StartY = startY;
                StartTileId = startTileId;
                StartRegion = startRegion;
                Route = new RouteProbeSummary(playerId);
            }

            public int MapEpoch { get; }
            public IntPtr TileManager { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int RelationshipMode { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int StartTileId { get; }
            public int StartRegion { get; }
            public int SelectedMoatId { get; set; }
            public int DigFallbackEvaluations { get; set; }
            public int DigFallbackAllowed { get; set; }
            public int FillFallbackEvaluations { get; set; }
            public int FillFallbackAllowed { get; set; }
            public int CheckedApproachTiles { get; set; }
            public int FillFriendlyMoatEndpoints { get; set; }
            public int FillInvalidTileRejected { get; set; }
            public int FillHeightRejected { get; set; }
            public int FillEnemyOrInvalidMoatRejected { get; set; }
            public int FillGroundBlockedRejected { get; set; }
            public int FillOwnerRouteRejected { get; set; }
            public int FillOccupiedRejected { get; set; }
            public bool ImprovedFillSelection { get; set; }
            public string LastRegionHelper { get; set; }
            public int LastMovementProfile { get; set; }
            public RouteProbeSummary Route;
            public Dictionary<int, bool> RegionDecisions { get; } =
                new Dictionary<int, bool>();
            public Dictionary<int, RouteProbeSummary> RegionSummaries { get; } =
                new Dictionary<int, RouteProbeSummary>();
            public Dictionary<int, MoatWorkApproach> FillApproaches { get; } =
                new Dictionary<int, MoatWorkApproach>();

            public bool Matches(int mapEpoch, IntPtr tileManager) =>
                MapEpoch == mapEpoch && TileManager == tileManager;

            public void MergeRoute(RouteProbeSummary summary) => Route.MergeObservations(summary);
        }

        private readonly struct MoatWorkApproach
        {
            public MoatWorkApproach(
                int moatId,
                int moatTileId,
                int x,
                int y,
                int tileId,
                int nativeOrder,
                RouteProbeSummary summary)
            {
                MoatId = moatId;
                MoatTileId = moatTileId;
                X = x;
                Y = y;
                TileId = tileId;
                NativeOrder = nativeOrder;
                Summary = summary;
            }

            public int MoatId { get; }
            public int MoatTileId { get; }
            public int X { get; }
            public int Y { get; }
            public int TileId { get; }
            public int NativeOrder { get; }
            public RouteProbeSummary Summary { get; }
        }

        private readonly struct ExcludedMoatReservation
        {
            public ExcludedMoatReservation(byte* record, byte originalReservation)
            {
                Record = record;
                OriginalReservation = originalReservation;
            }

            private byte* Record { get; }
            private byte OriginalReservation { get; }
            public void Restore() => Record[MoatRecordReservationOffset] = OriginalReservation;
        }

        private sealed class PendingFillMoatApproach
        {
            public PendingFillMoatApproach(
                int mapEpoch,
                IntPtr tileManager,
                int unitId,
                int playerId,
                int moatId,
                int startX,
                int startY,
                MoatWorkApproach approach,
                bool improvedFillSelection)
            {
                MapEpoch = mapEpoch;
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                MoatId = moatId;
                StartX = startX;
                StartY = startY;
                Approach = approach;
                ImprovedFillSelection = improvedFillSelection;
            }

            public int MapEpoch { get; }
            public IntPtr TileManager { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int MoatId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public MoatWorkApproach Approach { get; }
            public bool ImprovedFillSelection { get; }

            public bool Matches(
                int mapEpoch,
                IntPtr tileManager,
                int moatId,
                uint sourceX,
                uint sourceY) =>
                MapEpoch == mapEpoch && TileManager == tileManager && MoatId == moatId &&
                sourceX == unchecked((uint)StartX) && sourceY == unchecked((uint)StartY);
        }

        private sealed class PendingDigMoatTarget
        {
            public PendingDigMoatTarget(
                int mapEpoch,
                IntPtr tileManager,
                int unitId,
                int playerId,
                int moatId,
                int startX,
                int startY,
                int tileId,
                int x,
                int y,
                int targetRegion,
                RouteProbeSummary summary)
            {
                MapEpoch = mapEpoch;
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                MoatId = moatId;
                StartX = startX;
                StartY = startY;
                TileId = tileId;
                X = x;
                Y = y;
                TargetRegion = targetRegion;
                Summary = summary;
            }

            public int MapEpoch { get; }
            public IntPtr TileManager { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int MoatId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int TileId { get; }
            public int X { get; }
            public int Y { get; }
            public int TargetRegion { get; }
            public RouteProbeSummary Summary { get; }

            public bool Matches(
                int mapEpoch,
                IntPtr tileManager,
                int moatId,
                uint sourceX,
                uint sourceY) =>
                MapEpoch == mapEpoch && TileManager == tileManager && MoatId == moatId &&
                sourceX == unchecked((uint)StartX) && sourceY == unchecked((uint)StartY);
        }
    }
}
