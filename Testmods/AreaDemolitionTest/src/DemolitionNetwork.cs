using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Networking;
using APIShared.Simulation;
using BepInEx.Logging;
using SHCDESE.API;

namespace AreaDemolitionTest
{
    // Channel subscriptions and tick registration retain this runtime for the process.
    // Never acquire the engine monitor or send while holding sync: native receipt owns it already.
    internal sealed class DemolitionNetwork
    {
        private const string Owner = "AreaDemolitionTest_Serp";
        private readonly object sync = new object();
        private readonly ModApiClient api;
        private readonly NativeDemolition native;
        private readonly ManualLogSource log;
        private readonly IMissionPlayStateReadinessCapability readiness;
        private readonly AuthenticatedChoreChannel<DemolitionSimulationPacket> simulation;
        private readonly MessageChannel<DemolitionControlPacket> control;
        private readonly NativeProofLedger ledger = new NativeProofLedger();
        private readonly Dictionary<string, ProtocolPeer[]> vectors = new Dictionary<string, ProtocolPeer[]>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, string> echoes = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, string> seenProbes = new Dictionary<ulong, string>();
        private readonly ReadinessAckLedger proofAcks = new ReadinessAckLedger();
        private readonly Dictionary<ulong, DemolitionControlPacket> executionAcks = new Dictionary<ulong, DemolitionControlPacket>();
        private readonly Queue<Outbound> outbox = new Queue<Outbound>();
        private readonly Queue<string> diagnostics = new Queue<string>();
        private MissionContext mission;
        private ProtocolPeer[] peers = Array.Empty<ProtocolPeer>();
        private string rosterDigest = "", nonce = "", fingerprint = "", token = "", status = "";
        private ulong localSteam;
        private long generation, worldGeneration, operation, proofAttempt;
        private int actor, batchIndex;
        private DemolitionTarget[][] batches;
        private DemolitionSimulationPacket inflight;
        private GameplayDispatchTicket inflightDispatch;
        private bool proofSubmitted, ready, failed, cancelled;

