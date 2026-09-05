// Feature: Identify Disease projectiles that must retain their Vanilla lifetime.
using BepInEx.Logging;
using MessagePack;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal sealed unsafe class AiFlagDiseaseTracker : IDisposable
    {
        private const string SaveDataIdentifier = "ExtraFeatures.AiFlagDisease.v1";

        // AI decoration/flag update, reference RVA 0x504F0 for CrusaderDE.dll SHA-256
        // FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
        private const string AiFlagRoutinePattern =
            "4C 8B DC 55 41 56 41 57 48 83 EC 60 4C 8D 3D ?? ?? ?? ?? " +
            "48 63 EA 48 69 D5 3C 58 00 00 4A 63 84 3A ?? ?? ?? ?? 48 69 C8 E4 05 00 00";
        private const int AiFlagRoutineRva = 0x504F0;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AiFlagRoutineDelegate(IntPtr aiManager, int playerId);

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly PlagueFlagDiseaseRegistry registry = new PlagueFlagDiseaseRegistry();
        private HookTransaction transaction;
        private readonly DetourHandle<AiFlagRoutineDelegate> aiFlagRoutineHook =
            new DetourHandle<AiFlagRoutineDelegate>();
        private int activeFlagPlayerId;
        private bool saveHandlerRegistered;
        private bool trackingAvailable = true;
        private bool callbackFailureLogged;
        private bool disposed;

        public AiFlagDiseaseTracker(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            Shared.GameplayModActivationGate.StateChanged += OnModeStateChanged;
            int routineRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiFlagRoutinePattern,
                AiFlagRoutineRva,
                referenceHashMatches,
                "AI flag projectile routine",
                log).Rva;

            try
            {
                ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
                transaction = ExtraFeaturesHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddDetour(
                    aiFlagRoutineHook,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)routineRva)),
                    RunAiFlagRoutine);
                CommitResult commitResult = transaction.Commit();
                if (!commitResult.IsCompleteSuccess || !aiFlagRoutineHook.Success)
                    throw new InvalidOperationException("The AI flag projectile hook was not installed.");

                subscriptions.Add(ProjectileR3EventHooks.OnProjectileSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnProjectileSpawn));
                subscriptions.Add(ProjectileR3EventHooks.OnProjectileDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => registry.RemoveSlot(args.ProjectileId)));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ResetMapState()));

                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                        SaveDataIdentifier,
                        SaveState,
                        LoadState,
                        ResetMapState))
                {
                    throw new InvalidOperationException("AI flag disease save-data registration failed.");
                }
                saveHandlerRegistered = true;

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features AI flag disease tracking initialized: routineRva=0x{routineRva:X}.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsTracked(GameProjectile* projectile) =>
            Shared.GameplayModActivationGate.IsAllowed && trackingAvailable &&
            projectile != null &&
            projectile->r_ProjectileType == ProjectileType.Disease &&
            registry.ContainsGlobalId(projectile->r_GlobalId);

        public bool TryTrackExternalDiseaseFlag(int slotId)
        {
            if (!Shared.GameplayModActivationGate.IsAllowed || !trackingAvailable || slotId <= 0)
                return false;

            try
            {
                PruneInvalidProjectiles();
                if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(
                        slotId,
                        out GameProjectile* projectile) ||
                    projectile == null ||
                    projectile->r_ProjectileType != ProjectileType.Disease ||
                    projectile->r_GlobalId == 0 ||
                    (projectile->r_AliveState != AliveState.NeedsInit &&
                     projectile->r_AliveState != AliveState.IsAlive))
                {
                    return false;
                }

                registry.Track(slotId, projectile->r_GlobalId);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Registered external Vanilla-equivalent Disease flag: slot={slotId}, globalId={projectile->r_GlobalId}.");
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Could not register external Vanilla-equivalent Disease flag slot {slotId}: {ex.GetBaseException().Message}.");
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Shared.GameplayModActivationGate.StateChanged -= OnModeStateChanged;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
            transaction?.Dispose();
            transaction = null;
            ResetMapState();
        }

        private void RunAiFlagRoutine(IntPtr aiManager, int playerId)
        {
            if (!Shared.GameplayModActivationGate.IsAllowed || !trackingAvailable)
            {
                aiFlagRoutineHook.Original(aiManager, playerId);
                return;
            }

            int previousPlayerId = activeFlagPlayerId;
            activeFlagPlayerId = playerId;
            try
            {
                // The nested projectile-spawn event is the exact provenance boundary.
                aiFlagRoutineHook.Original(aiManager, playerId);
            }
            finally
            {
                activeFlagPlayerId = previousPlayerId;
            }
        }

        private void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            if (!Shared.GameplayModActivationGate.IsAllowed)
                return;

            int playerId = activeFlagPlayerId;
            if (!trackingAvailable || args.ProjectileType != ProjectileType.Disease)
            {
                return;
            }

            try
            {
                bool spawnedByAiFlag = playerId >= 1 && playerId <= 8 &&
                    args.PlayerSourceId == playerId;
                bool spawnedOverCesspit = !spawnedByAiFlag && IsSpawnedOverCesspit(args);
                if (!spawnedByAiFlag && !spawnedOverCesspit)
                    return;

                // Keep stale identities out of saves even if a delete event was skipped.
                PruneInvalidProjectiles();

                if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                    throw new InvalidOperationException($"Vanilla-duration Disease returned an invalid slot ID: {args.ReturnValue}.");

                int slotId = checked((int)args.ReturnValue);
                if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(slotId, out GameProjectile* projectile) ||
                    projectile == null ||
                    projectile->r_ProjectileType != ProjectileType.Disease ||
                    projectile->r_GlobalId == 0)
                {
                    throw new InvalidOperationException($"Vanilla-duration Disease could not be identified after spawn: slot={slotId}.");
                }

                registry.Track(slotId, projectile->r_GlobalId);
            }
            catch (Exception ex)
            {
                DisableTracking(ex);
            }
        }

        private static bool IsSpawnedOverCesspit(ProjectileSpawnEventArgs args)
        {
            if (args.SourceWorldTileX < 0 || args.SourceWorldTileY < 0)
            {
                return false;
            }

            // Projectile world coordinates use eight native units per map tile.
            int sourceTileX = args.SourceWorldTileX >> 3;
            int sourceTileY = args.SourceWorldTileY >> 3;
            var buildingEnumerator = GameBuildingManagerAPI.Instance
                .QueryBuildings()
                .GetEnumerator();
            while (buildingEnumerator.MoveNext())
            {
                ref GameBuilding building = ref buildingEnumerator.Current;
                if (building.r_BuildingType != eStructs.STRUCT_CESS_PIT ||
                    (building.r_AliveState != AliveState.NeedsInit &&
                     building.r_AliveState != AliveState.IsAlive))
                {
                    continue;
                }

                if (sourceTileX >= building.r_TilePositionXBegin &&
                    sourceTileX <= building.r_TilePositionXEnd &&
                    sourceTileY >= building.r_TilePositionYBegin &&
                    sourceTileY <= building.r_TilePositionYEnd)
                {
                    return true;
                }
            }

            return false;
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !Shared.GameplayModActivationGate.IsAllowed)
                return null;

            PruneInvalidProjectiles();
            PlagueFlagDiseaseIdentity[] identities = registry.Snapshot();
            var records = new PlagueFlagDiseaseSaveRecord[identities.Length];
            for (int index = 0; index < identities.Length; index++)
            {
                records[index] = new PlagueFlagDiseaseSaveRecord
                {
                    SlotId = identities[index].SlotId,
                    GlobalId = identities[index].GlobalId
                };
            }
            return MessagePackSerializer.Serialize(new PlagueFlagDiseaseSaveState { Projectiles = records });
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile)
                return;

            PlagueFlagDiseaseSaveState state = MessagePackSerializer.Deserialize<PlagueFlagDiseaseSaveState>(bytes);
            if (state == null || state.Version != PlagueFlagDiseaseSaveState.CurrentVersion || state.Projectiles == null)
                throw new InvalidOperationException("AI flag disease save state has an unsupported version.");

            var identities = new PlagueFlagDiseaseIdentity[state.Projectiles.Length];
            for (int index = 0; index < state.Projectiles.Length; index++)
            {
                PlagueFlagDiseaseSaveRecord record = state.Projectiles[index] ??
                    throw new InvalidOperationException("AI flag disease save state contains a null record.");
                identities[index] = new PlagueFlagDiseaseIdentity(record.SlotId, record.GlobalId);
            }
            registry.Restore(identities);
            Shared.DebugLogHelper.LogDebug(log, $"AI flag disease state loaded: projectiles={registry.Count}.");
        }

        private void PruneInvalidProjectiles()
        {
            PlagueFlagDiseaseIdentity[] identities = registry.Snapshot();
            for (int index = 0; index < identities.Length; index++)
            {
                PlagueFlagDiseaseIdentity identity = identities[index];
                if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(identity.SlotId, out GameProjectile* projectile) ||
                    projectile == null ||
                    projectile->r_GlobalId != identity.GlobalId ||
                    projectile->r_ProjectileType != ProjectileType.Disease ||
                    (projectile->r_AliveState != AliveState.NeedsInit && projectile->r_AliveState != AliveState.IsAlive))
                {
                    registry.RemoveSlot(identity.SlotId);
                }
            }
        }

        private void ResetMapState()
        {
            activeFlagPlayerId = 0;
            registry.Clear();
        }

        private void OnModeStateChanged(bool allowed)
        {
            if (!allowed)
                ResetMapState();
        }

        private void DisableTracking(Exception failure)
        {
            trackingAvailable = false;
            ResetMapState();
            if (callbackFailureLogged)
                return;

            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                "Extra Features Vanilla-duration Disease tracking was disabled for this process; " +
                $"the configured global plague duration remains active: {failure}");
        }

    }
}
