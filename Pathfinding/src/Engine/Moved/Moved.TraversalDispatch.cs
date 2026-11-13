using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using Pathfinding.UnitCommands;
using static Pathfinding.UnitCommands.UnitCommandPathRuntime;

namespace Pathfinding.Engine
{
    // Moved from Pathfinding TraversalDispatch.cs: friendly-moat code runs only with Pathfinding.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal const int FastSearchNodeBudget = 16384;
    }
}
