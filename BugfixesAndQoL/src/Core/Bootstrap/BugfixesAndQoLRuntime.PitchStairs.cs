using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime
    {
        private static PitchStairs.PitchStairsRuntime processPitchStairsRuntime;

        private void InitializePitchStairs(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            if (processPitchStairsRuntime != null) return;
            var candidate = new PitchStairs.PitchStairsRuntime(log, settings);
            // Root before subscribing or publishing any executable entry. Even a failed install
            // must not leave a published trampoline pointing at collected/freed state.
            processPitchStairsRuntime = candidate;
            candidate.SubscribeLifecycle();
            candidate.Install(context, referenceHashMatches);
        }
    }
}
