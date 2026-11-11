using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.PitchMoatLines
{
    // Process-rooted runtime; the plugin GameObject is destroyed during startup.
    internal static class PitchMoatLinesFeature
    {
        internal const string CompatibilityToken = "pitch-moat-lines-v1";
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource log;
        private static PitchLineNetwork network;
        private static bool attempted;
        private static volatile bool multiplayerCompatible;
        internal static bool MultiplayerCompatible => multiplayerCompatible;

        internal static void Configure(ManualLogSource logger, BugfixesAndQoLViewModel model)
        {
            log = logger;
            settings = model;
        }

        internal static void Install(CrusaderLibraryLoadContext context)
        {
            if (attempted) return;
            attempted = true;
            var api = ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid);
            try
            {
                // Packet IDs depend on registration order. Register on every peer,
                // including installations where a native contract subsequently fails.
                network = new PitchLineNetwork(api, () => multiplayerCompatible, log);
                ManagedBoundary.Install(log);
                if (!NativePlacement.TryCreate(context, log, out var native))
                    throw new InvalidOperationException("Native line placement contract unavailable.");
                network.SetNative(native);
                PlacementRuntime.Initialize(api, native, log, settings, network);
                api.WhenReady(_ =>
                {
                    settings.PitchMoatLineCompatibilityReport = network.IsAvailable && ManagedBoundary.IsReady && PlacementRuntime.IsReady
                        ? CompatibilityToken : string.Empty;
                    settings.System_RequestPerPlayerSettingsPublish();
                });
                log.LogInfo(Stamp("PITCH_MOAT_LINE_READY"));
            }
            catch (Exception error)
            {
                settings.PitchMoatLineCompatibilityReport = string.Empty;
                settings.System_RequestPerPlayerSettingsPublish();
                log.LogError(Stamp("PITCH_MOAT_LINE_DISABLED: " + error));
            }
        }

        // The lobby coordinator freezes these reports at map initialization. Capture
        // the agreed state once, independently of local options or the host toggle.
        internal static void CaptureMultiplayerCompatibility(MissionContext context)
        {
            multiplayerCompatible = false;
            if (context?.Mode.IsRealMultiplayer != true || network?.NativeReady != true)
                return;
            var members = Platform_Multiplayer.Instance?.gameMembers;
            if (members == null) return;
            var participants = new List<int>(8);
            int participantMask = 0, supportMask = 0;
            foreach (var member in members)
            {
                if (member == null || !MultiplayerSafetyPolicy.IsConnectedHuman(member.steamID, member.skirmishAI, member.kicked))
                    continue;
                int player = member.playerID;
                if (player < 1 || player >= settings.PitchMoatLineCompatibilityReportData.Length ||
                    (participantMask & (1 << player)) != 0)
                    return;
                participantMask |= 1 << player;
                if (string.Equals(settings.PitchMoatLineCompatibilityReportData[player], CompatibilityToken, StringComparison.Ordinal))
                    supportMask |= 1 << player;
                participants.Add(player);
            }
            multiplayerCompatible = LinePolicy.HasPeerSupport(participantMask, supportMask) &&
                settings.System_ArePerPlayerSettingsReady(participants, out _);
        }

        internal static string Stamp(string message) => $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
    }
}
