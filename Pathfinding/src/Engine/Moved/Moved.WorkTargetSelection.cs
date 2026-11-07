using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Pathfinding.UnitCommands;
using static Pathfinding.UnitCommands.UnitCommandPathRuntime;

namespace Pathfinding.Engine
{
    // Moved from Pathfinding UnitCommandPathRuntime.WorkTargetSelection.cs: friendly-moat code runs only with Pathfinding.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal bool TryCreatePendingDigMoatTarget(
            MoatWorkSelectionScope scope,
            int moatId,
            out PendingDigMoatTarget pending)
        {
            pending = runtime.TraversalEnabled ? runtime.Traversal.CreatePendingDigMoatTarget(scope, moatId) : null;
            return pending != null;
        }
    }
}
