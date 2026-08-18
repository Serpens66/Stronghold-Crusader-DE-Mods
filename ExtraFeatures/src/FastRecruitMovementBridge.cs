// Feature: Connect Fast Recruit Rally Movement to the optional Bugfixes and QoL hook owner.
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;

namespace ExtraFeatures
{
    internal interface IMovementCadenceServices
    {
        bool TryGetNativeRunningSpeedBonus(eChimps unitType, bool improvedSpearmen, out ushort runningSpeedBonus);
        bool TryGetNativeRunningState(eChimps unitType, uint currentState, out uint runningState);
    }

    internal sealed class FastRecruitMovementBridge : IMovementCadenceServices, IDisposable
    {
        private const string BugfixPluginGuid = "BugfixesAndQoL_Serp";
        private const string IntegrationTypeName = "BugfixesAndQoL.MovementCadenceIntegration";

        private delegate bool RegisterDelegate(Action<IntPtr> applyMaximumSpeed, Func<IntPtr, bool> tryApplyCadence);
        private delegate void UnregisterDelegate(Action<IntPtr> applyMaximumSpeed);
        private delegate bool TrySpeedDelegate(int unitType, bool improvedSpearmen, out ushort runningSpeedBonus);
        private delegate bool TryStateDelegate(int unitType, uint currentState, out uint runningState);

        private readonly ManualLogSource log;
        private readonly Action<IntPtr> applyMaximumSpeedCallback;
        private readonly Func<IntPtr, bool> tryApplyCadenceCallback;
        private readonly UnregisterDelegate unregister;
        private readonly TrySpeedDelegate trySpeed;
        private readonly TryStateDelegate tryState;
        private readonly FastRecruitRallyMovementRuntime runtime;
        private bool registered;

        public FastRecruitMovementBridge(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!Chainloader.PluginInfos.TryGetValue(BugfixPluginGuid, out var pluginInfo))
            {
                LogError("Fast Recruit Rally Movement was disabled because Bugfixes and QoL is not installed.");
                return;
            }

            try
            {
                Type integrationType = pluginInfo.Instance.GetType().Assembly.GetType(IntegrationTypeName, true);
                RegisterDelegate register = CreateDelegate<RegisterDelegate>(integrationType, "RegisterFastRecruitCallbacks");
                unregister = CreateDelegate<UnregisterDelegate>(integrationType, "UnregisterFastRecruitCallbacks");
                trySpeed = CreateDelegate<TrySpeedDelegate>(integrationType, "TryGetNativeRunningSpeedBonus");
                tryState = CreateDelegate<TryStateDelegate>(integrationType, "TryGetNativeRunningState");

                runtime = new FastRecruitRallyMovementRuntime(log, this);
                applyMaximumSpeedCallback = runtime.ApplyMaximumSpeed;
                tryApplyCadenceCallback = runtime.TryApplyRunningCadence;
                registered = register(applyMaximumSpeedCallback, tryApplyCadenceCallback);
                if (!registered)
                {
                    LogError("Fast Recruit Rally Movement was disabled because the shared Bugfixes and QoL movement hook could not be initialized.");
                    unregister(applyMaximumSpeedCallback);
                    runtime.Dispose();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (unregister != null && applyMaximumSpeedCallback != null)
                        unregister(applyMaximumSpeedCallback);
                }
                catch
                {
                    // Preserve the original integration failure in the log.
                }
                LogError($"Fast Recruit Rally Movement integration failed and only this feature was disabled: {ex}");
                runtime?.Dispose();
            }
        }

        public bool IsActive => registered;

        public bool TryGetNativeRunningSpeedBonus(eChimps unitType, bool improvedSpearmen, out ushort runningSpeedBonus)
        {
            runningSpeedBonus = 0;
            return registered && trySpeed((int)unitType, improvedSpearmen, out runningSpeedBonus);
        }

        public bool TryGetNativeRunningState(eChimps unitType, uint currentState, out uint runningState)
        {
            runningState = currentState;
            return registered && tryState((int)unitType, currentState, out runningState);
        }

        public void Dispose()
        {
            if (registered)
                unregister(applyMaximumSpeedCallback);
            registered = false;
            runtime?.Dispose();
        }

        private static T CreateDelegate<T>(Type type, string methodName) where T : Delegate
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);
            return (T)method.CreateDelegate(typeof(T));
        }

        private void LogError(string message)
        {
            log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Extra Features {message}");
        }
    }
}
