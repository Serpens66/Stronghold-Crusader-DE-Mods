using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using APIShared.Economy;
using APIShared.Presentation;
using APIShared.Simulation;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using Path = System.IO.Path;

namespace UiAcceptance
{
    // Deliberately destructive fixtures are confined to the fresh SP session explicitly
    // claimed by this scenario. No work occurs in another map or outside a queued case.
    internal static partial class AutoSendSteps
    {
        private static readonly object gate = new object();
        private static long session;
        private static int sender, first, second;
        private static int[] fixtureAI = Array.Empty<int>();
        private static bool bootstrapReported;
        private static bool registered;
        private static Work pending;
        private static string failure, evidence;
        private static bool done;
        private static bool manualWaiting;
        private static AllyTransferResult manualResult;
        private static object runtime;
        private static MethodInfo edit, readDisplay;
        private static Type editType;
        private static string savePath;
        private static long savedSession;
        private static SavedRule[] savedRules;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void Register()
        {
            Runner.Register("AutoSend.ResumeBriefing", ResumeBriefing);
            Runner.Register("AutoSend.Arm", Arm);
            Runner.Register("AutoSend.Case", Case);
            Runner.Register("AutoSend.Manual", Manual);
            Runner.Register("AutoSend.UiClicks", UiClicks);
            Runner.Register("AutoSend.InputRegression", InputRegression);
            Runner.Register("AutoSend.Save", Save);
            Runner.Register("AutoSend.Reload", Reload);
            Runner.Register("AutoSend.Empty", Empty);
            Runner.Register("AutoSend.Stop", s => GameSteps.Do(() => { lock (gate) { session = 0; pending = null; } }));
        }

        private static IEnumerable<Wait> ResumeBriefing(Step step)
        {
            // Vanilla opens this after the first rendered buffer, after Lobby.Play returns.
            yield return new Until("deferred vanilla new-game briefing", () => MainViewModel.Instance.Show_HUD_Briefing);
            AcceptLog.Info("AUTOSEND_BRIEFING visible=True simRunning=" + Director.instance.SimRunning +
                " nativePaused=" + GamePlayerManagerAPI.Instance.IsLocalPaused());
            MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return new Until("vanilla briefing resumed", () => !MainViewModel.Instance.Show_HUD_Briefing);
        }

        private sealed class Work
        {
            internal string Name;
            internal eGoods Good;
            internal int Source, ExpectedSource, ExpectedA, ExpectedB;
            internal int RecipientA = 0, RecipientB = 0;
            internal int Tick;
            internal bool Prepared;
        }

        private sealed class SavedRule
        {
            internal int Recipient, Reserve, Minimum, Maximum;
            internal eGoods Good;
            internal bool Enabled, Paused;
            internal string Describe() => Recipient + "/" + Good + " reserve=" + Reserve + " min=" + Minimum +
                " max=" + Maximum + " enabled=" + Enabled + " paused=" + Paused;
        }

