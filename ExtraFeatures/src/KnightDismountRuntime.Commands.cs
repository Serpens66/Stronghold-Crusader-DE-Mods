// Feature: Knight transformation commands, their Chore synchronization and spoken feedback.
using SHCDESE.API;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed unsafe partial class KnightDismountRuntime
    {
        // Units an action starts from: mounting turns swordsmen into knights, dismounting the reverse.
        private static eChimps SourceUnitType(int action) =>
            action == MountAction ? eChimps.CHIMP_TYPE_SWORDSMAN : eChimps.CHIMP_TYPE_KNIGHT;

        private void OnTransformationCommand(int action)
        {
            bool mount = action == MountAction;
            try
            {
                if (!IsFeatureActive())
                    return;

                int localPlayerId = GetSelectionPlayerId();
                List<UnitTransformSnapshot> snapshots = CaptureSelectedUnitSnapshots(localPlayerId, SourceUnitType(action));
                if (snapshots.Count == 0)
                {
                    RefreshButtonVisibility();
                    return;
                }

                int goldCost = KnightTransformationTooltipPolicy.ClampGoldCost(settings.KnightTransformationGoldCost);
                if (goldCost > 0 && GamePlayerManagerAPI.Instance.GetPlayerGold(localPlayerId) < goldCost)
                {
                    PlayMissingGoldSpeech();
                    RefreshButtonVisibility();
                    return;
                }

                // Mounting also needs a free stable horse; checked after the gold, as the warning order expects.
                if (mount && CountAvailableHorseSlots(localPlayerId) == 0)
                {
                    PlayMissingWeaponsSpeech();
                    RefreshButtonVisibility();
                    return;
                }

                IssueInternalStop();

                if (RequiresChoreTransport())
                {
                    TrySendTransformationChore(localPlayerId, action, snapshots);
                }
                else
                {
                    // Noesis commands run on the main thread; the engine owns this state.
                    lock (EngineLock.Gate)
                        StartPendingBatch(localPlayerId, action, snapshots, localAction: true, "local-click");
                }

                RefreshButtonVisibility();
            }
            catch (Exception ex)
            {
                LogError($"Knight {(mount ? "mount" : "dismount")} click failed: {ex}");
            }
        }

        private bool IsFeatureActive()
        {
            // Pending transformations need the published tick/save infrastructure (and with it the
            // engine monitor); without it a click would stop the troops and then fail.
            return initialized && !deactivated && ReferenceEquals(activeRuntime, this) &&
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                settings.EnableKnightDismount &&
                (!RequiresChoreTransport() || IsChoreTransportReady());
        }

        private bool RequiresChoreTransport()
        {
            return multiplayerFeatureGate.BlocksLocalStateChanges;
        }

        private bool IsChoreTransportReady()
        {
            return APIShared.Networking.ChoreTransport.IsAvailable(
                networkInitialized && transformationPacketHook != null);
        }

        private bool TrySendTransformationChore(int playerId, int action, List<UnitTransformSnapshot> snapshots)
        {
            if (!IsChoreTransportReady())
            {
                LogError("Knight transformation refused in multiplayer because the Chore transport is unavailable.");
                return false;
            }

            if (snapshots == null || snapshots.Count < 1 ||
                snapshots.Count > KnightTransformationPacket.MaximumEncodedTargetCount)
            {
                LogError($"Knight transformation refused because the target count cannot fit safely in a Chore packet: count={snapshots?.Count ?? 0}, absoluteMaximum={KnightTransformationPacket.MaximumEncodedTargetCount}.");
                return false;
            }

            var globalIds = new List<int>(snapshots.Count);
            for (int index = 0; index < snapshots.Count; index++)
            {
                if (snapshots[index].GlobalId <= 0)
                {
                    LogError($"Knight transformation refused because selected unit {snapshots[index].UnitId} has no stable global ID.");
                    return false;
                }

                globalIds.Add(snapshots[index].GlobalId);
            }

            return TrySendTransformationPacket(playerId, action, globalIds.ToArray());
        }

        private bool TrySendTransformationPacket(int playerId, int action, int[] unitGlobalIds)
        {
            int operationId = nextOperationId == int.MaxValue ? 1 : nextOperationId + 1;
            nextOperationId = operationId;
            var packet = new KnightTransformationPacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                PlayerId = playerId,
                OperationId = operationId,
                Action = action,
                UnitGlobalIds = unitGlobalIds ?? Array.Empty<int>()
            };

            if (!KnightTransformationPacketValidation.HasValidMetadataAndTargets(packet, GamePlayerManagerAPI.MAX_PLAYERS))
            {
                LogError($"Knight transformation refused because the outgoing packet metadata is invalid: playerId={playerId}, operationId={operationId}.");
                return false;
            }

            short packetId = transformationPacketHook?.GetPacketId() ?? (short)0;
            if (!APIShared.Networking.ChoreTransport.TrySend(
                    packet,
                    packetId,
                    networkInitialized && transformationPacketHook != null,
                    out byte[] body,
                    out string rejectionReason))
            {
                LogError($"Knight transformation Chore was not queued; no local action was applied: operationId={operationId}, reason={rejectionReason}.");
                return false;
            }

            return true;
        }

        private void OnTransformationPacketReceived(ReceiveCustomPacketEventArgs<KnightTransformationPacket> args)
        {
            if (!APIShared.Networking.ChoreTransport.IsChoreDelivery(args))
                return;

            if (!Shared.GameplayModActivationGate.IsAllowed)
                return;

            KnightTransformationPacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion ||
                !KnightTransformationPacketValidation.HasValidMetadataAndTargets(packet, GamePlayerManagerAPI.MAX_PLAYERS))
            {
                LogError("Rejected a Knight transformation Chore with an invalid payload.");
                return;
            }

            try
            {
                bool localAction = packet.PlayerId == ControlledPlayer.GetId();
                if (packet.Action == KnightTransformationPacket.CancelAllAction)
                {
                    CancelAllPending("multiplayer-cancel-all", refundGold: true, releaseReservedHorse: true);
                    if (localAction)
                        ScheduleDeferredSaveCompletion();
                    return;
                }

                if (packet.Action == KnightTransformationPacket.CancelSelectedAction)
                {
                    CancelPendingByGlobalIds(packet.PlayerId, packet.UnitGlobalIds, "multiplayer-stop");
                    return;
                }

                List<UnitTransformSnapshot> snapshots = CaptureSnapshotsByGlobalIds(
                    packet.PlayerId, SourceUnitType(packet.Action), packet.UnitGlobalIds);
                if (snapshots.Count != packet.UnitGlobalIds.Length)
                {
                    LogError($"Knight transformation Chore refused because not every unit resolved identically: operationId={packet.OperationId}, requested={packet.UnitGlobalIds.Length}, resolved={snapshots.Count}.");
                    return;
                }

                StartPendingBatch(packet.PlayerId, packet.Action, snapshots, localAction, "multiplayer-chore");

                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogDebug($"Knight transformation started: action={packet.Action}, unitCount={snapshots.Count}.");
            }
            catch (Exception ex)
            {
                LogError($"Knight transformation Chore execution failed: operationId={packet.OperationId}, exception={ex}");
            }
            finally
            {
                Shared.UnityMainThreadDispatch.TryEnqueue(RefreshButtonVisibility);
            }
        }

        private static List<UnitTransformSnapshot> CaptureSnapshotsByGlobalIds(int playerId, eChimps expectedType, int[] globalIds)
        {
            var snapshots = new List<UnitTransformSnapshot>(globalIds.Length);
            var seen = new HashSet<int>();
            // One pass resolves every requested global ID (first = lowest game ID, as a per-ID scan would)
            // instead of one full unit scan per selected unit.
            var resolvedIds = new Dictionary<int, int>(globalIds.Length);
            for (int index = 0; index < globalIds.Length; index++)
                if (globalIds[index] > 0) resolvedIds[globalIds[index]] = -1;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit candidate = ref units[spanIndex];
                int candidateGlobalId = (int)candidate.r_GlobalId;
                if (candidateGlobalId > 0 && resolvedIds.TryGetValue(candidateGlobalId, out int known) && known < 0 &&
                    APIShared.UnitAccess.IsReallyAlive(in candidate))
                    resolvedIds[candidateGlobalId] = spanIndex + 1;
            }

            for (int index = 0; index < globalIds.Length; index++)
            {
                int globalId = globalIds[index];
                if (globalId <= 0 || !seen.Add(globalId))
                    continue;

                int unitId = resolvedIds[globalId];
                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    !IsOwnAliveUnit(unit, playerId, expectedType))
                {
                    continue;
                }

                snapshots.Add(CreateSnapshotFromUnit(unitId, unit));
            }

            return snapshots;
        }

        private void PlayMissingWeaponsSpeech() =>
            PlaySpeech(MissingWeaponsSpeechFileName, "mount missing stable horse");

        private void PlayRandomLocalSpeech(string[] fileNames, string label) =>
            PlaySpeech(GetRandomSpeechFileName(fileNames), label);

        // Unity audio belongs to the main thread. Always queued: Chore batches run on the engine
        // thread, and single-player batches hold the engine monitor, which clip loading would extend.
        private void PlaySpeech(string speechFileName, string label)
        {
            if (!Shared.UnityMainThreadDispatch.TryEnqueue(() => PlaySpeechNow(speechFileName, label)))
                LogError($"Could not schedule knight {label} speech on the main thread.");
        }

        private void PlaySpeechNow(string speechFileName, string label)
        {
            try
            {
                SFXManager.instance?.playSpeech(VoiceSpeechChannel, speechFileName, 1f);
            }
            catch (Exception ex)
            {
                LogError($"Could not play knight {label} speech: {ex}");
            }
        }

        private static string GetRandomSpeechFileName(string[] speechFileNames)
        {
            lock (SpeechRandom)
            {
                return speechFileNames[SpeechRandom.Next(speechFileNames.Length)];
            }
        }
    }
}
