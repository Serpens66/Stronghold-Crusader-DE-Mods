using System;
using System.Linq;

namespace ExtendedData
{
    internal sealed class LordSaveOwnership
    {
        internal long? InitializingSession { get; private set; }
        internal bool MissionOwned { get; private set; }
        internal void Initializing(long session) { InitializingSession = session; }
        internal bool Started(long session)
        {
            if (InitializingSession != session) return false;
            MissionOwned = true; return true;
        }
        internal bool FailedBeforeStarted(long session) => InitializingSession == session && !MissionOwned;
        internal static bool IsCurrentObservation(string operation, long revision, string currentOperation, long currentRevision)
            => operation != null && operation == currentOperation && revision == currentRevision;
    }

    // An authenticated host may rebase session/schema and complete legacy missing
    // source history. It may never change values already authored into this file.
    internal static class LordSaveAuthorProof
    {
        internal static bool AllowsInheritedDigest(string authored, string current) => authored == null || authored == current;
        internal static bool Matches(LordDataSnapshot author, LordDataSnapshot received, Func<string, string> normalize)
        {
            if (author == null || received == null || received.FixesInstalled != author.FixesInstalled ||
                received.Slots.Count != author.Slots.Count) return false;
            foreach (LordDataSlot original in author.Slots)
            {
                var value = received.GetSlot(original.PlayerId);
                if (value == null || original.LordType.HasValue && original.LordType != value.LordType ||
                    original.LordName != value.LordName || original.ConfigName != value.ConfigName || original.ConfigChecksum != value.ConfigChecksum ||
                    original.ModLordJson != value.ModLordJson || normalize(original.FixesJson) != normalize(value.FixesJson) ||
                    Shared.DependencyFreeJson.Serialize(LordPresentationData.Encode(original.Presentation)) !=
                    Shared.DependencyFreeJson.Serialize(LordPresentationData.Encode(value.Presentation))) return false;
                string Sources(LordDataSlot slot) => FixesPreferenceSources.Digest(slot.FixesSources?.Select(source =>
                    new FixesPreferenceSource { CustomName = source.CustomName, LordType = source.LordType, Json = normalize(source.Json) }).ToArray());
                if (original.FixesSources != null && Sources(original) != Sources(value)) return false;
                FixesPreferenceSources.Validate(value.FixesSources);
                if (received.FixesInstalled && value.FixesSources == null) return false;
            }
            return true;
        }
    }
}
