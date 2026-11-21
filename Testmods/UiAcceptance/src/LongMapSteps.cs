using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using CrusaderDE;

namespace UiAcceptance
{
    internal static class LongMapSteps
    {
        internal static void Register()
        {
            Runner.Register("LongMap.EnableRadarZoom", _ =>
                GameSteps.Do(() => ConfigSettings.Settings_RadarDefaultZoomedOut = true));
            Runner.Register("LongMap.AssertLoaded", AssertLoaded);
            Runner.Register("LongMap.Restart", Restart);
            Runner.Register("LongMap.RejectDeepPath", RejectDeepPath);
        }

        private static IEnumerable<Wait> AssertLoaded(Step step)
        {
            string expectedPath = step.Arg(0);
            bool byPath = Path.IsPathRooted(expectedPath);
            bool multiplayer = step.Arg(1, "sp") == "mp";
            var view = MainViewModel.Instance;
            yield return new Until("drawn map and radar texture", () =>
                !view.Show_BlackOut && (!multiplayer || !view.Show_MP_LoadingBlack) &&
                GameMap.instance?.getRadarTexture()?.width >= 700);

            var context = UiAcceptancePlugin.LastContext;
            Runner.Require(context?.FilePath != null &&
                string.Equals(byPath ? Path.GetFullPath(context.FilePath) : Path.GetFileName(context.FilePath),
                    byPath ? Path.GetFullPath(expectedPath) : expectedPath, StringComparison.OrdinalIgnoreCase),
                "Loaded a different map: " + context?.FilePath);
            Runner.Require(context.Mode.IsRealMultiplayer == multiplayer, "Unexpected map launch mode.");
            string name = Path.GetFileNameWithoutExtension(expectedPath);
            if (!multiplayer)
            {
                var header = view.HUDIngameMenu.restartSkirmishMapInfo?.selectedHeader;
                Runner.Require(header != null && header.display_filename == name,
                    "The game replaced the long map display name: " + header?.display_filename);
            }
            var radar = GameMap.instance.getRadarTexture();
            AcceptLog.Info($"LONG_MAP_LOADED session={context.SessionId} radar={radar.width}x{radar.height} map={name}");
        }

        private static IEnumerable<Wait> Restart(Step step)
        {
            long before = UiAcceptancePlugin.StartedSession;
            Runner.Require(before > 0, "Restart requires a running mission.");
            var view = MainViewModel.Instance;
            view.HUDmain.InGameOptions(null, null);
            yield return new Delay(300);
            view.ButtonIngameMenuCommand.Execute("5");
            yield return new Until("restart confirmation", GameSteps.ConfirmationVisible);
            view.ButtonConfirmationCommand.Execute("1");
            yield return new Until("replacement mission", () => GameSteps.InMission(before));
            Runner.Require(UiAcceptancePlugin.LastContext.IsRestart &&
                UiAcceptancePlugin.LastContext.RestartedFromSessionId == before,
                "The restart did not preserve mission provenance.");
        }

        private static IEnumerable<Wait> RejectDeepPath(Step step)
        {
            var view = MainViewModel.Instance;
            var lobby = view.FRONTMultiplayer;
            var header = (FileHeader)GameSteps.Read(typeof(FRONT_Multiplayer), lobby, "selectedMPHeader", BindingFlags.Instance);
            Runner.Require(header != null && Path.GetFileName(header.filePath) == step.Arg(0),
                "Select the ordinary workshop map before replacing its path in this test.");
            long before = UiAcceptancePlugin.StartedSession;
            long position = AcceptLog.Position;
            string source = header.filePath;
            string directory = Path.GetDirectoryName(source);
            const string fixtureName = "Map.map";
            string deepPath = Path.Combine(directory, new string('d', 260 - directory.Length - fixtureName.Length - 2), fixtureName);
            Runner.Require(deepPath.Length == 260 && !File.Exists(deepPath), "Invalid test path fixture.");

            // The guard should reject before touching the file. Restore the borrowed header even on failure.
            try
            {
                header.filePath = deepPath;
                lobby.ButtonClicked("Play");
            }
            finally { header.filePath = source; }

            yield return new Until("load rejection in the existing lobby", () =>
                view.Show_MPGameCreation && GameSteps.ConfirmationVisible());
            Runner.Require(UiAcceptancePlugin.StartedSession == before && !view.Show_BlackOut,
                "Rejected map left the lobby or began loading a mission.");
            var expected = new Regex(@"full path length 260 exceeds 259", RegexOptions.CultureInvariant);
            Runner.Require(AcceptLog.Errors(position).FindAll(message => expected.IsMatch(message)).Count == 1,
                "Expected exactly one error reporting path length 260 and limit 259.");
            view.ButtonConfirmationCommand.Execute("1");
            yield return new Until("playable lobby after dismissal", () =>
                view.Show_MPGameCreation && !GameSteps.ConfirmationVisible());
        }
    }
}
