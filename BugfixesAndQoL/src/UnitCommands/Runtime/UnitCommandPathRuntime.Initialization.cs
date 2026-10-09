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
        public UnitCommandPathRuntime(
            ManualLogSource log,
            IUnitCommandSettings settings,
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            UnitCommandPathAPI.RootCandidate(this);
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            nativeRegion = context.Region ?? throw new ArgumentException(
                "The Script Extender did not provide a native scan region.", nameof(context));
            ReadOnlySpan<byte> memory = context.Memory;
            ulong libraryBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The friendly moat movement feature requires the validated CrusaderDE.dll layout.");
            }

            APIShared.Internal.NativeResolution floodResolution = Resolve(
                memory, TribeFloodFillMembershipPattern, TribeFloodFillMembershipRva,
                "Tribe flood-fill membership helper");
            APIShared.Internal.NativeResolution groupMoatResolution = Resolve(
                memory, FirstGroupUnitOnCompletedMoatPattern, FirstGroupUnitOnCompletedMoatRva,
                "first active group unit standing on completed moat helper");
            APIShared.Internal.NativeResolution groupUnitResolution = Resolve(
                memory, GetGroupUnitIdPattern, GetGroupUnitIdRva,
                "group unit iterator");
            APIShared.Internal.NativeResolution groupMovementModeResolution = Resolve(
                memory, GetTribeMovementModePattern, GetTribeMovementModeRva,
                "ordinary-movement group route-mode helper");
            APIShared.Internal.NativeResolution planResolution = Resolve(
                memory, CentralMovementPlanPattern, CentralMovementPlanRva,
                "central ordinary-movement planner");
            APIShared.Internal.NativeResolution combatFinishResumeResolution = Resolve(
                memory, CombatFinishResumePattern, CombatFinishResumeRva,
                "combat-finish movement-resume helper");
            APIShared.Internal.NativeResolution postCombatRepathResolution = Resolve(
                memory, PostCombatRepathPattern, PostCombatRepathRva,
                "post-combat saved-target repath helper");
            APIShared.Internal.NativeResolution cursorMoveStagerResolution = Resolve(
                memory, CursorMoveStagerPattern, CursorMoveStagerRva,
                "direct cursor move-command stager");
            APIShared.Internal.NativeResolution nativeSpecialStructureResolution = Resolve(
                memory, NativeSpecialStructurePredicatePattern,
                NativeSpecialStructurePredicateRva,
                "DAFD0/E1640 special-structure predicate");
            APIShared.Internal.NativeResolution modeResolution = Resolve(
                memory, UnitStandingOnCompletedMoatPattern, UnitStandingOnCompletedMoatRva,
                "unit-standing-on-completed-moat helper");
            APIShared.Internal.NativeResolution regionResolution = Resolve(
                memory, RegionReachabilityPattern, RegionReachabilityRva,
                "moat-aware region reachability");
            APIShared.Internal.NativeResolution builderResolution = Resolve(
                memory, PathBuilderPattern, PathBuilderRva,
                "central tile path builder");
            APIShared.Internal.NativeResolution moatLookupResolution = Resolve(
                memory, GetMoatIdAtTilePattern, GetMoatIdAtTileRva,
                "moat ID lookup by tile");
            APIShared.Internal.NativeResolution cursorResolution = Resolve(
                memory, CursorReachabilityFunctionPattern, CursorReachabilityRva,
                "ordinary-movement cursor reachability function");
            APIShared.Internal.NativeResolution selectionCanDigResolution = Resolve(
                memory, SelectionCanDigMoatPattern, SelectionCanDigMoatRva,
                "Vanilla selection-can-dig-moat helper");
            Resolve(
                memory, SelectionCanDigMoatCallPattern, SelectionCanDigMoatCallRva - 0x0C,
                "DigMoat cursor selection call context");
            APIShared.Internal.NativeResolution cursorTilePairResolution = Resolve(
                memory, CursorTilePairReachabilityPattern, CursorTilePairReachabilityRva,
                "cursor tile-pair reachability helper");
            APIShared.Internal.NativeResolution representativeUnitResolution = Resolve(
                memory, GetRepresentativeSelectedUnitPattern, GetRepresentativeSelectedUnitRva,
                "representative selected-unit helper");
            APIShared.Internal.NativeResolution cursorRegionResolution = Resolve(
                memory, CursorRegionPrecheckPattern, CursorRegionPrecheckRva,
                "ordinary-movement cursor region precheck");
            APIShared.Internal.NativeResolution cursorGateResolution = Resolve(
                memory, CursorCurrentTileFlagGatePattern, CursorCurrentTileFlagGateRva,
                "ordinary-movement current-tile cursor gate");
            Resolve(memory, AttackUnitPairGatePattern, AttackUnitPairGateJumpRva - 0x0E,
                "attack-unit cursor tile-pair gate context");
            Resolve(memory, AttackBuildingPairGatePattern, AttackBuildingPairGateJumpRva - 0x11,
                "attack-building cursor tile-pair gate context");
            Resolve(memory, AttackAlternativePairGatePattern, AttackAlternativePairGateJumpRva - 0x0E,
                "alternative attack cursor tile-pair gate context");

            ValidateExactBytes(
                memory,
                CursorCurrentTileFlagGateRva,
                new byte[] { 0xF7, 0x84, 0x97, 0x00, 0x84, 0x89, 0x00, 0x00, 0x01, 0x00, 0x10 },
                "ordinary-movement current-tile cursor gate");
            ValidateExactBytes(
                memory,
                CombatFinishResumeRva,
                new byte[]
                {
                    0x40, 0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63,
                    0xC2, 0x48, 0x69, 0xD8, 0x90, 0x04, 0x00, 0x00,
                    0x48, 0x03, 0xD9, 0x66, 0x83, 0xBB, 0x96, 0x09,
                    0x00, 0x00, 0x00, 0x75, 0x14
                },
                "combat-finish movement-resume detour entry");
            ValidateCallTarget(
                memory, CombatFinishPostCombatCallRva, PostCombatRepathRva,
                new byte[] { 0xE8, 0xAE, 0x22, 0x01, 0x00 },
                "combat-finish post-combat repath call");
            ValidateExactBytes(
                memory,
                PostCombatRepathRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74,
                    0x24, 0x10, 0x57, 0x48, 0x83, 0xEC, 0x30, 0x48,
                    0x63, 0xFA, 0x48, 0x8B, 0xF1, 0x48, 0x69, 0xDF,
                    0x90, 0x04, 0x00, 0x00, 0x48, 0x03, 0xD9
                },
                "post-combat saved-target repath entry");
            ValidateCallTarget(
                memory, PostCombatMoveHereCallRva, 0x196280,
                new byte[] { 0xE8, 0x50, 0xEB, 0xFF, 0xFF },
                "post-combat saved-target MoveHere call");
            ValidateExactBytes(
                memory,
                CursorMoveStagerRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x6C,
                    0x24, 0x18, 0x48, 0x89, 0x74, 0x24, 0x20, 0x48,
                    0x89, 0x4C, 0x24, 0x08, 0x57, 0x41, 0x54, 0x41,
                    0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC,
                    0x30
                },
                "direct cursor move-command stager entry");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[0], CursorMoveStagerRva,
                new byte[] { 0xE8, 0x71, 0x66, 0x10, 0x00 },
                "primary direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[1], CursorMoveStagerRva,
                new byte[] { 0xE8, 0xEF, 0x60, 0x10, 0x00 },
                "secondary direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[2], CursorMoveStagerRva,
                new byte[] { 0xE8, 0x65, 0x60, 0x10, 0x00 },
                "map-editor direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[3], CursorMoveStagerRva,
                new byte[] { 0xE8, 0xD7, 0x5F, 0x10, 0x00 },
                "alternate direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0xC5, 0xC6, 0xF4, 0xFF },
                "direct cursor move-command region-pair call");
            ValidateCallTarget(
                memory, MoatBuilderSpecialStructureCallRvas[0],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0xC1, 0xBE, 0x02, 0x00 },
                "DAFD0 first special-structure predicate call");
            ValidateCallTarget(
                memory, MoatBuilderSpecialStructureCallRvas[1],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x6F, 0xBE, 0x02, 0x00 },
                "DAFD0 second special-structure predicate call");
            ValidateCallTarget(
                memory, MoatBuilderSpecialStructureCallRvas[2],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0xE1, 0xBD, 0x02, 0x00 },
                "DAFD0 third special-structure predicate call");
            ValidateCallTarget(
                memory, MoatReconstructionSpecialStructureCallRvas[0],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x05, 0x59, 0x02, 0x00 },
                "E1640 first special-structure predicate call");
            ValidateCallTarget(
                memory, MoatReconstructionSpecialStructureCallRvas[1],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x7C, 0x58, 0x02, 0x00 },
                "E1640 second special-structure predicate call");
            ValidateCallTarget(
                memory, MoatReconstructionSpecialStructureCallRvas[2],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x00, 0x58, 0x02, 0x00 },
                "E1640 third special-structure predicate call");
            ValidateExactBytes(
                memory,
                CursorCurrentTileFlagGateJumpRva,
                CursorGateJumpOriginal,
                "ordinary-movement current-tile cursor-gate jump");
            ValidateExactBytes(memory, AttackUnitPairGateJumpRva,
                AttackUnitPairGateOriginal, "attack-unit cursor tile-pair gate jump");
            ValidateExactBytes(memory, AttackBuildingPairGateJumpRva,
                AttackBuildingPairGateOriginal, "attack-building cursor tile-pair gate jump");
            ValidateExactBytes(memory, AttackAlternativePairGateJumpRva,
                AttackAlternativePairGateOriginal, "alternative attack cursor tile-pair gate jump");
            ValidateExactBytes(
                memory,
                CursorTilePairReachabilityRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C,
                    0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x57,
                    0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x40
                },
                "cursor tile-pair reachability detour span");
            ValidateExactBytes(
                memory,
                GetRepresentativeSelectedUnitRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x18, 0x55, 0x48, 0x8D,
                    0x6C, 0x24, 0x80, 0x48, 0x81, 0xEC, 0x80, 0x01,
                    0x00, 0x00
                },
                "representative selected-unit helper entry");
            ValidateExactBytes(memory, SelectionCanDigMoatRva,
                new byte[]
                {
                    0x83, 0xB9, 0x80, 0x05, 0x00, 0x00, 0x00, 0x75, 0x54,
                    0x83, 0xB9, 0xB4, 0x05, 0x00, 0x00, 0x00, 0x75, 0x4B,
                    0x83, 0xB9, 0x68, 0x05, 0x00, 0x00, 0x00, 0x75, 0x42,
                    0x83, 0xB9, 0x64, 0x05, 0x00, 0x00, 0x00, 0x75, 0x39,
                    0x83, 0xB9, 0x6C, 0x05, 0x00, 0x00, 0x00, 0x75, 0x30,
                    0x83, 0xB9, 0xE8, 0x05, 0x00, 0x00, 0x00, 0x75, 0x27,
                    0x83, 0xB9, 0xEC, 0x05, 0x00, 0x00, 0x00, 0x75, 0x1E,
                    0x83, 0xB9, 0xE0, 0x05, 0x00, 0x00, 0x00, 0x75, 0x15,
                    0x83, 0xB9, 0xD8, 0x05, 0x00, 0x00, 0x00, 0x75, 0x0C,
                    0x83, 0xB9, 0x74, 0x05, 0x00, 0x00, 0x00, 0x75, 0x03,
                    0x33, 0xC0, 0xC3, 0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3
                }, "Vanilla selection-can-dig-moat helper body");
            ValidateCallTarget(memory, SelectionCanDigMoatCallRva, SelectionCanDigMoatRva,
                new byte[] { 0xE8, 0x2D, 0x48, 0x10, 0x00 },
                "DigMoat cursor selection-helper call");
            ValidateExactBytes(
                memory,
                FirstGroupUnitOnCompletedMoatRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C,
                    0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x48,
                    0x89, 0x7C, 0x24, 0x20, 0x41, 0x56, 0x48, 0x83,
                    0xEC, 0x20
                },
                "first group unit on completed moat detour span");
            ValidateExactBytes(
                memory,
                GetGroupUnitIdRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x63, 0xC2,
                    0x45, 0x33, 0xC9, 0x48, 0x69, 0xD0, 0x88, 0x06,
                    0x00, 0x00
                },
                "group unit iterator entry");
            ValidateCallTarget(
                memory, GroupMoatModeCallRva, FirstGroupUnitOnCompletedMoatRva,
                new byte[] { 0xE8, 0x55, 0xC5, 0xFF, 0xFF },
                "MoveHere group moat-mode helper call");
            ValidateExactBytes(
                memory, GroupMoatModeCallRva - 3,
                new byte[]
                {
                    0x48, 0x8B, 0xCF,
                    0xE8, 0x55, 0xC5, 0xFF, 0xFF,
                    0x44, 0x3B, 0xF8, 0x75, 0x72
                },
                "MoveHere group moat-mode decision sequence");
            ValidateExactBytes(
                memory, MovementTerrainPhaseRva,
                new byte[]
                {
                    0x0F, 0xB6, 0x83, 0xC8, 0x06, 0x00, 0x00, 0x45,
                    0x85, 0xC9, 0x74, 0x42, 0x3C, 0x18, 0x7D, 0x08,
                    0x04, 0x04, 0x88, 0x83, 0xC8, 0x06, 0x00, 0x00,
                    0x0F, 0xB7, 0x8B, 0xA2, 0x09, 0x00, 0x00, 0x3C
                },
                "movement terrain-delay phase contract");
            ValidateExactBytes(
                memory, MovementCadenceRva,
                new byte[]
                {
                    0x41, 0x0F, 0xBF, 0x80, 0x16, 0x09, 0x00, 0x00,
                    0x41, 0x0F, 0xBF, 0x88, 0xA2, 0x09, 0x00, 0x00,
                    0x45, 0x8B, 0x90, 0xA8, 0x09, 0x00, 0x00, 0x03,
                    0xC8, 0x41, 0x0F, 0xBF, 0x80, 0x4C, 0x07, 0x00,
                    0x00, 0x41, 0x2B, 0xD2, 0x03, 0xC1, 0x41, 0x8B,
                    0x88, 0xAC, 0x09, 0x00, 0x00, 0x3B, 0xD0
                },
                "movement cadence runtime-field contract");
            ValidateExactBytes(
                memory, MovementSubstepRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x6C,
                    0x24, 0x18, 0x48, 0x89, 0x74, 0x24, 0x20, 0x57,
                    0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
                    0x48, 0x83, 0xEC, 0x40, 0x48, 0x63, 0xDA, 0x41,
                    0x0F, 0xB6, 0xE9, 0x4C, 0x69, 0xE3, 0x90, 0x04,
                    0x00, 0x00, 0x45, 0x8B, 0xE8
                },
                "movement substep speed-bonus contract");
            ValidateExactBytes(
                memory, MovementAdditionalSubstepsRva,
                new byte[]
                {
                    0x41, 0x0F, 0xBF, 0x84, 0x3C, 0x2A, 0x0A, 0x00,
                    0x00, 0x44, 0x03, 0xE8, 0x0F, 0x88, 0xD3, 0x04,
                    0x00, 0x00
                },
                "movement additional-substeps contract");
            ValidateExactBytes(
                memory, MoatPathConsumptionReadRva,
                new byte[]
                {
                    0x66, 0x45, 0x85, 0xC9, 0x0F, 0x85, 0xC2, 0x04,
                    0x00, 0x00, 0x41, 0x0F, 0xBF, 0x84, 0x3C, 0xC8,
                    0x09, 0x00, 0x00, 0x48, 0x8D, 0x0D, 0x12, 0x7D,
                    0xF2, 0x05, 0x45, 0x0F, 0xBF, 0x8C, 0x3C, 0x1E,
                    0x07, 0x00, 0x00, 0x45, 0x8B, 0x84, 0x3C, 0x2C,
                    0x07, 0x00, 0x00, 0x89, 0x44, 0x24, 0x30, 0x89,
                    0x54, 0x24, 0x28, 0x8B, 0xD3, 0x44, 0x89, 0x74,
                    0x24, 0x20, 0xE8, 0xED, 0x74, 0xF5, 0xFF, 0x85,
                    0xC0
                },
                "movement moat-path consumption contract");
            ValidateExactBytes(
                memory, MoatPathConsumptionPersistRva,
                new byte[]
                {
                    0x41, 0x0F, 0xB7, 0xC0, 0x44, 0x39, 0x05, 0xCD,
                    0x6F, 0xF1, 0x05, 0x66, 0x0F, 0x45, 0xC3, 0x66,
                    0x89, 0x87, 0xC8, 0x09, 0x00, 0x00, 0x8B, 0xC3,
                    0x4C, 0x89, 0x05, 0xB9, 0x6F, 0xF1, 0x05, 0x4C,
                    0x89, 0x87, 0xD0, 0x0A, 0x00, 0x00
                },
                "MoveHere moat-path mode persistence contract");
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_PathPlanRelated1), 0xF0);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_TargetTilePositionX2), 0xE8);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_TargetTilePositionY2), 0xEA);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_AIState), 0x2BC);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_AttackMoveToTargetTileX), 0x2D8);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_AttackMoveToTargetTileY), 0x2DA);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_SpeedBonus), 0x2BA);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentSpeed2), 0x346);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentSpeed), 0x348);
            ValidateGameUnitFieldOffset(nameof(GameUnit.N000001CE), UnitMoatPathConsumptionModeOffset);
            ValidateGameUnitFieldOffset(
                nameof(GameUnit.UnknownRelevant1), UnitMoatPathConsumptionModeOffset + 1);
            ValidateStructFieldOffset(
                typeof(GameCursorManager), nameof(GameCursorManager.r_HoverOverUnitId), 0x30);
            if (Marshal.SizeOf(typeof(GameUnit)) != NativeUnitStride)
            {
                throw new InvalidOperationException(
                    $"Unexpected GameUnit size 0x{Marshal.SizeOf(typeof(GameUnit)):X}; " +
                    $"expected native stride 0x{NativeUnitStride:X}.");
            }
            ValidateStructFieldOffset(
                typeof(GameUnitManager), nameof(GameUnitManager.GameUnitArray),
                NativeUnitSlotDataOffset);
            if (Marshal.SizeOf(typeof(GameUnit)) <= UnitAdditionalMovementSubstepsOffset + 1)
            {
                throw new InvalidOperationException(
                    "GameUnit is too small for the native additional-substeps field.");
            }

            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            cursorTargetX = (int*)(libraryBase + CursorTargetXRva);
            cursorTargetY = (int*)(libraryBase + CursorTargetYRva);
            nativeUnitManager = (byte*)(libraryBase + NativeUnitManagerRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            nativeMovementMasks = (byte*)(libraryBase + NativeMovementMaskRva);
            nativeRowLookup = (int*)(libraryBase + RowLookupRva);
            nativeBuildingLayer = (ushort*)(libraryBase + NativeBuildingLayerRva);
            nativeHeightLayer = (byte*)(libraryBase + NativeHeightLayerRva);
            nativeDirectionMasks = (byte*)(libraryBase + NativeDirectionMaskRva);
            pathRegionGrid = (ushort*)(libraryBase + PathRegionGridRva);
            nativePathManager = (IntPtr)(libraryBase + NativePathManagerRva);
            nativeProbeGrid = (byte*)(libraryBase + 0x5225B10);
            nativeProbeRectangle = (byte*)(libraryBase + 0x34A9F50);
            nativeTribeManager = (IntPtr)(libraryBase + NativeTribeManagerRva);
            nativeSpecialStructureContext =
                (IntPtr)(libraryBase + NativeSpecialStructureContextRva);
            getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));
            getRepresentativeSelectedUnit = Marshal.GetDelegateForFunctionPointer<GetRepresentativeSelectedUnitDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)representativeUnitResolution.Rva)));
            selectionCanDigMoat = Marshal.GetDelegateForFunctionPointer<SelectionCanDigMoatDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)selectionCanDigResolution.Rva)));
            getGroupUnitId = Marshal.GetDelegateForFunctionPointer<GetGroupUnitIdDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)groupUnitResolution.Rva)));
            getTribeMovementMode =
                Marshal.GetDelegateForFunctionPointer<GetTribeMovementModeDelegate>(
                    (IntPtr)(libraryBase +
                        unchecked((ulong)groupMovementModeResolution.Rva)));
            nativeSpecialStructurePredicate =
                Marshal.GetDelegateForFunctionPointer<NativeSpecialStructurePredicateDelegate>(
                    (IntPtr)(libraryBase +
                        unchecked((ulong)nativeSpecialStructureResolution.Rva)));
            weightedMoatRoutePlanner = new WeightedMoatRoutePlanner(
                nativeRowLookup,
                tileFlags,
                nativeBuildingLayer,
                nativeHeightLayer,
                nativeMovementMasks,
                nativeDirectionMasks,
                (byte*)(libraryBase + NativeBuildingTypeBiasRva),
                ResolveCompletedMoatRelationship,
                ResolveNativeSpecialStructure);
            try
            {
                nativeMovementCadenceResolver = new NativeMovementCadenceResolver(
                    context, unchecked((ulong)nativeUnitManager), log);
            }
            catch (Exception exception)
            {
                // Cadence qualification is optional; keep shared commands and selectors available.
                nativeMovementCadenceResolver = null;
                APIShared.Internal.DebugLogHelper.LogWarning(log,
                    "CADENCE_RESOLVER_UNAVAILABLE: weighted route publication disabled; " +
                    "shared movement commands remain available: " + exception);
            }
            rootedCentralMovementPlan = RunCentralMovementPlanWithContext;
            rootedPathBuilder = BuildPathWithCompletedMoatRouteVariant;
            rootedPathReconstruction = BuildReconstructedUnitPath;
            Resolve(memory, "40 53 48 83 EC 30 44 8B 49 10 33 C0 44 8B 41 0C 48 8B D9 8B 51 08 89 44 24 28 89 81 68 5F 15 00 8B 41 14 89 44 24 20 E8 64 E3 FF FF 8B 83 68 5F 15 00 48 83 C4 30 5B C3", 0xE32B0, "unit field reconstruction");
            ValidateExactBytes(memory, 0xE32B0, new byte[] { 0x40, 0x53, 0x48, 0x83, 0xEC, 0x30, 0x44, 0x8B, 0x49, 0x10, 0x33, 0xC0, 0x44, 0x8B, 0x41, 0x0C, 0x48, 0x8B, 0xD9, 0x8B, 0x51, 0x08, 0x89, 0x44, 0x24, 0x28, 0x89, 0x81, 0x68, 0x5F, 0x15, 0x00, 0x8B, 0x41, 0x14, 0x89, 0x44, 0x24, 0x20, 0xE8, 0x64, 0xE3, 0xFF, 0xFF, 0x8B, 0x83, 0x68, 0x5F, 0x15, 0x00, 0x48, 0x83, 0xC4, 0x30, 0x5B, 0xC3 }, "complete E32B0 function");
            rootedTribeFloodFillMembership = AllowTribeFloodFillForMoveOrder;
            rootedFirstGroupUnitOnCompletedMoat = SelectOwnerSafeGroupMoatMode;
            rootedUnitStandingOnCompletedMoat = EnableCompletedMoatModeForScopedMovement;
            ValidateMoatModeDetourCandidate(libraryBase + unchecked((ulong)modeResolution.Rva));
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedCursorReachability = AllowCursorReachabilityThroughCompletedMoat;
            rootedCursorTilePairReachability = AllowAttackCursorTilePairThroughCompletedMoat;
            rootedCursorRegionPrecheck = AllowCursorRegionThroughCompletedMoat;
            rootedCombatFinishResume = ResumeMovementAfterCombatWithMoatContext;
            rootedCursorMoveStager = StageDirectCursorMoveWithOwnerRoute;

            HookTransaction pendingTransaction = CreateOwnedHookTransaction();
            RedBirdDetour<CentralMovementPlanDelegate> pendingPlanDetour = null;
            RedBirdDetour<CombatFinishResumeDelegate> pendingCombatFinishResume = null;
            RedBirdDetour<CursorMoveStagerDelegate> pendingCursorMoveStager = null;
            RedBirdDetour<PathBuilderDelegate> pendingBuilder = null;
            RedBirdDetour<PathReconstructionDelegate> pendingReconstruction = null;
            RedBirdDetour<TribeFloodFillMembershipDelegate> pendingFlood = null;
            RedBirdDetour<FirstGroupUnitOnCompletedMoatDelegate> pendingGroupMoat = null;
            RedBirdDetour<UnitStandingOnCompletedMoatDelegate> pendingMode = null;
            RedBirdDetour<RegionReachabilityDelegate> pendingRegion = null;
            RedBirdDetour<CursorReachabilityDelegate> pendingCursor = null;
            RedBirdDetour<CursorTilePairReachabilityDelegate> pendingCursorTilePair = null;
            RedBirdDetour<CursorRegionPrecheckDelegate> pendingCursorRegion = null;
            try
            {
                pendingPlanDetour = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)planResolution.Rva),
                    rootedCentralMovementPlan);
                pendingCombatFinishResume = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)combatFinishResumeResolution.Rva),
                    rootedCombatFinishResume);
                pendingCursorMoveStager = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)cursorMoveStagerResolution.Rva),
                    rootedCursorMoveStager);
                pendingBuilder = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)builderResolution.Rva),
                    rootedPathBuilder);
                pendingReconstruction = AddDetour(
                    pendingTransaction, libraryBase + 0xE32B0, rootedPathReconstruction);
                pendingFlood = AddDetour(
                    pendingTransaction,
                    libraryBase + unchecked((ulong)floodResolution.Rva),
                    rootedTribeFloodFillMembership);
                pendingGroupMoat = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)groupMoatResolution.Rva),
                    rootedFirstGroupUnitOnCompletedMoat);
                pendingMode = AddDetour(
                    pendingTransaction,
                    libraryBase + unchecked((ulong)modeResolution.Rva),
                    rootedUnitStandingOnCompletedMoat,
                    MoatModeFlagIntermediaryFactory.Instance);
                pendingRegion = AddDetour(
                    pendingTransaction,
                    libraryBase + unchecked((ulong)regionResolution.Rva),
                    rootedRegionReachability);
                pendingCursor = AddDetour(
                    pendingTransaction,
                    libraryBase + unchecked((ulong)cursorResolution.Rva),
                    rootedCursorReachability);
                AddSelectionCallAdapters(pendingTransaction, memory, libraryBase);
                pendingCursorTilePair = AddDetour(pendingTransaction,
                    libraryBase + unchecked((ulong)cursorTilePairResolution.Rva),
                    rootedCursorTilePairReachability);
                pendingCursorRegion = AddDetour(
                    pendingTransaction,
                    libraryBase + unchecked((ulong)cursorRegionResolution.Rva),
                    rootedCursorRegionPrecheck);

                CommitResult commitResult = CommitPermanentHooks(pendingTransaction);
                originalCentralMovementPlan = pendingPlanDetour.Original;
                originalCombatFinishResume = pendingCombatFinishResume.Original;
                originalCursorMoveStager = pendingCursorMoveStager.Original;
                originalPathBuilder = pendingBuilder.Original;
                originalPathReconstruction = pendingReconstruction.Original;
                originalTribeFloodFillMembership = pendingFlood.Original;
                originalFirstGroupUnitOnCompletedMoat = pendingGroupMoat.Original;
                originalUnitStandingOnCompletedMoat = pendingMode.Original;
                originalRegionReachability = pendingRegion.Original;
                originalCursorReachability = pendingCursor.Original;
                originalCursorTilePairReachability = pendingCursorTilePair.Original;
                originalCursorRegionPrecheck = pendingCursorRegion.Original;

                ValidatePublishedCommandHooks(pendingTransaction);
                ValidateSelectionCallAdapters(libraryBase);
                if (!commitResult.IsCompleteSuccess ||
                    !pendingPlanDetour.Committed || !pendingCombatFinishResume.Committed ||
                    !pendingCursorMoveStager.Committed || !pendingBuilder.Committed ||
                    !pendingReconstruction.Committed || !pendingFlood.Committed ||
                    !pendingGroupMoat.Committed || !pendingMode.Committed || !pendingRegion.Committed ||
                    !pendingCursor.Committed ||
                    !pendingCursorTilePair.Committed || !pendingCursorRegion.Committed)
                {
                    throw new InvalidOperationException(
                        $"The central friendly moat movement hooks were not installed atomically: {commitResult}.");
                }

                // A committed hook must stay installed. A post-commit regression is
                // diagnosed without disposing its executable entry or transaction.
                try
                {
                    var installedMode = pendingMode.Handle.Hook as NativeDetour<UnitStandingOnCompletedMoatDelegate>;
                    MoatModeFlagIntermediaryFactory.ValidateNativeHook(
                        installedMode,
                        libraryBase + unchecked((ulong)modeResolution.Rva),
                        Marshal.GetFunctionPointerForDelegate(rootedUnitStandingOnCompletedMoat),
                        installed: true);
                }
                catch (Exception exception)
                {
                    disposed = true;
                    APIShared.Internal.DebugLogHelper.LogError(log,
                        "Friendly moat movement disabled after a committed mode-detour contract mismatch: " + exception);
                }


                centralMovementPlanDetour = pendingPlanDetour;
                combatFinishResumeDetour = pendingCombatFinishResume;
                cursorMoveStagerDetour = pendingCursorMoveStager;
                pathBuilderDetour = pendingBuilder;
                pathReconstructionDetour = pendingReconstruction;
                tribeFloodFillMembershipDetour = pendingFlood;
                firstGroupUnitOnCompletedMoatDetour = pendingGroupMoat;
                unitStandingOnCompletedMoatDetour = pendingMode;
                regionReachabilityDetour = pendingRegion;
                cursorReachabilityDetour = pendingCursor;
                cursorTilePairReachabilityDetour = pendingCursorTilePair;
                cursorRegionPrecheckDetour = pendingCursorRegion;
                mainHookTransaction = pendingTransaction;

                weightedMoatRoutePlanner.AllowAdditionalMoatEntry = () => TraversalEnabled;
                tribeMoveSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(DispatchMoveEvent);
                unitMoveSubscription = UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(DispatchUnitMoveEvent);
                tribeTargetSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(DispatchTargetEvent);
                mapLoadSubscription = APIShared.Internal.MissionEvents.Loading.Subscribe(_ => ResetMapState());
                // SaveLifecycle: ResetOnly - every save load also raises map unload.
                mapStartSubscription = APIShared.Internal.MissionEvents.NativeStart.Subscribe(_ => ResetMapState());
                mapUnloadSubscription = APIShared.Internal.MissionEvents.Ended.Subscribe(_ => ResetMapState());
                GameTimeManagerAPI.Instance.OnTick += ObserveTrackedAttackStates;
                attackTickSubscribed = true;

                APIShared.Internal.DebugLogHelper.LogDebug(
                    log,
                    "Unit command shared hooks installed: " +
                    $"cursorGate=0x{cursorGateResolution.Rva:X}/jump=0x{CursorCurrentTileFlagGateJumpRva:X}(vanilla), " +
                    $"cursorRegion=0x{cursorRegionResolution.Rva:X}, cursorDirect=0x{cursorResolution.Rva:X}, " +
                    $"cursorPair=0x{cursorTilePairResolution.Rva:X}, representativeUnit=0x{representativeUnitResolution.Rva:X}, " +
                    $"attackPairGates=0x{AttackUnitPairGateJumpRva:X}/0x{AttackBuildingPairGateJumpRva:X}/" +
                    $"0x{AttackAlternativePairGateJumpRva:X}(all-vanilla), " +
                    "selectionCallAdapters=6, seSelectionResultPreserved=true, " +
                    $"plan=0x{planResolution.Rva:X}, mode=0x{modeResolution.Rva:X}, " +
                    $"region=0x{regionResolution.Rva:X}, builder=0x{builderResolution.Rva:X}, " +
                    $"postCombatResume=0x{combatFinishResumeResolution.Rva:X}->" +
                    $"0x{postCombatRepathResolution.Rva:X}->0x196280, " +
                    $"directCursorMove=0x{cursorMoveStagerResolution.Rva:X}, " +
                    $"selectionCanDigMoat=0x{selectionCanDigResolution.Rva:X}/call=0x{SelectionCanDigMoatCallRva:X}, " +
                    $"tribeFloodFill=0x{floodResolution.Rva:X}, moatLookup=0x{moatLookupResolution.Rva:X}; " +
                    $"groupMoatMode=0x{groupMoatResolution.Rva:X}/iterator=0x{groupUnitResolution.Rva:X}/" +
                    $"call=0x{GroupMoatModeCallRva:X}; " +
                    "additionalMoatEntry=provider-only, " +
                    $"nativeMovementContracts=0x{MovementTerrainPhaseRva:X}/" +
                    $"0x{MovementCadenceRva:X}/0x{MovementSubstepRva:X}/" +
                    $"0x{MovementAdditionalSubstepsRva:X}/consumerContracts=" +
                    $"0x{MoatPathConsumptionPersistRva:X}/0x{MoatPathConsumptionReadRva:X}.");

                // Optional cursor/diagnostic groups fail closed without rolling back the proven
                // ordinary movement hooks above.
                TryInstallBuildingCursorReachability(memory, libraryBase);
                TryInstallAttackApproachDiagnostics(memory, libraryBase);
                TryInstallMoatWorkTargetSelection(memory, libraryBase);
                InstallConnectivityAndRecovery(memory, libraryBase);
                UnityEngine.Application.onBeforeRender += ObserveCursorPerformance;
            }
            catch
            {
                if (IsPublished(pendingTransaction))
                {
                    disposed = true;
                    TryLogDiagnosticFailure("published-command-initialization", new InvalidOperationException("Published hooks retained; command extensions disabled."));
                    return;
                }
                tribeMoveSubscription?.Dispose();
                unitMoveSubscription?.Dispose();
                tribeTargetSubscription?.Dispose();
                mapLoadSubscription?.Dispose();
                mapStartSubscription?.Dispose();
                mapUnloadSubscription?.Dispose();
                if (attackTickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= ObserveTrackedAttackStates;
                    attackTickSubscribed = false;
                }
                try { RollbackUnpublishedConnectivityHooks(); } catch { }
                try { RollbackUnpublishedMoatWorkTargetSelection(); } catch { }
                try { RollbackUnpublishedTransaction(attackApproachHookTransaction); } catch { }
                attackApproachHookTransaction = null;
                try { RollbackUnpublishedTransaction(buildingCursorHookTransaction); } catch { }
                buildingCursorHookTransaction = null;
                try { RollbackUnpublishedTransaction(pendingTransaction); } catch { }
                throw;
            }
        }

        public void Dispose()
        {
            if (publishedCommandTransactions.Count != 0) throw new InvalidOperationException("Published command hooks are process-owned.");
            if (disposed)
                return;

            disposed = true;
            ClearUnitMoveFrames();
            activeMoveCommand = null;
            ClearDeferredFastMoveScope();
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null;
            activeBuildingConsumerPerformance = null;
            activeLadderAttackProbe = null;
            ResetDirectMoatCommandScopes();
            trackedAttackUnits.Clear();
            trackedMoatMoves.Clear();
            requiredBackgroundTrackedUnitIds.Clear();
            requiredBackgroundDiagnostics.Clear();
            requiredBackgroundSuppressedDiagnostics.Clear();
            requiredBackgroundDiagnosticTick = int.MinValue;
            lastAttackCommandCandidates.Clear();
            loggedBuildingCursorReachabilityDecisions.Clear();
        }

    }
}
