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
    {
        internal const int FindMoatWorkTargetRva = 0x69D60;
        internal const int ResolveMoatWorkTileRva = 0x6AF60;
        internal const int HasFillMoatApproachRva = 0x6C490;
        internal const int FillApproachCallRva = 0x69EE6;
        internal const int DigStandingOnMoatCallRva = 0x69F91;
        internal const int DigRegionSearchCallRva = 0x69FE3;
        internal const int DigRegionPairCallRva = 0x6A014;
        internal const int DigAlternativeRegionPairCallRva = 0x6A0C2;
        internal const int MovementPlannerLowFlagGateRva = 0x196464;
        internal const int MovementPlannerStructureFlagGateRva = 0x19648D;

        internal const int SelectedMoatTileIdOffset = 0x2038E40;
        internal const int SelectedMoatApproachXOffset = 0x2038E38;
        internal const int SelectedMoatApproachYOffset = 0x2038E3C;
        internal const int MoatRecordTileIdOffset = 0x00;
        internal const int MoatRecordXOffset = 0x04;
        internal const int MoatRecordYOffset = 0x06;
        internal const int MoatRecordReservationOffset = 0x0F;
        internal const int MaximumMoatRecordId = 63999;
        internal const byte MoatReservationStep = 20;
        internal static bool IsValidMoatRecordId(int id, int count) =>
            id > 0 && id <= MaximumMoatRecordId && count > 0 && count <= MaximumMoatRecordId + 1 && id < count;
        internal const uint MovementBlockedLowTileFlagMask = 0x00000030;
        internal const uint MovementBlockedStructureTileFlagMask = 0x10000100;

        // Exact order used by 0x6C490/0x6AF60: N, NE, E, SE, S, SW, W, NW.
        internal static readonly int[] MoatWorkNeighbourX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        internal static readonly int[] MoatWorkNeighbourY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        internal const string FindMoatWorkTargetPattern =
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 " +
            "48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4";

        internal const string ResolveMoatWorkTilePattern =
            "44 89 4C 24 20 53 57 41 57 48 83 EC 20 48 63 44 24 60 " +
            "45 8B D0 49 63 D9 4C 63 DA 81 FB 1F 03 00 00 " +
            "0F 87 ?? ?? ?? ?? 3D 1F 03 00 00 0F 87 ?? ?? ?? ??";

        internal const string HasFillMoatApproachPattern =
            "48 89 5C 24 08 48 89 7C 24 10 49 63 C0 45 33 DB 8B FA " +
            "48 8B D9 44 0F B6 94 08 A0 E5 D7 00 49 63 C1 41 83 C2 10 " +
            "48 C1 E0 05 48 03 C1";

        [ThreadStatic]
        internal static MoatWorkSelectionScope activeMoatWorkSelection;
        [ThreadStatic]
        internal static PendingFillMoatApproach pendingFillMoatApproach;
        [ThreadStatic]
        internal static PendingDigMoatTarget pendingDigMoatTarget;

        internal FindMoatWorkTargetDelegate originalFindMoatWorkTarget;
        internal FindMoatWorkTargetDelegate rootedFindMoatWorkTarget;
        internal ResolveMoatWorkTileDelegate originalResolveMoatWorkTile;
        internal ResolveMoatWorkTileDelegate rootedResolveMoatWorkTile;
        internal HasFillMoatApproachDelegate originalHasFillMoatApproach;
        internal HasFillMoatApproachDelegate rootedHasFillMoatApproach;
        internal RedBirdDetour<FindMoatWorkTargetDelegate> findMoatWorkTargetDetour;
        internal RedBirdDetour<ResolveMoatWorkTileDelegate> resolveMoatWorkTileDetour;
        internal RedBirdDetour<HasFillMoatApproachDelegate> hasFillMoatApproachDetour;
        internal HookTransaction moatWorkHookTransaction;
        internal readonly Dictionary<int, string> lastMoatWorkSelectionByUnit =
            new Dictionary<int, string>();
        internal readonly Dictionary<int, string> lastMoatWorkApproachByUnit =
            new Dictionary<int, string>();
    }
}
