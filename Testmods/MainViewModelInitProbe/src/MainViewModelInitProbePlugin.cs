using APIShared;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace MainViewModelInitProbe
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInDependency(ApiSharedGuid, "0.2.0")]
    [BepInIncompatibility(SkinTestGuid)]
    [BepInIncompatibility(VirtualUnitsGuid)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MainViewModelInitProbePlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ApiSharedGuid = "APIShared_Serp";
        private const string SkinTestGuid = "SkinTest_Serp";
        private const string VirtualUnitsGuid = "VirtualUnitsPrototype_Serp";
        private const string PluginGuid = "MainViewModelInitProbe_Serp";
        private const string PluginName = "MainViewModel Init Probe";
        private const string PluginVersion = "0.1.0";

        private static readonly FieldInfo InstanceField = typeof(MainViewModel).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo ViewModelLoadedField = typeof(MainViewModel).GetField("viewModelLoaded", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo HudMainField = typeof(MainViewModel).GetField("HUDmain", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static ManualLogSource rootedLog;
        private static ConfigEntry<bool> triggerHudRegistration;
        private static bool reflectionReady;
        private static bool prematureStateLogged;
        private static bool hudReadyLogged;
        private static bool callbackFailureLogged;
        private static bool hasLastState;
        private static ProbeState lastState;

        private void Awake()
        {
            rootedLog = Logger;
            triggerHudRegistration = Config.Bind(
                "Probe",
                "TriggerHudRegistration",
                true,
                "Register one no-op APIShared HUD image override to activate its first refresh callback.");

            reflectionReady = InstanceField != null && ViewModelLoadedField != null && HudMainField != null;
            Log($"PROBE_LOADED: version={PluginVersion}, triggerHudRegistration={triggerHudRegistration.Value}, reflectionReady={reflectionReady}.");
            if (!reflectionReady)
            {
                Log($"PROBE_BLOCKED: instanceField={InstanceField != null}, viewModelLoadedField={ViewModelLoadedField != null}, hudMainField={HudMainField != null}.");
                return;
            }

            Application.onBeforeRender += ObserveAfterApiSharedRenderCallback;
            LogSnapshot("PLUGIN_AWAKE");
            APIShared.ApiShared.WhenReady(OnApiReady);
        }

        private static void OnApiReady(IApiShared api)
        {
            LogSnapshot("BEFORE_HUD_REGISTRATION");
            if (!triggerHudRegistration.Value)
            {
                Log("PASSIVE_MODE: HUD registration was intentionally skipped.");
                LogSnapshot("AFTER_HUD_REGISTRATION");
                return;
            }

            if (!api.TryGetUnitHudPresentation(PluginGuid, out IUnitHudPresentationCapability capability, out NativeCapabilityDiagnostic diagnostic))
            {
                LogDiagnostic("HUD_CAPABILITY_UNAVAILABLE", diagnostic);
                return;
            }

            var definition = new UnitHudImageOverrideDefinition(
                "main-view-model-init-probe",
                UnitHudImageSlot.UIBuildingsO011);
            bool registered = capability.TryRegisterImageOverride(definition, ResolveNoOpImage, out diagnostic);
            LogDiagnostic(registered ? "HUD_REGISTRATION_ACCEPTED" : "HUD_REGISTRATION_REJECTED", diagnostic);
            LogSnapshot("AFTER_HUD_REGISTRATION");
        }

        private static Noesis.ImageSource ResolveNoOpImage(UnitHudImageOverrideContext context) => null;

        private static void ObserveAfterApiSharedRenderCallback()
        {
            try
            {
                ProbeState state = CaptureState();
                if (!hasLastState || !state.Equals(lastState))
                {
                    LogState("RENDER_STATE_CHANGED", state);
                    lastState = state;
                    hasLastState = true;
                }
                if (!prematureStateLogged && state.InstancePresent && state.ViewModelLoaded && !state.HudMainPresent)
                {
                    prematureStateLogged = true;
                    LogState("PREMATURE_VIEWMODEL_OBSERVED", state);
                }
                if (!hudReadyLogged && state.InstancePresent && state.HudMainPresent)
                {
                    hudReadyLogged = true;
                    LogState("HUDMAIN_READY", state);
                }
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;
                callbackFailureLogged = true;
                Log($"PROBE_CALLBACK_FAILED: {ex}");
            }
        }

        private static void LogSnapshot(string marker)
        {
            try { LogState(marker, CaptureState()); }
            catch (Exception ex) { Log($"{marker}: capture failed: {ex}"); }
        }

        private static ProbeState CaptureState()
        {
            object instance = InstanceField.GetValue(null);
            bool loaded = (bool)ViewModelLoadedField.GetValue(null);
            object hudMain = instance == null ? null : HudMainField.GetValue(instance);
            return new ProbeState(instance != null, loaded, hudMain != null);
        }

        private static void LogState(string marker, ProbeState state) =>
            Log($"{marker}: frame={Time.frameCount}, thread={Thread.CurrentThread.ManagedThreadId}, instance={state.InstancePresent}, viewModelLoaded={state.ViewModelLoaded}, hudMain={state.HudMainPresent}.");

        private static void LogDiagnostic(string marker, NativeCapabilityDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                Log(marker + ": diagnostic=null.");
                return;
            }
            Log($"{marker}: capability={diagnostic.CapabilityId}, state={diagnostic.State}, reason={diagnostic.Reason}");
        }

        private static void Log(string message) =>
            rootedLog?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private readonly struct ProbeState : IEquatable<ProbeState>
        {
            internal ProbeState(bool instancePresent, bool viewModelLoaded, bool hudMainPresent)
            {
                InstancePresent = instancePresent;
                ViewModelLoaded = viewModelLoaded;
                HudMainPresent = hudMainPresent;
            }

            internal bool InstancePresent { get; }
            internal bool ViewModelLoaded { get; }
            internal bool HudMainPresent { get; }

            public bool Equals(ProbeState other) =>
                InstancePresent == other.InstancePresent &&
                ViewModelLoaded == other.ViewModelLoaded &&
                HudMainPresent == other.HudMainPresent;
        }
    }
}
