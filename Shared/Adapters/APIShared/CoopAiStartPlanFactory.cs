using APIShared;
using CrusaderDE;
using System;
using System.Collections.Generic;

namespace Shared
{
    // Stable reason codes are localized by the consumer; Message is a bounded support diagnostic.
    internal sealed class CoopAiSelectionException : InvalidOperationException
    {
        internal string Code { get; }
        internal int PlayerId { get; }
        internal CoopAiSelectionException(string code, int playerId, string detail)
            : base("Coop AI selection: reason=" + code + ", player=" + playerId + ", " + detail)
        { Code = code; PlayerId = playerId; }
    }

    // Translates a selected, transmitted or packaged Lord into APIShared's Coop AI import plan. Every mod that
    // supplies plans uses this one translation, so equal selections produce equal plans and merge.
    internal static class CoopAiStartPlanFactory
    {
        internal static string Describe(FRONT_Multiplayer.MPAIVInfo info, int lordType) =>
            "lobbyType=" + lordType + ", selectionType=" + (info == null ? "missing" : info.lordType.ToString()) +
            ", lord=" + (info?.lordName ?? "").Replace('\r', ' ').Replace('\n', ' ').Substring(0, Math.Min(120, (info?.lordName ?? "").Length)) +
            ", aic=" + (info?.lordConfig != null) + ", builtInLord=" + info?.builtInLord +
            ", builtIn=" + info?.builtIn + ", community=" + info?.community + ", historical=" + info?.historical +
            ", castleSource=" + (info == null ? "missing" : info.builtIn ? "Standard" : info.community ? "Community"
                : info.historical ? "Historical" : info.aivs?.Count > 0 ? "Explicit" : "Standard") +
            ", candidates=" + (info?.aivs?.Count ?? 0);

        private static CoopAiSelectionException Failure(string code, int playerId,
            FRONT_Multiplayer.MPAIVInfo info, int lordType, string extra = "") =>
            new CoopAiSelectionException(code, playerId, Describe(info, lordType) + extra);

        internal static string DescribeLobby(Platform_Multiplayer.MPLobby lobby, FRONT_Multiplayer.MPAIVInfo[] infos)
        {
            if (lobby?.members == null || lobby.this_player_to_SteamID_mapping?.Length != 8)
                return "lobby mapping unavailable";
            var slots = new List<string>();
            for (int id = 2; id <= 8; id++)
            {
                var member = lobby.GetLobbyMemberFromThis_PlayerID(id);
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember) continue;
                slots.Add("player=" + id + ", " + Describe(infos != null && id <= infos.Length ? infos[id - 1] : null,
                    member.GetLordType()));
            }
            return string.Join("; ", slots);
        }

        // No imports, Unity access or source mutation. Both the last launch gate and Create use this contract.
        internal static CoopAiCastleKind Validate(FRONT_Multiplayer.MPAIVInfo info, int playerId, int lordType,
            out bool needsPlan, int candidateLimit = -1)
        {
            needsPlan = false;
            if (playerId < 2 || playerId > 8) throw Failure("InvalidSlot", playerId, info, lordType);
            if (lordType < 0) throw Failure("InvalidLordType", playerId, info, lordType);
            if (info == null) throw Failure("MissingSelection", playerId, info, lordType);
            string error = CoopAiSelectionRules.Classify(lordType, info.builtInLord, info.lordConfig != null,
                info.lordName, info.builtIn, info.community, info.historical, info.aivs?.Count ?? 0,
                out CoopAiCastleKind castle, out needsPlan);
            if (error != null)
            {
                string code = !info.builtInLord && info.lordConfig == null ? "MissingAic"
                    : lordType >= CoopAiSelectionRules.FirstCustomLordType && string.IsNullOrWhiteSpace(info.lordName)
                        ? "MissingLordName" : "InvalidCustomLord";
                throw Failure(code, playerId, info, lordType, ", castleSource=" + castle + ", problem=" + error);
            }
            // Vanilla does not initialize this redundant type in ordinary Coop lobbies.
            if (needsPlan && info.lordType != lordType)
                throw Failure("LordTypeMismatch", playerId, info, lordType, ", castleSource=" + castle);
            if (castle == CoopAiCastleKind.Explicit)
            {
                int count = candidateLimit < 0 ? info.aivs.Count : Math.Min(candidateLimit, info.aivs.Count);
                if (count < 1 || count > CoopAiSlotPlan.MaximumCandidates)
                    throw Failure("InvalidCandidate", playerId, info, lordType, ", candidateCount=" + count);
                for (int index = 0; index < count; index++)
                    if (info.aivs[index]?.data == null || info.aivs[index].data.Length == 0)
                        throw Failure("InvalidCandidate", playerId, info, lordType, ", candidate=" + index);
            }
            return castle;
        }

