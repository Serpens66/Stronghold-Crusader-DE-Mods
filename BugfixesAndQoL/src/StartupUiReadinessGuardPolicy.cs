using System;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class StartupUiReadinessGuardState
    {
        private int complete;

        internal bool IsComplete => Volatile.Read(ref complete) != 0;

        internal void MarkComplete() => Volatile.Write(ref complete, 1);

        internal bool Evaluate(bool viewModelLoaded, Func<bool> readinessProbe)
        {
            if (IsComplete)
                return viewModelLoaded;

            if (!viewModelLoaded)
                return false;

            if (readinessProbe == null)
                throw new ArgumentNullException(nameof(readinessProbe));

            if (!readinessProbe())
                return false;

            MarkComplete();
            return true;
        }
    }
}