        private static IEnumerable<Wait> Arm(Step step)
        {
            var context = UiAcceptancePlugin.LastContext;
            Runner.Require(context != null && !context.IsSave && !context.IsEditor && !context.Mode.IsRealMultiplayer,
                "AutoSend fixtures require a fresh singleplayer skirmish, never a save/editor/multiplayer");
            Runner.Require(context.LocalPlayerId.HasValue && context.LocalPlayerId.Value >= 1 && context.LocalPlayerId.Value <= 8,
                "mission has no controlled participant");
            yield return new Until("AutoSend native service", () => AllyTransfers.IsAvailable);
            runtime = GameSteps.ResolveStatic("AutoSendTest.AutoSendPlugin::runtime");
            edit = runtime.GetType().GetMethod("Edit", Instance);
            readDisplay = runtime.GetType().GetMethod("ReadDisplay", Instance);
            Runner.Require(edit != null && readDisplay != null, "AutoSend command/display contract missing");
            editType = edit.GetParameters()[0].ParameterType;
            if (!registered)
            {
                Runner.Require(SimulationTickEvents.TryRegister(UiAcceptancePlugin.Guid, "autosend-fixture", Before, out string reason, -100), reason);
                Runner.Require(SimulationTickEvents.TryRegister(UiAcceptancePlugin.Guid, "autosend-assert", After, out reason, 100), reason);
                Runner.Require(AllyTransferEvents.TryRegister(UiAcceptancePlugin.Guid, "autosend-manual", null, ObserveManual, out reason), reason);
                registered = true;
            }
            lock (gate)
            {
                session = context.SessionId; sender = context.LocalPlayerId.Value;
                var ai = new List<int>();
                var roster = Platform_Multiplayer.Instance?.gameMembers;
                if (roster != null) foreach (var member in roster)
                    // Fresh SP has one human; its mapped remaining members are AI.
                    // Vanilla leaves MPGameMember.skirmishAI false even for those AI.
                    if (member != null && !member.kicked && member.playerID >= 1 && member.playerID <= 8 && member.playerID != sender)
                        ai.Add(member.playerID);
                Runner.Require(ai.Count >= 3, "fixture lobby must contain two allied AI and one enemy AI");
                fixtureAI = ai.ToArray(); bootstrapReported = false;
                failure = evidence = null; done = false; pending = new Work { Name = "bootstrap" };
            }
            yield return AwaitWork();
            CheckWork();
            // Wait for the managed native snapshot and the real Hello/Ready Chore handshake.
            // Opening does not bypass the broker's own visibility/context checks.
            yield return new Until("two allies visible in vanilla snapshot", () => OpenSelection(first, eGoods.STORED_WOOD_PLANKS));
            yield return new Until("AutoSend epoch and allied snapshot ready", () => Flag(Display(), "Ready") && !Flag(Display(), "Pending"));
            yield return NextFrame.Instance;
            CheckButtons();
            AcceptLog.Info("AUTOSEND_UI ready session=" + session + " sender=" + sender + " allies=" + first + "," + second);
        }

        private static Until AwaitWork() => new Until("native fixture and post-planner assertions", () => { lock (gate) return done; });
        private static void CheckWork()
        {
            lock (gate)
            {
                Runner.Require(failure == null, failure ?? "fixture failed");
                AcceptLog.Info("AUTOSEND_NATIVE " + evidence);
            }
        }

        private static IEnumerable<Wait> Case(Step step)
        {
            string name = step.Arg(0);
            eGoods good = name == "gold" ? eGoods.STORED_GOLD : eGoods.STORED_WOOD_PLANKS;
            // Keep the same 60-unit surplus within the physical donor yard's
            // measured capacity even after ordinary production occupies its cells.
            int reserve = name == "gold" ? 1000 : 20;
            int source = name == "reserve" ? 20 : name == "gold" ? 1600 : 80;
            int maxA = name == "cap" ? 20 : name == "gold" ? 1000 : 100;
            int maxB = name == "cap" ? 0 : maxA;
            int minA = name == "emergency" ? 60 : 0;
            Runner.Require(name == "fair" || name == "emergency" || name == "cap" || name == "reserve" ||
                name == "pause" || name == "off" || name == "gold", "unknown native case " + name);
            yield return new Until("selected ally", () => OpenSelection(first, good));
            foreach (Wait wait in Change("Paused", 0, good, 0, true, d => Flag(d, "Paused"))) yield return wait;
            foreach (Wait wait in Change("Reserve", first, good, reserve, false, d => Number(d, "Reserve") == reserve)) yield return wait;
            foreach (Wait wait in Configure(first, good, minA, maxA, name != "off")) yield return wait;
            foreach (Wait wait in Configure(second, good, 0, maxB, name != "off" && maxB > 0)) yield return wait;
            if (name != "pause")
                foreach (Wait wait in Change("Paused", 0, good, 0, false, d => !Flag(d, "Paused"))) yield return wait;
            CheckButtons();
            int amountA = name == "fair" ? 30 : name == "emergency" ? 60 : name == "cap" ? 20 : name == "gold" ? 300 : 0;
            int amountB = name == "fair" ? 30 : name == "gold" ? 300 : 0;
            lock (gate)
            {
                Runner.Require(session != 0 && pending == null, "fixture already active or session absent");
                done = false; failure = evidence = null;
                pending = new Work { Name = name, Good = good, Source = source,
                    ExpectedSource = source - amountA - amountB, ExpectedA = amountA, ExpectedB = amountB };
            }
            yield return AwaitWork();
            CheckWork();
        }

