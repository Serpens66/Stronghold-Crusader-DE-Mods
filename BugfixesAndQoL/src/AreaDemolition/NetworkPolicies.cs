using System;
using System.Collections.Generic;
using System.Linq;
using APIShared.Networking;

namespace BugfixesAndQoL.AreaDemolition
{
    internal static class LocalDemolitionGate
    {
        // SettingChanged is synchronous. Cancellation precedes any deferred UI work,
        // regardless of the order in which network and presentation callbacks run.
        internal static void ApplyChange(bool enabled,Action cancelUnsent,Action requestPresentation)
        {
            if(!enabled)cancelUnsent();
            requestPresentation();
        }
    }

    // Validates the remaining demolition wire fields before the shared proof accepts a receipt.
    internal sealed class ReadinessAckLedger
    {
        private readonly NativeReadinessProof proof = new NativeReadinessProof();
        private string localNonce = "";

        internal void Clear()
        {
            proof.Clear();
            localNonce = "";
        }

        internal void Begin(ulong ownSteam,string ownNonce,string worldToken,long proofAttempt,ProtocolPeer[] vector)
        {
            Clear();
            if(ownSteam==0 || !ProtocolPolicy.IsNonce(ownNonce) || !ProtocolPolicy.IsDigest(worldToken) || proofAttempt<=0 || vector==null)
                throw new ArgumentException("Invalid outgoing readiness proof.");
            var copy=ProtocolPolicy.CanonicalPeers(vector);
            ProtocolPolicy.EncodePeers(copy,true);
            if(!ProtocolPolicy.OwnFreshNonce(copy,ownSteam,ownNonce))throw new ArgumentException("The readiness vector omits the local world.");
            proof.Begin(ownSteam, worldToken, proofAttempt,
                copy.Select(peer => new KeyValuePair<ulong, string>(peer.SteamId, peer.Nonce)));
            localNonce = ownNonce;
        }

        internal bool TryAccept(ulong sender,DemolitionControlPacket ack) =>
            ack != null && ack.Protocol == ProtocolPolicy.Version && ack.Kind == (byte)ControlPacketKind.ProofAck &&
            ack.ProbeNonce == localNonce && ack.Batch == 0 &&
            proof.TryAccept(sender, ack.PeerNonce, ack.Token, ack.Operation, ack.Command, ack.Tick);

        internal NativeReadinessState Evaluate() => proof.Evaluate();
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
