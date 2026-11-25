using APIShared.Multiplayer;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace ExtraFeatures
{
    internal sealed unsafe partial class AiFlagDiseaseTracker
    {
        private const string ProjectileResyncRecord = "disease-origin";
        private const int ProjectileRowBytes = sizeof(ushort) + sizeof(uint);
        private const int MaximumProjectileResyncBytes = PlagueFlagDiseaseRegistry.NativeProjectileSlotCount * ProjectileRowBytes;
        private bool projectileResyncReady;
        private volatile bool projectileRepairing;
        private volatile bool projectileTrackingSuspended;

        private bool CanTrackInWorld => trackingAvailable && !projectileTrackingSuspended &&
            (projectileResyncReady || !Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer);

        internal void RegisterProjectileResync()
        {
            projectileResyncReady = EngineRunEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid,
                ProjectileResyncRecord, () => { }, SuspendProjectileTrackingDuringRepair, out string reason) &&
                MultiplayerResyncEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid,
                    ProjectileResyncRecord, BeginProjectileRepair, CompleteProjectileRepair, out reason) &&
                MultiplayerResyncData.TryRegister<PlagueFlagDiseaseRegistry>(ExtraFeaturesPlugin.PluginGuid,
                    ProjectileResyncRecord, 1, MaximumProjectileResyncBytes, CaptureProjectileResync,
                    PrepareProjectileResync, CommitProjectileResync, out reason);
            if (!projectileResyncReady)
                Shared.DebugLogHelper.LogError(log, "Vanilla-duration Disease multiplayer repair is unavailable: " + reason);
        }

        private void BeginProjectileRepair() => projectileRepairing = projectileResyncReady &&
            Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer;

        // Synchronized Disease creation can still run after Chore54 in the same due list.
        private void SuspendProjectileTrackingDuringRepair() => projectileTrackingSuspended = projectileRepairing;

        private void CompleteProjectileRepair() => projectileRepairing = projectileTrackingSuspended = false;

        private byte[] CaptureProjectileResync()
        {
            if (!projectileRepairing || !Shared.GameplayModActivationGate.IsAllowed)
                return Array.Empty<byte>();
            if (!trackingAvailable)
                throw new InvalidDataException("Disease origin tracking failed on an admitted peer.");

            PlagueFlagDiseaseIdentity[] identities = registry.Snapshot();
            var data = new byte[identities.Length * ProjectileRowBytes];
            for (int index = 0; index < identities.Length; index++)
            {
                Span<byte> row = data.AsSpan(index * ProjectileRowBytes, ProjectileRowBytes);
                BinaryPrimitives.WriteUInt16LittleEndian(row, checked((ushort)identities[index].SlotId));
                BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(sizeof(ushort)), identities[index].GlobalId);
            }
            return data;
        }

        private static PlagueFlagDiseaseRegistry PrepareProjectileResync(ReadOnlyMemory<byte> data)
        {
            var prepared = new PlagueFlagDiseaseRegistry();
            if (data.IsEmpty)
                return prepared;
            if (data.Length % ProjectileRowBytes != 0 || data.Length > MaximumProjectileResyncBytes)
                throw new InvalidDataException("Invalid Disease origin record size.");

            Span<GameProjectile> projectiles = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            if (!Shared.GameProjectileSlotPolicy.TryResolve(projectiles, out var layout))
                throw new InvalidDataException("Cannot resolve the repaired projectile pool.");

            int previousSlot = 0;
            var globalIds = new HashSet<uint>();
            for (int offset = 0; offset < data.Length; offset += ProjectileRowBytes)
            {
                ReadOnlySpan<byte> row = data.Span.Slice(offset, ProjectileRowBytes);
                int slotId = BinaryPrimitives.ReadUInt16LittleEndian(row);
                uint globalId = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(sizeof(ushort)));
                if (slotId <= previousSlot || slotId > PlagueFlagDiseaseRegistry.NativeProjectileSlotCount ||
                    globalId == 0 || !globalIds.Add(globalId))
                    throw new InvalidDataException("Invalid or duplicate Disease origin identity.");
                previousSlot = slotId;
                if (!layout.TryGetSpanIndex(slotId, out int spanIndex))
                    continue;

                ref GameProjectile projectile = ref projectiles[spanIndex];
                if (projectile.r_GlobalId == globalId && projectile.r_ProjectileType == ProjectileType.Disease &&
                    (projectile.r_AliveState == AliveState.IsAlive || projectile.r_AliveState == AliveState.NeedsInit))
                    prepared.Track(slotId, globalId);
            }
            return prepared;
        }

        private void CommitProjectileResync(PlagueFlagDiseaseRegistry prepared)
        {
            if (projectileRepairing)
                registry = prepared;
        }
    }
}