        private static IEnumerable<Wait> Configure(int recipient, eGoods good, int minimum, int maximum, bool on)
        {
            yield return new Until("vanilla ally selection " + recipient, () => OpenSelection(recipient, good));
            // Every intermediate rule must satisfy the production bounds: disable before
            // a zero maximum and lower the minimum before reducing the maximum.
            foreach (Wait wait in Change("Enabled", recipient, good, 0, false, d => !Flag(Read(d, "Rule"), "Enabled"))) yield return wait;
            foreach (Wait wait in Change("Minimum", recipient, good, 0, false, d => Number(Read(d, "Rule"), "Minimum") == 0)) yield return wait;
            foreach (Wait wait in Change("Maximum", recipient, good, maximum, false, d => Number(Read(d, "Rule"), "Maximum") == maximum)) yield return wait;
            foreach (Wait wait in Change("Minimum", recipient, good, minimum, false, d => Number(Read(d, "Rule"), "Minimum") == minimum)) yield return wait;
            foreach (Wait wait in Change("Enabled", recipient, good, 0, on, d => Flag(Read(d, "Rule"), "Enabled") == on)) yield return wait;
        }

        private static IEnumerable<Wait> UiClicks(Step step)
        {
            foreach (Wait wait in CompactUi()) yield return wait;
        }

        private static IEnumerable<Wait> ClickAndAcknowledge(string member, Func<object, bool> acknowledged)
        {
            yield return new Until("ready before Noesis click " + member, () => Flag(Display(), "Ready") && !Flag(Display(), "Pending"));
            Runner.Require(!acknowledged(Display()), "UI click fixture already has its intended result for " + member);
            var button = Read(Read(runtime, "view"), member) as Button;
            Runner.Require(button != null, "actual Noesis button missing: " + member);
            button.BringIntoView();
            yield return NextFrame.Instance;
            Runner.Require(button.IsVisible && button.IsEnabled && button.ActualWidth > 0 && button.ActualHeight > 0,
                "actual Noesis button not visible/enabled: " + member);
            Point center = button.PointToScreen(new Point((float)button.ActualWidth / 2, (float)button.ActualHeight / 2));
            int x = (int)center.X, y = (int)center.Y;
            Runner.Require(x >= 0 && y >= 0 && x < UnityEngine.Screen.width && y < UnityEngine.Screen.height,
                "actual Noesis button outside screen: " + member + " at " + x + "," + y);
            NoesisView view = FatControler.instance?.NGview ?? throw new StepFailure("Noesis input view unavailable");
            // The same public input route as InputSteps.NoesisClick; no event/delegate shortcut.
            view.MouseMove(x, y);
            view.MouseButtonDown(x, y, MouseButton.Left);
            view.MouseButtonUp(x, y, MouseButton.Left);
            yield return new Until("actual UI Chore acknowledgement " + member, () =>
            {
                object display = Display();
                if (Flag(display, "Pending")) return false;
                Runner.Require(string.IsNullOrEmpty((string)Read(display, "Feedback")), "UI edit rejected: " + Read(display, "Feedback"));
                return Flag(display, "Ready") && acknowledged(display);
            });
            AcceptLog.Info("AUTOSEND_UI_CLICK " + member + " at=" + x + "," + y + " nativeChoreAcknowledged=True");
        }

