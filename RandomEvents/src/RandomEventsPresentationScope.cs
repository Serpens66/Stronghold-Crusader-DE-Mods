using System;

namespace RandomEvents
{
    internal static class RandomEventsPresentationScope
    {
        [ThreadStatic]
        private static int suppressionDepth;
        [ThreadStatic]
        private static int suppressedPresentationCalls;
        [ThreadStatic]
        private static int suppressedActionPointCalls;

        public static bool IsSuppressed => suppressionDepth > 0;

        public static bool ShouldSuppress(int targetPlayerId, int localPlayerId) =>
            targetPlayerId != localPlayerId;

        public static void RecordSuppressedPresentation() => suppressedPresentationCalls++;
        public static void RecordSuppressedActionPoint() => suppressedActionPointCalls++;

        public static void GetSuppressedCallCounts(out int presentationCalls, out int actionPointCalls)
        {
            presentationCalls = suppressedPresentationCalls;
            actionPointCalls = suppressedActionPointCalls;
        }

        public static IDisposable Begin(int targetPlayerId, int localPlayerId)
        {
            if (!ShouldSuppress(targetPlayerId, localPlayerId))
                return NoopScope.Instance;

            suppressionDepth++;
            return new SuppressionScope();
        }

        private sealed class SuppressionScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                if (suppressionDepth > 0)
                    suppressionDepth--;
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new NoopScope();
            public void Dispose() { }
        }
    }
}
