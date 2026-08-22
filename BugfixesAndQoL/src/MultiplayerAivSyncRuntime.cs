using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed class MultiplayerAivSyncRuntime : INotifyPropertyChanged, IDisposable
    {
        private delegate void HostStartGameDelegate(Platform_Multiplayer self);
        private delegate void StartGameDelegate(
            Platform_Multiplayer self,
            EngineInterface.MultiplayerSetupData setup,
            FileHeader map,
            int coopTrailId,
            int coopMissionId);
        private delegate EngineInterface.LoadMapReturnData LoadMultiplayerMapDelegate(
            string mapName,
            bool multiplayerSave);
        private delegate void FrontendUpdateDelegate(FRONT_Multiplayer self);

        private const int TimeoutSeconds = 60;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<ulong, byte[]> incomingChunks = new Dictionary<ulong, byte[]>();
        private readonly HashSet<ulong> expectedAcks = new HashSet<ulong>();
        private readonly HashSet<ulong> receivedAcks = new HashSet<ulong>();
        private R3PacketEventHook<MultiplayerAivSyncPacket> packetHook;
        private IDisposable packetSubscription;
        private Hook hostStartHook;
        private Hook startGameHook;
        private Hook loadMapHook;
        private Hook updateHook;
        private HostStartGameDelegate hostStartTrampoline;
        private StartGameDelegate startGameTrampoline;
        private LoadMultiplayerMapDelegate loadMapTrampoline;
        private FrontendUpdateDelegate updateTrampoline;
        private MultiplayerAivManifest confirmedManifest;
        private MultiplayerAivManifest activeStartManifest;
        private MultiplayerAivManifest pendingClientManifest;
        private MultiplayerAivSyncPacket incomingBegin;
        private MultiplayerAivSyncPacket pendingClientBegin;
        private CSteamID pendingClientOwner;
        private int generation;
        private long transferStartedTimestamp;
        private long clientTransferStartedTimestamp;
        private string sourceFingerprint = string.Empty;
        private string rosterFingerprint = string.Empty;
        private string activeManifestHash = string.Empty;
        private ulong extendedTransferLobbyId;
        private string syncStatusText = string.Empty;
        private bool transferInProgress;
        private bool bypassHostStartHook;
        private bool initialized;

        public MultiplayerAivSyncRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string SyncStatusText
        {
            get => syncStatusText;
            private set
            {
                if (string.Equals(syncStatusText, value, StringComparison.Ordinal))
                    return;
                syncStatusText = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SyncStatusText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SyncStatusVisibility)));
            }
        }

        public Visibility SyncStatusVisibility =>
            string.IsNullOrEmpty(SyncStatusText) ? Visibility.Collapsed : Visibility.Visible;

        public void Initialize()
        {
            if (initialized)
                return;

            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<MultiplayerAivSyncPacket>();
            packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);

            Hook installedHostStart = null;
            Hook installedStartGame = null;
            Hook installedLoadMap = null;
            Hook installedUpdate = null;
            try
            {
                MethodInfo hostStart = FindMethod(
                    typeof(Platform_Multiplayer),
                    "HostStartGame",
                    Type.EmptyTypes);
                MethodInfo startGame = FindMethod(
                    typeof(Platform_Multiplayer),
                    "StartGame",
                    typeof(EngineInterface.MultiplayerSetupData),
                    typeof(FileHeader),
                    typeof(int),
                    typeof(int));
                MethodInfo loadMap = FindMethod(
                    typeof(EngineInterface),
                    "loadMultiplayerMap",
                    typeof(string),
                    typeof(bool));
                MethodInfo update = FindMethod(typeof(FRONT_Multiplayer), "Update", Type.EmptyTypes);

                installedHostStart = new Hook(hostStart, (HostStartGameDelegate)HostStartGameHook);
                hostStartTrampoline = installedHostStart.GenerateTrampoline<HostStartGameDelegate>();
                installedStartGame = new Hook(startGame, (StartGameDelegate)StartGameHook);
                startGameTrampoline = installedStartGame.GenerateTrampoline<StartGameDelegate>();
                installedLoadMap = new Hook(loadMap, (LoadMultiplayerMapDelegate)LoadMultiplayerMapHook);
                loadMapTrampoline = installedLoadMap.GenerateTrampoline<LoadMultiplayerMapDelegate>();
                installedUpdate = new Hook(update, (FrontendUpdateDelegate)FrontendUpdateHook);
                updateTrampoline = installedUpdate.GenerateTrampoline<FrontendUpdateDelegate>();
                hostStartHook = installedHostStart;
                startGameHook = installedStartGame;
                loadMapHook = installedLoadMap;
                updateHook = installedUpdate;
                initialized = true;
            }
            catch
            {
                installedUpdate?.Dispose();
                installedLoadMap?.Dispose();
                installedStartGame?.Dispose();
                installedHostStart?.Dispose();
                packetSubscription?.Dispose();
                packetSubscription = null;
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL multiplayer AIV sync initialized: packetId={packetHook.GetPacketId()}, " +
                $"protocol={MultiplayerAivSyncProtocol.ProtocolVersion}.");
        }

        private void HostStartGameHook(Platform_Multiplayer self)
        {
            if (bypassHostStartHook || !IsFeatureActive() || !IsEligibleLobby(self))
            {
                hostStartTrampoline(self);
                return;
            }
            if (transferInProgress)
                return;

            try
            {
                FRONT_Multiplayer frontend = MainViewModel.viewModelLoaded
                    ? MainViewModel.Instance?.FRONTMultiplayer
                    : null;
                MultiplayerAivManifest manifest = BuildHostManifest(self, frontend);
                if (!MultiplayerAivSyncPolicy.RequiresTransfer(
                        manifest.Slots.Count > 0,
                        manifest.LobbyId,
                        extendedTransferLobbyId))
                {
                    // A fresh Vanilla-sized selection needs no custom traffic or import context.
                    confirmedManifest = null;
                    extendedTransferLobbyId = 0UL;
                    SyncStatusText = string.Empty;
                    hostStartTrampoline(self);
                    return;
                }

                if (manifest.Slots.Count > 0)
                    extendedTransferLobbyId = manifest.LobbyId;
                BeginHostTransfer(self, frontend, manifest);
            }
            catch (Exception ex)
            {
                FailTransfer("BugfixesAndQoL.AivSyncFailed", ex.GetBaseException().Message);
            }
        }

        private void BeginHostTransfer(
            Platform_Multiplayer platform,
            FRONT_Multiplayer frontend,
            MultiplayerAivManifest manifest)
        {
            byte[] encoded = MultiplayerAivSyncProtocol.Encode(manifest);
            byte[] compressed = MultiplayerAivSyncProtocol.Compress(encoded);
            List<byte[]> chunks = MultiplayerAivSyncProtocol.Split(compressed);
            string hash = MultiplayerAivSyncProtocol.ToHex(MultiplayerAivSyncProtocol.HashBytes(encoded));
            activeManifestHash = hash;

            generation = generation == int.MaxValue ? 1 : generation + 1;
            confirmedManifest = MultiplayerAivSyncProtocol.Decode(encoded);
            expectedAcks.Clear();
            receivedAcks.Clear();
            foreach (CSteamID member in GetHumanPeers(platform.activeLobby))
                expectedAcks.Add(member.m_SteamID);
            transferStartedTimestamp = Stopwatch.GetTimestamp();
            rosterFingerprint = BuildRosterFingerprint(platform.activeLobby);
            sourceFingerprint = BuildSelectionFingerprint(frontend, platform.activeLobby);
            transferInProgress = expectedAcks.Count > 0;

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL AIV sync generation prepared: generation={generation}, lobby={manifest.LobbyId}, " +
                $"slots={manifest.Slots.Count}, rawBytes={encoded.Length}, compressedBytes={compressed.Length}, " +
                $"chunks={chunks.Count}, peers={expectedAcks.Count}, hash={hash}, candidates={DescribeManifest(manifest)}.");

            if (!transferInProgress)
            {
                ContinueVanillaStart(platform);
                return;
            }

            SyncStatusText = SerpLocalization.Get(
                "BugfixesAndQoL.AivSyncProgress",
                "Received", 0,
                "Total", expectedAcks.Count);
            var begin = new MultiplayerAivSyncPacket
            {
                ProtocolVersion = MultiplayerAivSyncProtocol.ProtocolVersion,
                Kind = (int)MultiplayerAivSyncPacketKind.Begin,
                LobbyId = manifest.LobbyId,
                Generation = generation,
                VanillaChecksum = manifest.VanillaChecksum,
                ManifestHash = hash,
                UncompressedLength = encoded.Length,
                CompressedLength = compressed.Length,
                ChunkCount = chunks.Count
            };

            foreach (ulong peer in expectedAcks.ToArray())
            {
                SendReliable(new CSteamID(peer), begin);
                for (int index = 0; index < chunks.Count; index++)
                {
                    SendReliable(new CSteamID(peer), new MultiplayerAivSyncPacket
                    {
                        ProtocolVersion = begin.ProtocolVersion,
                        Kind = (int)MultiplayerAivSyncPacketKind.Chunk,
                        LobbyId = begin.LobbyId,
                        Generation = begin.Generation,
                        ManifestHash = hash,
                        ChunkIndex = index,
                        ChunkCount = chunks.Count,
                        DataBase64 = Convert.ToBase64String(chunks[index])
                    });
                }
            }
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<MultiplayerAivSyncPacket> args)
        {
            MultiplayerAivSyncPacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != MultiplayerAivSyncProtocol.ProtocolVersion ||
                !args.SenderSteamId.HasValue)
                return;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            if (lobby == null || packet.LobbyId != lobby.id.m_SteamID)
                return;

            ulong sender = args.SenderSteamId.Value.m_SteamID;
            MultiplayerAivSyncPacketKind kind = (MultiplayerAivSyncPacketKind)packet.Kind;
            if (kind == MultiplayerAivSyncPacketKind.Ack || kind == MultiplayerAivSyncPacketKind.Reject)
            {
                try
                {
                    HandleHostResponse(platform, packet, sender, kind);
                }
                catch (Exception ex)
                {
                    FailTransfer("BugfixesAndQoL.AivSyncFailed", ex.GetBaseException().Message);
                }
                return;
            }

            CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby.id);
            if (!MultiplayerAivSyncPolicy.CanAcceptHostPacket(
                    lobby.isHost,
                    sender,
                    owner.m_SteamID))
                return;

            try
            {
                if (kind == MultiplayerAivSyncPacketKind.Begin)
                    AcceptBegin(packet);
                else if (kind == MultiplayerAivSyncPacketKind.Chunk)
                    AcceptChunk(platform, packet, owner);
            }
            catch (Exception ex)
            {
                Reject(owner, packet, ex.GetBaseException().Message);
                incomingBegin = null;
                incomingChunks.Clear();
                ClearPendingClientManifest(clearConfirmed: true);
                SyncStatusText = SerpLocalization.Get(
                    "BugfixesAndQoL.AivSyncFailed",
                    "Reason", ex.GetBaseException().Message);
            }
        }

        private void AcceptBegin(MultiplayerAivSyncPacket packet)
        {
            if (packet.Generation <= 0 || packet.UncompressedLength <= 0 ||
                packet.UncompressedLength > MultiplayerAivSyncProtocol.MaximumUncompressedBytes ||
                packet.CompressedLength <= 0 ||
                packet.CompressedLength > MultiplayerAivSyncProtocol.MaximumCompressedBytes ||
                packet.ChunkCount <= 0 ||
                packet.ChunkCount > (MultiplayerAivSyncProtocol.MaximumCompressedBytes +
                    MultiplayerAivSyncProtocol.MaximumChunkBytes - 1) /
                    MultiplayerAivSyncProtocol.MaximumChunkBytes ||
                packet.ChunkCount != (packet.CompressedLength +
                    MultiplayerAivSyncProtocol.MaximumChunkBytes - 1) /
                    MultiplayerAivSyncProtocol.MaximumChunkBytes ||
                string.IsNullOrEmpty(packet.ManifestHash) || packet.ManifestHash.Length != 64 ||
                MultiplayerAivSyncProtocol.FromHex(packet.ManifestHash).Length != 32 ||
                string.IsNullOrEmpty(packet.VanillaChecksum) || packet.VanillaChecksum.Length > 256)
            {
                throw new InvalidOperationException("Invalid AIV transfer header.");
            }
            incomingBegin = packet;
            incomingChunks.Clear();
            confirmedManifest = null;
            ClearPendingClientManifest(clearConfirmed: false);
            clientTransferStartedTimestamp = Stopwatch.GetTimestamp();
            SyncStatusText = SerpLocalization.Get("BugfixesAndQoL.AivSyncReceiving");
        }

        private void AcceptChunk(
            Platform_Multiplayer platform,
            MultiplayerAivSyncPacket packet,
            CSteamID owner)
        {
            if (incomingBegin == null || packet.Generation != incomingBegin.Generation ||
                !string.Equals(packet.ManifestHash, incomingBegin.ManifestHash, StringComparison.Ordinal) ||
                packet.ChunkCount != incomingBegin.ChunkCount ||
                packet.ChunkIndex < 0 || packet.ChunkIndex >= packet.ChunkCount)
                throw new InvalidOperationException("AIV chunk does not match the active transfer.");

            int maximumBase64Length = ((MultiplayerAivSyncProtocol.MaximumChunkBytes + 2) / 3) * 4;
            if (string.IsNullOrEmpty(packet.DataBase64) || packet.DataBase64.Length > maximumBase64Length)
                throw new InvalidOperationException("AIV chunk encoding exceeds the size limit.");
            byte[] data = Convert.FromBase64String(packet.DataBase64);
            if (data.Length > MultiplayerAivSyncProtocol.MaximumChunkBytes)
                throw new InvalidOperationException("AIV chunk exceeds the size limit.");
            ulong key = ((ulong)(uint)packet.Generation << 32) | (uint)packet.ChunkIndex;
            incomingChunks[key] = data;
            if (incomingChunks.Count != incomingBegin.ChunkCount)
                return;

            var compressed = new byte[incomingBegin.CompressedLength];
            int offset = 0;
            for (int index = 0; index < incomingBegin.ChunkCount; index++)
            {
                ulong chunkKey = ((ulong)(uint)incomingBegin.Generation << 32) | (uint)index;
                if (!incomingChunks.TryGetValue(chunkKey, out byte[] chunk) || offset + chunk.Length > compressed.Length)
                    throw new InvalidOperationException("AIV transfer is incomplete or oversized.");
                Buffer.BlockCopy(chunk, 0, compressed, offset, chunk.Length);
                offset += chunk.Length;
            }
            if (offset != compressed.Length)
                throw new InvalidOperationException("Compressed AIV transfer length mismatch.");

            byte[] encoded = MultiplayerAivSyncProtocol.Decompress(compressed, incomingBegin.UncompressedLength);
            string actualHash = MultiplayerAivSyncProtocol.ToHex(MultiplayerAivSyncProtocol.HashBytes(encoded));
            if (!string.Equals(actualHash, incomingBegin.ManifestHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("AIV manifest hash mismatch.");
            MultiplayerAivManifest manifest = MultiplayerAivSyncProtocol.Decode(encoded);
            if (!string.Equals(
                    manifest.VanillaChecksum,
                    incomingBegin.VanillaChecksum,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("AIV manifest and transfer checksum differ.");
            pendingClientManifest = manifest;
            pendingClientBegin = incomingBegin;
            pendingClientOwner = owner;
            incomingBegin = null;
            incomingChunks.Clear();
            TryConfirmPendingClientManifest(platform);
        }

        private void TryConfirmPendingClientManifest(Platform_Multiplayer platform)
        {
            if (pendingClientManifest == null || pendingClientBegin == null)
                return;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            CSteamID currentOwner = lobby == null ? new CSteamID(0UL) : SteamMatchmaking.GetLobbyOwner(lobby.id);
            if (lobby == null || lobby.isHost ||
                pendingClientManifest.LobbyId != lobby.id.m_SteamID || currentOwner != pendingClientOwner)
            {
                ClearPendingClientManifest(clearConfirmed: true);
                return;
            }

            if (!IsFeatureActive())
            {
                if (Stopwatch.GetTimestamp() - clientTransferStartedTimestamp >
                    TimeoutSeconds * Stopwatch.Frequency)
                    throw new TimeoutException("Host AIV synchronization settings did not converge before timeout.");
                return;
            }

            if (!MultiplayerAivSyncPolicy.IsVanillaChecksumReady(
                    pendingClientManifest.VanillaChecksum,
                    lobby.AIVDataChecksum()))
            {
                if (Stopwatch.GetTimestamp() - clientTransferStartedTimestamp >
                    TimeoutSeconds * Stopwatch.Frequency)
                    throw new TimeoutException("Vanilla AIV lobby data did not converge before timeout.");
                return;
            }

            ValidateManifestAgainstLobby(platform, pendingClientManifest);
            confirmedManifest = pendingClientManifest;
            SendReliable(
                pendingClientOwner,
                CreateResponse(MultiplayerAivSyncPacketKind.Ack, pendingClientBegin, string.Empty));
            SyncStatusText = SerpLocalization.Get("BugfixesAndQoL.AivSyncReady");
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL AIV sync accepted: generation={pendingClientBegin.Generation}, " +
                $"lobby={pendingClientBegin.LobbyId}, slots={confirmedManifest.Slots.Count}, " +
                $"hash={pendingClientBegin.ManifestHash}, candidates={DescribeManifest(confirmedManifest)}.");
            ClearPendingClientManifest(clearConfirmed: false);
        }

        private void HandleHostResponse(
            Platform_Multiplayer platform,
            MultiplayerAivSyncPacket packet,
            ulong sender,
            MultiplayerAivSyncPacketKind kind)
        {
            if (!MultiplayerAivSyncPolicy.IsCurrentResponse(
                    platform.activeLobby.isHost,
                    transferInProgress,
                    expectedAcks.Contains(sender),
                    packet.Generation,
                    generation,
                    packet.ManifestHash,
                    activeManifestHash,
                    rosterFingerprint,
                    BuildRosterFingerprint(platform.activeLobby)))
                return;
            if (kind == MultiplayerAivSyncPacketKind.Reject)
            {
                string reason = string.IsNullOrEmpty(packet.Message) || packet.Message.Length > 512
                    ? "Client rejected AIV data."
                    : packet.Message;
                FailTransfer("BugfixesAndQoL.AivSyncFailed", reason);
                return;
            }
            receivedAcks.Add(sender);
            SyncStatusText = SerpLocalization.Get(
                "BugfixesAndQoL.AivSyncProgress",
                "Received", receivedAcks.Count,
                "Total", expectedAcks.Count);
            if (receivedAcks.SetEquals(expectedAcks))
                ContinueVanillaStart(platform);
        }

        private void ContinueVanillaStart(Platform_Multiplayer platform)
        {
            ValidateManifestAgainstLobby(platform, confirmedManifest);
            FRONT_Multiplayer frontend = MainViewModel.viewModelLoaded
                ? MainViewModel.Instance?.FRONTMultiplayer
                : null;
            if (!string.Equals(rosterFingerprint, BuildRosterFingerprint(platform.activeLobby), StringComparison.Ordinal) ||
                !string.Equals(sourceFingerprint, BuildSelectionFingerprint(frontend, platform.activeLobby), StringComparison.Ordinal))
                throw new InvalidOperationException("AIV selection or lobby roster changed during synchronization.");
            MultiplayerAivManifest currentManifest = BuildHostManifest(platform, frontend);
            string currentManifestHash = MultiplayerAivSyncProtocol.ToHex(
                MultiplayerAivSyncProtocol.HashBytes(MultiplayerAivSyncProtocol.Encode(currentManifest)));
            if (!string.Equals(activeManifestHash, currentManifestHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("AIV binary data changed during synchronization.");

            transferInProgress = false;
            SyncStatusText = SerpLocalization.Get("BugfixesAndQoL.AivSyncReady");
            bypassHostStartHook = true;
            try
            {
                hostStartTrampoline(platform);
            }
            finally
            {
                bypassHostStartHook = false;
            }
        }

        private void StartGameHook(
            Platform_Multiplayer self,
            EngineInterface.MultiplayerSetupData setup,
            FileHeader map,
            int coopTrailId,
            int coopMissionId)
        {
            activeStartManifest = ResolveManifestForStart(coopTrailId);
            try
            {
                startGameTrampoline(self, setup, map, coopTrailId, coopMissionId);
            }
            finally
            {
                activeStartManifest = null;
                confirmedManifest = null;
                incomingBegin = null;
                incomingChunks.Clear();
                ClearPendingClientManifest(clearConfirmed: false);
                extendedTransferLobbyId = 0UL;
                SyncStatusText = string.Empty;
            }
        }

        private MultiplayerAivManifest ResolveManifestForStart(int coopTrailId)
        {
            Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
            if (!MultiplayerAivSyncPolicy.CanUseConfirmedManifest(
                    IsFeatureActive(),
                    coopTrailId,
                    confirmedManifest != null,
                    lobby?.id.m_SteamID ?? 0UL,
                    confirmedManifest?.LobbyId ?? 0UL))
            {
                // Never let a confirmation from an older lobby affect a later Vanilla start.
                confirmedManifest = null;
                return null;
            }

            ValidateManifestAgainstLobby(Platform_Multiplayer.Instance, confirmedManifest);
            return confirmedManifest;
        }

        private EngineInterface.LoadMapReturnData LoadMultiplayerMapHook(string mapName, bool multiplayerSave)
        {
            if (!multiplayerSave && activeStartManifest != null)
            {
                ValidateManifestAgainstLobby(Platform_Multiplayer.Instance, activeStartManifest);
                foreach (MultiplayerAivSlot slot in activeStartManifest.Slots)
                {
                    for (int candidateId = 1; candidateId < slot.Candidates.Count; candidateId++)
                    {
                        EngineInterface.ImportAIV(
                            slot.PlayerId - 1,
                            candidateId,
                            slot.Candidates[candidateId].Data,
                            1);
                    }
                }
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL imported synchronized extra AIV candidates immediately before map load: " +
                    $"lobby={activeStartManifest.LobbyId}, slots={activeStartManifest.Slots.Count}, " +
                    $"candidates={DescribeManifest(activeStartManifest)}.");
            }
            return loadMapTrampoline(mapName, multiplayerSave);
        }

        private void FrontendUpdateHook(FRONT_Multiplayer self)
        {
            updateTrampoline(self);
            if (pendingClientManifest != null)
            {
                try
                {
                    TryConfirmPendingClientManifest(Platform_Multiplayer.Instance);
                }
                catch (Exception ex)
                {
                    Reject(pendingClientOwner, pendingClientBegin, ex.GetBaseException().Message);
                    ClearPendingClientManifest(clearConfirmed: true);
                    SyncStatusText = SerpLocalization.Get(
                        "BugfixesAndQoL.AivSyncFailed",
                        "Reason", ex.GetBaseException().Message);
                }
            }
            if (!transferInProgress)
                return;
            try
            {
                Platform_Multiplayer platform = Platform_Multiplayer.Instance;
                bool timedOut = Stopwatch.GetTimestamp() - transferStartedTimestamp >
                    TimeoutSeconds * Stopwatch.Frequency;
                if (platform?.activeLobby == null ||
                    MultiplayerAivSyncPolicy.HasContextChanged(
                        rosterFingerprint,
                        BuildRosterFingerprint(platform.activeLobby),
                        sourceFingerprint,
                        BuildSelectionFingerprint(self, platform.activeLobby)) ||
                    timedOut)
                {
                    FailTransfer(
                        "BugfixesAndQoL.AivSyncFailed",
                        timedOut
                            ? SerpLocalization.Get("BugfixesAndQoL.AivSyncTimeout")
                            : SerpLocalization.Get("BugfixesAndQoL.AivSyncChanged"));
                }
            }
            catch (Exception ex)
            {
                FailTransfer("BugfixesAndQoL.AivSyncFailed", ex.GetBaseException().Message);
            }
        }

        private MultiplayerAivManifest BuildHostManifest(
            Platform_Multiplayer platform,
            FRONT_Multiplayer frontend)
        {
            if (platform?.activeLobby == null || frontend?.AIVs == null)
                throw new InvalidOperationException("The multiplayer lobby AIV state is unavailable.");
            var manifest = new MultiplayerAivManifest
            {
                LobbyId = platform.activeLobby.id.m_SteamID,
                VanillaChecksum = platform.activeLobby.AIVDataChecksum()
            };
            foreach (Platform_Multiplayer.MPLobbyMember member in platform.activeLobby.members)
            {
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                    continue;
                int playerId = platform.activeLobby.getThisPlayerFromSteamID(member.GetSteamID());
                if (playerId < 1 || playerId > frontend.AIVs.Length)
                    throw new InvalidOperationException($"Invalid AI player ID {playerId}.");
                FRONT_Multiplayer.MPAIVInfo info = frontend.AIVs[playerId - 1];
                if (info == null || info.builtIn || info.community || info.historical ||
                    info.aivs == null || info.aivs.Count <= 1)
                    continue;
                if (info.aivs.Count > MultiplayerAivSyncProtocol.MaximumCandidatesPerLord)
                    throw new InvalidOperationException($"Player {playerId} has more than 50 AIV candidates.");
                var slot = new MultiplayerAivSlot { PlayerId = playerId };
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    if (aiv?.data == null)
                        throw new InvalidOperationException($"Player {playerId} has an invalid AIV candidate.");
                    slot.Candidates.Add(new MultiplayerAivCandidate
                    {
                        Checksum = aiv.checksum,
                        Data = (short[])aiv.data.Clone()
                    });
                }
                manifest.Slots.Add(slot);
            }
            manifest.Slots.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return manifest;
        }

        private static void ValidateManifestAgainstLobby(
            Platform_Multiplayer platform,
            MultiplayerAivManifest manifest)
        {
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            if (manifest == null || lobby == null || manifest.LobbyId != lobby.id.m_SteamID ||
                !string.Equals(manifest.VanillaChecksum, lobby.AIVDataChecksum(), StringComparison.Ordinal))
                throw new InvalidOperationException("AIV manifest no longer matches the Vanilla lobby checksum.");
            foreach (MultiplayerAivSlot slot in manifest.Slots)
            {
                string encoded = GetVanillaAivData(lobby, slot.PlayerId);
                var vanilla = new FRONT_Multiplayer.MPAIVInfo();
                vanilla.decode(encoded ?? string.Empty);
                if (vanilla.aivs == null || vanilla.aivs.Count != 1 || slot.Candidates.Count < 1 ||
                    vanilla.aivs[0].checksum != slot.Candidates[0].Checksum ||
                    !MultiplayerAivSyncProtocol.FixedEquals(
                        MultiplayerAivSyncProtocol.HashData(vanilla.aivs[0].data),
                        MultiplayerAivSyncProtocol.HashData(slot.Candidates[0].Data)))
                    throw new InvalidOperationException($"Vanilla candidate 0 mismatch for player {slot.PlayerId}.");
            }
        }

        private void SendReliable(CSteamID target, MultiplayerAivSyncPacket packet)
        {
            byte[] body = GameNetworkAPI.Serialize(packet);
            var envelope = new Platform_Multiplayer.MPData
            {
                packetType = packetHook.GetPacketId(),
                dataLength = body.Length,
                data = body,
                dataOffset = 0
            };
            byte[] raw = envelope.ToBytes();
            SteamNetworkingIdentity identity = default(SteamNetworkingIdentity);
            identity.SetSteamID(target);
            GCHandle handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            try
            {
                EResult result = SteamNetworkingMessages.SendMessageToUser(
                    ref identity,
                    handle.AddrOfPinnedObject(),
                    (uint)raw.Length,
                    40,
                    2);
                if (result != EResult.k_EResultOK)
                    throw new InvalidOperationException($"Steam AIV transfer to {target.m_SteamID} failed: {result}.");
            }
            finally
            {
                handle.Free();
            }
        }

        private void Reject(CSteamID owner, MultiplayerAivSyncPacket source, string reason)
        {
            try
            {
                string safeReason = reason ?? string.Empty;
                if (safeReason.Length > 512)
                    safeReason = safeReason.Substring(0, 512);
                SendReliable(owner, CreateResponse(MultiplayerAivSyncPacketKind.Reject, source, safeReason));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not reject invalid AIV data: {ex}");
            }
        }

        private static MultiplayerAivSyncPacket CreateResponse(
            MultiplayerAivSyncPacketKind kind,
            MultiplayerAivSyncPacket source,
            string message) =>
            new MultiplayerAivSyncPacket
            {
                ProtocolVersion = MultiplayerAivSyncProtocol.ProtocolVersion,
                Kind = (int)kind,
                LobbyId = source.LobbyId,
                Generation = source.Generation,
                ManifestHash = source.ManifestHash,
                Message = message ?? string.Empty
            };

        private bool IsFeatureActive() =>
            settings.EnableMod && settings.EnableCustomLordListEnhancements;

        private static bool IsEligibleLobby(Platform_Multiplayer platform) =>
            platform?.activeLobby != null &&
            platform.activeLobby.isHost &&
            !FRONT_Multiplayer.skirmishGame &&
            Shared.GameModeHelper.IsRealMultiplayer();

        private static IEnumerable<CSteamID> GetHumanPeers(Platform_Multiplayer.MPLobby lobby)
        {
            CSteamID local = SteamUser.GetSteamID();
            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
            {
                if (member == null || member.dummyToBeKicked ||
                    (member.SkirmishMember && !member.SkirmishHumanMember) || member.id == local)
                    continue;
                yield return member.id;
            }
        }

        private static string BuildRosterFingerprint(Platform_Multiplayer.MPLobby lobby) =>
            lobby == null
                ? string.Empty
                : string.Join(",", GetHumanPeers(lobby).Select(id => id.m_SteamID).OrderBy(id => id));

        private static string BuildSelectionFingerprint(
            FRONT_Multiplayer frontend,
            Platform_Multiplayer.MPLobby lobby)
        {
            if (frontend?.AIVs == null || lobby == null)
                return string.Empty;
            var parts = new List<string> { lobby.id.m_SteamID.ToString(), lobby.AIVDataChecksum() };
            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
            {
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                    continue;
                int playerId = lobby.getThisPlayerFromSteamID(member.GetSteamID());
                if (playerId < 1 || playerId > frontend.AIVs.Length)
                    continue;
                FRONT_Multiplayer.MPAIVInfo info = frontend.AIVs[playerId - 1];
                parts.Add(playerId + ":" + string.Join(",", info?.aivs?.Select(aiv => aiv?.checksum ?? 0UL) ?? Enumerable.Empty<ulong>()));
            }
            return string.Join("|", parts);
        }

        private static string GetVanillaAivData(Platform_Multiplayer.MPLobby lobby, int playerId)
        {
            switch (playerId)
            {
                case 2: return lobby.AIVDataPlayer2;
                case 3: return lobby.AIVDataPlayer3;
                case 4: return lobby.AIVDataPlayer4;
                case 5: return lobby.AIVDataPlayer5;
                case 6: return lobby.AIVDataPlayer6;
                case 7: return lobby.AIVDataPlayer7;
                case 8: return lobby.AIVDataPlayer8;
                default: throw new InvalidOperationException($"Player {playerId} has no Vanilla AIV data slot.");
            }
        }

        private static string DescribeManifest(MultiplayerAivManifest manifest) =>
            string.Join(";", manifest.Slots.Select(slot =>
                "p" + slot.PlayerId + "=[" + string.Join(",", slot.Candidates.Select((candidate, id) =>
                    id + ":" + candidate.Checksum + ":" +
                    MultiplayerAivSyncProtocol.ToHex(
                        candidate.DataHash ?? MultiplayerAivSyncProtocol.HashData(candidate.Data)))) + "]"));

        private void FailTransfer(string localeKey, string reason)
        {
            transferInProgress = false;
            confirmedManifest = null;
            expectedAcks.Clear();
            receivedAcks.Clear();
            activeManifestHash = string.Empty;
            SyncStatusText = SerpLocalization.Get(localeKey, "Reason", reason ?? string.Empty);
            Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL AIV synchronization blocked game start: {reason}");
        }

        private void ClearPendingClientManifest(bool clearConfirmed)
        {
            pendingClientManifest = null;
            pendingClientBegin = null;
            pendingClientOwner = new CSteamID(0UL);
            if (clearConfirmed)
                confirmedManifest = null;
        }

        public void Dispose()
        {
            packetSubscription?.Dispose();
            packetSubscription = null;
            Release(ref updateHook);
            Release(ref loadMapHook);
            Release(ref startGameHook);
            Release(ref hostStartHook);
            initialized = false;
        }

        private static void Release(ref Hook hook)
        {
            Hook value = hook;
            hook = null;
            value?.Undo();
            value?.Dispose();
        }

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameters) =>
            type.GetMethod(
                name,
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameters,
                null) ?? throw new MissingMethodException(type.FullName, name);
    }
}
