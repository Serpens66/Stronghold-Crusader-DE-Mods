#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AIMarketOverbuyDiagnostic
{
    // Explicit requests only. APIShared owns the Unity driver, while a statically rooted
    // simulation subscription supplies numeric observations; no Windows input is generated.
    internal static class MarketAutomation
    {
        internal static string RunId, Scenario;
        internal static volatile bool IsRunning;
        internal static long LocalCallbacks, NativeAdvances, ProfileSamples, ClockDiscontinuities;
        internal static int LastLocalTick, LastNativeTick, LastProfileNativeTick;
        internal static long LastProfileUtcTicks;
        internal static string LastProfileUtc;
        internal static string SuspectSavePath;
        internal static long SuspectSaveCandidateCount;
        internal static int SuspectSaveNativeTick;
        private static bool armed, tickRegistered, nativeSeen;
        private static int profileRequested, profileReady;
        private static Exception profileFailure;
        private static readonly PlayerProfile[] profiles = new PlayerProfile[GamePlayerManagerAPI.MAX_PLAYERS + 1];
        private static readonly AILords[] EuropeanLords = { AILords.SK_WOLF, AILords.SK_RICHARD,
            AILords.SK_FREDERICK, AILords.SK_PHILLIP, AILords.SK_SHERIFF, AILords.SK_MARSHAL, AILords.SK_ABBOT };
        private static readonly FieldInfo SetupField = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SelectedMapField = typeof(FRONT_Multiplayer).GetField("selectedMPHeader", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateHost = typeof(FRONT_Multiplayer).GetMethod("UpdateHostInfo",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);

        internal static void PrepareTestStartup()
        {
            string path = Path.Combine(Paths.ConfigPath, "SerpAutomation", AIMarketOverbuyDiagnosticPlugin.Guid + ".run");
            if (!File.Exists(path)) return;
            string[] request = File.ReadAllLines(path);
            if (request.Length < 2 || !Owns(request[1]) || !Guid.TryParseExact(request[0], "N", out Guid id) || id == Guid.Empty) return;
            // The runner snapshots/restores the complete config tree. This public
            // SE option retains normal Workshop listing while preventing its forced
            // Steam restart from escaping the test's hidden desktop.
            SHCDESE.BepInEx.Bootstrap.Plugin.Instance.DisableSteamUGCUpdates.Value = true;
            Shared.DevDiag.Log(() => "MARKET_TEST_STARTUP: scenario=" + request[1] +
                " extenderAutomaticWorkshopDownloads=False regularWorkshopListing=True configRestoredByRunner=True");
        }

        internal static bool NeedsCausalTrace()
        {
            string path = Path.Combine(Paths.ConfigPath, "SerpAutomation", AIMarketOverbuyDiagnosticPlugin.Guid + ".run");
            if (!File.Exists(path)) return false;
            string[] request = File.ReadAllLines(path);
            return request.Length >= 2 && Guid.TryParseExact(request[0], "N", out Guid id) && id != Guid.Empty &&
                (request[1] == "market-smoke" || request[1] == "market-causal" || request[1] == "market-backlog" || request[1] == "market-backlog-fixed");
        }

        internal static void TryStart(ManualLogSource log)
        {
            if (armed) return;
            string path = Path.Combine(Paths.ConfigPath, "SerpAutomation", AIMarketOverbuyDiagnosticPlugin.Guid + ".run");
            if (!File.Exists(path)) return;
            string[] request = File.ReadAllLines(path);
            if (request.Length < 2 || !Owns(request[1])) return; // Never consume another scenario's request.
            if (!Guid.TryParseExact(request[0], "N", out Guid id) || id == Guid.Empty)
                throw new InvalidDataException("Invalid owned AI market automation run ID; request retained.");
            armed = true;
            Shared.DevDiag.Attach(log);
            var api = ApiShared.ForMod(AIMarketOverbuyDiagnosticPlugin.Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    // A different controller may have replaced the file while initialization waited.
                    if (!File.Exists(path)) throw new InvalidOperationException("Owned market request disappeared before Unity startup.");
                    string[] current = File.ReadAllLines(path);
                    if (current.Length < 2 || current[0] != request[0] || current[1] != request[1])
                        throw new InvalidOperationException("Market request changed before Unity startup; not consumed.");
                    RunId = id.ToString("N"); Scenario = request[1];
                    if (!tickRegistered)
                    {
                        GameTimeManagerAPI.Instance.OnTick += OnTick;
                        tickRegistered = true;
                    }
                    IsRunning = true;
                    File.Delete(path);
                    try
                    {
                        api.Automation.Start(id, Steps(), log, result => {
                            IsRunning = false;
                            Shared.DevDiag.Log(() => "MARKET_AUTO run=" + RunId + " scenario=" + Scenario +
                                " result=" + result.Status + " step=" + result.Step + " detail=" + result.Detail);
                        }, quitWhenDone: true);
                    }
                    catch { IsRunning = false; throw; }
                }))
                    throw new InvalidOperationException("Unity dispatch unavailable; market run request retained.");
            });
        }

        private static bool Owns(string value) => value == "market-smoke" || value == "market-loaded" ||
            value == "market-fresh" || value == "market-fresh-smoke" || value == "market-causal" || value == "market-backlog" || value == "market-backlog-fixed";

        private static bool IsFresh => Scenario == "market-fresh" || Scenario == "market-fresh-smoke";

        private static IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("start", TimeSpan.FromMinutes(4), StartMission);
            yield return new AutomationStep("ready-and-200-native-ticks", TimeSpan.FromSeconds(120), Ready, true);
            if (Scenario == "market-backlog" || Scenario == "market-backlog-fixed")
            {
                yield return new AutomationStep("controlled-small-trades", TimeSpan.FromMinutes(8), RunBacklog, true);
                yield break;
            }
            int seconds = Scenario == "market-smoke" || Scenario == "market-fresh-smoke" ? 60 : Scenario == "market-causal" ? 240 : 20 * 60;
            yield return new AutomationStep("natural-economy-observation", TimeSpan.FromSeconds(seconds + 150),
                run => Observe(run, seconds), true);
        }

        private static IEnumerable<AutomationWait> StartMission(AutomationRun run)
        {
            if (IsFresh)
            {
                bool positionsSet = false;
                // Each yielded wait returns on Unity. The last opponent's completed wait
                // therefore lets us validate/fix the lobby before StartSkirmish resumes Play.
                // Vanilla hides peace-time settings for AI Skirmish (Show_MPPeacetime = !skirmishGame).
                foreach (var wait in run.StartSkirmish("The Killing Plains.map", FreshLobbyArguments()))
                {
                    yield return wait;
                    PrepareFreshLobby(ref positionsSet);
                }
                Require(positionsSet, "fresh lobby start positions were not confirmed before launch");
            }
            else
            {
                string save = Scenario == "market-smoke" ? "test_dustynoattack2.sav" : "test_dustynoattack.sav";
                string fixture = Path.Combine(Paths.BepInExRootPath, "SerpAutomationRollback", "fixture.sav");
                if (Scenario == "market-causal" && File.Exists(fixture)) save = fixture;
                foreach (var wait in run.LoadSave(save)) yield return wait;
            }
            Shared.DevDiag.Log(() => "MARKET_AUTO run=" + RunId + " scenario=" + Scenario +
                " mission=" + run.CurrentMission.SessionId + " file=" + run.CurrentMission.FilePath +
                " realMultiplayer=" + run.CurrentMission.Mode.IsRealMultiplayer);
        }

        private static AILords[] FreshLobbyArguments()
        {
            // Temporary adapter: current APIShared passes enum IDs directly to Vanilla's
            // zero-based lobby selector. The mandatory native roster check below fails
            // if the helper is corrected later, rather than silently testing other lords.
            var result = new AILords[EuropeanLords.Length];
            for (int index = 0; index < result.Length; index++)
            {
                Require((uint)EuropeanLords[index] > 1, "fresh lord cannot use the current helper adapter");
                result[index] = (AILords)((uint)EuropeanLords[index] - 1);
            }
            return result;
        }

        private static void PrepareFreshLobby(ref bool positionsSet)
        {
            if (positionsSet || !MainViewModel.viewModelLoaded || !MainViewModel.Instance.Show_MPGameCreation) return;
            var lobby = MainViewModel.Instance.FRONTMultiplayer;
            if (lobby?.currentLobby == null || !FRONT_Multiplayer.skirmishGame) return;
            Require(SelectedMapField != null && !SelectedMapField.IsStatic && SelectedMapField.FieldType == typeof(FileHeader),
                "selected-map reflection contract changed");
            var selected = (FileHeader)SelectedMapField.GetValue(lobby);
            if (!string.Equals(Path.GetFileName(selected?.filePath), "The Killing Plains.map", StringComparison.OrdinalIgnoreCase)) return;
            int[,] locations = GameData.Instance.Keep_Locations;
            Require(locations != null && locations.GetLength(0) >= GamePlayerManagerAPI.MAX_PLAYERS && locations.GetLength(1) >= 2,
                "fresh map does not expose eight start coordinates");
            var uniquePlaces = new HashSet<long>();
            for (int place = 0; place < GamePlayerManagerAPI.MAX_PLAYERS; place++)
            {
                Require(locations[place, 0] >= 0 && locations[place, 1] >= 0, "fresh map lacks valid start place " + place);
                Require(uniquePlaces.Add(((long)locations[place, 0] << 32) | (uint)locations[place, 1]),
                    "fresh map start coordinates are duplicated or uninitialized");
            }
            int members = lobby.currentLobby.members.Count(member => !member.dummyToBeKicked);
            if (members < GamePlayerManagerAPI.MAX_PLAYERS) return;
            Require(members == GamePlayerManagerAPI.MAX_PLAYERS, "unexpected fresh lobby participant count " + members);
            Require(SetupField != null && !SetupField.IsStatic && SetupField.FieldType == typeof(EngineInterface.MultiplayerSetupData) &&
                UpdateHost != null && UpdateHost.ReturnType == typeof(void), "lobby setup reflection contract changed");
            var setup = (EngineInterface.MultiplayerSetupData)SetupField.GetValue(lobby);
            Require(setup?.start_keep_location_order != null && setup.start_keep_location_order.Length >= GamePlayerManagerAPI.MAX_PLAYERS,
                "start-place assignment array unavailable");
            // -10 is Vanilla's empty lobby-place protocol value (same as radar-shield reassignment).
            for (int place = 0; place < setup.start_keep_location_order.Length; place++) setup.start_keep_location_order[place] = -10;
            for (int lobbyIndex = 0; lobbyIndex < GamePlayerManagerAPI.MAX_PLAYERS; lobbyIndex++)
                setup.start_keep_location_order[lobbyIndex] = lobbyIndex; // Lobby indices, not 1-based game IDs.
            UpdateHost.Invoke(lobby, new object[] { false });
            var description = new StringBuilder("MARKET_AUTO run=").Append(RunId).Append(" fixedStarts=");
            for (int place = 0; place < GamePlayerManagerAPI.MAX_PLAYERS; place++)
            {
                Require(setup.start_keep_location_order[place] == place, "Vanilla changed a requested fixed start");
                description.Append(place).Append('@').Append(locations[place, 0]).Append(',').Append(locations[place, 1]).Append(';');
            }
            positionsSet = true;
            Shared.DevDiag.Log(() => description.ToString());
        }

        private static IEnumerable<AutomationWait> Ready(AutomationRun run)
        {
            MissionContext mission = run.CurrentMission;
            Require(mission != null && !mission.IsEditor && !mission.Mode.IsRealMultiplayer, "expected a local gameplay mission");
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            // Keep the short entry smoke slow; causal replay matches the accelerated candidate run.
            int speed = Scenario == "market-smoke" || Scenario == "market-backlog" || Scenario == "market-backlog-fixed" ? 40 : 250;
            Director.instance.SetEngineFrameRate(speed);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            long callbacksBefore = Interlocked.Read(ref LocalCallbacks);
            long observedBefore = Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.ObservedTicks);
            yield return run.WaitForTicks(mission, 200);
            Require(Interlocked.Read(ref LocalCallbacks) > callbacksBefore, "local OnTick callbacks did not progress with native map time");
            yield return AutomationWait.Until("200 native ticks captured by the diagnostic producer", () => {
                CheckDiagnosticHealth();
                return Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.ObservedTicks) - observedBefore >= 200;
            });
            RequestProfile();
            yield return WaitForProfile("initial simulation-context player profile");
            ValidateAndLogProfile(true);
            Shared.DevDiag.Log(() => "MARKET_AUTO run=" + RunId + " scenario=" + Scenario + " ready=True speed=" + speed +
                " nativeTick=" + Volatile.Read(ref LastNativeTick) + " localTick=" + Volatile.Read(ref LastLocalTick) +
                " localCallbacks=" + Interlocked.Read(ref LocalCallbacks));
        }

        private static IEnumerable<AutomationWait> RunBacklog(AutomationRun run)
        {
            foreach (var wait in MarketBacklogScenario.Run(run)) yield return wait;
            // Reuse the normal health and final-drain checks after the scenario has
            // restored its profile edits and disabled physical interventions.
            foreach (var wait in Observe(run, 10)) yield return wait;
        }

        private static IEnumerable<AutomationWait> Observe(AutomationRun run, int seconds)
        {
            var clock = Stopwatch.StartNew();
            long lastNativeAdvance = Stopwatch.GetTimestamp(), lastLocalAdvance = lastNativeAdvance;
            long lastProducerAdvance = lastNativeAdvance;
            long lastCallbacks = Interlocked.Read(ref LocalCallbacks), firstCallbacks = lastCallbacks;
            int previousNative = Volatile.Read(ref LastNativeTick), firstNative = previousNative;
            int previousProducer = Volatile.Read(ref AIMarketOverbuyDiagnosticRuntime.LastObservedMapTick), firstProducer = previousProducer;
            long firstObserved = Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.ObservedTicks);
            long candidateBaseline = Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.ObservedBuySellCandidates);
            bool suspectSaved = false;
            double nextStatus = 10;
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                int native = Volatile.Read(ref LastNativeTick);
                long callbacks = Interlocked.Read(ref LocalCallbacks), now = Stopwatch.GetTimestamp();
                if (native != previousNative) { previousNative = native; lastNativeAdvance = now; }
                if (callbacks != lastCallbacks) { lastCallbacks = callbacks; lastLocalAdvance = now; }
                int producer = Volatile.Read(ref AIMarketOverbuyDiagnosticRuntime.LastObservedMapTick);
                if (producer != previousProducer) { previousProducer = producer; lastProducerAdvance = now; }
                CheckDiagnosticHealth();
                Require(now - lastNativeAdvance < Stopwatch.Frequency * 120, "native simulation stalled for 120 seconds");
                Require(now - lastLocalAdvance < Stopwatch.Frequency * 120, "local tick callback stalled for 120 seconds");
                Require(now - lastProducerAdvance < Stopwatch.Frequency * 120, "diagnostic producer stalled for 120 seconds");
                long candidates = Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.ObservedBuySellCandidates);
                if (!suspectSaved && candidates > candidateBaseline)
                {
                    foreach (var wait in SaveSuspect(run, candidates)) yield return wait;
                    suspectSaved = true;
                    // Saving intentionally quiesces the engine; do not count that protected
                    // native save scope as an unexplained simulation stall afterwards.
                    lastNativeAdvance = lastLocalAdvance = lastProducerAdvance = Stopwatch.GetTimestamp();
                }
                if (clock.Elapsed.TotalSeconds >= nextStatus)
                {
                    RequestProfile();
                    yield return WaitForProfile("ten-second simulation profile");
                    ValidateAndLogProfile(false);
                    Shared.DevDiag.Log(() => "MARKET_AUTO run=" + RunId + " mission=" + run.CurrentMission.SessionId +
                        " elapsedSeconds=" + clock.Elapsed.TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                        " nativeTick=" + Volatile.Read(ref LastNativeTick) + " localTick=" + Volatile.Read(ref LastLocalTick) +
                        " localCallbacks=" + Interlocked.Read(ref LocalCallbacks) + " nativeAdvances=" + Interlocked.Read(ref NativeAdvances) +
                        " clockDiscontinuities=" + Interlocked.Read(ref ClockDiscontinuities));
                    nextStatus = clock.Elapsed.TotalSeconds + 10;
                }
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(250));
            }
            RequestProfile();
            yield return WaitForProfile("final simulation profile");
            ValidateAndLogProfile(false);
            Require(SimulationClock.TryGetElapsedTicks(firstNative, Volatile.Read(ref LastNativeTick), out int advanced) && advanced >= 200,
                "measurement did not advance at least 200 native map ticks");
            Require(Interlocked.Read(ref LocalCallbacks) - firstCallbacks >= 200, "measurement did not receive 200 local callbacks");
            Require(Interlocked.Read(ref ClockDiscontinuities) == 0, "native tick timeline changed during this run");
            Require(Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.ObservedTicks) - firstObserved >= 200,
                "diagnostic producer observed fewer than 200 measurement ticks");
            Require(SimulationClock.TryGetElapsedTicks(firstProducer, Volatile.Read(ref AIMarketOverbuyDiagnosticRuntime.LastObservedMapTick), out int producerAdvanced) && producerAdvanced >= 200,
                "diagnostic producer native time did not advance 200 ticks");
            CheckDiagnosticHealth();
            long writerBeforeStop = AIMarketOverbuyDiagnosticPlugin.StopForAutomation();
            long stopClock = Stopwatch.GetTimestamp();
            yield return AutomationWait.Until("final diagnostic output drained by two writer passes", () => {
                CheckDiagnosticHealth();
                Require(Stopwatch.GetTimestamp() - stopClock < Stopwatch.Frequency * 10, "diagnostic writer did not drain within ten seconds");
                return Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.WriterPasses) >= writerBeforeStop + 2 &&
                    AIMarketOverbuyDiagnosticPlugin.TraceOutputDrainedForAutomation;
            });
            CheckDiagnosticHealth(true);
            Shared.DevDiag.Log(() => "MARKET_AUTO run=" + RunId + " OUTPUT_DRAINED=True rootObserved=" +
                Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.ObservedTicks) + " nativeTicks=" + advanced +
                " producerNativeTicks=" + producerAdvanced + " writerPasses=" + Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.WriterPasses));
            Shared.DevDiag.Log(() => "MARKET_AUTO run=" + RunId + " observationComplete=True nativeTicks=" + advanced +
                " callbackDelta=" + (Interlocked.Read(ref LocalCallbacks) - firstCallbacks) +
                " elapsedSeconds=" + clock.Elapsed.TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void RequestProfile()
        {
            Require(Volatile.Read(ref profileReady) == 0, "previous profile not consumed");
            Volatile.Write(ref profileRequested, 1);
        }

        private static void CheckDiagnosticHealth(bool requireNativeCalls = false)
        {
            AIMarketOverbuyDiagnosticPlugin.CheckTraceHealthForAutomation(requireNativeCalls);
            Require(Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.CaptureFailures) == 0, "diagnostic capture reported a failure");
            Require(Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.WriterFailures) == 0, "diagnostic writer reported a failure");
            Require(Interlocked.Read(ref AIMarketOverbuyDiagnosticRuntime.IncompleteSessions) == 0, "diagnostic reported incomplete sessions");
            Require(Interlocked.Read(ref MarketStaleEquipmentFix.CorrectionFailures) == 0 &&
                Interlocked.Read(ref MarketStaleEquipmentFix.OmittedEvidence) == 0, "equipment correction failed or lost evidence");
        }

        private static IEnumerable<AutomationWait> SaveSuspect(AutomationRun run, long candidates)
        {
            string path = Path.Combine(ConfigSettings.GetSavesPath(), "SerpAcceptance-" + RunId + ".sav");
            Require(!File.Exists(path), "run-owned suspect save already exists; refusing to overwrite it");
            long requested = Stopwatch.GetTimestamp();
            yield return AutomationWait.Until("safe native save after buy/sell candidate", () => {
                CheckDiagnosticHealth();
                Require(Stopwatch.GetTimestamp() - requested < Stopwatch.Frequency * 120,
                    "native save did not become safe within 120 seconds");
                return Director.instance.SafeToSave();
            });
            // Balance SafeToSave's pause scope before the ordinary save writer opens its own.
            Director.instance.FinishedSaving();
            Require(CameraControls2D.instance != null && GameMap.instance != null, "save camera/map context unavailable");
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1);
            int centerX = -1, centerY = -1;
            GameMap.instance.getRotationCentre(ref centerX, ref centerY);
            int nativeTick = Volatile.Read(ref LastNativeTick);
            Require(EngineInterface.SaveSaveGame(path, screen.x, screen.y, centerX, centerY, false, false, false),
                "native suspect-save writer rejected the run-owned fixture");
            Require(File.Exists(path) && new FileInfo(path).Length > 0, "run-owned suspect save missing or empty");
            SuspectSavePath = path; SuspectSaveNativeTick = nativeTick; SuspectSaveCandidateCount = candidates;
            Shared.DevDiag.Log(() => "MARKET_SUSPECT_SAVE run=" + RunId + " path=" + path + " nativeTick=" + nativeTick +
                " candidateCount=" + candidates + " sourceUnconfirmed=True");
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
        }

        private static AutomationWait WaitForProfile(string description)
        {
            long requested = Stopwatch.GetTimestamp();
            return AutomationWait.Until(description, () => {
                Require(Stopwatch.GetTimestamp() - requested < Stopwatch.Frequency * 120,
                    "simulation profile did not arrive within 120 seconds");
                return Volatile.Read(ref profileReady) != 0;
            });
        }

        private static void OnTick(int localTick)
        {
            if (!IsRunning || !SimulationTickEvents.TryGetCurrentTick(out int nativeTick)) return;
            Interlocked.Increment(ref LocalCallbacks);
            Volatile.Write(ref LastLocalTick, localTick);
            if (nativeSeen && nativeTick != LastNativeTick)
            {
                if (SimulationClock.TryGetElapsedTicks(LastNativeTick, nativeTick, out int elapsed)) Interlocked.Add(ref NativeAdvances, elapsed);
                else Interlocked.Increment(ref ClockDiscontinuities);
            }
            nativeSeen = true;
            Volatile.Write(ref LastNativeTick, nativeTick);
            if (Interlocked.CompareExchange(ref profileRequested, 0, 1) != 1) return;
            try { CaptureProfile(nativeTick); }
            catch (Exception error) { profileFailure = error; }
            Volatile.Write(ref profileReady, 1); // Publishes all copied values to Unity; no pointers escape.
        }

        private static unsafe void CaptureProfile(int tick)
        {
            var players = GamePlayerManagerAPI.Instance;
            for (int playerId = 1; playerId <= GamePlayerManagerAPI.MAX_PLAYERS; playerId++)
            {
                profiles[playerId] = default;
                if (!players.IsAIPlayer(playerId)) continue;
                ref PlayerProfile profile = ref profiles[playerId];
                profile.Player = playerId; profile.Lord = players.GetAILord(playerId);
                if (!players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources)) continue;
                profile.Resources = true;
                profile.Gold = resources->r_TotalGoodsGold;
                profile.Phase = (int)resources->r_AISellOrBuyPhase;
                profile.Market = resources->r_TradePostId;
                profile.CachedMarket = resources->r_bCachedTradePostExists;
                profile.KeepX = resources->r_KeepTilePositionX; profile.KeepY = resources->r_KeepTilePositionY;
                profile.Peasants = resources->r_ExistingPeasants;
                profile.Recruitment = (int)resources->r_AICombatRecruitmentMode;
                uint* pending = &resources->r_AIPendingMarketPurchaseAmountNull;
                uint* stock = &resources->r_TotalGoodsNull;
                for (int goodIndex = (int)eGoods.STORED_WOOD_LOGS; goodIndex < (int)eGoods.Count; goodIndex++)
                {
                    if (goodIndex == (int)eGoods.STORED_GOLD) continue;
                    profile.Pending += pending[goodIndex];
                    if (goodIndex >= (int)eGoods.STORED_BOWS && goodIndex <= (int)eGoods.STORED_METAL_ARMOUR)
                        profile.Equipment += stock[goodIndex];
                }
            }
            LastProfileNativeTick = tick;
            LastProfileUtcTicks = DateTime.UtcNow.Ticks;
            Interlocked.Increment(ref ProfileSamples);
        }

        private static void ValidateAndLogProfile(bool initial)
        {
            if (profileFailure != null) throw new InvalidOperationException("simulation profile failed", profileFailure);
            LastProfileUtc = new DateTime(LastProfileUtcTicks, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
            int ai = 0, resources = 0, cachedMarkets = 0;
            var text = new StringBuilder("MARKET_PROFILE run=").Append(RunId).Append(" scenario=").Append(Scenario)
                .Append(" initial=").Append(initial).Append(" nativeTick=").Append(LastProfileNativeTick)
                .Append(" capturedUtc=").Append(LastProfileUtc).Append(" players=[");
            for (int player = 1; player < profiles.Length; player++)
            {
                PlayerProfile p = profiles[player]; if (p.Player == 0) continue;
                ai++; if (p.Resources) resources++; if (p.CachedMarket != 0) cachedMarkets++;
                text.Append(p.Player).Append(':').Append(p.Lord).Append(" keep=").Append(p.KeepX).Append(',').Append(p.KeepY)
                    .Append(" gold=").Append(p.Gold).Append(" phase=").Append(p.Phase).Append(" marketId=").Append(p.Market)
                    .Append(" cachedMarket=").Append(p.CachedMarket).Append(" pending=").Append(p.Pending)
                    .Append(" equipment=").Append(p.Equipment).Append(" peasants=").Append(p.Peasants)
                    .Append(" recruitment=").Append(p.Recruitment).Append(';');
            }
            text.Append("] ai=").Append(ai).Append(" resourceRecords=").Append(resources).Append(" cachedMarkets=").Append(cachedMarkets);
            Volatile.Write(ref profileReady, 0);
            Require(ai > 0 && resources == ai, "AI roster or player resource view unavailable");
            if (initial && !IsFresh) Require(cachedMarkets > 0, "loaded fixture has no cached AI market");
            if (initial && IsFresh)
            {
                Require(ai == EuropeanLords.Length, "fresh fixture does not contain seven AI lords");
                for (int index = 0; index < EuropeanLords.Length; index++)
                {
                    int player = index + 2; // Fixed lobby slots: local human 1, requested lords 2..8.
                    Require(profiles[player].Player == player && (uint)profiles[player].Lord == (uint)EuropeanLords[index],
                        "fresh native roster mismatch at player " + player + ": expected=" + EuropeanLords[index] +
                        " actual=" + profiles[player].Lord);
                }
            }
            Shared.DevDiag.Log(() => text.ToString());
        }

        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private struct PlayerProfile
        {
            internal int Player, Gold, Phase, Recruitment;
            internal Enums.AILords Lord;
            internal bool Resources;
            internal uint Market, CachedMarket, KeepX, KeepY, Peasants;
            internal ulong Pending, Equipment;
        }
    }
}
#endif