        private static IEnumerable<Wait> Save(Step step)
        {
            Runner.Require(OwnsSession() && savedRules == null && savePath == null, "save fixture session absent or already saved");
            yield return new Until("save selection", () => OpenSelection(first, eGoods.STORED_WOOD_PLANKS));
            foreach (Wait wait in Change("Paused", 0, eGoods.STORED_WOOD_PLANKS, 0, true, d => Flag(d, "Paused"))) yield return wait;
            foreach (Wait wait in Change("Reserve", first, eGoods.STORED_WOOD_PLANKS, 111, false, d => Number(d, "Reserve") == 111)) yield return wait;
            foreach (Wait wait in Configure(first, eGoods.STORED_WOOD_PLANKS, 25, 125, true)) yield return wait;
            foreach (Wait wait in Configure(second, eGoods.STORED_WOOD_PLANKS, 5, 75, true)) yield return wait;
            yield return new Until("save gold selection", () => OpenSelection(first, eGoods.STORED_GOLD));
            foreach (Wait wait in Change("Reserve", first, eGoods.STORED_GOLD, 17777, false, d => Number(d, "Reserve") == 17777)) yield return wait;
            foreach (Wait wait in Configure(first, eGoods.STORED_GOLD, 200, 800, true)) yield return wait;
            foreach (Wait wait in Configure(second, eGoods.STORED_GOLD, 50, 600, true)) yield return wait;
            foreach (Wait wait in Change("Paused", 0, eGoods.STORED_GOLD, 0, false, d => !Flag(d, "Paused"))) yield return wait;
            var snapshots = new List<SavedRule>();
            foreach (eGoods good in new[] { eGoods.STORED_WOOD_PLANKS, eGoods.STORED_GOLD })
                foreach (int recipient in new[] { first, second })
                {
                    yield return new Until("capture saved selection", () => OpenSelection(recipient, good));
                    yield return new Until("capture acknowledged rule", () => Flag(Display(), "Ready") && !Flag(Display(), "Pending"));
                    object display = Display(), rule = Read(display, "Rule");
                    snapshots.Add(new SavedRule { Recipient = recipient, Good = good, Reserve = Number(display, "Reserve"),
                        Minimum = Number(rule, "Minimum"), Maximum = Number(rule, "Maximum"),
                        Enabled = Flag(rule, "Enabled"), Paused = Flag(display, "Paused") });
                }
            // A missing mod record cannot accidentally satisfy the later restore check:
            // every captured rule is enabled with positive minimum/reserve.
            foreach (var snapshot in snapshots)
                Runner.Require(snapshot.Enabled && !snapshot.Paused && snapshot.Minimum > 0 && snapshot.Reserve > 0,
                    "save fixture is not a nondefault rule: " + snapshot.Describe());
            string directory = Path.GetFullPath(ConfigSettings.GetSavesPath());
            Runner.Require(Directory.Exists(directory), "native saves directory missing");
            var label = new StringBuilder();
            foreach (char value in AcceptLog.RunId)
                if (label.Length < 36) label.Append(char.IsLetterOrDigit(value) || value == '-' || value == '_' ? value : '_');
            string path = Path.Combine(directory, "AutoSendCheck_" + label + "_" + Guid.NewGuid().ToString("N") + ".sav");
            Runner.Require(!File.Exists(path), "refusing to overwrite an existing save: " + path);
            var location = CameraControls2D.instance.getScreenXYForSaveCentring(
                (int)((float)(UnityEngine.Screen.width / 64) / PerfectPixelWithZoom.instance.getZoom()),
                (int)((float)(UnityEngine.Screen.height / 16) / PerfectPixelWithZoom.instance.getZoom()));
            int centreX = -1, centreY = -1;
            GameMap.instance.getRotationCentre(ref centreX, ref centreY);
            Runner.Require(EngineInterface.SaveSaveGame(path, location.x, location.y, centreX, centreY, false, false, false),
                "native save writer reported failure");
            Runner.Require(File.Exists(path) && new FileInfo(path).Length > 0, "native save file missing/empty");
            var header = MapFileManager.Instance.GetFileInfoFromFileName(path, path, 0, true);
            Runner.Require(header != null, "vanilla rejected the created save header");
            savePath = path; savedRules = snapshots.ToArray(); savedSession = session;
            AcceptLog.Info("AUTOSEND_SAVE path=" + savePath + " bytes=" + new FileInfo(path).Length + " rules=" + savedRules.Length);
        }

