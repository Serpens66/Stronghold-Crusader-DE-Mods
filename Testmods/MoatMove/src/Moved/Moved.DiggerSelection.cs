extern alias BugfixesRuntime;
using BepInEx.Logging;
using APIShared;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.DiggerSelection.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogDiggerDecision(
            string source, int unitId, GameUnit* unit, int targetX, int targetY,
            bool accepted, bool friendlyMoatRequired = false)
        {
#if SERP_DEVDIAG
            if (unit == null)
                return;
            string key = $"{mapEpoch}:{source}:{unit->r_UnitChimp}:{unit->r_AI_LastIssuedTribeCommand}:" +
                $"{targetX}:{targetY}:{accepted}:{friendlyMoatRequired}";
            if (!loggedDiggerDecisions.Add(key))
                return;
            LogCommandDiagnostic(
                $"stage=vanilla-digger source={source} unit={unitId} " +
                $"type={unit->r_UnitChimp} command=" +
                $"{(TribeAICommand)unit->r_AI_LastIssuedTribeCommand} " +
                $"target=({targetX},{targetY}) accepted={accepted} " +
                $"friendlyMoatRequired={friendlyMoatRequired}");
#endif
        }
    }
}
