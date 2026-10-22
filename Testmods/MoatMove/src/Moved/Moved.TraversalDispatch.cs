extern alias BugfixesRuntime;
using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL TraversalDispatch.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal const int FastSearchNodeBudget = 16384;
    }
}
