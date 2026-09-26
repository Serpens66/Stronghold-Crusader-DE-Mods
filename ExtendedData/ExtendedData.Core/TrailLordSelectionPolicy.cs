using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedData
{
    public static class TrailLordSelectionPolicy
    {
        public static bool UsesEmbeddedLord(TrailLordSlot slot, bool builtInLord,
            string configChecksum, IEnumerable<string> aivChecksums)
        {
            return slot != null && !builtInLord &&
                string.Equals(slot.ConfigChecksum, configChecksum, StringComparison.Ordinal) &&
                (aivChecksums ?? Enumerable.Empty<string>()).SequenceEqual(
                    slot.AivChecksums ?? Array.Empty<string>(), StringComparer.Ordinal);
        }
    }
}