        private sealed class Outbound
        {
            internal long Generation;
            internal ulong Destination;
            internal DemolitionControlPacket Control;
            internal DemolitionSimulationPacket Simulation;
            internal GameplayDispatchTicket Dispatch;
        }
        internal DemolitionNetwork(ModApiClient api, NativeDemolition native, ManualLogSource log)
        {
            this.api=api; this.native=native; this.log=log;
            if (!api.TryGetMissionPlayStateReadiness(out readiness, out var diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            if (!readiness.TryRegisterObserver("demolition-network-ready", _ => Notify(), out diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
            simulation=api.Network.RegisterAuthenticatedChore<DemolitionSimulationPacket>("demolition-simulation", ReceiveSimulation,
                new AuthenticatedChoreChannelOptions<DemolitionSimulationPacket> { MaximumPayloadBytes=1200, ValidatePayload=ValidateSimulation, Diagnostic=d => Report(d.Reason) });
            control=api.Network.RegisterMessage<DemolitionControlPacket>("demolition-control", ReceiveControl,
                new MessageChannelOptions<DemolitionControlPacket> { MaximumBodyBytes=4096, Validate=m => ValidateControl(m.Packet), Diagnostic=d => Report(d.Reason) });
            if (!SimulationTickEvents.TryRegister(Owner,"offline-demolition",OfflineTick,out string reason)) throw new InvalidOperationException(reason);
        }
        internal event Action Changed;
        internal bool IsBusy { get { lock(sync) return batches!=null; } }
        internal string Status { get { lock(sync) return status; } }

        internal void Start(MissionContext context)
        {
            lock(sync)
            {
                Reset(); mission=context;
                status="Waiting for the current world.";
            }
            Notify();
        }
        internal void End() { lock(sync) { Reset(); mission=null; status=""; } Notify(); }
        private void Reset()
        {
            inflightDispatch?.CancelQueued();
            generation++; worldGeneration++; nonce=""; token=""; rosterDigest=""; localSteam=0; peers=Array.Empty<ProtocolPeer>();
            ledger.Clear(); vectors.Clear(); echoes.Clear(); seenProbes.Clear(); proofAcks.Clear(); executionAcks.Clear(); outbox.Clear();
            proofSubmitted=ready=failed=cancelled=false; batches=null; inflight=null; inflightDispatch=null; batchIndex=0; actor=0;proofAttempt=0;
        }
        private bool IsCurrent() => mission!=null && readiness.Current?.SessionId==mission.SessionId;

        // Called only on Unity. No periodic world scan is required while this mod is idle.
        internal void Pump()
        {
            RefreshWorld();
            lock(sync)
            {
                if(IsCurrent() && mission.Mode.IsRealMultiplayer)
                {
                    EvaluateProofAcks();
                    EvaluateExecutionAcks();
                }
            }
            while(true)
            {
                Outbound item;
                lock(sync)
                {
                    if(outbox.Count==0)break;
                    item=outbox.Dequeue();
                    if(item.Generation!=generation || !IsCurrent())
                    {item.Dispatch?.CancelQueued();if(item.Dispatch!=null)ForgetQueuedBatch(item.Dispatch);continue;}
                    if(item.Dispatch!=null && !item.Dispatch.TryBeginSend(generation,operation,batchIndex,inflightDispatch,cancelled,failed))
                    {ForgetQueuedBatch(item.Dispatch);continue;}
                    // Mark Submitting under this lock before crossing the external transport.
                }
                NetworkSendResult result=item.Simulation!=null ? simulation.Send(item.Simulation) : control.SendToSteamPeer(item.Control,item.Destination);
                lock(sync)
                {
                    item.Dispatch?.CompleteSend(result.Submitted);
                    if(!result.Submitted && item.Generation==generation)
                    {
                        // Entered work may already have reached native code. Keep its receipts,
                        // but cancel every still-queued Apply without dropping control ACKs.
                        failed=true; status="Network submission stopped: "+result.Reason;
                        if(inflight==null)batches=null;
                        else CancelQueuedBatch();
                    }
                }
                if(!result.Submitted)Report(result.Reason);
            }
            string[] messages;
            lock(sync) { messages=diagnostics.ToArray(); diagnostics.Clear(); }
            foreach(string message in messages) log.LogWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Area demolition: {message}");
        }
        private void RefreshWorld()
        {
            MissionContext current;
            lock(sync) { current=mission; if(!IsCurrent())return; }
            if(!current.Mode.IsRealMultiplayer)
            {
                lock(sync) { if(nonce.Length==0) { nonce=ProtocolPolicy.NewNonce(); ready=true; status="Ready."; } }
                return;
            }
            var members=Platform_Multiplayer.Instance?.gameMembers;
            if(members==null)return;
            var humans=members.Where(m=>m!=null && !m.skirmishAI && !m.kicked && m.steamID!=0).ToArray();
            var self=humans.Where(m=>m.isSelf).ToArray();
            if(self.Length!=1 || humans.Length<1 || humans.Length>ProtocolPolicy.MaximumPeers || humans.Select(m=>m.steamID).Distinct().Count()!=humans.Length)
            {
                lock(sync)
                {
                    // Missing roster evidence can stop submissions, but cannot revoke a
                    // native proof for a Chore that may already be executing elsewhere.
                    // A roster invalidation abandons outgoing tracking rather than waiting
                    // forever for a departed peer. Old native world proofs remain valid.
                    inflightDispatch?.CancelQueued();
                    ready=proofSubmitted=false;token="";rosterDigest="";proofAcks.Clear();
                    batches=null;inflight=null;inflightDispatch=null;outbox.Clear();executionAcks.Clear();
                    status="Waiting for a complete simulator roster.";
                }
                return;
            }
            var roster=ProtocolPolicy.CanonicalPeers(humans.Select(m=>new ProtocolPeer {SteamId=m.steamID,PlayerId=m.playerID>=1 && m.playerID<=8 ? m.playerID : 0}));
            string digest=ProtocolPolicy.RosterDigest(roster);
            lock(sync)
            {
                if(!ReferenceEquals(mission,current) || !IsCurrent())return;
                if(nonce.Length!=0 && rosterDigest==digest)return;
                bool changed=nonce.Length!=0;
                // Local roster observations only stop future sends. Retire native proofs only
                // at world replacement; otherwise asynchronous leave timing could drop a
                // submitted batch on one simulator while the others still execute it.
                generation++; echoes.Clear(); seenProbes.Clear(); proofAcks.Clear(); executionAcks.Clear(); outbox.Clear();
                inflightDispatch?.CancelQueued();
                ready=proofSubmitted=failed=cancelled=false;token=""; batches=null; inflight=null;inflightDispatch=null;
                peers=roster; localSteam=self[0].steamID; rosterDigest=digest;
                fingerprint=DemolitionPolicy.Digest(native.Fingerprint); if(nonce.Length==0)nonce=ProtocolPolicy.NewNonce(); echoes[localSteam]=nonce;
                status=changed ? "Roster changed; unsent demolition cancelled. Waiting for peers." : "Waiting for all simulators.";
                foreach(var peer in peers) if(peer.SteamId!=localSteam) QueueControl(peer.SteamId,Probe());
                PrepareProof();
            }
        }
        private DemolitionControlPacket Probe() => new DemolitionControlPacket {Kind=(byte)ControlPacketKind.Probe,ProbeNonce=nonce,Fingerprint=fingerprint,Roster=ProtocolPolicy.EncodePeers(peers,false)};
        private void QueueControl(ulong destination, DemolitionControlPacket packet)
        { if(outbox.Count<256)outbox.Enqueue(new Outbound {Generation=generation,Destination=destination,Control=packet}); else { failed=true; status="Protocol queue limit reached."; } }
        private void QueueSimulation(DemolitionSimulationPacket packet,GameplayDispatchTicket dispatch=null) =>
            outbox.Enqueue(new Outbound {Generation=generation,Simulation=packet,Dispatch=dispatch});

        private void ReceiveControl(NetworkMessage<DemolitionControlPacket> message)
        {
            RefreshWorld();
            var packet=message.Packet;
            lock(sync)
            {
                if(!IsCurrent() || nonce.Length==0 || !mission.Mode.IsRealMultiplayer || !peers.Any(p=>p.SteamId==message.SenderSteamId))return;
                switch((ControlPacketKind)packet.Kind)
                {
                    case ControlPacketKind.Probe:
                        if(packet.Fingerprint!=fingerprint || DemolitionPolicy.Digest(ProtocolPolicy.Hex(packet.Roster))!=rosterDigest)return;
                        QueueControl(message.SenderSteamId,new DemolitionControlPacket {Kind=(byte)ControlPacketKind.Echo,ProbeNonce=packet.ProbeNonce,PeerNonce=nonce,Fingerprint=fingerprint,Roster=ProtocolPolicy.EncodePeers(peers,false)});
                        // A newly ready peer may have missed our first Probe while loading.
                        if(!seenProbes.TryGetValue(message.SenderSteamId,out string last) || last!=packet.ProbeNonce)
                        { seenProbes[message.SenderSteamId]=packet.ProbeNonce; QueueControl(message.SenderSteamId,Probe()); }
                        break;
                    case ControlPacketKind.Echo:
                        if(packet.ProbeNonce!=nonce || packet.Fingerprint!=fingerprint || DemolitionPolicy.Digest(ProtocolPolicy.Hex(packet.Roster))!=rosterDigest)return;
                        if(echoes.TryGetValue(message.SenderSteamId,out string previous) && previous!=packet.PeerNonce)
                            InvalidateRendezvous("A peer loaded another world; unsent demolition cancelled.");
                        echoes[message.SenderSteamId]=packet.PeerNonce;
                        PrepareProof();
                        break;
                    case ControlPacketKind.ProofAck:
                        if(!proofSubmitted || !proofAcks.TryAccept(message.SenderSteamId,packet))return;
                        EvaluateProofAcks();
                        break;
                    case ControlPacketKind.ExecutionAck:
                        AcceptExecutionAck(message.SenderSteamId,packet);
                        break;
                }
            }
            Notify();
        }
        private void InvalidateRendezvous(string reason)
        {
            // Keep our current-world nonce and native receipt ledger. Replacing our nonce
            // in response to a remote nonce would cause endless mutual epoch changes.
            ready=proofSubmitted=failed=cancelled=false; proofAcks.Clear(); executionAcks.Clear(); outbox.Clear();
            inflightDispatch?.CancelQueued();
            token=""; batches=null; inflight=null;inflightDispatch=null; status=reason;
        }
        private void PrepareProof()
        {
            if(proofSubmitted || failed || peers.Length==0 || peers.Any(p=>!echoes.ContainsKey(p.SteamId)))return;
            var own=peers.First(p=>p.SteamId==localSteam);
            // Observers acknowledge over the control channel; they never impersonate a player.
            if(own.PlayerId<1 || PlayerPerspectiveAPI.GetControlledPlayerId()!=own.PlayerId)return;
            var vector=peers.Select(p=>new ProtocolPeer {SteamId=p.SteamId,PlayerId=p.PlayerId,Nonce=echoes[p.SteamId]}).ToArray();
            token=ProtocolPolicy.WorldToken(fingerprint,vector);
            proofAttempt=checked(proofAttempt+1);
            proofAcks.Begin(localSteam,nonce,token,proofAttempt,vector);
            vectors[token]=vector; proofSubmitted=true;
            QueueSimulation(new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ReadyProof,Token=token,Nonce=nonce,Operation=proofAttempt,Payload=ProtocolPolicy.EncodePeers(vector,true),Digest=fingerprint});
        }
        private void EvaluateProofAcks()
        {
            if(ready || !proofSubmitted || failed)return;
            var outcome=proofAcks.Evaluate();
            if(outcome==ReadinessAckState.Pending)return;
            if(outcome==ReadinessAckState.Mismatch)
            { failed=true; status="Peers disagree about the native readiness proof."; return; }
            ready=true; status=batches==null ? "Ready." : "Waiting for synchronized demolition.";
            TryQueueBatch();
        }
        internal void Submit(int actor, DemolitionTarget[] targets)
        {
            if(targets==null || targets.Length==0)return;
            RefreshWorld();
            // Linked demolition closures must stay together. Never split a native-approved batch.
            var prepared=native.MakeBatches(DemolitionPolicy.Canonicalize(targets));
            lock(sync)
            {
                if(!IsCurrent() || batches!=null || failed) {status="Another demolition is pending or the world is not ready.";return;}
                if(actor<1 || actor>8 || targets.Any(t=>t.Owner!=actor)) {status="The selected objects do not belong to the issuer.";return;}
                if(prepared.Length==0){status="Unsent demolition cancelled.";Notify();return;}
                if(mission.Mode.IsRealMultiplayer && !peers.Any(p=>p.SteamId==localSteam && p.PlayerId==actor)) {status="A real local player is required.";return;}
                this.actor=actor; batches=prepared; batchIndex=0; operation=checked(operation+1); cancelled=false;
                status="Waiting for synchronized demolition.";
                TryQueueBatch();
            }
            Notify();
        }
        private void TryQueueBatch()
        {
            if(!ready || batches==null || inflight!=null || cancelled || failed || !mission.Mode.IsRealMultiplayer)return;
            try
            {
                var targets=batches[batchIndex];
                var packet=new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ApplyBatch,Token=token,Nonce=nonce,Operation=operation,Batch=batchIndex,Payload=ProtocolPolicy.PackTargets(targets),Digest=ProtocolPolicy.TargetHash(targets)};
                if(GameNetworkAPI.Serialize(packet).Length+2>1200) throw new InvalidOperationException("A linked demolition batch exceeds the native transport limit.");
                executionAcks.Clear(); inflight=packet;
                inflightDispatch=new GameplayDispatchTicket(generation,operation,batchIndex);
                QueueSimulation(packet,inflightDispatch);
            }
            catch(Exception error) {failed=true;batches=null;status="Demolition batch preparation stopped: "+error.Message;}
        }

        private void ReceiveSimulation(AuthenticatedChoreMessage<DemolitionSimulationPacket> message)
        {
            var packet=message.Packet;
            ProtocolPeer origin;
            long currentGeneration;
            DemolitionTarget[] targets=null;
            NativeProofLedger.Receipt duplicate=null;
            lock(sync)
            {
                if(!IsCurrent() || nonce.Length==0 || !mission.Mode.IsRealMultiplayer)return;
                currentGeneration=worldGeneration;
                if((SimulationPacketKind)packet.Kind==SimulationPacketKind.ReadyProof)
                {
                    var vector=ProtocolPolicy.DecodePeers(packet.Payload,true);
                    if(packet.Digest!=fingerprint || ProtocolPolicy.RosterDigest(vector)!=rosterDigest || !ProtocolPolicy.OwnFreshNonce(vector,localSteam,nonce) || ProtocolPolicy.WorldToken(fingerprint,vector)!=packet.Token)return;
                    origin=vector.SingleOrDefault(p=>p.PlayerId==message.SenderPlayerId && p.Nonce==packet.Nonce);
                    if(origin==null)return;
                    if(vectors.Count>=ProtocolPolicy.MaximumPeers && !vectors.ContainsKey(packet.Token))return;
                    vectors[packet.Token]=vector; ledger.Prove(packet.Token,nonce);
                    Reply(origin.SteamId,new DemolitionControlPacket {Kind=(byte)ControlPacketKind.ProofAck,ProbeNonce=origin.Nonce,PeerNonce=nonce,Token=packet.Token,Operation=packet.Operation,Command=message.CommandId,Tick=message.ScheduledTick});
                    Notify(); return;
                }
                if(!ledger.HasProof(packet.Token,nonce) || !vectors.TryGetValue(packet.Token,out var proved))return;
                origin=proved.SingleOrDefault(p=>p.PlayerId==message.SenderPlayerId && p.Nonce==packet.Nonce);
                if(origin==null)return;
                targets=ProtocolPolicy.UnpackTargets(packet.Payload);
                if(targets.Any(t=>t.Owner!=message.SenderPlayerId) || ProtocolPolicy.TargetHash(targets)!=packet.Digest)return;
                string key=packet.Token+":"+origin.SteamId+":"+origin.Nonce;
                if(!ledger.CanApply(key,packet.Operation,packet.Batch,packet.Digest,out duplicate))
                {
                    if(duplicate!=null && ProtocolPolicy.IsDigest(duplicate.Result))Reply(origin.SteamId,ExecutionAck(origin,packet,duplicate.Result,duplicate.Failed));
                    Notify(); return;
                }
                // Reserve before native entry: a partial native exception must never make
                // an exact replay apply the batch a second time.
                ledger.Complete(key,packet.Operation,packet.Batch,packet.Digest,"");
            }
            // Sole MP mutation entry. It is already inside the authenticated native simulation scope.
            var results=native.Apply(message.SenderPlayerId,targets);
            string resultDigest=DemolitionPolicy.ResultDigest(results);
            bool nativeFailed=ProtocolPolicy.HasNativeFailure(results);
            lock(sync)
            {
                if(worldGeneration!=currentGeneration)return;
                ledger.Complete(packet.Token+":"+origin.SteamId+":"+origin.Nonce,packet.Operation,packet.Batch,packet.Digest,resultDigest,nativeFailed);
                Reply(origin.SteamId,ExecutionAck(origin,packet,resultDigest,nativeFailed));
            }
            Notify();
        }
        private DemolitionControlPacket ExecutionAck(ProtocolPeer origin,DemolitionSimulationPacket packet,string result,bool failed) =>
            new DemolitionControlPacket {Kind=(byte)ControlPacketKind.ExecutionAck,ProbeNonce=origin.Nonce,PeerNonce=nonce,Token=packet.Token,Origin=origin.SteamId,Operation=packet.Operation,Batch=packet.Batch,InputDigest=packet.Digest,ResultDigest=result,Failed=failed};
        private void Reply(ulong destination,DemolitionControlPacket packet)
        {
            // Only copied protocol data crosses threads; Pump performs every send on Unity.
            if(destination==localSteam)
            {
                if((ControlPacketKind)packet.Kind==ControlPacketKind.ProofAck) {if(proofSubmitted)proofAcks.TryAccept(localSteam,packet);}
                else AcceptExecutionAck(localSteam,packet);
                // Do not queue subsequent Chores from simulation; evaluation happens in Pump.
            }
            else QueueControl(destination,packet);
        }
        private void AcceptExecutionAck(ulong sender,DemolitionControlPacket packet)
        {
            if(!ProtocolPolicy.IsDigest(packet?.Token) || !vectors.TryGetValue(packet.Token,out var vector) ||
                !ExecutionAckPolicy.Matches(sender,localSteam,nonce,inflight,vector,packet))return;
            executionAcks[sender]=packet;
        }
        private void EvaluateExecutionAcks()
        {
            if(inflight==null)return;
            if(ProtocolPolicy.HasFailedAcknowledgement(executionAcks.Values))
            { failed=true;status="Demolition stopped after a native failure; earlier changes remain applied.";batches=null;inflight=null;inflightDispatch=null;return; }
            if(peers.Any(p=>!executionAcks.ContainsKey(p.SteamId)))return;
            string result=executionAcks[localSteam].ResultDigest;
            if(executionAcks.Values.Any(p=>p.ResultDigest!=result))
            { failed=true; status="Demolition stopped: peers reported different results."; batches=null; inflight=null;inflightDispatch=null;return; }
            inflight=null;inflightDispatch=null;
            if(cancelled || failed || ++batchIndex>=batches.Length)
            { batches=null;if(!failed)status=cancelled ? "Unsent demolition cancelled." : "Demolition completed.";return; }
            TryQueueBatch();
        }
        internal void CancelPending()
        {
            lock(sync)
            {
                cancelled=true;
                if(inflight==null || CancelQueuedBatch()) {batches=null;status="Unsent demolition cancelled.";}
                else status="Submitted batch remains pending; unsent remainder cancelled.";
            }
            Notify();
        }
        private bool CancelQueuedBatch()
        {
            var ticket=inflightDispatch;
            if(ticket==null || !ticket.CancelQueued())return false;
            ForgetQueuedBatch(ticket);return true;
        }
        private void ForgetQueuedBatch(GameplayDispatchTicket ticket)
        {
            if(!ReferenceEquals(ticket,inflightDispatch) || ticket.Phase!=GameplayDispatchPhase.Cancelled)return;
            var keep=outbox.Where(item=>!ReferenceEquals(item.Dispatch,ticket)).ToArray();
            outbox.Clear();foreach(var item in keep)outbox.Enqueue(item);
            batches=null;inflight=null;inflightDispatch=null;executionAcks.Clear();
        }
        private void OfflineTick(int ignored)
        {
            DemolitionTarget[] targets; DemolitionTarget[][] pending; int owner; long version;
            lock(sync)
            {
                if(!IsCurrent() || mission.Mode.IsRealMultiplayer || batches==null || cancelled || failed)return;
                pending=batches; targets=pending[batchIndex]; owner=actor; version=generation;
            }
            ApplyResult[] results;
            try { results=native.Apply(owner,targets); }
            catch(Exception error)
            {
                lock(sync) if(generation==version) {failed=true;batches=null;status="Offline demolition stopped after a native failure.";}
                Report(error.Message);return;
            }
            lock(sync)
            {
                if(generation!=version || !ReferenceEquals(batches,pending))return;
                if(ProtocolPolicy.HasNativeFailure(results))
                { failed=true;batches=null;status="Offline demolition stopped after a native failure; earlier changes remain applied.";Notify();return; }
                if(++batchIndex>=batches.Length) {batches=null;status="Demolition completed.";}
            }
            Notify();
        }
        private static bool ValidateSimulation(DemolitionSimulationPacket packet) => ProtocolPolicy.ValidateSimulationPacket(packet);
        private static bool ValidateControl(DemolitionControlPacket packet) => ProtocolPolicy.ValidateControlPacket(packet);
        private void Report(string text) {lock(sync) {if(diagnostics.Count<32)diagnostics.Enqueue(text);} Notify();}
        private void Notify() => Changed?.Invoke();
    }
}
