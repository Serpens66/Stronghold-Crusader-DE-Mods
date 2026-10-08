using System;

namespace ExtendedData
{
    public static class TrailLordSelectionPolicy
    {
        public static bool UsesEmbeddedLord(TrailLordSlot slot, bool builtInLord,
            string selectedLordName, string mediaAlias = null)
        {
            return UsesEmbeddedLord(slot, builtInLord, selectedLordName, mediaAlias, null);
        }

        public static bool UsesEmbeddedLord(TrailLordSlot slot, bool builtInLord,
            string selectedLordName, string mediaAlias, int? selectedLordType)
        {
            return slot != null && !builtInLord &&
                (string.IsNullOrWhiteSpace(selectedLordName) && selectedLordType.HasValue &&
                 selectedLordType.Value >= 0 && slot.LordType == selectedLordType ||
                 string.Equals(slot.LordName, selectedLordName, StringComparison.Ordinal) ||
                 !string.IsNullOrEmpty(mediaAlias) &&
                 string.Equals(mediaAlias, selectedLordName, StringComparison.Ordinal));
        }
    }
}
