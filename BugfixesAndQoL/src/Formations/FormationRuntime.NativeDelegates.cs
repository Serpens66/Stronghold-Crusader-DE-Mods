using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private delegate int EngineRunDelegate(bool mpFrameSkip);
        private delegate void CameraUpdateDelegate(CameraControls2D self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FormationSlotDelegate(IntPtr manager, int spacing, int x, int y);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AssassinFormationSlotDelegate(IntPtr manager, int spacing, int x, int y);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate long CommonGroupMoveDelegate(
            IntPtr manager, int tribeId, short x, short y, short patrol, int newOrder);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetGroupUnitIdDelegate(IntPtr tribeManager, int tribeId, int ordinal);
    }
}
