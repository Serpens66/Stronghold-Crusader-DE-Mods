using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;

namespace ThirdPartyMod
{
    [BepInPlugin(Guid, "APIShared Example", "1.0.0")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.4.12")]
    public sealed class ExamplePlugin : BaseUnityPlugin
    {
        public const string Guid = "Example.Author.APISharedDemo";
        private static readonly ModApiClient Api = ApiShared.ForMod(Guid);
        private static readonly ExampleSettings Settings = new ExampleSettings();
        private static ManualLogSource log;

        private void Awake()
        {
            log = Logger;
            MissionExample.Register(Api, Log);
            Api.WhenReady(OnReady);
        }

        private void OnReady(ModApiClient client)
        {
            // The publisher roots this callback across SHCDE startup cleanup.
            APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                this, log, "APIShared Example", Settings, "ScriptExtenderUI/APISharedExample.xaml");
            if (client.TryGetUnitHudPresentation(out var hud, out var diagnostic))
                HudExample.Register(hud, Log);
            else Log(diagnostic.Reason);
        }

        internal static void Log(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
