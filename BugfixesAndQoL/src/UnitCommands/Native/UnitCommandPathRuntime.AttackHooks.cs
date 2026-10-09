using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal void TryInstallBuildingCursorReachability(
            ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            HookTransaction pendingTransaction = null;
            RedBirdDetour<BuildingCursorReachabilityDelegate> pendingBuildingCursor = null;
            try
            {
                APIShared.Internal.NativeResolution buildingCursorResolution = Resolve(
                    memory, BuildingCursorReachabilityPattern, BuildingCursorReachabilityRva,
                    "building cursor approach reachability helper");
                ValidateExactBytes(
                    memory, BuildingCursorReachabilityRva,
                    new byte[]
                    {
                        0x48, 0x89, 0x5C, 0x24, 0x08, 0x55, 0x56, 0x57,
                        0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
                        0x48, 0x83, 0xEC, 0x40, 0x4C, 0x8B, 0xE1, 0x85,
                        0xD2
                    },
                    "building cursor approach reachability detour span");
                ValidateCallTarget(
                    memory, BuildingCursorReachabilityCallRva, BuildingCursorReachabilityRva,
                    new byte[] { 0xE8, 0xC5, 0x90, 0x02, 0x00 },
                    "building cursor approach reachability call");

                rootedBuildingCursorReachability = AllowBuildingCursorThroughCompletedMoat;
                pendingTransaction = CreateOwnedHookTransaction();
                pendingBuildingCursor = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)buildingCursorResolution.Rva),
                    rootedBuildingCursorReachability);
                CommitResult commitResult = CommitPermanentHooks(pendingTransaction);
                originalBuildingCursorReachability = pendingBuildingCursor.Original;
                ValidatePublishedCommandHooks(pendingTransaction);

                if (!commitResult.IsCompleteSuccess || !pendingBuildingCursor.Committed)
                    throw new InvalidOperationException(
                        $"The building cursor hook was not installed atomically: {commitResult}.");
                buildingCursorReachabilityDetour = pendingBuildingCursor;
                buildingCursorHookTransaction = pendingTransaction;
                APIShared.Internal.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL friendly-moat-movement building cursor reachability installed: " +
                    $"helper=0x{buildingCursorResolution.Rva:X}, " +
                    $"call=0x{BuildingCursorReachabilityCallRva:X}.");
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
                buildingCursorHookTransaction = null;
                buildingCursorReachabilityDetour = null;
                rootedBuildingCursorReachability = null;
                APIShared.Internal.DebugLogHelper.LogError(
                    log,
                    "Bugfixes and QoL friendly-moat-movement building cursor reachability was not installed; " +
                    $"building cursors remain Vanilla and the movement feature remains active: {ex}");
            }
        }

        internal void TryInstallAttackApproachDiagnostics(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            HookTransaction pendingTransaction = null;
            RedBirdDetour<AttackApproachFloodBuilderDelegate> pendingUnitFlood = null;
            RedBirdDetour<BuildingApproachBuilderDelegate> pendingBuildingApproach = null;
            RedBirdDetour<BuildingCandidateConsumerDelegate> pendingBuildingConsumer = null;
            RedBirdDetour<RegionPairReachabilityDelegate> pendingRegionPair = null;
            try
            {
                APIShared.Internal.NativeResolution unitFloodResolution = Resolve(
                    memory, AttackApproachFloodBuilderPattern, AttackApproachFloodBuilderRva,
                    "unit attack-approach flood builder");
                APIShared.Internal.NativeResolution buildingApproachResolution = Resolve(
                    memory, BuildingApproachBuilderPattern, BuildingApproachBuilderRva,
                    "building attack-approach builder");
                APIShared.Internal.NativeResolution buildingConsumerResolution = Resolve(
                    memory, BuildingCandidateConsumerPattern, BuildingCandidateConsumerRva,
                    "building attack candidate consumer");
                APIShared.Internal.NativeResolution regionPairResolution = Resolve(
                    memory, RegionPairReachabilityPattern, RegionPairReachabilityRva,
                    "attack-approach region-pair reachability helper");
                APIShared.Internal.NativeResolution directFillApproachResolution = Resolve(
                    memory, DirectFillApproachPattern, DirectFillApproachRva,
                    "direct FillMoat approach search");

                ValidateAttackApproachEntries(memory);
                ValidateAttackApproachCalls(memory);

                rootedAttackApproachFloodBuilder = ObserveAttackApproachFloodBuilder;
                rootedBuildingApproachBuilder = ObserveBuildingApproachBuilder;
                rootedBuildingCandidateConsumer = ObserveBuildingCandidateConsumer;
                rootedRegionPairReachability = ObserveScopedRegionPairReachability;

                pendingTransaction = CreateOwnedHookTransaction();
                pendingUnitFlood = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)unitFloodResolution.Rva),
                    rootedAttackApproachFloodBuilder);
                pendingBuildingApproach = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)buildingApproachResolution.Rva),
                    rootedBuildingApproachBuilder);
                pendingBuildingConsumer = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)buildingConsumerResolution.Rva),
                    rootedBuildingCandidateConsumer);
                pendingRegionPair = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)regionPairResolution.Rva),
                    rootedRegionPairReachability);
                CommitResult commitResult = CommitPermanentHooks(pendingTransaction);
                originalAttackApproachFloodBuilder = pendingUnitFlood.Original;
                originalBuildingApproachBuilder = pendingBuildingApproach.Original;
                originalBuildingCandidateConsumer = pendingBuildingConsumer.Original;
                originalRegionPairReachability = pendingRegionPair.Original;
                ValidatePublishedCommandHooks(pendingTransaction);

                if (!commitResult.IsCompleteSuccess || !pendingUnitFlood.Committed ||
                    !pendingBuildingApproach.Committed || !pendingBuildingConsumer.Committed ||
                    !pendingRegionPair.Committed)
                {
                    throw new InvalidOperationException(
                        $"The attack-approach hooks were not installed atomically: {commitResult}.");
                }

                attackApproachFloodBuilderDetour = pendingUnitFlood;
                buildingApproachBuilderDetour = pendingBuildingApproach;
                buildingCandidateConsumerDetour = pendingBuildingConsumer;
                regionPairReachabilityDetour = pendingRegionPair;
                attackApproachHookTransaction = pendingTransaction;
                APIShared.Internal.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL friendly-moat-movement attack-approach hooks installed: " +
                    $"unitFlood=0x{unitFloodResolution.Rva:X}, " +
                    $"buildingApproach=0x{buildingApproachResolution.Rva:X}, " +
                    $"buildingConsumer=0x{buildingConsumerResolution.Rva:X}, " +
                    $"regionPair=0x{regionPairResolution.Rva:X}, " +
                    $"vanillaGroupMode=0x{GetTribeMovementModeRva:X}, " +
                    $"directFillApproach=0x{directFillApproachResolution.Rva:X}->" +
                    $"0x{DirectFillApproachRegionPairCallRva:X}.");
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
                attackApproachHookTransaction = null;
                attackApproachFloodBuilderDetour = null;
                buildingApproachBuilderDetour = null;
                buildingCandidateConsumerDetour = null;
                regionPairReachabilityDetour = null;
                rootedAttackApproachFloodBuilder = null;
                rootedBuildingApproachBuilder = null;
                rootedBuildingCandidateConsumer = null;
                rootedRegionPairReachability = null;
                APIShared.Internal.DebugLogHelper.LogError(
                    log,
                    "Bugfixes and QoL friendly-moat-movement shared E2610/attack-approach hooks were not installed; " +
                    "the ladder attack fix, building commands, and direct FillMoat staging remain Vanilla while the " +
                    $"existing movement feature remains active: {ex}");
            }
        }

        internal static void ValidateAttackApproachEntries(ReadOnlySpan<byte> memory)
        {
            ValidateExactBytes(
                memory, AttackApproachFloodBuilderRva,
                new byte[]
                {
                    0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x56, 0x41,
                    0x54, 0x41, 0x55, 0x41, 0x56, 0x48, 0x83, 0xEC,
                    0x60, 0x48, 0x8B, 0xD9, 0x4D, 0x63, 0xE9, 0x45,
                    0x33, 0xF6, 0x48, 0x8D, 0x0D, 0x9F, 0xAA, 0xBE,
                    0x07, 0x45, 0x8B, 0xE6, 0x44, 0x89, 0x74, 0x24,
                    0x3C, 0xE8, 0x92, 0xBB, 0x03, 0x00, 0x48, 0x63,
                    0xF0, 0x41, 0x81, 0xFD, 0x1F, 0x03, 0x00, 0x00
                },
                "unit attack-approach flood builder entry");
            ValidateExactBytes(
                memory, BuildingApproachBuilderRva,
                new byte[]
                {
                    0x48, 0x89, 0x4C, 0x24, 0x08, 0x53, 0x55, 0x56,
                    0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41,
                    0x57, 0x48, 0x83, 0xEC, 0x78, 0x48, 0x8D, 0x0D,
                    0x74, 0x2B, 0x3F, 0x06, 0x4C, 0x63, 0xD2, 0x49,
                    0x69, 0xD2, 0x88, 0x06, 0x00, 0x00, 0x4D, 0x63,
                    0xF0, 0x4C, 0x8D, 0x25, 0xB0, 0x5F, 0xF2, 0xFF,
                    0x4D, 0x69, 0xFE, 0x2C, 0x03, 0x00, 0x00, 0x33,
                    0xED, 0x41, 0x8B, 0xD9, 0x44, 0x8B, 0xED, 0x4C
                },
                "building attack-approach builder entry");
            ValidateExactBytes(
                memory, BuildingCandidateConsumerRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74,
                    0x24, 0x10, 0x57, 0x48, 0x83, 0xEC, 0x40, 0x48,
                    0x63, 0xDA, 0x41, 0x8B, 0xF0, 0x8B, 0xD3, 0x48,
                    0x8B, 0xF9, 0xE8, 0x71, 0x47, 0xFF, 0xFF, 0x4C,
                    0x69, 0xCB, 0x88, 0x06, 0x00, 0x00, 0x48, 0x8D,
                    0x1D, 0xA3, 0xA5, 0xF8, 0x05, 0x49, 0x0F, 0xBF,
                    0x4C, 0x39, 0x5A, 0x48, 0x8D, 0x3D, 0x36, 0xCF,
                    0xED, 0xFF, 0x48, 0x69, 0xD1, 0x90, 0x04, 0x00
                },
                "building attack candidate consumer entry");
            ValidateExactBytes(
                memory, RegionPairReachabilityRva,
                new byte[]
                {
                    0x40, 0x55, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56,
                    0x48, 0x8D, 0xAC, 0x24, 0x78, 0xF7, 0xFF, 0xFF,
                    0x48, 0x81, 0xEC, 0x88, 0x09, 0x00, 0x00, 0x48,
                    0x8B, 0x05, 0x7A, 0x5D, 0x25, 0x00, 0x48, 0x33,
                    0xC4, 0x48, 0x89, 0x85, 0x70, 0x08, 0x00, 0x00
                },
                "attack-approach region-pair helper entry");
            ValidateExactBytes(
                memory, GetTribeMovementModeRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C,
                    0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x48,
                    0x89, 0x7C, 0x24, 0x20, 0x41, 0x56, 0x48, 0x83,
                    0xEC, 0x20, 0x48, 0x63, 0xF2, 0x33, 0xDB, 0x4C,
                    0x69, 0xC6, 0x88, 0x06, 0x00, 0x00, 0x48, 0x8B,
                    0xE9, 0x41, 0x0F, 0xBF, 0x7C, 0x08, 0x5C, 0x85,
                    0xFF, 0x7E, 0x4D, 0x4C, 0x8D, 0x35, 0x56, 0x07,
                    0x6D, 0x06
                },
                "ordinary-movement group route-mode helper entry");
        }

        internal static void ValidateAttackApproachCalls(ReadOnlySpan<byte> memory)
        {
            int tribeManagerTarget = APIShared.Internal.NativePatternResolver.ResolveRelativeTarget(
                memory, AttackApproachFloodBuilderRva + 0x1D, AttackApproachFloodBuilderRva + 0x21);
            if (tribeManagerTarget != NativeTribeManagerRva)
            {
                throw new InvalidOperationException(
                    $"The unit attack-approach builder references tribe manager 0x{tribeManagerTarget:X} " +
                    $"instead of 0x{NativeTribeManagerRva:X}.");
            }

            ValidateCallTarget(memory, AttackApproachFloodCallRva, AttackApproachFloodBuilderRva,
                new byte[] { 0xE8, 0x14, 0xCE, 0xFB, 0xFF }, "primary unit attack-approach call");
            ValidateCallTarget(memory, AttackApproachFloodAlternativeCallRva, AttackApproachFloodBuilderRva,
                new byte[] { 0xE8, 0xF0, 0xC7, 0xFB, 0xFF }, "alternative unit attack-approach call");
            ValidateCallTarget(memory, BuildingApproachCallRva, BuildingApproachBuilderRva,
                new byte[] { 0xE8, 0x81, 0xA0, 0xFB, 0xFF }, "building attack-approach call");
            ValidateCallTarget(memory, BuildingCandidateConsumerCallRva, BuildingCandidateConsumerRva,
                new byte[] { 0xE8, 0xE4, 0x30, 0x00, 0x00 }, "building candidate consumer call");
            ValidateCallTarget(memory, BuildingCandidateConsumerAlternativeCallRva, BuildingCandidateConsumerRva,
                new byte[] { 0xE8, 0xAC, 0x29, 0x00, 0x00 }, "alternative building candidate consumer call");
            ValidateCallTarget(memory, BuildingCandidateConsumerForceCallRva, BuildingCandidateConsumerRva,
                new byte[] { 0xE8, 0xBE, 0x23, 0x00, 0x00 }, "force-building candidate consumer call");

            ValidateCallTarget(memory, AttackFloodRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0xFE, 0x66, 0x00, 0x00 }, "unit flood region-pair call");
            ValidateCallTarget(memory, AttackFloodTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0x68, 0x6D, 0x00, 0x00 }, "unit flood tile-pair call");

            ValidateCallTarget(memory, BuildingApproachRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x12, 0x84, 0x00, 0x00 }, "building approach region-pair call");
            ValidateCallTarget(memory, BuildingApproachTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0x69, 0x8A, 0x00, 0x00 }, "building approach tile-pair call");
            ValidateCallTarget(memory, BuildingApproachAlternativeRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x8F, 0x81, 0x00, 0x00 }, "alternative building region-pair call");
            ValidateCallTarget(memory, BuildingApproachAlternativeTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0xEA, 0x87, 0x00, 0x00 }, "alternative building tile-pair call");

            ValidateCallTarget(memory, OrdinaryMovementGroupModeCallRva, GetTribeMovementModeRva,
                new byte[] { 0xE8, 0x35, 0xC5, 0xFF, 0xFF },
                "ordinary-movement group route-mode call");

            ValidateCallTarget(memory, OrdinaryMovementLadderPrecheckCallRva, CursorRegionPrecheckRva,
                new byte[] { 0xE8, 0x23, 0xE6, 0xFC, 0xFF }, "ordinary-movement Vanilla ladder precheck call");
            ValidateCallTarget(memory, OrdinaryMovementLadderReachabilityCallRva, CursorReachabilityRva,
                new byte[] { 0xE8, 0x66, 0xE8, 0xFC, 0xFF }, "ordinary-movement Vanilla ladder reachability call");

            ValidateCallTarget(memory, BuildingConsumerFallbackBuilderCallRva, AlternativePathBuilderRva,
                new byte[] { 0xE8, 0x49, 0x85, 0xFB, 0xFF }, "building consumer fallback-builder call");
            ValidateCallTarget(memory, BuildingConsumerGroundBuilderCallRva, GroundPathBuilderRva,
                new byte[] { 0xE8, 0x5F, 0x74, 0xFB, 0xFF }, "building consumer ground-builder call");
            ValidateExactBytes(
                memory, DirectFillApproachRva,
                new byte[]
                {
                    0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                    0x10, 0x53, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55,
                    0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x50
                },
                "direct FillMoat approach-search entry");
            ValidateCallTarget(
                memory, DirectFillApproachCommandCallRva, DirectFillApproachRva,
                new byte[] { 0xE8, 0xBE, 0x70, 0xFC, 0xFF },
                "FillMoat command approach-search call");
            ValidateCallTarget(
                memory, DirectFillApproachRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x20, 0xA4, 0xFF, 0xFF },
                "FillMoat approach-search region-pair call");
        }

    }
}
