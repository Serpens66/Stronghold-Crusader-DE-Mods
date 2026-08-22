using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AIVParser.Core;

namespace CastlePlanner.AIVPlacement.Core
{
    public sealed class LobbyRequestBuilder
    {
        public AivPlacementRequestBatch Build(
            long generation,
            LobbyStateCapture capture,
            string vanillaAivDirectory)
        {
            if (generation < 1)
                throw new ArgumentOutOfRangeException(nameof(generation));
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            string mapPath = NormalizePath(capture.MapPath);
            LobbyRequestFailureKind mapFailure = GetMapFailure(mapPath);
            var assetOverrides = new HashSet<string>(
                capture.ScriptExtenderAivAssets,
                StringComparer.OrdinalIgnoreCase);
            var requests = new List<AivPlacementCheckRequest>(capture.AiSlots.Count);
            var retainedStartSlots = new HashSet<int>();
            LobbyRequestFailureKind retainedStateFailure = LobbyRequestFailureKind.None;
            foreach (int humanPlayerId in capture.HumanPlayerIds)
            {
                int humanKeepSlot = FindKeepSlot(
                    capture.KeepToPlayerOrder,
                    humanPlayerId,
                    out LobbyRequestFailureKind humanKeepFailure);
                if (humanKeepFailure == LobbyRequestFailureKind.None)
                    retainedStartSlots.Add(humanKeepSlot);
                else if (retainedStateFailure == LobbyRequestFailureKind.None)
                    retainedStateFailure = humanKeepFailure;
            }

            foreach (LobbyAiSlotInput slot in capture.AiSlots.OrderBy(value => value.PlayerId))
            {
                int keepSlotIndex = FindKeepSlot(
                    capture.KeepToPlayerOrder,
                    slot.PlayerId,
                    out LobbyRequestFailureKind keepFailure);
                AivRotation rotation = ToRotation(
                    slot.RotationIndex,
                    out bool usesMapFacingRotation,
                    out bool validRotation);
                IReadOnlyList<AivPlacementCandidateRequest> candidates = ResolveCandidates(
                    slot,
                    vanillaAivDirectory,
                    assetOverrides);

                LobbyRequestFailureKind failure = mapFailure;
                if (failure == LobbyRequestFailureKind.None)
                    failure = keepFailure;
                if (failure == LobbyRequestFailureKind.None)
                    failure = retainedStateFailure;
                if (failure == LobbyRequestFailureKind.None && !validRotation)
                    failure = LobbyRequestFailureKind.InvalidRotation;
                if (failure == LobbyRequestFailureKind.None && candidates.Count == 0)
                    failure = LobbyRequestFailureKind.AivCandidatesUnavailable;
                if (failure == LobbyRequestFailureKind.None && candidates.Any(value => !value.IsAvailable))
                    failure = LobbyRequestFailureKind.AivFileUnavailable;

                // Only the host owns lobby setup and the multiplayer start payload.
                if (!capture.IsHost)
                    failure = LobbyRequestFailureKind.ClientEvaluationNotRequired;

                // Sequential prebuild state is intentionally deferred to Chats 14-16.
                if (capture.PreBuildSetting == 1)
                    failure = LobbyRequestFailureKind.PreBuildSequenceUnsupported;
                else if (capture.PreBuildSetting != 0 && failure == LobbyRequestFailureKind.None)
                    failure = LobbyRequestFailureKind.PreBuildSequenceUnsupported;

                string lordName = string.IsNullOrEmpty(slot.CustomLordName)
                    ? DescribeLord(slot.LordEnumName)
                    : slot.CustomLordName;
                requests.Add(new AivPlacementCheckRequest(
                    generation,
                    mapPath,
                    capture.MapName,
                    capture.MapOrigin,
                    capture.IsHost,
                    capture.PreBuildSetting,
                    slot.PlayerId,
                    keepSlotIndex,
                    slot.LordType,
                    lordName,
                    slot.Mode,
                    rotation,
                    usesMapFacingRotation,
                    retainedStartSlots,
                    candidates,
                    failure));

                // Native scans AI players in ID order; completed prior starts remain on the map.
                if (keepFailure == LobbyRequestFailureKind.None)
                    retainedStartSlots.Add(keepSlotIndex);
                else if (retainedStateFailure == LobbyRequestFailureKind.None)
                    retainedStateFailure = keepFailure;
            }

            return new AivPlacementRequestBatch(generation, requests);
        }

