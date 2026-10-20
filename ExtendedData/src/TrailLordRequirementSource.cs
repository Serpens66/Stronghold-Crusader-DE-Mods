using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData
{
    // Thrown when a Custom-AIC slot has neither stored, identity-bound extra values nor a
    // verifiable installed author package. The caller decides; nothing is assumed absent.
    internal sealed class TrailLordAuthoringUnavailableException : Exception
    {
        internal TrailLordAuthoringUnavailableException(string trailPath, int playerId, string lordName,
            Exception inner)
            : base("Lord " + lordName + " (player " + playerId + ") of [" + Path.GetFileName(trailPath) +
                "] has no verifiable extra values: " + inner.Message, inner)
        {
            TrailPath = trailPath;
            PlayerId = playerId;
            LordName = lordName;
        }

        internal string TrailPath { get; }
        internal int PlayerId { get; }
        internal string LordName { get; }
    }

    internal static class TrailLordRequirementSource
    {
        internal sealed class Resolution
        {
            internal IReadOnlyList<TrailLordSlot> Slots;
            // Only slots captured from an installation; reused and embedded-only slots have none.
            internal IReadOnlyDictionary<int, TrailLordPackageRuntime.Candidate> InstalledSources;
            internal IReadOnlyList<int> ReusedPlayerIds;
            internal IReadOnlyList<int> EmbeddedOnlyPlayerIds;
        }

        // Stored requirements are the source of extra values and installation duties. A re-saved
        // Trail keeps a slot's stored values only while that slot's authored Lord, configuration
        // and ordered AIV list are unchanged; changed or new slots use the author capture.
        internal static Resolution Resolve(string trailPath,
            HUD_IngameMenu.RestartSkirmishMapInfo restart, ISet<int> includedPlayerIds,
            bool allowEmbeddedValuesOnly)
        {
            // A damaged or contradictory sidecar throws here and is never silently replaced.
            TrailLordRequirements stored = TrailLordRequirements.ReadUnbound(trailPath, out _);
            var fixes = new FixesLordPreferencesBridge();
            var slots = new List<TrailLordSlot>();
            var sources = new Dictionary<int, TrailLordPackageRuntime.Candidate>();
            var reused = new List<int>();
            var embeddedOnly = new List<int>();
            foreach (int playerId in TrailLordPackageRuntime.CustomLordPlayerIds(restart, includedPlayerIds))
            {
                FRONT_Multiplayer.MPAIVInfo info = restart.aivs[playerId - 1];
                TrailLordSlot current = TrailLordPackageRuntime.SavedIdentity(info, playerId);
                TrailLordSlot storedSlot = stored?.Slots.FirstOrDefault(slot => slot.PlayerId == playerId);
                if (storedSlot != null && TrailLordRequirements.HasSameAuthoredIdentity(storedSlot, current))
                {
                    slots.Add(storedSlot);
                    reused.Add(playerId);
                    continue;
                }
                try
                {
                    slots.Add(TrailLordPackageRuntime.CaptureSlot(info, playerId, fixes,
                        out TrailLordPackageRuntime.Candidate source));
                    sources[playerId] = source;
                }
                catch (Exception exception)
                {
                    if (!allowEmbeddedValuesOnly)
                        throw new TrailLordAuthoringUnavailableException(trailPath, playerId,
                            current.LordName, exception);
                    current.EmbeddedValuesOnly = true;
                    slots.Add(current);
                    embeddedOnly.Add(playerId);
                }
            }
            return new Resolution
            {
                Slots = slots,
                InstalledSources = sources,
                ReusedPlayerIds = reused,
                EmbeddedOnlyPlayerIds = embeddedOnly,
            };
        }
    }
}
