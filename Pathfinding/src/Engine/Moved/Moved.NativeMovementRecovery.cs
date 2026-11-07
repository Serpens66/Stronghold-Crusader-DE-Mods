using APIShared;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using static Iced.Intel.AssemblerRegisters;
using Pathfinding.UnitCommands;
using static Pathfinding.UnitCommands.UnitCommandPathRuntime;

namespace Pathfinding.Engine
{
    // Moved from Pathfinding NativeMovementRecovery.cs: friendly-moat code runs only with Pathfinding.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal void ObserveNativeModeEntry(UnitMoveFrame frame, int id)
        {
            runtime.nativeModeEntries++;
            if (frame == null || frame.Args.UnitId != id) return;
            frame.NativeModeReached = true;
            if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null)
                frame.PrePortalRegion = *(short*)((byte*)unit + (0x8EC - NativeUnitSlotDataOffset));
        }
    }
}
