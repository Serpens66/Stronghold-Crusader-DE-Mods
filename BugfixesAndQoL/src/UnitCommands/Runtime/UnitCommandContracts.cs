using System;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("MoatMove")]
namespace BugfixesAndQoL.UnitCommands
{
    /// <summary>Host-synchronized command policy supplied by the main mod.</summary>
    internal interface IUnitCommandSettings
    {
        /// <summary>Master activation.</summary>
        bool EnableMod { get; }
        /// <summary>Improve human commands using existing native paths.</summary>
        bool EnableImprovedManualUnitCommands { get; }
        /// <summary>Independent moat work policy.</summary>
        bool EnableImprovedMoatFilling { get; }
        /// <summary>Independent ladder attack policy.</summary>
        bool EnableLadderAttackPathfindingFix { get; }
        /// <summary>Independent formation policy.</summary>
        bool EnableMoveFormationEnhancements { get; }
    }
    internal interface IMoatSearchKernel
    {
        long Expanded { get; }
        long Searches { get; }
        long FieldHits { get; }
        int CachedFields { get; }
        bool LastSearchBudgetExceeded { get; }
        void Invalidate();
        int Direction(int from, int to);
        bool Search(int start, int destination, long ground, long moat, int maximumEdges,
            bool requireMoat, bool excludeStructures, MoatSearchLimit[] limits,
            bool shareField, out int[] path, int maximumExpanded = int.MaxValue);
    }
}
