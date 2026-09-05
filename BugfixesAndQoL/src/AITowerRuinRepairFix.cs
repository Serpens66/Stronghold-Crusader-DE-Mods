// Feature: Route runtime-created AI tower ruins through Vanilla cleanup in both placement branches.
//
// CrusaderDE.dll SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// the AIV placement helper at RVA 0x5CD90 removes tower ruins only through its broad blocker
// scan. Ordinary tower placement selects that scan only within a Manhattan distance of 20
// from the stored keep; the narrow branch used outside that radius skips ruin types 79 and
// 86-89. Live traces proved both the native removal and the complementary failure.
//
// UCP2 fixes the analogous HD bug by routing ruins into an existing native demolition branch.
// We do the same selectively in both DE classifiers: only an exact, runtime-created,
// same-owner AI ruin with the matching tower mapper receives a temporary classifier value.
// Vanilla reloads the real type before performing its complete deletion and tile cleanup.
// Ruin cleanup intentionally remains independent of rebuild delay and enemy proximity.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Hooks.Context;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AITowerRuinRepairFix : IDisposable
    {
        // movabs rax,0x3C0100038 is encoded little-endian as 38 00 10 C0 03...
        // The earlier 38 00 01 C0 diagnostic pattern described a different mask.
        private const string NarrowRuinClassifierPattern =
            "66 83 F9 21 0F 87 ?? ?? ?? ?? 48 B8 38 00 10 C0 03 00 00 00 " +
            "48 0F A3 C8 0F 83 ?? ?? ?? ??";
        private const int NarrowRuinClassifierRva = 0x5D055;
        private const int NarrowRuinClassifierHookSize = 20;
        private const string BroadRuinClassifierPattern =
            "66 83 E9 28 66 83 F9 21 77 ?? " +
            "48 B8 07 80 00 80 03 00 00 00 48 0F A3 C8";
        private const int BroadRuinClassifierRva = 0x5D025;
        // sub/cmp/short-ja/movabs occupy exactly 20 bytes; do not split movabs.
        private const int BroadRuinClassifierHookSize = 20;
        private const int PlacementPlayerIdStackOffset = 0x98;
        private const int PlacementMapperStackOffset = 0xB0;
        private const int NativeDeletableSurrogateType = 3;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        // Placement and removal events both provide the building ID. Indexing by it avoids a
        // scan while GlobalId still protects every lookup against native ID reuse.
        private readonly Dictionary<int, RuntimeTowerRuin> runtimeRuins =
            new Dictionary<int, RuntimeTowerRuin>();
        private HookTransaction classifierTransaction;
        private readonly HookHandle<X64InlineHook> narrowRuinClassifierHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> broadRuinClassifierHook = new HookHandle<X64InlineHook>();
        private bool callbackFailureLogged;
        private bool mapActive;
        private bool disposed;

        public AITowerRuinRepairFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializeRuntimeRuinTracking();

            try
            {
                InstallRuinClassifiers(region, memory, libraryBase, referenceHashMatches);
            }
            catch (Exception ex)
            {
                classifierTransaction?.Dispose();
                classifierTransaction = null;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "AI tower-ruin native classifiers could not be installed; the fix stays " +
                    $"fail-closed and Vanilla behavior remains active: {ex}");
            }

            ApplySetting();
        }

        private void InstallRuinClassifiers(
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            // The callbacks read fixed stack slots and the audited building layout. A signature
            // match alone cannot prove those invariants on a future DLL.
            if (!referenceHashMatches)
                throw new InvalidOperationException(
                    "The AI tower-ruin classifiers require the audited CrusaderDE.dll layout.");

            Shared.NativeResolution narrowResolution = Shared.NativePatternResolver.ResolveUnique(
                memory, NarrowRuinClassifierPattern, NarrowRuinClassifierRva, referenceHashMatches,
                "AI narrow tower-ruin classifier", log);
            Shared.NativeResolution broadResolution = Shared.NativePatternResolver.ResolveUnique(
                memory, BroadRuinClassifierPattern, BroadRuinClassifierRva, referenceHashMatches,
                "AI broad tower-ruin classifier", log);
            classifierTransaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
            BugfixesHookInfrastructure.AddContextHook(
                classifierTransaction,
                narrowRuinClassifierHook,
                libraryBase + unchecked((ulong)narrowResolution.Rva),
                RouteTrackedRuinThroughNarrowCleanup,
                registers: X64SmartCPUContextRegs.All,
                hookSize: NarrowRuinClassifierHookSize,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);
            BugfixesHookInfrastructure.AddContextHook(
                classifierTransaction,
                broadRuinClassifierHook,
                libraryBase + unchecked((ulong)broadResolution.Rva),
                RouteTrackedRuinThroughBroadCleanup,
                registers: X64SmartCPUContextRegs.All,
                hookSize: BroadRuinClassifierHookSize,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);
            CommitResult commitResult = classifierTransaction.Commit();
            if (!commitResult.IsCompleteSuccess || !narrowRuinClassifierHook.Success || !broadRuinClassifierHook.Success)
                throw new InvalidOperationException("The AI tower-ruin classifier hooks were not installed atomically.");

            Shared.DebugLogHelper.LogDebug(
                log,
                $"AI tower-ruin classifiers installed at RVAs 0x{narrowResolution.Rva:X} and " +
                $"0x{broadResolution.Rva:X}.");
        }

        public void ApplySetting()
        {
            if (disposed || !narrowRuinClassifierHook.Success || !broadRuinClassifierHook.Success)
                return;

            if (IsEnabled)
            {
                if (!narrowRuinClassifierHook.IsInstalled)
                    narrowRuinClassifierHook.Hook.Enable();
                if (!broadRuinClassifierHook.IsInstalled)
                    broadRuinClassifierHook.Hook.Enable();
            }
            else
            {
                if (narrowRuinClassifierHook.IsInstalled)
                    narrowRuinClassifierHook.Hook.Disable();
                if (broadRuinClassifierHook.IsInstalled)
                    broadRuinClassifierHook.Hook.Disable();
                runtimeRuins.Clear();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            runtimeRuins.Clear();
            classifierTransaction?.Dispose();
            classifierTransaction = null;
        }

        private void RouteTrackedRuinThroughNarrowCleanup(NativePointer<X64SmartCPUContext> context) =>
            RouteTrackedRuinToVanillaCleanup(context, "narrow");

        private void RouteTrackedRuinThroughBroadCleanup(NativePointer<X64SmartCPUContext> context) =>
            RouteTrackedRuinToVanillaCleanup(context, "broad");

        private void RouteTrackedRuinToVanillaCleanup(
            NativePointer<X64SmartCPUContext> context,
            string branch)
        {
            if (!IsEnabled || !mapActive)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                eStructs type = (eStructs)unchecked((short)registers->RCX);
                // RVA 0x5CFB6 has replaced RSI with the tile building ID. Argument 5 (mapper)
                // remains at RSP+0xB0. Requiring the matching live mapper prevents a different
                // tower footprint or size from clearing the ruin.
                int mapperValue = *(short*)(registers->RSP + PlacementMapperStackOffset);
                if (!IsTowerRuin(type) || !MatchesTowerRuinMapper(type, mapperValue))
                    return;

                int buildingId = unchecked((int)(uint)registers->R8);
                // Argument 2 was stored at entry RSP+0x10; eight pushes plus sub rsp,0x48 put
                // that exact slot at RSP+0x98 here.
                int playerId = *(int*)(registers->RSP + PlacementPlayerIdStackOffset);
                if (buildingId <= 0 || playerId < 1 || playerId > 8 ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId) ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                        buildingId, out GameBuilding* building) ||
                    building->r_AliveState != AliveState.IsAlive ||
                    building->r_PlayerIdOwner != playerId ||
                    building->r_BuildingType != type ||
                    !runtimeRuins.TryGetValue(buildingId, out RuntimeTowerRuin tracked) ||
                    tracked.GlobalId != building->r_GlobalId ||
                    tracked.PlayerId != playerId ||
                    tracked.Type != type ||
                    tracked.AnchorX != building->r_TilePositionXBegin ||
                    tracked.AnchorY != building->r_TilePositionYBegin)
                {
                    return;
                }

                // Type 3 reaches native demolition in both classifier branches. Vanilla reloads
                // the true building type before cleanup, so this register-only surrogate never
                // changes the building record.
                registers->RCX = NativeDeletableSurrogateType;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"AI tower ruin routed through Vanilla {branch} cleanup: player={playerId}, " +
                    $"type={type}, buildingId={buildingId}, globalId={building->r_GlobalId}.");
            }
            catch (Exception ex)
            {
                LogCallbackFailure("native ruin classification", ex);
            }
        }

        private void InitializeRuntimeRuinTracking()
        {
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => RemoveTrackedRuin(args.BuildingId)));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => RemoveTrackedRuin(args.BuildingId)));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnMapStart));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ResetMap()));
        }

        private void OnMapStart(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
                ResetMap();
            else if (args.Phase == EventHookPhase.Post)
                mapActive = true;
        }

        private void ResetMap()
        {
            mapActive = false;
            runtimeRuins.Clear();
            callbackFailureLogged = false;
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!IsEnabled || !mapActive || args.Phase != EventHookPhase.Post ||
                args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
            {
                return;
            }

            try
            {
                int buildingId = unchecked((int)args.ReturnValue);
                // A spawn proves that any older object with this reusable native ID is gone.
                // Avoid touching the dictionary for ordinary spawns when no ruin is tracked.
                if (runtimeRuins.Count != 0)
                    runtimeRuins.Remove(buildingId);
                if (!IsTowerRuin(args.Building))
                    return;

                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId) ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building->r_GlobalId == 0 || building->r_PlayerIdOwner != args.PlayerId ||
                    building->r_BuildingType != args.Building ||
                    (building->r_AliveState != AliveState.NeedsInit &&
                     building->r_AliveState != AliveState.IsAlive))
                {
                    return;
                }

                var tracked = new RuntimeTowerRuin(
                    buildingId, building->r_GlobalId, args.PlayerId, args.Building,
                    args.TileX, args.TileY);
                runtimeRuins[tracked.BuildingId] = tracked;
            }
            catch (Exception ex)
            {
                LogCallbackFailure("runtime ruin spawn tracking", ex);
            }
        }

        private void RemoveTrackedRuin(int buildingId)
        {
            if (!IsEnabled || !mapActive || buildingId <= 0)
                return;

            try
            {
                if (!runtimeRuins.TryGetValue(buildingId, out RuntimeTowerRuin tracked))
                    return;
                if (GameBuildingManagerAPI.Instance.TryGetBuildingById(
                        buildingId, out GameBuilding* current) &&
                    current->r_GlobalId != tracked.GlobalId)
                {
                    runtimeRuins.Remove(buildingId);
                    return;
                }

                runtimeRuins.Remove(buildingId);
            }
            catch (Exception ex)
            {
                LogCallbackFailure("runtime ruin removal tracking", ex);
            }
        }

        private bool IsEnabled =>
            narrowRuinClassifierHook.Success && broadRuinClassifierHook.Success &&
            settings.EnableMod && settings.EnableAiFixes && settings.FixAITowerRepair;

        private void LogCallbackFailure(string operation, Exception ex)
        {
            if (callbackFailureLogged)
                return;
            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"AI tower-ruin repair callback failed during {operation}; further callback errors are " +
                $"suppressed and Vanilla remains active: {ex}");
        }

        private static bool MatchesTowerRuinMapper(eStructs ruinType, int mapperValue)
        {
            eMappers mapper = (eMappers)mapperValue;
            switch (ruinType)
            {
                case eStructs.STRUCT_TOWER1_DESTROYED:
                    return mapper == eMappers.MAPPER_TOWER1;
                case eStructs.STRUCT_TOWER2_DESTROYED:
                    return mapper == eMappers.MAPPER_TOWER2;
                case eStructs.STRUCT_TOWER3_DESTROYED:
                    return mapper == eMappers.MAPPER_TOWER3;
                case eStructs.STRUCT_TOWER4_DESTROYED:
                    return mapper == eMappers.MAPPER_TOWER4;
                case eStructs.STRUCT_TOWER5_DESTROYED:
                    return mapper == eMappers.MAPPER_TOWER5;
                default:
                    return false;
            }
        }

        private static bool IsTowerRuin(eStructs type) =>
            type == eStructs.STRUCT_TOWER5_DESTROYED ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1_DESTROYED &&
             (int)type <= (int)eStructs.STRUCT_TOWER4_DESTROYED);

        private readonly struct RuntimeTowerRuin
        {
            internal RuntimeTowerRuin(
                int buildingId, uint globalId, int playerId, eStructs type, int anchorX, int anchorY)
            {
                BuildingId = buildingId;
                GlobalId = globalId;
                PlayerId = playerId;
                Type = type;
                AnchorX = anchorX;
                AnchorY = anchorY;
            }

            internal int BuildingId { get; }
            internal uint GlobalId { get; }
            internal int PlayerId { get; }
            internal eStructs Type { get; }
            internal int AnchorX { get; }
            internal int AnchorY { get; }
        }
    }
}