        private static IEnumerable<Wait> Reload(Step step)
        {
            Runner.Require(savePath != null && savedRules != null && File.Exists(savePath) &&
                Path.GetFileName(savePath).StartsWith("AutoSendCheck_", StringComparison.Ordinal), "no owned acceptance save to reload");
            Runner.Require(GameSteps.MenuReady(), "reload must begin in the main menu");
            lock (gate) { session = 0; pending = null; manualWaiting = false; }
            // Reuse the audited production runner load sequence, including header checks,
            // scene setup, native load and first-tick wait; no synthetic lifecycle event.
            MethodInfo load = typeof(GameSteps).GetMethod("LoadSave", BindingFlags.Static | BindingFlags.NonPublic);
            Runner.Require(load != null, "existing runner save loader unavailable");
            var loadStep = new Step { Id = step.Id + "-native", Args = new[] { savePath } };
            foreach (Wait wait in (IEnumerable<Wait>)load.Invoke(null, new object[] { loadStep })) yield return wait;
            var context = UiAcceptancePlugin.LastContext;
            Runner.Require(context != null && context.IsSave && !context.Mode.IsRealMultiplayer && context.SessionId != savedSession &&
                context.LocalPlayerId == sender, "reloaded mission identity/start kind differs");
            lock (gate) session = context.SessionId;
            foreach (var expected in savedRules)
            {
                yield return new Until("restored vanilla selection", () => OpenSelection(expected.Recipient, expected.Good));
                yield return new Until("restored epoch/stock ready", () => Flag(Display(), "Ready") && !Flag(Display(), "Pending"));
                object display = Display(), rule = Read(display, "Rule");
                Runner.Require(Number(display, "Reserve") == expected.Reserve && Flag(display, "Paused") == expected.Paused &&
                    Number(rule, "Minimum") == expected.Minimum && Number(rule, "Maximum") == expected.Maximum &&
                    Flag(rule, "Enabled") == expected.Enabled, "actual saved rule was not restored: " + expected.Describe());
                yield return new Until("restored rule labels rendered", RenderedValuesMatch);
                yield return NextFrame.Instance;
                CheckButtons();
                AcceptLog.Info("AUTOSEND_RESTORED " + expected.Describe());
            }
        }

        private static IEnumerable<Wait> Empty(Step step)
        {
            Runner.Require(OwnsSession() && savedSession != 0 && session != savedSession &&
                UiAcceptancePlugin.LastContext != null && !UiAcceptancePlugin.LastContext.IsSave,
                "fresh-reset check requires another newly created skirmish after the saved mission");
            int checkedRules = 0;
            foreach (eGoods good in AllyTransfers.SupportedGoods)
                foreach (int recipient in new[] { first, second })
                {
                    yield return new Until("fresh vanilla selection", () => OpenSelection(recipient, good));
                    yield return new Until("fresh epoch and ally ready", () => Flag(Display(), "Ready") && !Flag(Display(), "Pending"));
                    object display = Display(), rule = Read(display, "Rule");
                    Runner.Require(Number(display, "Reserve") == 0 && !Flag(display, "Paused") && Number(display, "ActiveRules") == 0 &&
                        Number(rule, "Minimum") == 0 && Number(rule, "Maximum") == 0 && !Flag(rule, "Enabled"),
                        "previous mission rules leaked into fresh skirmish for " + recipient + "/" + good);
                    checkedRules++;
                }
            yield return NextFrame.Instance;
            CheckButtons();
            AcceptLog.Info("AUTOSEND_FRESH checkedRules=" + checkedRules + " allDefaults=True");
        }

