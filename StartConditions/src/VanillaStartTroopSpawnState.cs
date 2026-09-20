using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace StartConditions
{
    internal sealed class VanillaStartTroopSpawnState
    {
        private readonly ManualLogSource log;
        private readonly VanillaStartTroopCompletionReader reader =
            new VanillaStartTroopCompletionReader();
        private bool readFailureLogged;

        internal VanillaStartTroopSpawnState(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Initialize(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            int memoryLength = context == null ? 0 : context.Memory.Length;
            if (!reader.TryInitialize(
                context?.ModuleHandle ?? IntPtr.Zero,
                memoryLength,
                referenceHashMatches))
            {
                string reason = referenceHashMatches
                    ? "an incomplete native library context"
                    : "an unrecognized CrusaderDE.dll";
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Start Conditions cannot safely read Vanilla's start-troop completion state because of {reason}. " +
                    "Start-troop multiplication uses its legacy timing.");
                return;
            }

            TryGetIsComplete(out _);
        }

        internal bool TryGetIsComplete(out bool complete)
        {
            complete = false;
            if (!reader.TryGetIsComplete(out complete, out int value, out Exception failure))
            {
                if (failure != null)
                    LogReadFailureOnce($"native read failed: {failure.Message}");
                else if (value < StartTroopSpawnCompletionContract.CompleteValue)
                    LogReadFailureOnce($"unexpected completion value {value}");
                return false;
            }

            return true;
        }

        private void LogReadFailureOnce(string reason)
        {
            if (readFailureLogged)
                return;

            readFailureLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                "Start Conditions could no longer use Vanilla's start-troop completion state. " +
                $"Start-troop multiplication falls back to its legacy timing. Reason: {reason}");
        }
    }
}
