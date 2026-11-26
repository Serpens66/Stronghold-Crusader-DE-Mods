using System;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed class PlacementFeedback
    {
        [ThreadStatic] private static PlacementFeedback active;
        private bool soundPlayed, voicePlayed;

        // Enter only while submitting one of this gesture's synthetic clicks, never during a quote.
        internal static Scope Enter(PlacementFeedback gesture)
        {
            var scope = new Scope(active);
            active = gesture;
            return scope;
        }

        internal static bool AllowSound()
        {
            if (active == null) return true;
            if (active.soundPlayed) return false;
            active.soundPlayed = true;
            return true;
        }

        internal static bool AllowVoice()
        {
            if (active == null) return true;
            if (active.voicePlayed) return false;
            active.voicePlayed = true;
            return true;
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly PlacementFeedback previous;
            internal Scope(PlacementFeedback previous) => this.previous = previous;
            public void Dispose() => active = previous;
        }
    }
}
