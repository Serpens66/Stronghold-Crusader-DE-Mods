using APIShared.Multiplayer;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL
{
    internal sealed partial class SingleBuildingPauseHook
    {
        private const string ResyncId = "single-building-pause";
        // The audited building pool has 3,999 live-record slots and a zero sentinel.
        private const int ResyncBuildingSlots = 3999;
        private const int ResyncRowBytes = sizeof(int) * 3 + sizeof(byte);
        private volatile bool repairing;
        private bool resyncReady, resyncRegistrationAttempted;
        private ResyncOverride[] pendingRepair;

        private readonly struct ResyncOverride
        {
            internal ResyncOverride(int slot, int globalId, int owner, eStructs type, bool sleeping)
            { Slot = slot; GlobalId = globalId; Owner = owner; Type = type; Sleeping = sleeping; }

            internal readonly int Slot, GlobalId, Owner;
            internal readonly eStructs Type;
            internal readonly bool Sleeping;
        }

        private void InitializeResync()
        {
            if (resyncRegistrationAttempted) return;
            resyncRegistrationAttempted = true;
            resyncReady = MultiplayerResyncEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, ResyncId,
                BeginResync, CompleteResync, out string reason) &&
                MultiplayerResyncData.TryRegister<ResyncOverride[]>(BugfixesAndQoLPlugin.PluginGuid, ResyncId, 1,
                    ResyncBuildingSlots * ResyncRowBytes, CaptureResync, PrepareResync, CommitResync, out reason);
            if (!resyncReady) LogError("Single-building pause multiplayer data registration failed: " + reason);
        }

        private void BeginResync()
        {
            if (!resyncReady) return;
            pendingRepair = null;
            repairing = true;
        }

        private byte[] CaptureResync()
        {
            if (!settings.EnableMod || !settings.EnableSingleBuildingPause) return Array.Empty<byte>();
            var rows = overrides.Snapshot();
            Array.Sort(rows, (left, right) => left.GlobalId.CompareTo(right.GlobalId));
            var bytes = new byte[rows.Length * ResyncRowBytes];
            for (int i = 0; i < rows.Length; i++)
            {
                var entry = rows[i];
                var row = bytes.AsSpan(i * ResyncRowBytes, ResyncRowBytes);
                BinaryPrimitives.WriteInt32LittleEndian(row, entry.GlobalId);
                BinaryPrimitives.WriteInt32LittleEndian(row.Slice(4), entry.Owner);
                BinaryPrimitives.WriteInt32LittleEndian(row.Slice(8), (int)entry.BuildingType);
                row[12] = entry.IsSleeping ? (byte)1 : (byte)0;
            }
            return bytes;
        }

        private ResyncOverride[] PrepareResync(ReadOnlyMemory<byte> record)
        {
            if (!repairing || record.Length % ResyncRowBytes != 0)
                throw new InvalidDataException("Invalid building-pause repair record.");
            if (record.IsEmpty) return Array.Empty<ResyncOverride>();
            if (!settings.EnableMod || !settings.EnableSingleBuildingPause || !localHooksInstalled ||
                synchronizeSleepStates == null || setSleepOverrideInterceptionEnabled == null)
                throw new InvalidOperationException("The local building-pause runtime is unavailable for repair.");

            var requested = new Dictionary<int, ResyncOverride>();
            int previousGlobal = 0;
            var remaining = record.Span;
            while (!remaining.IsEmpty)
            {
                int global = BinaryPrimitives.ReadInt32LittleEndian(remaining);
                int owner = BinaryPrimitives.ReadInt32LittleEndian(remaining.Slice(4));
                var type = (eStructs)BinaryPrimitives.ReadInt32LittleEndian(remaining.Slice(8));
                byte sleeping = remaining[12];
                if (global <= previousGlobal || !GamePlayerManagerAPI.Instance.IsPlayerIdValid(owner) || sleeping > 1)
                    throw new InvalidDataException("Invalid building-pause repair identity or state.");
                requested.Add(global, new ResyncOverride(0, global, owner, type, sleeping != 0));
                previousGlobal = global;
                remaining = remaining.Slice(ResyncRowBytes);
            }

            var result = new List<ResyncOverride>(requested.Count);
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_AliveState != AliveState.IsAlive ||
                    !requested.TryGetValue((int)building.r_GlobalId, out ResyncOverride row) ||
                    row.Owner != building.r_PlayerIdOwner || row.Type != building.r_BuildingType) continue;
                requested.Remove(row.GlobalId);
                result.Add(new ResyncOverride(spanIndex + 1, row.GlobalId, row.Owner, row.Type, row.Sleeping));
            }
            return result.ToArray();
        }

        private void CommitResync(ResyncOverride[] state) => pendingRepair = state;

        private unsafe void CompleteResync()
        {
            if (!repairing) return;
            if (pendingRepair == null)
                throw new InvalidOperationException("Building-pause repair completed without an override snapshot.");

            // Chore 67 has only re-enqueued held actions. Build the local sleeping-address index
            // here, before the next simulation step, without retaining pointers in Prepare.
            var replacement = new SingleBuildingPauseOverrideStore();
            foreach (var row in pendingRepair)
            {
                if (!APIShared.BuildingAccess.TryGetById(row.Slot, out GameBuilding* building, out _) ||
                    building->r_AliveState != AliveState.IsAlive || (int)building->r_GlobalId != row.GlobalId ||
                    building->r_PlayerIdOwner != row.Owner || building->r_BuildingType != row.Type)
                    throw new InvalidOperationException("A prepared building-pause identity changed before completion.");
                replacement.Set(new SingleBuildingPauseOverride(row.Slot, row.Sleeping,
                    (IntPtr)(&building->r_IsSleeping), row.Type, row.Owner, row.GlobalId));
            }

            if (replacement.Count != 0)
            {
                if (!TryActivateOverrideHooks())
                    throw new InvalidOperationException("Building-pause interception did not resume after repair.");
            }
            else if (overrideHooksActive)
            {
                setSleepOverrideInterceptionEnabled(false);
                overrideHooksActive = false;
            }
            overrides = replacement;
            pendingRepair = null;
            repairing = false;
        }
    }
}