        public static string BuildFingerprint(LobbyStateCapture capture)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            var result = new StringBuilder();
            Append(result, capture.MapPath);
            Append(result, capture.MapName);
            Append(result, capture.MapOrigin);
            result.Append(capture.IsHost ? 'H' : 'C').Append('|');
            result.Append(capture.PreBuildSetting).Append('|');
            foreach (int value in capture.KeepToPlayerOrder)
                result.Append(value).Append(',');
            result.Append('|');
            foreach (int playerId in capture.HumanPlayerIds.OrderBy(value => value))
                result.Append(playerId).Append(',');
            result.Append('|');
            foreach (LobbyAiSlotInput slot in capture.AiSlots.OrderBy(value => value.PlayerId))
            {
                result.Append(slot.PlayerId).Append(',').Append(slot.LordType).Append(',')
                    .Append((int)slot.Mode).Append(',').Append(slot.RotationIndex).Append('|');
                Append(result, slot.LordEnumName);
                Append(result, slot.CustomLordName);
                foreach (LobbyAivCandidateInput candidate in slot.Candidates)
                {
                    Append(result, candidate.Name);
                    Append(result, candidate.DirectoryPath);
                    Append(result, candidate.LordEnumName);
                    result.Append(candidate.Checksum).Append(',')
                        .Append(candidate.BuiltIn ? '1' : '0').Append('|');
                }
            }
            foreach (string asset in capture.ScriptExtenderAivAssets.OrderBy(value => value))
                Append(result, asset);
            return result.ToString();
        }

        private static IReadOnlyList<AivPlacementCandidateRequest> ResolveCandidates(
            LobbyAiSlotInput slot,
            string vanillaAivDirectory,
            ISet<string> assetOverrides)
        {
            var result = new List<AivPlacementCandidateRequest>();
            if (slot.Mode == LobbyAivMode.Custom)
            {
                // Vanilla imports the full selected list and chooses its best fit natively.
                // Never collapse this list to a random or preselected candidate here.
                for (int index = 0; index < slot.Candidates.Count; index++)
                {
                    LobbyAivCandidateInput candidate = slot.Candidates[index];
                    if (candidate.BuiltIn && TryDecodeBuiltInChecksum(
                            candidate.Checksum,
                            out LobbyAivMode mode,
                            out int sourceIndex))
                    {
                        result.Add(ResolveBuiltIn(
                            index,
                            candidate.Name,
                            candidate.LordEnumName,
                            mode,
                            sourceIndex,
                            candidate.Checksum,
                            vanillaAivDirectory,
                            assetOverrides));
                    }
                    else
                    {
                        string fileName = EnsureExtension(candidate.Name);
                        string path = string.IsNullOrEmpty(candidate.DirectoryPath)
                            ? fileName
                            : Path.Combine(candidate.DirectoryPath, fileName);
                        path = NormalizePath(path);
                        result.Add(new AivPlacementCandidateRequest(
                            index,
                            candidate.Name,
                            LobbyCandidateSourceKind.File,
                            path,
                            candidate.Checksum,
                            File.Exists(path)
                                ? LobbyRequestFailureKind.None
                                : LobbyRequestFailureKind.AivFileUnavailable));
                    }
                }
                return result;
            }

            int count = slot.Mode == LobbyAivMode.Historical ? 1 : 8;
            for (int index = 0; index < count; index++)
            {
                result.Add(ResolveBuiltIn(
                    index,
                    string.Empty,
                    slot.LordEnumName,
                    slot.Mode,
                    index,
                    0,
                    vanillaAivDirectory,
                    assetOverrides));
            }
            return result;
        }

        private static AivPlacementCandidateRequest ResolveBuiltIn(
            int candidateId,
            string capturedName,
            string lordEnumName,
            LobbyAivMode mode,
            int sourceIndex,
            ulong checksum,
            string vanillaAivDirectory,
            ISet<string> assetOverrides)
        {
            string asset = $"AIV/{lordEnumName}_{sourceIndex}.aivjson";
            if (assetOverrides.Contains(asset))
            {
                return new AivPlacementCandidateRequest(
                    candidateId,
                    string.IsNullOrEmpty(capturedName) ? asset : capturedName,
                    LobbyCandidateSourceKind.ScriptExtenderAsset,
                    asset,
                    checksum,
                    LobbyRequestFailureKind.None);
            }

            string stem = GetAivFileStem(lordEnumName);
            string fileName = mode == LobbyAivMode.Community
                ? $"Community_{stem}{sourceIndex + 1}.aivjson"
                : mode == LobbyAivMode.Historical
                    ? $"Community_Historical_{stem}.aivjson"
                    : $"{stem}{sourceIndex + 1}.aivjson";
            string path = NormalizePath(Path.Combine(vanillaAivDirectory ?? string.Empty, fileName));
            return new AivPlacementCandidateRequest(
                candidateId,
                string.IsNullOrEmpty(capturedName)
                    ? Path.GetFileNameWithoutExtension(fileName)
                    : capturedName,
                LobbyCandidateSourceKind.File,
                path,
                checksum,
                File.Exists(path)
                    ? LobbyRequestFailureKind.None
                    : LobbyRequestFailureKind.AivFileUnavailable);
        }

