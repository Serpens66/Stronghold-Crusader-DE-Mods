using System.Threading;

namespace BugfixesAndQoL
{
    // Identifies one managed EngineInterface.run call on the thread executing it, between entering
    // the engine lock and publishing the tick (VanillaPerformance.EnginePresentationBridge marks both).
    // Native audit FBCB9319: run calls DLL_RunTick exactly once, which runs the game tick 0xCDE60 at
    // most once; its building dispatcher 0xC60F0 updates every building exactly once. Native work
    // outside run (load/map initialization, editor) never has a current scope.
    internal static class SimulationRunScope
    {
        private static long next;
        [System.ThreadStatic] private static long current;

        // 0 outside a marked run on this thread; otherwise unique for the run.
        internal static long Current => current;

        internal static void Enter() => current = Interlocked.Increment(ref next);

        internal static void Exit() => current = 0;
    }
}
