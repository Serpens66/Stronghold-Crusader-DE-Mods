using System;
using System.Collections.Generic;

namespace AreaDemolitionTest
{
    internal enum ReadinessAckState { Pending, Ready, Mismatch }

    // Called under the network owner's lock, including for native local replies.
    // A world token can recur after a roster change; only this outgoing attempt admits ACKs.
    internal sealed class ReadinessAckLedger
    {
        private readonly Dictionary<ulong,string> expected=new Dictionary<ulong,string>();
        private readonly Dictionary<ulong,Receipt> receipts=new Dictionary<ulong,Receipt>();
        private ulong localSteam;
        private string localNonce="",token="";
        private long attempt;
        private readonly struct Receipt
        {
            internal Receipt(int command,int tick){Command=command;Tick=tick;}
            internal readonly int Command,Tick;
        }
        internal void Clear()
        {expected.Clear();receipts.Clear();localSteam=0;localNonce=token="";attempt=0;}
        internal void Begin(ulong ownSteam,string ownNonce,string worldToken,long proofAttempt,ProtocolPeer[] vector)
        {
            Clear();
            if(ownSteam==0 || !ProtocolPolicy.IsNonce(ownNonce) || !ProtocolPolicy.IsDigest(worldToken) || proofAttempt<=0 || vector==null)
                throw new ArgumentException("Invalid outgoing readiness proof.");
            var copy=ProtocolPolicy.CanonicalPeers(vector);
            ProtocolPolicy.EncodePeers(copy,true);
            if(!ProtocolPolicy.OwnFreshNonce(copy,ownSteam,ownNonce))throw new ArgumentException("The readiness vector omits the local world.");
            foreach(var peer in copy)expected.Add(peer.SteamId,peer.Nonce);
            localSteam=ownSteam;localNonce=ownNonce;token=worldToken;attempt=proofAttempt;
        }
        internal bool TryAccept(ulong sender,DemolitionControlPacket ack)
        {
            if(attempt<=0 || ack==null || ack.Protocol!=ProtocolPolicy.Version || ack.Kind!=(byte)ControlPacketKind.ProofAck ||
                ack.Operation!=attempt || ack.Token!=token || ack.ProbeNonce!=localNonce || ack.Batch!=0 ||
                !expected.TryGetValue(sender,out string peerNonce) || ack.PeerNonce!=peerNonce)return false;
            receipts[sender]=new Receipt(ack.Command,ack.Tick);
            return true;
        }
        internal ReadinessAckState Evaluate()
        {
            if(attempt<=0 || receipts.Count!=expected.Count || !receipts.TryGetValue(localSteam,out var own))return ReadinessAckState.Pending;
            foreach(var receipt in receipts.Values)
                if(receipt.Command!=own.Command || receipt.Tick!=own.Tick)return ReadinessAckState.Mismatch;
            return ReadinessAckState.Ready;
        }
    }

    internal enum GameplayDispatchPhase { Queued, Submitting, Submitted, Cancelled }

    internal static class ExecutionAckPolicy
    {
        // Local cached replies and remote control messages share exactly the same boundary.
        internal static bool Matches(ulong sender,ulong localSteam,string localNonce,DemolitionSimulationPacket inflight,
            ProtocolPeer[] vector,DemolitionControlPacket ack)
        {
            if(inflight==null || vector==null || !ProtocolPolicy.ValidateControlPacket(ack) || ack.Kind!=(byte)ControlPacketKind.ExecutionAck ||
                ack.Origin!=localSteam || ack.ProbeNonce!=localNonce || ack.Token!=inflight.Token ||
                ack.Operation!=inflight.Operation || ack.Batch!=inflight.Batch || ack.InputDigest!=inflight.Digest)return false;
            foreach(var peer in vector)if(peer.SteamId==sender && peer.Nonce==ack.PeerNonce)return true;
            return false;
        }
    }

    // One ticket belongs to one immutable queued ApplyBatch. Entering the external Send
    // is the uncertainty boundary: cancellation may drop Queued work, never replay entered work.
    internal sealed class GameplayDispatchTicket
    {
        private readonly long generation,operation;
        private readonly int batch;
        internal GameplayDispatchTicket(long generation,long operation,int batch)
        {this.generation=generation;this.operation=operation;this.batch=batch;}
        internal GameplayDispatchPhase Phase {get;private set;}=GameplayDispatchPhase.Queued;
        internal bool TryBeginSend(long currentGeneration,long currentOperation,int currentBatch,GameplayDispatchTicket currentTicket,bool cancelled,bool failed)
        {
            if(Phase!=GameplayDispatchPhase.Queued)return false;
            if(!ReferenceEquals(this,currentTicket) || generation!=currentGeneration || operation!=currentOperation || batch!=currentBatch || cancelled || failed)
            {CancelQueued();return false;}
            Phase=GameplayDispatchPhase.Submitting;return true;
        }
        internal void CompleteSend(bool submitted)
        {if(Phase==GameplayDispatchPhase.Submitting && submitted)Phase=GameplayDispatchPhase.Submitted;}
        internal bool CancelQueued()
        {if(Phase!=GameplayDispatchPhase.Queued)return false;Phase=GameplayDispatchPhase.Cancelled;return true;}
    }
}
