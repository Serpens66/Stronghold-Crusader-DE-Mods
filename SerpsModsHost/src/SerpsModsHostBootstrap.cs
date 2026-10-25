using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.ViewModels;
using Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SerpsModsHost
{
    // This entry type intentionally has no APIShared fields, attributes or method
    // signatures. The dependent implementation is constructed only after the gate.
    [BepInDependency("000shcdese", "2.3.0")]
    [BepInDependency("APIShared_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("CrusaderDETweaker", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SerpsModsHostPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "SerpsMods_Serp";
        public const string PluginName = "Serps Mods";
        public const string PluginVersion = "1.0.31";
        public const bool ExtendedDataModSettingsOptOut = true;
        internal ManualLogSource HostLogger => Logger;
        private static object runtime;
        private static SerpsModsHostPlugin instance;
        private static MissingApiViewModel failure;
        private static string workshopUrl;

        private void Awake()
        {
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            instance = this;
            try
            {
                string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var info = (Dictionary<string, object>)DependencyFreeJson.Parse(File.ReadAllText(Path.Combine(root, "info.json")));
                string minimum = null;
                foreach (object item in (IList)info["Dependencies"])
                {
                    var dependency = (Dictionary<string, object>)item;
                    if ((string)dependency["GUID"] == "APIShared_Serp") minimum = (string)dependency["MinimumVersion"];
                }
                if (!Version.TryParse(minimum, out Version required)) throw new InvalidDataException("APIShared minimum is missing from info.json.");
                workshopUrl = info.TryGetValue("APISharedWorkshopUrl", out object url) ? url as string : null;
                bool loaded = Chainloader.PluginInfos.TryGetValue("APIShared_Serp", out PluginInfo api);
                string found = loaded ? api.Metadata.Version.ToString() : "not installed / nicht installiert";
                var dependencyStatus = ApiSharedDependencyPolicy.Evaluate(
                    loaded ? api.Metadata.Version : null, required, loaded ? api.Location : null, root);
                if (dependencyStatus != ApiSharedDependencyStatus.Satisfied)
                {
                    string reason;
                    switch (dependencyStatus)
                    {
                        case ApiSharedDependencyStatus.Missing:
                            reason = "APIShared was not loaded / APIShared wurde nicht geladen.";
                            break;
                        case ApiSharedDependencyStatus.BelowMinimum:
                            reason = "The loaded version is too old / Die geladene Version ist zu alt.";
                            break;
                        case ApiSharedDependencyStatus.Embedded:
                            reason = "APIShared was loaded from inside the Serps Mods package / APIShared wurde aus dem Serps-Mods-Paket geladen.";
                            break;
                        default:
                            reason = "The loaded installation path could not be validated / Der geladene Installationspfad konnte nicht geprüft werden.";
                            break;
                    }
                    string message = "Serps Mods requires / benötigt APIShared " + minimum + "+ as a separate installation / als eigenständige Installation.\n\n" +
                        "Reason / Grund: " + reason + "\n\nFound / Gefunden: " + found +
                        "\nLocation / Pfad: " + (loaded ? api.Location : "-") +
                        "\n\nSubscribe to APIShared, wait for installation, then restart the game. / APIShared abonnieren, Installation abwarten und das Spiel neu starten.\n\n" +
                        "Yes / Ja: open APIShared Workshop page / Workshop-Seite öffnen.\nNo / Nein: close this notice / Hinweis schließen.";
                    Logger.LogError(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " H009: " + message);
                    failure = new MissingApiViewModel(message);
                    CrusaderLibrary.Instance.LibraryLoaded += RegisterFailurePage;
                    if (MessageBoxW(IntPtr.Zero, message, "Serps Mods — APIShared required", 0x34) == 6) OpenWorkshop();
                    return;
                }
                // Reflection prevents the JIT from resolving any dependent implementation
                // or APIShared type before the dependency gate has succeeded.
                Type implementation = Assembly.GetExecutingAssembly().GetType("SerpsModsHost.SerpsModsHostRuntime", true);
                runtime = Activator.CreateInstance(implementation, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { this }, null);
            }
            catch (Exception ex) { Logger.LogError(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " H000: Host startup failed: " + ex); }
        }

        private static void RegisterFailurePage(CrusaderLibraryLoadContext context)
        {
            try { GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(instance, PluginName, failure,
                "ScriptExtenderUI/SerpsModsMissingApi.xaml"); }
            catch (Exception ex) { instance.HostLogger.LogError("H009: Cannot register APIShared dependency page: " + ex); }
        }

        internal static void QueueModSettingsSort() => SerpsModsHostRuntime.QueueModSettingsSort();

        internal static void OpenWorkshop()
        {
            if (!Uri.TryCreate(workshopUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != "https" ||
                uri.Host != "steamcommunity.com" || !uri.AbsolutePath.Equals("/sharedfiles/filedetails/", StringComparison.OrdinalIgnoreCase) ||
                !System.Text.RegularExpressions.Regex.IsMatch(uri.Query, @"^\?id=[1-9][0-9]*$"))
            {
                instance.HostLogger.LogError("H009: APIShared Workshop URL is not configured; this package is not ready for Steam release.");
                return;
            }
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int MessageBoxW(IntPtr owner, string message, string title, uint type);
    }

    public sealed class MissingApiViewModel : LobbyModSettingsBaseViewModel
    {
        public string Message { get; }
        public ICommand OpenWorkshopCommand { get; } = new WorkshopCommand();
        public MissingApiViewModel(string message) { Message = message; }
        private sealed class WorkshopCommand : ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => SerpsModsHostPlugin.OpenWorkshop();
        }
    }
}
