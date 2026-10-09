using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int FindMoatWorkTargetDelegate(
            IntPtr tileManager, int playerId, int unitId, int relationshipMode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int ResolveMoatWorkTileDelegate(
            IntPtr tileManager, int moatId, int mode, uint sourceX, uint sourceY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int HasFillMoatApproachDelegate(
            IntPtr tileManager, int sourceRegion, int moatTileId, int moatY);
        internal void TryInstallMoatWorkTargetSelection(
            ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            HookTransaction pendingTransaction = null;
            RedBirdDetour<FindMoatWorkTargetDelegate> pendingFind = null;
            RedBirdDetour<ResolveMoatWorkTileDelegate> pendingResolve = null;
            RedBirdDetour<HasFillMoatApproachDelegate> pendingFillApproach = null;
            try
            {
                if (regionPairReachabilityDetour == null || originalRegionPairReachability == null ||
                    regionReachabilityDetour == null || originalRegionReachability == null)
                {
                    throw new InvalidOperationException(
                        "A shared E2610/E7C40 reachability hook is unavailable.");
                }

                APIShared.Internal.NativeResolution findResolution = Resolve(
                    memory, FindMoatWorkTargetPattern, FindMoatWorkTargetRva,
                    "shared moat work-target selector");
                APIShared.Internal.NativeResolution resolveResolution = Resolve(
                    memory, ResolveMoatWorkTilePattern, ResolveMoatWorkTileRva,
                    "shared moat work-tile resolver");
                APIShared.Internal.NativeResolution fillApproachResolution = Resolve(
                    memory, HasFillMoatApproachPattern, HasFillMoatApproachRva,
                    "fill-moat neighbouring approach check");

                ValidateMoatWorkTargetContracts(memory);

                rootedFindMoatWorkTarget = FindMoatWorkTargetWithOwnerRoute;
                rootedResolveMoatWorkTile = ResolveMoatWorkTileWithOwnerRoute;
                rootedHasFillMoatApproach = AllowFillMoatApproachThroughFriendlyMoat;

                pendingTransaction = CreateOwnedHookTransaction();
                pendingFind = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)findResolution.Rva),
                    rootedFindMoatWorkTarget);
                pendingResolve = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)resolveResolution.Rva),
                    rootedResolveMoatWorkTile);
                pendingFillApproach = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)fillApproachResolution.Rva),
                    rootedHasFillMoatApproach);
                var commitResult = CommitPermanentHooks(pendingTransaction);
                originalFindMoatWorkTarget = pendingFind.Original;
                originalResolveMoatWorkTile = pendingResolve.Original;
                originalHasFillMoatApproach = pendingFillApproach.Original;
                ValidatePublishedCommandHooks(pendingTransaction);

                if (!commitResult.IsCompleteSuccess || !pendingFind.Committed ||
                    !pendingResolve.Committed || !pendingFillApproach.Committed)
                {
                    throw new InvalidOperationException(
                        $"The moat-work hooks were not installed atomically: {commitResult}.");
                }

                findMoatWorkTargetDetour = pendingFind;
                resolveMoatWorkTileDetour = pendingResolve;
                APIShared.Internal.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL friendly-moat-movement moat-work target selection installed: " +
                    $"selector=0x{findResolution.Rva:X}, resolver=0x{resolveResolution.Rva:X}, " +
                    $"fillApproach=0x{fillApproachResolution.Rva:X}, " +
                    $"regionPair=0x{RegionPairReachabilityRva:X}, " +
                    $"regionSearch=0x{RegionReachabilityRva:X}.");
                hasFillMoatApproachDetour = pendingFillApproach;
                moatWorkHookTransaction = pendingTransaction;
            }
            catch (Exception ex)
            {
                if (IsPublished(pendingTransaction))
                {
                    disposed = true;
                    TryLogDiagnosticFailure("published-command-initialization", new InvalidOperationException("Published hooks retained; command extensions disabled."));
                    return;
                }
                try { RollbackUnpublishedTransaction(pendingTransaction); } catch { }
                moatWorkHookTransaction = null;
                hasFillMoatApproachDetour = null;
                resolveMoatWorkTileDetour = null;
                findMoatWorkTargetDetour = null;
                rootedFindMoatWorkTarget = null;
                rootedResolveMoatWorkTile = null;
                rootedHasFillMoatApproach = null;
                ResetMoatWorkTargetSelection();
                APIShared.Internal.DebugLogHelper.LogError(
                    log,
                    "Bugfixes and QoL friendly-moat-movement moat-work target selection was not installed; " +
                    $"existing movement remains active and work-target selection stays Vanilla: {ex}");
            }
        }

        internal static void ValidateMoatWorkTargetContracts(ReadOnlySpan<byte> memory)
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



        internal void RollbackUnpublishedMoatWorkTargetSelection()
        {
            RollbackUnpublishedTransaction(moatWorkHookTransaction);
            moatWorkHookTransaction = null;
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
    }
}