        private static IEnumerable<Wait> Manual(Step step)
        {
            yield return new Until("manual gold selection", () => OpenSelection(first, eGoods.STORED_GOLD));
            foreach (Wait wait in Change("Paused", 0, eGoods.STORED_GOLD, 0, true, d => Flag(d, "Paused"))) yield return wait;
            lock (gate)
            {
                done = false; failure = evidence = null;
                pending = new Work { Name = "manual-gold-setup", Good = eGoods.STORED_GOLD,
                    Source = 1600, ExpectedSource = 1600 };
            }
            yield return AwaitWork(); CheckWork();
            yield return new Until("manual gold quantity 10", () => OpenSelection(first, eGoods.STORED_GOLD));
            yield return NextFrame.Instance;
            CheckButtons();
            lock (gate) { manualResult = null; manualWaiting = true; }
            object panel = GameSteps.Read(typeof(MainViewModel), MainViewModel.Instance, "HUDAlliesPanel", BindingFlags.Instance);
            panel.GetType().GetMethod("ButtonClicked", Instance, null, new[] { typeof(string) }, null)
                .Invoke(panel, new object[] { "ConfirmGoods" });
            yield return new Until("actual vanilla Chore delivery of 10 gold", () => { lock (gate) return manualResult != null; });
            lock (gate)
            {
                var result = manualResult;
                manualWaiting = false;
                Runner.Require(result.Status == AllyTransferStatus.Completed && result.OriginalWasCalled &&
                    result.SenderDebit == 10 && result.RecipientCredit == 10 && !result.HasNestedTransfer,
                    "manual vanilla gift was not a balanced native 10-gold transfer: " + result.Status);
                AcceptLog.Info("AUTOSEND_MANUAL original=True debit=" + result.SenderDebit + " credit=" + result.RecipientCredit);
            }
        }

        private static void ObserveManual(AllyTransferPostEventArgs args)
        {
            lock (gate)
            {
                if (!OwnsSession() || !manualWaiting) return;
                var result = args.Result;
                if (result.SenderPlayerId == sender && result.RecipientPlayerId == first &&
                    result.Good == eGoods.STORED_GOLD && result.RequestedAmount == 10) manualResult = result;
            }
        }

        private static IEnumerable<Wait> Change(string operation, int recipient, eGoods good, int amount, bool enabled, Func<object, bool> acknowledged)
        {
            yield return new Until("ready before " + operation, () => Flag(Display(), "Ready") && !Flag(Display(), "Pending"));
            // This is the production Unity entry; it submits an authenticated native Chore.
            edit.Invoke(runtime, new object[] { Enum.Parse(editType, operation), recipient, (int)good, amount, enabled });
            yield return new Until("Chore acknowledgement " + operation, () =>
            {
                object display = Display();
                if (Flag(display, "Pending")) return false;
                Runner.Require(string.IsNullOrEmpty((string)Read(display, "Feedback")), "edit rejected: " + Read(display, "Feedback"));
                return acknowledged(display);
            });
            yield return new Until("rendered rule values after " + operation, RenderedValuesMatch);
        }

        private static bool RenderedValuesMatch()
        {
            object display = Display(), rule = Read(display, "Rule"), view = Read(runtime, "view");
            return RenderedValue(view, "reserve", Number(display, "Reserve")) &&
                RenderedValue(view, "minimum", Number(rule, "Minimum")) &&
                RenderedValue(view, "maximum", Number(rule, "Maximum"));
        }

