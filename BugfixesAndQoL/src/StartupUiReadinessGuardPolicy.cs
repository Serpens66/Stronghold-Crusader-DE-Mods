using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class StartupUiReadinessGuardState
    {
        private int complete;

        internal bool IsComplete => Volatile.Read(ref complete) != 0;

        internal void MarkComplete() => Volatile.Write(ref complete, 1);
    }
}
