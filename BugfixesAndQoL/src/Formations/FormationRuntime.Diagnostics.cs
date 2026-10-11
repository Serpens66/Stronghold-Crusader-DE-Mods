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
        private void DisableAfterNativeFailure(string stage, Exception exception)
        {
            lock (stateSync)
            {
                ClearUnitAssignmentFrames(null);
                pendingCommand = null;
                activeCommand = null;
                commonGroupCommand = null;
                unitFallbackAttempt = null;
                failed = true;
            }
            try { menuViewModel.CloseMenu(); } catch { }
            try { APIShared.Internal.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.RefreshHostState); } catch { }
            try
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Diagnostics.cs:44", $"Formation native assignment failed open at {stage}; " +
                    $"future formation commands are disabled: {exception}");
            }
            catch
            {
            }
        }

        private void LogDebugNoThrow(string message)
        {
            APIShared.Internal.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Diagnostics.cs:60", message);
                }
                catch
                {
                }
            });
        }

        private void LogWarningNoThrow(string message)
        {
            APIShared.Internal.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Diagnostics.cs:74", message);
                }
                catch
                {
                }
            });
        }

        private void LogErrorNoThrow(string message)
        {
            APIShared.Internal.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Diagnostics.cs:88", message);
                }
                catch
                {
                }
            });
        }
    }
}
