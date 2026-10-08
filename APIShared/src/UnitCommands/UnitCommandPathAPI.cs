using System;
using System.Collections.Generic;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
namespace APIShared.UnitCommands
{
    /// <summary>Process-owned native command dispatcher. Settings change logical policy only.</summary>
    internal static class UnitCommandPathAPI
    {
        private static readonly object Sync = new object();
        private static readonly List<UnitCommandPathRuntime> Candidates = new List<UnitCommandPathRuntime>();
        internal static Func<string> AssassinReconstructionRelaxation;
        internal static UnitCommandPathRuntime Runtime { get; private set; }
        internal static UnitCommandTraversalProvider Traversal { get; private set; }
        internal static bool FormationDispatchActive => Runtime?.formationRuntime?.IsDispatching == true;
        internal static LargeMoveTargetMarkerRenderer MoveMarkers { get; private set; }
        internal static LargeMoveTargetMarkerRenderer GetMoveMarkers(ManualLogSource log, Func<bool> enabled)
        {
            lock (Sync)
                return MoveMarkers ?? (MoveMarkers = new LargeMoveTargetMarkerRenderer(log, enabled));
        }
        internal static void RootCandidate(UnitCommandPathRuntime runtime) { lock (Sync) Candidates.Add(runtime); }
        internal static UnitCommandPathRuntime RegisterCommands(ManualLogSource log, IUnitCommandSettings settings,
            CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            lock (Sync)
            {
                if (Runtime != null) return Runtime;
                Runtime = new UnitCommandPathRuntime(log, settings, context, referenceHashMatches);
                return Runtime;
            }
        }
        internal static void RegisterTraversal(UnitCommandTraversalProvider provider)
        {
            lock (Sync)
            {
                if (Runtime == null) throw new InvalidOperationException("BugfixesAndQoL command provider is unavailable.");
                if (Traversal != null) throw new InvalidOperationException("A moat traversal provider is already registered.");
                Traversal = provider;
                Runtime.weightedMoatRoutePlanner.KernelFactory = provider.CreateKernel;
            }
        }
    }
}
