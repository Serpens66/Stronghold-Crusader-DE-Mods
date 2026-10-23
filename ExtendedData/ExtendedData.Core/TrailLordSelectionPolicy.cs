using System;

namespace ExtendedData
{
    public static class TrailLordSelectionPolicy
    {
        // Name-only comparison. Kept for existing callers; it cannot detect a different
        // configuration selected under the same Lord name. Use the configuration overload.
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

        // A slot is still the embedded Lord only while both its Lord identity and its saved
        // configuration (type, name and checksum) are selected. A media alias alone never
        // identifies an AIC, so another configuration under the same name is a replacement.
        public static bool UsesEmbeddedLord(TrailLordSlot slot, bool builtInLord,
            string selectedLordName, string mediaAlias, int? selectedLordType,
            int selectedConfigLordType, string selectedConfigName, string selectedConfigChecksum)
        {
            return UsesEmbeddedLord(slot, builtInLord, selectedLordName, mediaAlias, selectedLordType) &&
                HasSameConfiguration(slot, selectedConfigLordType, selectedConfigName, selectedConfigChecksum);
        }

        public static bool HasSameConfiguration(TrailLordSlot slot, int configLordType,
            string configName, string configChecksum)
        {
            return slot != null && !string.IsNullOrWhiteSpace(configChecksum) &&
                string.Equals(slot.ConfigChecksum, configChecksum, StringComparison.Ordinal) &&
                string.Equals(slot.ConfigName, configName, StringComparison.OrdinalIgnoreCase) &&
                (!slot.LordType.HasValue || slot.LordType.Value == configLordType);
        }
    }
}
