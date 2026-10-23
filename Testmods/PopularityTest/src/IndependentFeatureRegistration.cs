using System;

namespace PopularityTest
{
    // Optional setup runs before required registration, whose replay may already
    // apply settings. Optional failures (including their logger) cannot block it.
    internal static class IndependentFeatureRegistration
    {
        internal static void Register(Action optional, Action required, Action<Exception> report)
        {
            try { optional(); }
            catch (Exception ex) { try { report(ex); } catch { } }
            required();
        }
    }
}
