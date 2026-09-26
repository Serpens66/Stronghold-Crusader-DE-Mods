using System;

namespace ExtendedData
{
    public static class TrailLordSelectionPolicy
    {
        public static bool UsesEmbeddedLord(TrailLordSlot slot, bool builtInLord,
            string selectedLordName, string mediaAlias = null)
        {
            return slot != null && !builtInLord &&
                (string.Equals(slot.LordName, selectedLordName, StringComparison.Ordinal) ||
                 !string.IsNullOrEmpty(mediaAlias) &&
                 string.Equals(mediaAlias, selectedLordName, StringComparison.Ordinal));
        }
    }
}
