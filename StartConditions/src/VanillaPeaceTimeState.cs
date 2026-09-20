using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using System.Runtime.InteropServices;

namespace StartConditions
{
    internal sealed class VanillaPeaceTimeState
    {
        internal const int ActiveFlagRva = 0x38722DC;

        private readonly ManualLogSource log;
        private IntPtr activeFlagAddress;
        private bool available;
        private bool readFailureLogged;

        internal VanillaPeaceTimeState(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Initialize(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            available = false;
            activeFlagAddress = IntPtr.Zero;

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Start Conditions cannot safely read Vanilla's peace-time state for this CrusaderDE.dll. " +
                    "Start-troop processing uses its legacy timing.");
                return;
            }

            if (context == null || context.ModuleHandle == IntPtr.Zero ||
                context.Memory.Length <= ActiveFlagRva)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Start Conditions received an incomplete native library context. " +
                    "Start-troop processing uses its legacy timing.");
                return;
            }

            activeFlagAddress = IntPtr.Add(context.ModuleHandle, ActiveFlagRva);
            available = true;

            TryGetIsActive(out _);
        }

        internal bool TryGetIsActive(out bool active)
        {
            active = false;
            if (!available || activeFlagAddress == IntPtr.Zero)
                return false;

            try
            {
                active = Marshal.ReadByte(activeFlagAddress) != 0;
                return true;
            }
            catch (Exception ex)
            {
                available = false;
                activeFlagAddress = IntPtr.Zero;
                if (!readFailureLogged)
                {
                    readFailureLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Start Conditions could no longer read Vanilla's peace-time state. " +
                        $"Start-troop processing falls back to its legacy timing. Reason: {ex.Message}");
                }
                return false;
            }
        }
    }
}
