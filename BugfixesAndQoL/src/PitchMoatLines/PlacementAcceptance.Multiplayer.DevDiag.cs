#if SERP_DEVDIAG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared.Automation;
using APIShared.Simulation;
using BepInEx;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Steamworks;

namespace BugfixesAndQoL.PitchMoatLines
{
    // External text records coordinate observations only. Game mutations use
    // ordinary synchronized Vanilla commands, with no local stock writes.
    internal static partial class PlacementAcceptance
    {
        private static bool multiplayerHost;
        private static string pair;
        private static Observation completedObservation;
        private static string Exchange => Path.Combine(Paths.ConfigPath, "SerpAutomation", "PitchMoatLines");
        private static string Role => multiplayerHost ? "host" : "client";
        private static string Peer => multiplayerHost ? "client" : "host";
        private static FRONT_Multiplayer Lobby => MainViewModel.Instance.FRONTMultiplayer;

        private static bool LobbyReady() => MainViewModel.viewModelLoaded && Lobby != null &&
            Lobby.currentLobby != null && Lobby.panelActive && MainViewModel.Instance.Show_MPGameCreation;

        private static bool IsHuman(Platform_Multiplayer.MPLobbyMember member) =>
            (!member.SkirmishMember || member.SkirmishHumanMember) && !member.dummyToBeKicked;

