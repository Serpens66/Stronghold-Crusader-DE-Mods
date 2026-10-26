// Only external boundaries are doubled. The state machine and hook are linked production sources.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
    }
}

namespace Shared
{
    public static class DebugLogHelper
    {
        public static bool DiagnosticsEnabled => false;
        public static void LogWarningLimited(BepInEx.Logging.ManualLogSource log, string key, string text) => log.Warnings.Add(text);
        public static void LogErrorLimited(BepInEx.Logging.ManualLogSource log, string key, string text) => log.Errors.Add(text);
        public static void LogDebugLimited(BepInEx.Logging.ManualLogSource log, string key, string text) { }
    }
}

namespace MonoMod.RuntimeDetour
{
    public sealed class Hook
    {
        private static readonly Dictionary<string, Delegate> callbacks = new Dictionary<string, Delegate>();
        private readonly MethodInfo method;
        public Hook(MethodInfo method, Delegate callback)
        {
            this.method = method;
            callbacks.Add(method.DeclaringType.FullName + "." + method.Name, callback);
        }
        public T GenerateTrampoline<T>() where T : class => Delegate.CreateDelegate(typeof(T),
            method.DeclaringType.GetMethod(method.Name + "Original")) as T;
        public static void Invoke(Type type, string method, params object[] arguments) =>
            callbacks[type.FullName + "." + method].DynamicInvoke(arguments);
        // Reset the simulated process; no real hook is installed or removed by this harness.
        public static void ResetProcess() => callbacks.Clear();
        public void Undo() { }
        public void Dispose() { }
    }
}

namespace UnityEngine
{
    public enum FullScreenMode { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
    public struct Resolution { public int width, height, refreshRate; }
    public static class Screen
    {
        public static int width, height;
        public static FullScreenMode fullScreenMode;
        public static Resolution currentResolution;
        public static Resolution[] resolutions;
        public static readonly List<ResolutionRequest> Requests = new List<ResolutionRequest>();
        public static void SetResolution(int width, int height, FullScreenMode mode, int refresh) =>
            Requests.Add(new ResolutionRequest { Width = width, Height = height, Mode = mode, Refresh = refresh });
        public struct ResolutionRequest { public int Width, Height, Refresh; public FullScreenMode Mode; }
    }
    public static class Time { public static int frameCount; }
    public static class Application
    {
        public static bool isFocused;
        public static event Action<bool> focusChanged;
        public static event Action onBeforeRender;
        public static int RenderObservers => onBeforeRender?.GetInvocationList().Length ?? 0;
        public static void Focus(bool focused) { isFocused = focused; focusChanged?.Invoke(focused); }
        public static void Render(bool nextFrame = true) { if (nextFrame) Time.frameCount++; onBeforeRender?.Invoke(); }
        public static void ResetProcess() { focusChanged = null; onBeforeRender = null; }
    }
}

public static class ConfigSettings
{
    public static int Settings_LastWindowWidth, Settings_LastWindowHeight, Settings_LastFullscreenWidth,
        Settings_LastFullscreenHeight, Settings_LastFullscreenRefresh, Settings_LastFullscreenType;
    public static int LoadCalls, SaveCalls;
    public static bool SavedOnlyWhenAlreadyExists;
    internal static BugfixesAndQoL.DisplaySettingsSnapshot Saved;
    public static void LoadSettings() => MonoMod.RuntimeDetour.Hook.Invoke(typeof(ConfigSettings), nameof(LoadSettings));
    public static void SaveSettings(bool onlyWhenAlreadyExists = false) =>
        MonoMod.RuntimeDetour.Hook.Invoke(typeof(ConfigSettings), nameof(SaveSettings), onlyWhenAlreadyExists);
    public static void LoadSettingsOriginal() => LoadCalls++;
    public static void SaveSettingsOriginal(bool onlyWhenAlreadyExists)
    {
        SaveCalls++;
        SavedOnlyWhenAlreadyExists = onlyWhenAlreadyExists;
        Saved = new BugfixesAndQoL.DisplaySettingsSnapshot(Settings_LastWindowWidth, Settings_LastWindowHeight,
            Settings_LastFullscreenWidth, Settings_LastFullscreenHeight, Settings_LastFullscreenRefresh, Settings_LastFullscreenType);
    }
}

namespace CrusaderDE
{
    public sealed class HUD_Options
    {
        public Action<int> OriginalAction;
        public int OriginalCalls;
        public void ButtonClicked(int parameter) =>
            MonoMod.RuntimeDetour.Hook.Invoke(typeof(HUD_Options), nameof(ButtonClicked), this, parameter);
        public void ButtonClickedOriginal(int parameter) { OriginalCalls++; OriginalAction?.Invoke(parameter); }
    }
}

namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLViewModel
    {
        public bool PreserveDisplayResolution { get; set; } = true;
        public bool EnableClientFeatures { get; set; } = true;
        public event Action<string> SettingChanged;
        public void Notify(string name) => SettingChanged?.Invoke(name);
    }
}
