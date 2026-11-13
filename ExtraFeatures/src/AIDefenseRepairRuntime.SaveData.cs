// Feature: Keep AI tower/gate rebuild delays across save/load. Intact defenses are rebuilt from the
// loaded buildings; only targets that are missing at save time need their history persisted.
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed partial class AIDefenseRepairRuntime
    {
        private const string SaveDataIdentifier = "ExtraFeatures.AIDefenseRebuild.v1";
        private const string ConfirmedDestructionSource = "confirmed-destruction";
        private const string FirstMissingAttemptSource = "first-missing-attempt";

        private readonly object archivedStateSync = new object();
        private byte[] archivedState;
        // The load callback fires for every archive that carries this entry (and twice per load);
        // only bytes from the archive of the session being started belong to it.
        private object archivedStateSource;
        private bool saveDataRegistered;

        private void RegisterSaveData()
        {
            if (saveDataRegistered)
                return;
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                    SaveDataIdentifier, SaveState, BufferArchivedState, ClearArchivedState))
                throw new InvalidOperationException("AI defense rebuild save-data registration failed.");
            saveDataRegistered = true;
        }

        // Runs inside the native save; the simulation does not advance meanwhile.
        private byte[] SaveState(SaveContext context)
        {
            if (!Shared.GameplayModActivationGate.IsAllowed || !context.IsSaveFile ||
                context.IsMapEditorSave || !mapActive)
                return null;

            var records = new List<AIDefenseRebuildSaveRecord>();
            foreach (DefenseTargetKey target in observedDefenseTargets)
            {
                if (liveDefenseTargets.TryGetValue(target, out LiveDefenseIdentity identity) &&
                    IsLiveDefense(target, identity))
                    continue;

                var record = new AIDefenseRebuildSaveRecord
                {
                    PlayerId = target.PlayerId,
                    TileX = target.X,
                    TileY = target.Y,
                    Family = (byte)target.Family
                };
                if (destroyedDefenseTicks.TryGetValue(target, out int destroyedTick))
                {
                    record.Flags |= AIDefenseRebuildSaveRecord.DestroyedFlag;
                    record.DestroyedTick = destroyedTick;
                }
                if (rebuildDelays.TryGetValue(target, out RebuildDelayState delay))
                {
                    record.Flags |= AIDefenseRebuildSaveRecord.DelayFlag;
                    if (delay.Source == ConfirmedDestructionSource)
                        record.Flags |= AIDefenseRebuildSaveRecord.DelayFromConfirmedDestructionFlag;
                    record.DelayStartTick = delay.FirstDetectedTick;
                }
                records.Add(record);
            }
            if (records.Count == 0)
                return null;

            // Deterministic payload independent of HashSet iteration order.
            records.Sort((left, right) =>
            {
                int order = left.PlayerId.CompareTo(right.PlayerId);
                if (order == 0) order = left.TileX.CompareTo(right.TileX);
                if (order == 0) order = left.TileY.CompareTo(right.TileY);
                return order != 0 ? order : left.Family.CompareTo(right.Family);
            });
            byte[] bytes = MessagePackSerializer.Serialize(new AIDefenseRebuildSaveState { Targets = records.ToArray() });
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/AIDefenseRepairRuntime/SaveState",
                    $"AI defense rebuild state saved: missingTargets={records.Count}, payloadBytes={bytes.Length}.");
            return bytes;
        }

        private void BufferArchivedState(byte[] bytes, LoadContext context)
        {
            object archive = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            lock (archivedStateSync) { archivedState = bytes; archivedStateSource = archive; }
        }

        private void ClearArchivedState()
        {
            lock (archivedStateSync) { archivedState = null; archivedStateSource = null; }
        }

        // Always clears the buffer, so a later session never sees bytes of an earlier archive.
        private byte[] TakeArchivedState()
        {
            object archive = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            lock (archivedStateSync)
            {
                byte[] bytes = archive != null && ReferenceEquals(archive, archivedStateSource) ? archivedState : null;
                archivedState = null;
                archivedStateSource = null;
                return bytes;
            }
        }

        // Called after ReconstructLoadedDefenses. A rejected payload leaves the pre-existing
        // behavior: unknown missing targets count as first placements and stay Vanilla.
        private void RestoreSavedTargets(byte[] bytes)
        {
            if (bytes == null)
                return;
            try
            {
                AIDefenseRebuildSaveState state = MessagePackSerializer.Deserialize<AIDefenseRebuildSaveState>(bytes);
                if (state == null || state.Version != AIDefenseRebuildSaveState.CurrentVersion)
                    throw new InvalidOperationException("The AI defense rebuild state has an unsupported version.");

                AIDefenseRebuildSaveRecord[] records = state.Targets ?? Array.Empty<AIDefenseRebuildSaveRecord>();
                var targets = new DefenseTargetKey[records.Length];
                var seen = new HashSet<DefenseTargetKey>();
                for (int index = 0; index < records.Length; index++)
                {
                    AIDefenseRebuildSaveRecord record = records[index];
                    if (record == null || !TryCreateSavedTarget(record, out targets[index]) || !seen.Add(targets[index]))
                        throw new InvalidOperationException($"The AI defense rebuild state has an invalid or duplicate record at index {index}.");
                }

                // The ticks are the saved native map clock, which continues after loading. They are
                // restored verbatim (no local clock read), so every peer derives identical AI decisions.
                // Should a tick ever lie ahead of the clock, the consumers stay deterministic: a delay
                // start counts as elapsed, a destruction tick is ignored and the delay starts anew.
                int restored = 0;
                for (int index = 0; index < records.Length; index++)
                {
                    DefenseTargetKey target = targets[index];
                    if (HasLiveDefense(target))
                        continue;

                    AIDefenseRebuildSaveRecord record = records[index];
                    observedDefenseTargets.Add(target);
                    if ((record.Flags & AIDefenseRebuildSaveRecord.DestroyedFlag) != 0)
                        destroyedDefenseTicks[target] = record.DestroyedTick;
                    if ((record.Flags & AIDefenseRebuildSaveRecord.DelayFlag) != 0)
                        rebuildDelays[target] = new RebuildDelayState(
                            record.DelayStartTick,
                            (record.Flags & AIDefenseRebuildSaveRecord.DelayFromConfirmedDestructionFlag) != 0
                                ? ConfirmedDestructionSource
                                : FirstMissingAttemptSource);
                    restored++;
                }
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/AIDefenseRepairRuntime/RestoreSavedTargets",
                        $"AI defense rebuild state restored: missingTargets={restored}, records={records.Length}.");
            }
            catch (Exception ex)
            {
                LogFailure("save-load rebuild-state restoration", ex);
            }
        }

        private static bool TryCreateSavedTarget(AIDefenseRebuildSaveRecord record, out DefenseTargetKey target)
        {
            target = default;
            if (record.PlayerId < 1 || record.PlayerId > GamePlayerManagerAPI.MAX_PLAYERS ||
                record.TileX < 0 || record.TileX >= GameTileManagerAPI.MAX_WIDTH ||
                record.TileY < 0 || record.TileY >= GameTileManagerAPI.MAX_HEIGHT ||
                (record.Flags & ~AIDefenseRebuildSaveRecord.KnownFlags) != 0)
                return false;
            DefenseFamily family = (DefenseFamily)record.Family;
            if (family != DefenseFamily.Tower && family != DefenseFamily.Gate)
                return false;
            target = new DefenseTargetKey(record.PlayerId, record.TileX, record.TileY, family);
            return true;
        }

        private static bool IsNotInFuture(int tick, int now) => unchecked((uint)(now - tick)) <= int.MaxValue;
    }
}