        private static IEnumerable<AutomationWait> OpenMultiplayer(AutomationRun run)
        {
            pair = File.ReadAllText(Path.Combine(Exchange, "pair.txt")).Trim();
            Require(Guid.TryParseExact(pair, "N", out Guid pairId) && pairId != Guid.Empty, "Missing fresh MP pair identity.");
            yield return AutomationWait.Until("ready main menu", () => MainViewModel.viewModelLoaded &&
                FatControler.currentScene == Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu &&
                !MainViewModel.Instance.Show_IntroSequence);
            if (multiplayerHost)
            {
                MainViewModel.Instance.FrontEndMenu.ButtonClicked("Multiplayer");
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(1500));
                Lobby.ButtonClicked("Host");
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(800));
                Lobby.ButtonClicked("DoHost");
                yield return AutomationWait.Until("hosted lobby", () => LobbyReady() &&
                    Lobby.currentLobby.isHost && !FRONT_Multiplayer.skirmishGame);
                WriteExchange("host-lobby.txt", pair + "\n" + Lobby.currentLobby.id.m_SteamID);
                Log("MP_HOST lobby=" + Lobby.currentLobby.id.m_SteamID);
                SelectMultiplayerMap("4spielerkarte.map");
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(500));
            }
            else
            {
                ulong lobby = 0;
                yield return AutomationWait.Until("fresh host lobby relayed by test coordinator", () =>
                {
                    string text = ReadExchange("host-lobby.txt");
                    if (text == null) return false;
                    string[] fields = text.Split('\n');
                    return fields.Length == 2 && fields[0] == pair && ulong.TryParse(fields[1], out lobby) && lobby != 0;
                });
                Platform_Multiplayer.Instance.SetInviteLobbyID(lobby);
                Platform_Multiplayer.Instance.ResumeInvite();
                yield return AutomationWait.Until("joined host lobby", () => LobbyReady() &&
                    !Lobby.currentLobby.isHost && Lobby.currentLobby.id.m_SteamID == lobby);
            }
            yield return AutomationWait.Until("two human lobby members", () => LobbyReady() &&
                Lobby.currentLobby.members.Count(IsHuman) >= 2);
            yield return AutomationWait.Until("own member and valid map", () =>
            {
                if (Lobby.currentLobby.members.All(member => member.id != SteamUser.GetSteamID())) return false;
                return multiplayerHost || (bool)typeof(FRONT_Multiplayer)
                    .GetField("MPMapValid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Lobby);
            });
            var self = Lobby.currentLobby.members.First(member => member.id == SteamUser.GetSteamID());
            if (!self.ready) Lobby.ButtonClicked("Ready");
            yield return AutomationWait.Until("own ready state", () => LobbyReady() &&
                Lobby.currentLobby.members.Any(member => member.id == SteamUser.GetSteamID() && member.ready));
            if (multiplayerHost)
            {
                yield return AutomationWait.Until("all human peers ready", () => Lobby.currentLobby.members
                    .All(member => !IsHuman(member) || member.ready));
                Lobby.ButtonClicked("Play");
            }
            yield return AutomationWait.Until("new genuine multiplayer mission", () => run.CurrentMission != null &&
                run.CurrentMission.Mode.IsRealMultiplayer && run.CurrentMission.Mode.RealNetworkGameMembers >= 2);
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return run.WaitForTicks(run.CurrentMission, 4);
            Log("MP_MODE role=" + Role + " " + run.CurrentMission.Mode.ToDiagnosticString());
        }

        private static void SelectMultiplayerMap(string name)
        {
            var list = (Noesis.ListView)typeof(FRONT_Multiplayer)
                .GetField("RefFileLists", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Lobby);
            object selected = null;
            foreach (object row in (IEnumerable)list.ItemsSource)
                if (row is FileRow file && file.fileHeader != null &&
                    string.Equals(Path.GetFileName(file.fileHeader.filePath), name, StringComparison.OrdinalIgnoreCase))
                    selected = row;
            Require(selected != null, "MP fixture map is missing: " + name);
            list.SelectedItem = selected;
        }

        private static IEnumerable<AutomationWait> CheckMultiplayer(AutomationRun run)
        {
            Require(run.CurrentMission.Mode.IsRealMultiplayer && run.CurrentMission.Mode.RealNetworkGameMembers >= 2,
                "The acceptance requires two genuine human peers.");
            int owner = EditorDirector.instance.ActivePlayerID;
            // Audited route: DLL_GameAction400 -> D7080 -> Chore26 -> 12EF0 ->
            // D5A00 -> B58B0. Mode0 buys5, then debits gold only on success.
            while (ReadStock(owner) < 30)
            {
                int before = ReadStock(owner);
                int gold = GamePlayerManagerAPI.Instance.GetPlayerGold(owner);
                Require(gold > 0, "No gold for synchronized fixture pitch purchases.");
                int error = EngineInterface.GameAction(Enums.GameActionCommand.BuyGoods, 0, (int)eGoods.STORED_PITCH_RAW);
                Require(error == 0, "Vanilla market rejected fixture pitch purchase: " + error);
                yield return AutomationWait.Until("synchronized purchase of five pitch", () => ReadStock(owner) >= before + 5);
                Require(ReadStock(owner) == before + 5 && GamePlayerManagerAPI.Instance.GetPlayerGold(owner) < gold,
                    "The Vanilla purchase did not add five pitch and debit gold.");
            }
            bool hostOnly = scenarioName.Contains("-host-only-");
            bool clientOnly = scenarioName.Contains("-client-only-");
            string variant = hostOnly ? "host-only" : clientOnly ? "client-only" : "both";
            WriteExchange(Role + "-ready.txt", pair + "\n" + owner + "\n" + variant);
            yield return AutomationWait.Until("both synchronized stock fixtures ready", () =>
            {
                string peerReady = ReadExchange(Peer + "-ready.txt");
                if (peerReady == null) return false;
                string[] parts = peerReady.Split('\n');
                Require(parts.Length == 3 && parts[0] == pair && parts[2] == variant, "MP pair variants differ.");
                return true;
            });
            int serial = 0;
            string[] issuers = hostOnly ? new[] { "host" } : clientOnly ? new[] { "client" } : new[] { "host", "client" };
            foreach (string issuer in issuers)
            {
                LineTile[] prior = null;
                foreach (int operation in Enumerable.Range(0, 6))
                {
                    serial++;
                    bool issue = issuer == Role;
                    bool pitch = operation == 0 || operation == 3 || operation == 4;
                    eMappers mapper = pitch ? eMappers.MAPPER_PITCH_DITCH : eMappers.MAPPER_MOAT;
                    int count = operation < 4 ? 3 : 64;
                    string key = pair + "-" + serial.ToString("D2", CultureInfo.InvariantCulture);
                    Observation sample;
                    if (issue)
                    {
                        foreach (var wait in Select(mapper, run)) yield return wait;
                        LineTile[] line = operation == 1 || operation == 3 || operation == 5
                            ? prior : FindLine(owner, mapper, count, false);
                        prior = line;
                        WriteExchange(key + ".plan", owner + "\n" + (int)mapper + "\n" +
                            string.Join(";", line.Select(tile => tile.X + "," + tile.Y)));
                        yield return AutomationWait.Until("peer has armed native readback", () =>
                            ReadExchange(key + ".armed") == pair);
                        foreach (var wait in Place(run, owner, mapper, line)) yield return wait;
                        sample = completedObservation;
                    }
                    else
                    {
                        string plan = null;
                        yield return AutomationWait.Until("issuer's next line plan", () =>
                            (plan = ReadExchange(key + ".plan")) != null);
                        string[] fields = plan.Split('\n');
                        Require(fields.Length == 3, "Invalid MP fixture plan.");
                        int issuerOwner = int.Parse(fields[0], CultureInfo.InvariantCulture);
                        Require(issuerOwner != owner && int.Parse(fields[1], CultureInfo.InvariantCulture) == (int)mapper,
                            "Wrong MP issuer or mapper.");
                        var line = fields[2].Split(';').Select(value =>
                        {
                            string[] xy = value.Split(',');
                            Require(xy.Length == 2, "Invalid line coordinate.");
                            return new LineTile(int.Parse(xy[0], CultureInfo.InvariantCulture),
                                int.Parse(xy[1], CultureInfo.InvariantCulture));
                        }).ToArray();
                        Require(line.Length == count, "Wrong MP fixture line length.");
                        lock (ManagedBoundary.EngineGate)
                        {
                            Require(line.All(tile => !Present(tile, pitch)), "Remote fixture already placed.");
                            Require(SimulationClock.TryGetCurrentTick(run.CurrentMission, out int tick), "No MP readback clock.");
                            sample = new Observation(run.CurrentMission, issuerOwner, pitch, line,
                                Stock(issuerOwner), ReadPhase(issuerOwner), tick);
                            observation = sample;
                        }
                        WriteExchange(key + ".armed", pair);
                        yield return AutomationWait.Until("peer line observed in native simulation", () =>
                        {
                            lock (ManagedBoundary.EngineGate)
                            {
                                Require(sample.Failure == null, sample.Failure);
                                return sample.Count == count;
                            }
                        });
                        yield return run.WaitForTicks(run.CurrentMission, 2);
                    }
                    string result;
                    lock (ManagedBoundary.EngineGate)
                    {
                        Require(sample.Failure == null && sample.Count == count, "MP line readback failed: " + sample.Failure);
                        result = DescribeMultiplayerResult(sample, mapper);
                        observation = null;
                    }
                    WriteExchange(key + "." + Role + ".result", result);
                    string peerResult = null;
                    yield return AutomationWait.Until("peer's matching native placement trace", () =>
                        (peerResult = ReadExchange(key + "." + Peer + ".result")) != null);
                    Require(peerResult == result, "Host/client field, record, tick, stock or phase traces differ for " + key);
                    Log($"MP_COMPARE pair={pair} case={serial} issuer={issuer} mapper={mapper} fields={count} MATCH");
                }
            }
        }

        private static string DescribeMultiplayerResult(Observation sample, eMappers mapper)
        {
            var tiles = GameTileManagerAPI.Instance;
            var states = new List<string>(sample.Line.Length);
            foreach (var tile in sample.Line)
            {
                int id = tiles.GetTileId(tile.X, tile.Y);
                var flags = tiles.GetTilePropertyFlag(id);
                var moat = Moat(tile);
                int pitchSlot = (flags & TilePropertyFlag.PitchTrap) != 0 ? PitchSlot(tile, sample.Owner) : 0;
                var placedFlags = flags & (TilePropertyFlag.PitchTrap | TilePropertyFlag.PlannedMoat | TilePropertyFlag.IsMoat);
                states.Add($"{tile.X},{tile.Y},{(uint)placedFlags},{pitchSlot},{moat.Index},{moat.Owner},{moat.Progress}");
            }
            return sample.Owner + "\n" + (int)mapper + "\n" + string.Join(",", sample.ProgressTicks) + "\n" +
                string.Join(",", sample.ProgressStocks) + "\n" + string.Join(",", sample.ProgressPhases) + "\n" +
                string.Join(";", states);
        }

        private static void WriteExchange(string name, string text)
        {
            Directory.CreateDirectory(Exchange);
            string path = Path.Combine(Exchange, name), stage = path + ".new";
            File.WriteAllText(stage, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(stage, path);
        }

        private static string ReadExchange(string name)
        {
            string path = Path.Combine(Exchange, name);
            try
            {
                var file = new FileInfo(path);
                return file.Exists && file.Length <= 32768 ? File.ReadAllText(path) : null;
            }
            catch (IOException) { return null; }
        }
    }
}
#endif
