using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
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

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal static string FormatCostProfile(WeightedMovementCostProfile profile) =>
            $"speed={profile.CurrentSpeed}/effective={profile.CurrentSpeed2}/" +
            $"bonus={profile.SpeedBonus}/additionalSubsteps={profile.AdditionalSubsteps}/" +
            $"extraDelay={profile.ExtraDelay}/phase={profile.MoatPhase}/" +
            $"terrainPenalty={profile.CurrentTerrainPenalty}/" +
            $"normalized={profile.NormalizedDelay}/progress={profile.CadenceProgress}";

        internal static int EncodeDirectionDelta(int dx, int dy)
        {
            if (dx == 0 && dy == -1) return 0;
            if (dx == 1 && dy == -1) return 1;
            if (dx == 1 && dy == 0) return 2;
            if (dx == 1 && dy == 1) return 3;
            if (dx == 0 && dy == 1) return 4;
            if (dx == -1 && dy == 1) return 5;
            if (dx == -1 && dy == 0) return 6;
            if (dx == -1 && dy == -1) return 7;
            return -1;
        }

    }
}
