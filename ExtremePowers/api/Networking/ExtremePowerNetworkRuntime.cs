using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using System;
using System.Collections.Generic;
using System.Threading;
using SHCDESE.EventAPI.MapLoader;

namespace ExtremePowers.API
{
    internal sealed class ExtremePowerNetworkRuntime
    {
        private readonly ExtremePowersApi owner;
        private readonly R3PacketEventHook<ExtremePowerChore> packetHook;
        private readonly IDisposable subscription;
        private readonly IDisposable mapUnloadSubscription;
        private readonly HashSet<string> completedOperations = new HashSet<string>(StringComparer.Ordinal);
        private long nextOperationId;

        internal ExtremePowerNetworkRuntime(ExtremePowersApi owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<ExtremePowerChore>();
            subscription = packetHook.GetBaseHook().Observable.Subscribe(Receive);
            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => completedOperations.Clear());
        }

        internal bool Queue(ExtremePowerId power, int playerId, ExtremePowerTarget target)
        {
            if (!owner.NativeBackendAvailable || !owner.IsSynchronizedSessionReady() || !ChoreNetworkTransport.IsAvailable || playerId < 1 || playerId > 8 || !IsTargetValid(target)) return false;
            if (!owner.TryGetReplacement(power, out ExtremePowerReplacement replacement) || replacement.TargetKind != target.Kind) return false;
            var packet = new ExtremePowerChore(ExtremePowerChoreCodec.CurrentProtocol, power, playerId, target, unchecked((ulong)Interlocked.Increment(ref nextOperationId)));
            byte[] body;
            try { body = MessagePackSerializer.Serialize(packet); }
            catch { return false; }
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            Func<byte[], bool> send = ChoreNetworkTransport.SendRawBlob;
            return send != null && send(blob);
        }

        private void Receive(ReceiveCustomPacketEventArgs<ExtremePowerChore> args)
        {
            if (args == null || args.SenderSteamId.HasValue || !owner.NativeBackendAvailable || !owner.IsSynchronizedSessionReady()) return;
            ExtremePowerChore packet = args.Packet;
            if (packet.Protocol != ExtremePowerChoreCodec.CurrentProtocol || (uint)packet.Power > 7 || packet.PlayerId < 1 || packet.PlayerId > 8 || packet.OperationId == 0 || !GamePlayerManagerAPI.Instance.IsPlayerIdValid(packet.PlayerId) || !IsTargetValid(packet.Target)) return;
            if (!owner.TryGetReplacement(packet.Power, out ExtremePowerReplacement replacement) || replacement.TargetKind != packet.Target.Kind) return;
            ExtremePowersTuning tuning = owner.Snapshot();
            int cost = tuning.Costs[(int)packet.Power];
            int mana = GamePlayerManagerAPI.Instance.GetLocalPlayerExtremePowersMana(packet.PlayerId);
            if (mana < cost) return;
            int tick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            var context = new ExtremePowerExecutionContext(packet.Power, packet.PlayerId, packet.Target, packet.OperationId, tick);
            string operationKey = packet.PlayerId + ":" + packet.OperationId;
            if (!completedOperations.Add(operationKey)) return;
            if (!owner.TryExecuteReplacement(context, out _)) return;
            GamePlayerManagerAPI.Instance.SetLocalPlayerExtremePowersMana(packet.PlayerId, mana - cost);
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
