using APIShared;
using CrusaderDE;
using System;
using System.Collections.Generic;

namespace Shared
{
    // Translates a selected, transmitted or packaged Lord into APIShared's Coop AI import plan. Every mod that
    // supplies plans uses this one translation, so equal selections produce equal plans and merge.
    internal static class CoopAiStartPlanFactory
    {
        // Returns null for a fully Vanilla selection. candidateLimit < 0 takes every known candidate; extra
        // candidates continue the list. confirmedCandidates < 0 confirms every candidate; a smaller value marks
        // the rest as assumed (see CoopAiSlotPlan.ConfirmedCandidateCount). Invalid or changed data throws
        // InvalidOperationException.
        internal static CoopAiSlotPlan Create(FRONT_Multiplayer.MPAIVInfo info, int playerId, int lordType,
            int candidateLimit, IReadOnlyList<short[]> extraCandidates, bool candidateListComplete,
            int confirmedCandidates = -1)
        {
            if (info == null)
                throw new InvalidOperationException($"Coop AI slot {playerId} changed during start.");
            // A fully Vanilla selection keeps Vanilla's upload before any type comparison: Vanilla Coop
            // lobbies may still carry the "0:0:0:0" placeholder for an AI whose real type is elsewhere.
            if (lordType < CoopAiSelectionRules.FirstCustomLordType && info.builtInLord &&
                !info.community && !info.historical && (info.builtIn || (info.aivs?.Count ?? 0) == 0))
                return null;
            if (info.lordType != lordType)
                throw new InvalidOperationException($"Coop AI slot {playerId} changed during start.");
            string error = CoopAiSelectionRules.Classify(lordType, info.builtInLord, info.lordConfig != null,
                info.lordName, info.builtIn, info.community, info.historical, info.aivs?.Count ?? 0,
                out CoopAiCastleKind castle, out bool needsPlan);
            if (error != null)
                throw new InvalidOperationException($"Coop AI slot {playerId} {error}.");
            if (!needsPlan)
                return null;

            List<short[]> candidates = null;
            if (castle == CoopAiCastleKind.Explicit)
            {
                int count = candidateLimit < 0 ? info.aivs.Count : Math.Min(candidateLimit, info.aivs.Count);
                candidates = new List<short[]>(count + (extraCandidates?.Count ?? 0));
                for (int index = 0; index < count; index++)
                {
                    short[] data = info.aivs[index]?.data;
                    if (data == null || data.Length == 0)
                        throw new InvalidOperationException($"Coop AI slot {playerId} has an invalid AIV candidate {index}.");
                    candidates.Add(data);
                }
                if (extraCandidates != null)
                    candidates.AddRange(extraCandidates);
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
            CoopAiSelectionRules.Classify(left.lordType, left.builtInLord, left.lordConfig != null, left.lordName,
                left.builtIn, left.community, left.historical, left.aivs?.Count ?? 0, out CoopAiCastleKind leftCastle, out _);
            CoopAiSelectionRules.Classify(right.lordType, right.builtInLord, right.lordConfig != null, right.lordName,
                right.builtIn, right.community, right.historical, right.aivs?.Count ?? 0, out CoopAiCastleKind rightCastle, out _);
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
