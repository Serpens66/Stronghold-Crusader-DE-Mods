using System;
using System.Collections.Generic;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
namespace Pathfinding.UnitCommands
{
    /// <summary>Process-owned native command dispatcher. Settings change logical policy only.</summary>
    internal static class UnitCommandPathAPI
    {
        private static readonly object Sync = new object();
        private static readonly List<UnitCommandPathRuntime> Candidates = new List<UnitCommandPathRuntime>();
#pragma warning disable CS0649 // Assigned only in DEVDIAG builds.
        internal static Func<string> AssassinReconstructionRelaxation;
#pragma warning restore CS0649
        internal static UnitCommandPathRuntime Runtime { get; private set; }
        internal static UnitCommandTraversalProvider Traversal { get; private set; }
        internal static bool FormationDispatchActive => Runtime?.formationRuntime?.IsDispatching == true;
        internal static void RootCandidate(UnitCommandPathRuntime runtime) { lock (Sync) Candidates.Add(runtime); }
        internal static UnitCommandPathRuntime RegisterCommands(ManualLogSource log, IUnitCommandSettings settings,
            CrusaderLibraryLoadContext context, bool referenceHashMatches, string publisherGuid)
        {
            if (string.IsNullOrWhiteSpace(publisherGuid))
                throw new ArgumentException("A route publisher GUID is required.", nameof(publisherGuid));
            lock (Sync)
            {
                if (Runtime != null) return Runtime;
                Runtime = new UnitCommandPathRuntime(log, settings, context, referenceHashMatches);
                if (Runtime.weightedMoatRoutePlanner != null)
                    Runtime.weightedMoatRoutePlanner.PublisherGuid = publisherGuid;
                return Runtime;
            }
        }
        internal static void RegisterTraversal(UnitCommandTraversalProvider provider)
        {
            lock (Sync)
            {
                if (Runtime == null) throw new InvalidOperationException("Pathfinding command provider is unavailable.");
                if (Traversal != null) throw new InvalidOperationException("A moat traversal provider is already registered.");
                Traversal = provider;
                Runtime.weightedMoatRoutePlanner.KernelFactory = provider.CreateKernel;
            }
        }
    }
}