        private static bool RenderedValue(object view, string member, int expected)
        {
            var box = Read(Read(view, member), "Box") as TextBox;
            return box != null && box.Text == expected.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static object Display()
        {
            Runner.Require(AllyGoodsPanelExtensions.TryGetContext(out var context) && context.IsOpen && context.IsSendMode && context.HasSelection,
                "vanilla Send selection context unavailable");
            Runner.Require(context.LocalPlayerId == sender && context.ViewedPlayerId == sender,
                "selection is not the controlled sender");
            return readDisplay.Invoke(runtime, new object[] { context });
        }

        private static bool OpenSelection(int recipient, eGoods good)
        {
            HUD_AlliesPanel.Open(true);
            object panel = GameSteps.Read(typeof(MainViewModel), MainViewModel.Instance, "HUDAlliesPanel", BindingFlags.Instance);
            int count = Number(panel, "num_allies");
            int[] ids = (int[])Read(panel, "ally");
            if (count <= 0 || count > ids.Length) return false;
            int index = Array.IndexOf(ids, recipient, 0, count);
            if (index < 0) return false;
            MethodInfo button = panel.GetType().GetMethod("ButtonClicked", Instance, null, new[] { typeof(string) }, null);
            Runner.Require(button != null, "vanilla allies command unavailable");
            button.Invoke(panel, new object[] { index.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            button.Invoke(panel, new object[] { "Send" });
            button.Invoke(panel, new object[] { "G" + ((int)good).ToString("00", System.Globalization.CultureInfo.InvariantCulture) });
            // Send can prefill an AI's outstanding request. Clear that quantity with
            // vanilla decrement commands before selecting our exact test amount.
            for (int attempt = 0; attempt < 128 && Number(panel, "selectedGoodsAmount") > 0; attempt++)
                button.Invoke(panel, new object[] { "X500-" });
            Runner.Require(Number(panel, "selectedGoodsAmount") <= 0, "vanilla prefilled quantity exceeds the bounded selection reset");
            // Use the actual amount button; zero remains covered by the broker contract tests.
            button.Invoke(panel, new object[] { "X10" });
            return AllyGoodsPanelExtensions.TryGetContext(out var context) && context.IsOpen && context.IsSendMode &&
                context.RecipientPlayerId == recipient && context.Good == good && context.Amount == 10;
        }

        private static void CheckButtons()
        {
            object view = Read(runtime, "view");
            foreach (string member in new[] { "reserve", "minimum", "maximum" })
            {
                var slider = Read(Read(view, member), "Slider") as Slider;
                var box = Read(Read(view, member), "Box") as TextBox;
                Runner.Require(slider != null && box != null && slider.IsEnabled && box.IsEnabled,
                    "actual Noesis numeric editor " + member + " is disabled/missing");
            }
            var root = Read(view, "root") as FrameworkElement;
            Runner.Require(root != null && root.Visibility == Visibility.Visible && root.ActualWidth > 0 && root.ActualHeight > 0,
                "AutoSend section has no visible Noesis layout");
        }

        private static bool OwnsSession() => session != 0 && Interlocked.Read(ref UiAcceptancePlugin.StartedSession) == session &&
            Interlocked.Read(ref UiAcceptancePlugin.EndedSession) != session;

        private static void Before(int tick)
        {
            lock (gate)
            {
                if (!OwnsSession() || pending == null || pending.Prepared || unchecked((uint)tick) % 40 != 0) return;
                try
                {
                    var players = GamePlayerManagerAPI.Instance;
                    if (pending.Name == "bootstrap")
                    {
                        var targets = new List<int>();
                        foreach (int id in fixtureAI)
                            if (players.GetLordUnitId(id) > 0) targets.Add(id);
                        if (!bootstrapReported)
                        {
                            var rows = new List<string>();
                            for (int id = 1; id <= 8; id++) rows.Add(id + ":ai=" + players.IsAIPlayer(id) +
                                ",type=" + players.GetAILord(id) + ",lord=" + players.GetLordUnitId(id));
                            AcceptLog.Info("AUTOSEND_FIXTURE rosterAI=" + string.Join(",", fixtureAI) + " native=" + string.Join(";", rows));
                            bootstrapReported = true;
                        }
                        // Roster identity is explicit. Lord initialization is a separate,
                        // bounded wait; unknown native AI classifications are observations.
                        if (targets.Count < 3 || players.GetLordUnitId(sender) <= 0) return;
                        first = targets[0]; second = targets[1];
                        players.SetPlayerTeam(sender, sender); players.SetPlayerTeam(first, sender); players.SetPlayerTeam(second, sender);
                        for (int index = 2; index < targets.Count; index++) players.SetPlayerTeam(targets[index], targets[index]);
                        if (!players.IsPlayerIdValid(sender) || players.GetLordUnitId(sender) <= 0) throw new InvalidOperationException("sender has no living Lord");
                        foreach (int id in new[] { sender, first, second }) SetStock(id, eGoods.STORED_WOOD_PLANKS, id == sender ? 80 : 0);
                        foreach (int id in new[] { sender, first, second })
                            if (!AllyTransfers.TryGetCapacity(id, eGoods.STORED_WOOD_PLANKS, out var capacity, out string why) ||
                                !capacity.HasStorage || capacity.FreeAmount < (id == sender ? 0 : 100))
                                throw new InvalidOperationException("wood storage fixture rejected for player " + id + ": " + why);
                        evidence = "bootstrap teams and physical wood storage verified"; pending = null; done = true; return;
                    }
                    SetStock(sender, pending.Good, pending.Source);
                    SetStock(first, pending.Good, pending.RecipientA); SetStock(second, pending.Good, pending.RecipientB);
                    if (!AllyTransfers.TryGetCapacity(first, pending.Good, out var capA, out string reasonA) || capA.FreeAmount < pending.ExpectedA ||
                        !AllyTransfers.TryGetCapacity(second, pending.Good, out var capB, out string reasonB) || capB.FreeAmount < pending.ExpectedB)
                        throw new InvalidOperationException("native fixture capacity insufficient");
                    pending.Prepared = true; pending.Tick = tick;
                }
                catch (Exception error) { failure = error.ToString(); pending = null; done = true; }
            }
        }

        private static void After(int tick)
        {
            lock (gate)
            {
                if (!OwnsSession() || pending == null || !pending.Prepared || pending.Tick != tick) return;
                try
                {
                    int own = Stock(sender, pending.Good), a = Stock(first, pending.Good), b = Stock(second, pending.Good);
                    evidence = pending.Name + " tick=" + unchecked((uint)tick) + " stock=" + own + "/" + a + "/" + b +
                        " expected=" + pending.ExpectedSource + "/" + pending.ExpectedA + "/" + pending.ExpectedB;
                    if (own != pending.ExpectedSource || a != pending.ExpectedA || b != pending.ExpectedB)
                        throw new InvalidOperationException("post-planner native balance mismatch: " + evidence);
                }
                catch (Exception error) { failure = error.ToString(); }
                finally { pending = null; done = true; }
            }
        }

        private static int Stock(int player, eGoods good) => good == eGoods.STORED_GOLD ?
            GamePlayerManagerAPI.Instance.GetPlayerGold(player) : GamePlayerManagerAPI.Instance.GetGoodAmount(player, good);
        private static void SetStock(int player, eGoods good, int target)
        {
            var players = GamePlayerManagerAPI.Instance;
            int before = Stock(player, good);
            int? freeBefore = AllyTransfers.TryGetCapacity(player, good, out var capacity, out _) ? (int?)capacity.FreeAmount : null;
            bool? addReturned = null;
            if (good == eGoods.STORED_GOLD) players.SetPlayerGold(player, target);
            else
            {
                if (before > target) players.RemoveGood(player, good, before - target);
                else if (before < target) addReturned = players.TryAddGood(player, good, target - before);
            }
            int actual = Stock(player, good);
            string observation = "player=" + player + " good=" + good + " before=" + before + " free=" + freeBefore +
                " addReturned=" + addReturned + " actual=" + actual + " target=" + target;
            // SE Post may replace the return flag, and native failure can have partial
            // effects. The test requires the exact readback, never an assumed mutation.
            if (actual != target) throw new InvalidOperationException("native fixture stock mismatch: " + observation);
            if (addReturned == false) AcceptLog.Info("AUTOSEND_FIXTURE_RETURN actual stock verified despite false status: " + observation);
        }
        private static object Read(object owner, string name) => GameSteps.Read(owner.GetType(), owner, name, BindingFlags.Instance);
        private static bool Flag(object owner, string name) => (bool)Read(owner, name);
        private static int Number(object owner, string name) => Convert.ToInt32(Read(owner, name));
    }
}
