using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using System;
using System.Threading;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;

namespace ExtremePowers.API
{
    internal sealed class ExtremePowerNetworkRuntime
    {
        private readonly ExtremePowersApi owner;
        private readonly R3PacketEventHook<ExtremePowerChore> packetHook;
        private readonly IDisposable subscription;
        private readonly IDisposable mapUnloadSubscription;
        private readonly ExtremePowerOperationTracker completedOperations = new ExtremePowerOperationTracker();
        private long nextOperationId;
        private ushort mapEpoch = 1;

        internal short PacketId => packetHook.GetPacketId();

        internal ExtremePowerNetworkRuntime(ExtremePowersApi owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<ExtremePowerChore>();
            subscription = packetHook.GetBaseHook().Observable.Subscribe(Receive);
            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => ResetMapState());
        }

        internal bool Queue(ExtremePowerId power, int playerId, ExtremePowerTarget target, out string rejectionReason)
        {
            rejectionReason = null;
            if (!owner.NativeBackendAvailable) return Reject("Native backend is unavailable.", out rejectionReason);
            ExtremePowersReadiness readiness = owner.GetSessionReadiness();
            if (!readiness.Ready) return Reject(readiness.Reason, out rejectionReason);
            if ((uint)power > 7 || playerId < 1 || playerId > 8) return Reject("Invalid power or player.", out rejectionReason);
            if (!IsTargetValid(target)) return Reject("Invalid or stale target.", out rejectionReason);
            if (!owner.TryGetReplacement(power, out ExtremePowerReplacement replacement)) return Reject("No replacement is registered.", out rejectionReason);
            if (replacement.TargetKind != target.Kind) return Reject("Target kind does not match the replacement contract.", out rejectionReason);
            ulong sequence = unchecked((ulong)Interlocked.Increment(ref nextOperationId)) & 0xFFFFFFFFFFUL;
            ulong operation = ((ulong)mapEpoch << 48) | ((ulong)(byte)playerId << 40) | sequence;
            if (operation == 0) return Reject("Operation id generation failed.", out rejectionReason);
            var packet = new ExtremePowerChore(ExtremePowerChoreCodec.CurrentProtocol, power, playerId, target, operation);
            if (!ExtremePowerChoreSender.TrySend(
                packet,
                packetHook.GetPacketId(),
                packetHook != null,
                value => GameNetworkAPI.Serialize(value),
                () => GameGlobalsManager.Instance.ChoreManagerVA,
                (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                out byte[] body,
                out string sendFailure))
                return Reject(sendFailure, out rejectionReason);
            owner.Log("Queued replacement power=" + power + " player=" + playerId + " target=" + target.Kind + " operation=" + operation + ".");
            return true;
        }

        private void Receive(ReceiveCustomPacketEventArgs<ExtremePowerChore> args)
        {
            if (args == null || args.SenderSteamId.HasValue || !owner.NativeBackendAvailable) return;
            ExtremePowersReadiness readiness = owner.GetSessionReadiness();
            if (!readiness.Ready) { owner.Log("Rejected replacement chore: " + readiness.Reason); return; }
            ExtremePowerChore packet = args.Packet;
            if (packet.Protocol != ExtremePowerChoreCodec.CurrentProtocol || (uint)packet.Power > 7 || packet.PlayerId < 1 || packet.PlayerId > 8 || packet.OperationId == 0 || !GamePlayerManagerAPI.Instance.IsPlayerIdValid(packet.PlayerId) || !IsTargetValid(packet.Target)) return;
            if (!owner.TryGetReplacement(packet.Power, out ExtremePowerReplacement replacement) || replacement.TargetKind != packet.Target.Kind) return;
            ExtremePowersTuning tuning = owner.Snapshot();
            int cost = tuning.Costs[(int)packet.Power];
            int mana = GamePlayerManagerAPI.Instance.GetLocalPlayerExtremePowersMana(packet.PlayerId);
            if (mana < cost) return;
            int tick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            var context = new ExtremePowerExecutionContext(packet.Power, packet.PlayerId, packet.Target, packet.OperationId, tick);
            if (!completedOperations.TryBegin(packet.PlayerId, packet.OperationId)) return;
            if (!owner.TryExecuteReplacement(context, out string rejection)) { owner.Log("Rejected replacement power=" + packet.Power + " player=" + packet.PlayerId + ": " + rejection); return; }
            GamePlayerManagerAPI.Instance.SetLocalPlayerExtremePowersMana(packet.PlayerId, mana - cost);
            owner.Log("Executed replacement power=" + packet.Power + " player=" + packet.PlayerId + " target=" + packet.Target.Kind + " mana=" + mana + " cost=" + cost + " operation=" + packet.OperationId + ".");
        }

        private void ResetMapState()
        {
            completedOperations.Reset();
            Interlocked.Exchange(ref nextOperationId, 0);
            unchecked { mapEpoch++; if (mapEpoch == 0) mapEpoch = 1; }
        }

        private bool Reject(string reason, out string rejectionReason)
        {
            rejectionReason = string.IsNullOrWhiteSpace(reason) ? "Replacement request was rejected." : reason;
            owner.Log("Rejected replacement queue: " + rejectionReason);
            return false;
        }

        private static bool IsTargetValid(ExtremePowerTarget target)
        {
            if (!ExtremePowerTargetValidator.IsValid(target)) return false;
            if (target.Kind == ExtremePowerTargetKind.MapPoint) return GameTileManagerAPI.Instance.IsValidTileId(target.TileIndex);
            if (target.Kind == ExtremePowerTargetKind.Unit) return GameUnitManagerAPI.Instance.IsValid(target.UnitId);
            return target.Kind == ExtremePowerTargetKind.None;
        }
    }
}