        internal static void ValidateLobby(Platform_Multiplayer.MPLobby lobby, FRONT_Multiplayer.MPAIVInfo[] infos)
        {
            if (lobby?.members == null || infos == null || infos.Length != 8)
                throw new CoopAiSelectionException("MissingSelection", 0, "lobby or eight-slot selection array unavailable");
            if (lobby.this_player_to_SteamID_mapping == null || lobby.this_player_to_SteamID_mapping.Length != 8)
                throw new CoopAiSelectionException("InvalidSlot", 0, "eight-slot lobby mapping unavailable");
            // Vanilla's restart snapshot copies all eight entries and enumerates each list, including humans.
            for (int index = 0; index < infos.Length; index++)
                if (infos[index] == null || infos[index].aivs == null)
                    throw Failure("MissingSelection", index + 1, infos[index], -1);
            var seen = new HashSet<int>();
            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
            {
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember) continue;
                int playerId = lobby.getThisPlayerFromSteamID(member.GetSteamID());
                if (playerId < 2 || playerId > 8 || !seen.Add(playerId))
                    throw Failure("InvalidSlot", playerId, null, member.GetLordType());
                Validate(infos[playerId - 1], playerId, member.GetLordType(), out _);
            }
        }

        // Fresh COOP_START and entry into a new Customize setup only; never a saved/restarted selection.
        // Replacing the entry avoids mutating remembered/package objects.
        internal static void PrepareDirectPartner(Platform_Multiplayer.MPLobby lobby,
            FRONT_Multiplayer.MPAIVInfo[] infos)
        {
            if (lobby?.members == null || lobby.this_player_to_SteamID_mapping == null ||
                lobby.this_player_to_SteamID_mapping.Length != 8)
                throw new CoopAiSelectionException("InvalidSlot", 2, "partner lobby mapping unavailable");
            Platform_Multiplayer.MPLobbyMember partner = lobby?.GetLobbyMemberFromThis_PlayerID(2);
            if (partner == null || !partner.SkirmishMember || partner.SkirmishHumanMember) return;
            int type = partner.GetLordType();
            if (type < 0 || type >= CoopAiSelectionRules.FirstCustomLordType) return;
            if (infos == null || infos.Length != 8) throw Failure("MissingSelection", 2, null, type);
            var standard = new FRONT_Multiplayer.MPAIVInfo();
            standard.Init(type, string.Empty);
            infos[1] = standard;
        }

        // Compare the import that will really win provider combination, rather than merely its presence.
        internal static void RequirePlan(CoopAiSlotPlan expected, CoopAiSlotPlan actual, bool requireComplete)
        {
            if (expected == null) return;
            bool matches = actual != null && actual.PlayerId == expected.PlayerId &&
                actual.LordType == expected.LordType && actual.CastleSource == expected.CastleSource &&
                actual.HasLordConfiguration == expected.HasLordConfiguration &&
                (!expected.HasLordConfiguration || actual.LordConfiguration.Equals(expected.LordConfiguration)) &&
                actual.CandidateCount >= expected.CandidateCount &&
                (!requireComplete || actual.CandidateListComplete && actual.ConfirmedCandidateCount == actual.CandidateCount &&
                    actual.CandidateCount == expected.CandidateCount);
            if (matches)
                for (int index = 0; index < expected.CandidateCount; index++)
                {
                    short[] left = expected.CopyCandidate(index), right = actual.CopyCandidate(index);
                    if (left.Length != right.Length) { matches = false; break; }
                    for (int item = 0; item < left.Length; item++)
                        if (left[item] != right[item]) { matches = false; break; }
                    if (!matches) break;
                }
            if (!matches)
                throw new CoopAiSelectionException(actual == null ? "MissingImportPlan" : "ImportPlanMismatch",
                    expected.PlayerId, "lobbyType=" + expected.LordType + ", castleSource=" + expected.CastleSource +
                    ", expectedCandidates=" + expected.CandidateCount + ", actualCandidates=" + (actual?.CandidateCount ?? 0));
        }

        // Returns null for a fully Vanilla selection. candidateLimit < 0 takes every known candidate; extra
        // candidates continue the list. confirmedCandidates < 0 confirms every candidate; a smaller value marks
        // the rest as assumed (see CoopAiSlotPlan.ConfirmedCandidateCount). Invalid or changed data throws
        // InvalidOperationException.
        internal static CoopAiSlotPlan Create(FRONT_Multiplayer.MPAIVInfo info, int playerId, int lordType,
            int candidateLimit, IReadOnlyList<short[]> extraCandidates, bool candidateListComplete,
            int confirmedCandidates = -1)
        {
            CoopAiCastleKind castle = Validate(info, playerId, lordType, out bool needsPlan, candidateLimit);
            if (!needsPlan)
                return null;

            List<short[]> candidates = null;
            if (castle == CoopAiCastleKind.Explicit)
            {
                int count = candidateLimit < 0 ? info.aivs.Count : Math.Min(candidateLimit, info.aivs.Count);
                candidates = new List<short[]>(count + (extraCandidates?.Count ?? 0));
                for (int index = 0; index < count; index++)
                    candidates.Add(info.aivs[index].data);
                if (extraCandidates != null)
                    candidates.AddRange(extraCandidates);
                if (candidates.Count > CoopAiSlotPlan.MaximumCandidates)
                    throw Failure("InvalidCandidate", playerId, info, lordType, ", candidateCount=" + candidates.Count);
                for (int index = count; index < candidates.Count; index++)
                    if (candidates[index] == null || candidates[index].Length == 0)
                        throw Failure("InvalidCandidate", playerId, info, lordType, ", candidate=" + index);
            }
            return new CoopAiSlotPlan(playerId, lordType, ToSource(castle), candidates,
                info.builtInLord ? (EngineInterface.AILordConfigTransferData?)null : info.lordConfig.lordData,
                candidateListComplete, candidates == null || confirmedCandidates < 0
                    ? -1 : Math.Min(confirmedCandidates, candidates.Count));
        }

        // True when both describe the same Lord, AIC and castles. compareAllCandidates=false compares only
        // the first candidate, which is all Vanilla's lobby data transmits.
        internal static bool IsSameSelection(FRONT_Multiplayer.MPAIVInfo left, FRONT_Multiplayer.MPAIVInfo right,
            bool compareAllCandidates)
        {
            if (left == null || right == null || left.lordType != right.lordType || left.builtInLord != right.builtInLord)
                return false;
            if (!left.builtInLord && (left.lordConfig == null || right.lordConfig == null ||
                    left.lordConfig.checksum != right.lordConfig.checksum ||
                    !left.lordConfig.lordData.Equals(right.lordConfig.lordData)))
                return false;
            if (CoopAiSelectionRules.Classify(left.lordType, left.builtInLord, left.lordConfig != null, left.lordName,
                left.builtIn, left.community, left.historical, left.aivs?.Count ?? 0, out CoopAiCastleKind leftCastle, out _) != null ||
                CoopAiSelectionRules.Classify(right.lordType, right.builtInLord, right.lordConfig != null, right.lordName,
                right.builtIn, right.community, right.historical, right.aivs?.Count ?? 0, out CoopAiCastleKind rightCastle, out _) != null)
                return false;
            if (leftCastle != rightCastle)
                return false;
            if (leftCastle != CoopAiCastleKind.Explicit)
                return true;
            int count = compareAllCandidates ? left.aivs.Count : 1;
            if (compareAllCandidates && right.aivs.Count != count || right.aivs.Count < count)
                return false;
            for (int index = 0; index < count; index++)
            {
                short[] a = left.aivs[index]?.data, b = right.aivs[index]?.data;
                if (a == null || b == null || a.Length != b.Length) return false;
                for (int item = 0; item < a.Length; item++)
                    if (a[item] != b[item]) return false;
            }
            return true;
        }

        private static CoopAiCastleSource ToSource(CoopAiCastleKind castle)
        {
            switch (castle)
            {
                case CoopAiCastleKind.Community: return CoopAiCastleSource.Community;
                case CoopAiCastleKind.Historical: return CoopAiCastleSource.Historical;
                case CoopAiCastleKind.Explicit: return CoopAiCastleSource.Explicit;
                default: return CoopAiCastleSource.Standard;
            }
        }
    }
}
