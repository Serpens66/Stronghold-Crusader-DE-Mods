using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIVParser.Core;
using AIVPlacement.Core;

namespace AIVPlacementLobby.Core
{
    public enum LobbyAivMode
    {
        Default,
        Community,
        Historical,
        Custom
    }

    public enum LobbyCandidateSourceKind
    {
        File,
        ScriptExtenderAsset
    }

    public enum LobbyCandidateSelectionPolicy
    {
        NativeBestFit
    }

    public enum LobbyRequestFailureKind
    {
        None,
        MapUnavailable,
        UnsupportedMapFile,
        KeepAssignmentMissing,
        KeepAssignmentAmbiguous,
        InvalidRotation,
        AivCandidatesUnavailable,
        AivFileUnavailable,
        PreBuildSequenceUnsupported
    }

    public sealed class LobbyAivCandidateInput
    {
        public LobbyAivCandidateInput(
            string name,
            string directoryPath,
            ulong checksum,
            bool builtIn,
            string lordEnumName)
        {
            Name = name ?? string.Empty;
            DirectoryPath = directoryPath ?? string.Empty;
            Checksum = checksum;
            BuiltIn = builtIn;
            LordEnumName = lordEnumName ?? string.Empty;
        }

        public string Name { get; }
        public string DirectoryPath { get; }
        public ulong Checksum { get; }
        public bool BuiltIn { get; }
        public string LordEnumName { get; }
    }

    public sealed class LobbyAiSlotInput
    {
        public LobbyAiSlotInput(
            int playerId,
            int lordType,
            string lordEnumName,
            string customLordName,
            LobbyAivMode mode,
            int rotationIndex,
            IEnumerable<LobbyAivCandidateInput> candidates)
        {
            PlayerId = playerId;
            LordType = lordType;
            LordEnumName = lordEnumName ?? string.Empty;
            CustomLordName = customLordName ?? string.Empty;
            Mode = mode;
            RotationIndex = rotationIndex;
            Candidates = new ReadOnlyCollection<LobbyAivCandidateInput>(
                new List<LobbyAivCandidateInput>(candidates ?? Array.Empty<LobbyAivCandidateInput>()));
        }

        public int PlayerId { get; }
        public int LordType { get; }
        public string LordEnumName { get; }
        public string CustomLordName { get; }
        public LobbyAivMode Mode { get; }
        public int RotationIndex { get; }
        public IReadOnlyList<LobbyAivCandidateInput> Candidates { get; }
    }

    public sealed class LobbyStateCapture
    {
        public LobbyStateCapture(
            string mapPath,
            string mapName,
            string mapOrigin,
            bool isHost,
            int preBuildSetting,
            IEnumerable<int> keepToPlayerOrder,
            IEnumerable<LobbyAiSlotInput> aiSlots,
            IEnumerable<string> scriptExtenderAivAssets)
        {
            MapPath = mapPath ?? string.Empty;
            MapName = mapName ?? string.Empty;
            MapOrigin = mapOrigin ?? string.Empty;
            IsHost = isHost;
            PreBuildSetting = preBuildSetting;
            KeepToPlayerOrder = new ReadOnlyCollection<int>(
                new List<int>(keepToPlayerOrder ?? Array.Empty<int>()));
            AiSlots = new ReadOnlyCollection<LobbyAiSlotInput>(
                new List<LobbyAiSlotInput>(aiSlots ?? Array.Empty<LobbyAiSlotInput>()));
            ScriptExtenderAivAssets = new ReadOnlyCollection<string>(
                new List<string>(scriptExtenderAivAssets ?? Array.Empty<string>()));
        }

        public string MapPath { get; }
        public string MapName { get; }
        public string MapOrigin { get; }
        public bool IsHost { get; }
        public int PreBuildSetting { get; }
        public IReadOnlyList<int> KeepToPlayerOrder { get; }
        public IReadOnlyList<LobbyAiSlotInput> AiSlots { get; }
        public IReadOnlyList<string> ScriptExtenderAivAssets { get; }
    }

    public sealed class AivPlacementCandidateRequest
    {
        internal AivPlacementCandidateRequest(
            int candidateId,
            string name,
            LobbyCandidateSourceKind sourceKind,
            string source,
            ulong checksum,
            LobbyRequestFailureKind failureKind)
        {
            CandidateId = candidateId;
            Name = name ?? string.Empty;
            SourceKind = sourceKind;
            Source = source ?? string.Empty;
            Checksum = checksum;
            FailureKind = failureKind;
        }

        public int CandidateId { get; }
        public string Name { get; }
        public LobbyCandidateSourceKind SourceKind { get; }
        public string Source { get; }
        public ulong Checksum { get; }
        public LobbyRequestFailureKind FailureKind { get; }
        public bool IsAvailable => FailureKind == LobbyRequestFailureKind.None;
    }

    public sealed class AivPlacementCheckRequest
    {
        internal AivPlacementCheckRequest(
            long generation,
            string mapPath,
            string mapName,
            string mapOrigin,
            bool isHost,
            int preBuildSetting,
            int playerId,
            int keepSlotIndex,
            int lordType,
            string lordName,
            LobbyAivMode aivMode,
            AivRotation initialRotation,
            IEnumerable<AivPlacementCandidateRequest> candidates,
            LobbyRequestFailureKind failureKind)
        {
            Generation = generation;
            MapPath = mapPath ?? string.Empty;
            MapName = mapName ?? string.Empty;
            MapOrigin = mapOrigin ?? string.Empty;
            IsHost = isHost;
            PreBuildSetting = preBuildSetting;
            PlayerId = playerId;
            KeepSlotIndex = keepSlotIndex;
            LordType = lordType;
            LordName = lordName ?? string.Empty;
            AivMode = aivMode;
            InitialRotation = initialRotation;
            CandidateSelectionPolicy = LobbyCandidateSelectionPolicy.NativeBestFit;
            Candidates = new ReadOnlyCollection<AivPlacementCandidateRequest>(
                new List<AivPlacementCandidateRequest>(candidates));
            FailureKind = failureKind;
            ImmediateResultStatus = failureKind == LobbyRequestFailureKind.None
                ? (AivPlacementStatus?)null
                : AivPlacementStatus.NotEvaluable;
        }

        public long Generation { get; }
        public string MapPath { get; }
        public string MapName { get; }
        public string MapOrigin { get; }
        public bool IsHost { get; }
        public int PreBuildSetting { get; }
        public int PlayerId { get; }
        public int KeepSlotIndex { get; }
        public int LordType { get; }
        public string LordName { get; }
        public LobbyAivMode AivMode { get; }
        public AivRotation InitialRotation { get; }
        public LobbyCandidateSelectionPolicy CandidateSelectionPolicy { get; }
        // Candidate IDs preserve the import order consumed by Vanilla's native fit scan.
        public IReadOnlyList<AivPlacementCandidateRequest> Candidates { get; }
        public LobbyRequestFailureKind FailureKind { get; }
        public AivPlacementStatus? ImmediateResultStatus { get; }
        public bool IsReady => FailureKind == LobbyRequestFailureKind.None;
    }

    public sealed class AivPlacementRequestBatch
    {
        internal AivPlacementRequestBatch(
            long generation,
            IEnumerable<AivPlacementCheckRequest> requests)
        {
            Generation = generation;
            Requests = new ReadOnlyCollection<AivPlacementCheckRequest>(
                new List<AivPlacementCheckRequest>(requests));
        }

        public long Generation { get; }
        public IReadOnlyList<AivPlacementCheckRequest> Requests { get; }
    }
}
