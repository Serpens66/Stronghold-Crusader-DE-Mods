using SHCDESE.Interop;

namespace Shared
{
    internal enum CoopAiCastleKind
    {
        Standard,
        Community,
        Historical,
        Explicit,
    }

    // Validity of one Coop AI selection before its castles and AIC are imported at start.
    internal static class CoopAiSelectionRules
    {
        // FRONT_Multiplayer stores Lord types zero-based, while the Extender enum includes SK_NULL.
        // Zero-based types from SK_X1 on are named custom Lords without Vanilla castles.
        internal static int FirstCustomLordType => checked((int)AILords.SK_X1 - 1);

        // The AIC (builtInLord/lordConfig) and the castle source (builtIn/community/historical/candidates) are
        // independent. The flags take precedence like Vanilla's AI settings; an empty candidate list means
        // Vanilla's standard castles. Returns null for a valid selection, otherwise the reason. needsPlan is
        // false only for a fully Vanilla selection, whose default upload stays unchanged.
        internal static string Classify(int lordType, bool builtInLord, bool hasLordConfig, string lordName,
            bool builtIn, bool community, bool historical, int candidateCount,
            out CoopAiCastleKind castle, out bool needsPlan)
        {
            castle = builtIn ? CoopAiCastleKind.Standard
                : community ? CoopAiCastleKind.Community
                : historical ? CoopAiCastleKind.Historical
                : candidateCount > 0 ? CoopAiCastleKind.Explicit
                : CoopAiCastleKind.Standard;
            needsPlan = !builtInLord || castle != CoopAiCastleKind.Standard;
            if (lordType < 0)
                return "has no valid Lord type";
            if (!builtInLord && !hasLordConfig)
                return "has a custom AIC without configuration data";
            if (lordType >= FirstCustomLordType)
            {
                if (builtInLord || string.IsNullOrWhiteSpace(lordName))
                    return "is a custom Lord without its name or configuration";
                if (castle != CoopAiCastleKind.Explicit)
                    return "is a custom Lord without castles of its own";
            }
            return null;
        }
    }
}
