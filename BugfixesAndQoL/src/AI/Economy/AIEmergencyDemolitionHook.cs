using System;
using System.Threading;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;

namespace BugfixesAndQoL
{
    // Static ownership survives the early destruction of the BepInEx plugin component.
    internal static class AIEmergencyDemolitionHook
    {
        private const string EmergencyDemolitionFunctionPattern = AIEmergencyDemolitionNativeContract.Signature;
        private static NativeDetour<AIEmergencyDemolitionDispatch.DemolitionDelegate> permanentHook;
        private static AIEmergencyDemolitionDispatch dispatch;
        private static readonly AIEmergencyDemolitionDispatch.DemolitionDelegate callback = Invoke;
        private static int attempted, ready, callbackError;

        internal static void Install(ManualLogSource log, BugfixesAndQoLViewModel settings,
            IntPtr module, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            NativeDetour<AIEmergencyDemolitionDispatch.DemolitionDelegate> candidate = null;
            bool published = false;
            try
            {
                int rva = BfqolNativeCache.Current.ResolveUnique(memory, EmergencyDemolitionFunctionPattern,
                    AIEmergencyDemolitionNativeContract.FunctionRva,
                    "AI emergency-demolition function", log).Rva;
                AIEmergencyDemolitionNativeContract.ValidateImage(memory, rva);
                if (!referenceHashMatches) AIEmergencyDemolitionNativeContract.ValidateIncomingBranches(memory, rva);
                ulong target = unchecked((ulong)module.ToInt64()) + (uint)rva;
                AIEmergencyDemolitionNativeContract.ValidateEntry(target);
                var request = new DetourRequest<AIEmergencyDemolitionDispatch.DemolitionDelegate>
                {
                    Name = "AI emergency demolition", TargetAddress = target, Callback = callback
                };
                candidate = (NativeDetour<AIEmergencyDemolitionDispatch.DemolitionDelegate>)
                    HookInstallPhase.Backend(AIEmergencyDemolitionNativeContract.Backend()).CreateDetour(in request);
                AIEmergencyDemolitionNativeContract.Validate(candidate, target, false);
                permanentHook = candidate;
                dispatch = new AIEmergencyDemolitionDispatch(
                    () => Volatile.Read(ref ready) != 0 && settings.EnableMod && settings.PreventEmergencyDemolition,
                    candidate.Original,
                    error =>
                    {
                        Volatile.Write(ref ready, 0);
                        if (Interlocked.Exchange(ref callbackError, 1) == 0)
                            Shared.DebugLogHelper.LogError(log, "AI_EMERGENCY_DEMOLITION_CALLBACK_DISABLED: " + error);
                    });
                candidate.Enable();
                AIEmergencyDemolitionNativeContract.Validate(candidate, target, true);
                published = true;
                Volatile.Write(ref ready, 1);
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_EMERGENCY_DEMOLITION_READY: rva=0x{rva:X}, span=6, scheme=Indirect; only demolition and its refund are gated.");
            }
            catch (Exception error)
            {
                Volatile.Write(ref ready, 0);
                if (!published)
                {
                    candidate?.Dispose(); // Only the unpublished, failed initialization candidate.
                    permanentHook = null;
                    dispatch = null;
                }
                Shared.DebugLogHelper.LogError(log, "AI_EMERGENCY_DEMOLITION_UNAVAILABLE: Vanilla retained; " + error);
            }
        }

        private static void Invoke(IntPtr manager, int playerId, int category, byte specialFlag) =>
            dispatch.Invoke(manager, playerId, category, specialFlag);
    }
}