        private static int FindKeepSlot(
            IReadOnlyList<int> keepToPlayerOrder,
            int playerId,
            out LobbyRequestFailureKind failure)
        {
            int expected = playerId - 1;
            int found = -1;
            for (int index = 0; index < keepToPlayerOrder.Count; index++)
            {
                if (keepToPlayerOrder[index] != expected)
                    continue;
                if (found >= 0)
                {
                    failure = LobbyRequestFailureKind.KeepAssignmentAmbiguous;
                    return -1;
                }
                found = index;
            }
            failure = found < 0
                ? LobbyRequestFailureKind.KeepAssignmentMissing
                : LobbyRequestFailureKind.None;
            return found;
        }

        private static LobbyRequestFailureKind GetMapFailure(string mapPath)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
                return LobbyRequestFailureKind.MapUnavailable;
            if (!string.Equals(Path.GetExtension(mapPath), ".map", StringComparison.OrdinalIgnoreCase))
                return LobbyRequestFailureKind.UnsupportedMapFile;
            return File.Exists(mapPath)
                ? LobbyRequestFailureKind.None
                : LobbyRequestFailureKind.MapUnavailable;
        }

        private static AivRotation ToRotation(
            int rotationIndex,
            out bool usesMapFacingRotation,
            out bool valid)
        {
            valid = rotationIndex >= 0 && rotationIndex <= 4;
            usesMapFacingRotation = rotationIndex == 0;
            // UI values 1..4 become native orientations 0, 2, 4 and 6.
            return valid && rotationIndex > 0
                ? (AivRotation)((rotationIndex - 1) * 90)
                : AivRotation.Degrees0;
        }

        private static bool TryDecodeBuiltInChecksum(
            ulong checksum,
            out LobbyAivMode mode,
            out int sourceIndex)
        {
            if (checksum >= 1 && checksum <= 8)
            {
                mode = LobbyAivMode.Default;
                sourceIndex = (int)checksum - 1;
                return true;
            }
            if (checksum >= 51 && checksum <= 58)
            {
                mode = LobbyAivMode.Community;
                sourceIndex = (int)checksum - 51;
                return true;
            }
            if (checksum == 61)
            {
                mode = LobbyAivMode.Historical;
                sourceIndex = 0;
                return true;
            }
            mode = LobbyAivMode.Custom;
            sourceIndex = -1;
            return false;
        }

        private static string GetAivFileStem(string lordEnumName)
        {
            if (string.Equals(lordEnumName, "SK_PHILLIP", StringComparison.Ordinal))
                return "philip";
            if (string.Equals(lordEnumName, "SK_KAHIN", StringComparison.Ordinal))
                return "kahinah";
            if (string.Equals(lordEnumName, "SK_CROCODILE", StringComparison.Ordinal))
                return "croc";
            if (string.Equals(lordEnumName, "SK_DLC4A", StringComparison.Ordinal))
                return "surgeon";
            if (string.Equals(lordEnumName, "SK_DLC4B", StringComparison.Ordinal))
                return "baibars";
            return DescribeLord(lordEnumName).ToLowerInvariant();
        }

        private static string DescribeLord(string lordEnumName) =>
            lordEnumName != null && lordEnumName.StartsWith("SK_", StringComparison.Ordinal)
                ? lordEnumName.Substring(3)
                : lordEnumName ?? string.Empty;

        private static string EnsureExtension(string name) =>
            name != null && name.EndsWith(".aivjson", StringComparison.OrdinalIgnoreCase)
                ? name
                : (name ?? string.Empty) + ".aivjson";

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private static void Append(StringBuilder result, string value)
        {
            value = value ?? string.Empty;
            result.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }

    public sealed class LobbyRequestGenerationGate
    {
        private long currentGeneration;

        public long Advance() => Interlocked.Increment(ref currentGeneration);
        public long Current => Interlocked.Read(ref currentGeneration);
        public bool IsCurrent(long generation) => generation == Current;
    }
}
