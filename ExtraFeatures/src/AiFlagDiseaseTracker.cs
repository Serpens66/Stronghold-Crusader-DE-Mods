// Feature: Identify Disease projectiles created specifically by Vanilla's AI flag routine.
using BepInEx.Logging;
using MessagePack;
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
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace ExtraFeatures
{
    internal sealed unsafe class AiFlagDiseaseTracker : IDisposable
    {
        private const string SaveDataIdentifier = "ExtraFeatures.AiFlagDisease.v1";

        // AI decoration/flag update, reference RVA 0x504F0 for CrusaderDE.dll SHA-256
        // 33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469.
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
        private HookRef<X64ManagedFunctionDetourAOB<AiFlagRoutineDelegate>> aiFlagRoutineHook =
            new HookRef<X64ManagedFunctionDetourAOB<AiFlagRoutineDelegate>>();
        private int activeFlagPlayerId;
        private bool saveHandlerRegistered;
        private bool trackingAvailable = true;
        private bool callbackFailureLogged;
        private bool disposed;

        public AiFlagDiseaseTracker(
            ManualLogSource log,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
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
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref aiFlagRoutineHook,
                    libraryBase + unchecked((ulong)routineRva),
                    RunAiFlagRoutine);
                transaction.Commit();
                if (!aiFlagRoutineHook.Success)
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
            trackingAvailable &&
            projectile != null &&
            projectile->r_ProjectileType == ProjectileType.Disease &&
            registry.ContainsGlobalId(projectile->r_GlobalId);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            ResetMapState();
        }

        private void RunAiFlagRoutine(IntPtr aiManager, int playerId)
        {
            if (!trackingAvailable)
            {
                aiFlagRoutineHook.Value.Hook.Trampoline(aiManager, playerId);
                return;
            }

            int previousPlayerId = activeFlagPlayerId;
            activeFlagPlayerId = playerId;
            try
            {
                // The nested projectile-spawn event is the exact provenance boundary.
                aiFlagRoutineHook.Value.Hook.Trampoline(aiManager, playerId);
            }
            finally
            {
                activeFlagPlayerId = previousPlayerId;
            }
        }

        private void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            int playerId = activeFlagPlayerId;
            if (!trackingAvailable || playerId < 1 || playerId > 8 ||
                args.ProjectileType != ProjectileType.Disease ||
                args.PlayerSourceId != playerId)
            {
                return;
            }

            try
            {
                // Keep stale identities out of saves even if a delete event was skipped.
                PruneInvalidProjectiles();

                if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                    throw new InvalidOperationException($"AI flag Disease returned an invalid slot ID: {args.ReturnValue}.");

                int slotId = checked((int)args.ReturnValue);
                if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(slotId, out GameProjectile* projectile) ||
                    projectile == null ||
                    projectile->r_ProjectileType != ProjectileType.Disease ||
                    projectile->r_GlobalId == 0)
                {
                    throw new InvalidOperationException($"AI flag Disease could not be identified after spawn: slot={slotId}.");
                }

                registry.Track(slotId, projectile->r_GlobalId);
            }
            catch (Exception ex)
            {
                DisableTracking(ex);
            }
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile)
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

        private void DisableTracking(Exception failure)
        {
            trackingAvailable = false;
            ResetMapState();
            if (callbackFailureLogged)
                return;

            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                "Extra Features AI flag Disease tracking was disabled for this process; " +
                $"the configured global plague duration remains active: {failure}");
        }

    }
}
