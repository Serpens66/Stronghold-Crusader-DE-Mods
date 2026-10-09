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
        internal sealed class RedBirdDetour<TDelegate> where TDelegate : Delegate
        {
            internal readonly ulong targetAddress;

            internal RedBirdDetour(ulong targetAddress)
            {
                this.targetAddress = targetAddress;
            }

            internal DetourHandle<TDelegate> Handle { get; } = new DetourHandle<TDelegate>();
            internal TDelegate Original => Handle.Original;
            internal bool Committed => Handle.Success && Handle.IsInstalled &&
                Handle.Failure == null && Handle.ResolvedAddress == targetAddress;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int UnitStandingOnCompletedMoatDelegate(IntPtr unitManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int RegionReachabilityDelegate(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int TribeFloodFillMembershipDelegate(
            IntPtr tribeManager, int tribeId, int floodFillStamp);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int FirstGroupUnitOnCompletedMoatDelegate(
            IntPtr tribeManager, int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int GetGroupUnitIdDelegate(
            IntPtr tribeManager, int tribeId, int ordinal);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int CursorReachabilityDelegate(
            IntPtr pathManager, int unitId, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int SelectionCanDigMoatDelegate(IntPtr selectionState);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int CursorTilePairReachabilityDelegate(
            IntPtr pathManager, int targetTileId, int selectedUnitTileId, byte useCache);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int GetRepresentativeSelectedUnitDelegate(IntPtr unitManager, int startIndex);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int CursorRegionPrecheckDelegate(IntPtr pathManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int CentralMovementPlanDelegate(
            IntPtr unitManager, int unitId, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int PathBuilderDelegate(
            IntPtr pathManager, int movementClass, int movementProfile);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int GetMoatIdAtTileDelegate(IntPtr tileManager, int tileId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void AttackApproachFloodBuilderDelegate(
            IntPtr pathManager,
            int tribeId,
            int targetContext,
            uint targetX,
            uint targetY,
            int requestedResults,
            int sourceRegion,
            int movementClass);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void BuildingApproachBuilderDelegate(
            IntPtr pathManager,
            int tribeId,
            int buildingId,
            int requestedResults,
            int sourceRegion,
            int movementClass);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void BuildingCandidateConsumerDelegate(
            IntPtr tribeManager, int tribeId, int builderVariant);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int RegionPairReachabilityDelegate(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int GetTribeMovementModeDelegate(
            IntPtr tribeManager, int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int BuildingCursorReachabilityDelegate(
            IntPtr buildingManager, int buildingId, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void CombatFinishResumeDelegate(IntPtr unitManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void CursorMoveStagerDelegate(
            IntPtr unitManager,
            int tribeId,
            int targetX,
            int targetY,
            int targetContext,
            int actionFlags);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal delegate bool NativeSpecialStructurePredicateDelegate(
            IntPtr structureContext, int tileId);

    }
}
