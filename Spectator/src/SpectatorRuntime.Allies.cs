using System;
using System.Collections.Generic;
using System.Reflection;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static HUD_AlliesPanel allyTestPanel;
        private static Grid allyTestHost, allyTestBody, allyGoodsFooter;
        private static AllyTestViewModel allyTestModel;
        private static long allyTestSession;
        private static bool allyTestFailed, allyTestDirty, allyWasOpen;
        private static EngineInterface.PlayState allyTestSnapshot;
        private static readonly List<ToolbarProperty> allyTestOverrides = new List<ToolbarProperty>();
        private static readonly TextBlock[] allyStockLabels = new TextBlock[25];
        private static readonly GoodsBalance[] allyBalances = new GoodsBalance[25];
        private static readonly Button[] allyCards = new Button[9];
        private static readonly Image[] allyFaces = new Image[9];
                private static readonly Dictionary<TextBlock, string> allyStockValues = new Dictionary<TextBlock, string>();
        private static readonly Dictionary<string, FrameworkElement> allyElements = new Dictionary<string, FrameworkElement>();
        private static readonly FieldInfo allySelectedGood = AllyField("selectedGoods");
        private static readonly FieldInfo allySelectedAmount = AllyField("selectedGoodsAmount");
        private static readonly MethodInfo allyUpdateGoods = typeof(HUD_AlliesPanel).GetMethod("UpdateGoods", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static bool IsAllyTestMode => allyTestModel?.State.Enabled == true && IsCurrentSession(allyTestSession) && IsToolsSession();

        private static bool IsAllyOccupied(int player) => player >= 1 && player <= 8 &&
            (GameData.Instance.lastGameState.is_human_or_skirmish_player(player) || (IsEditorToolsSession() && IsToolsPlayer(player, false)));

        private static FieldInfo AllyField(string name)
        {
            var field = typeof(HUD_AlliesPanel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(int) ? field : null;
        }

        internal static void OnAllyPanelClosed()
        {
            if (allyTestHost == null || !allyWasOpen) return;
            allyWasOpen = false;
            CloseAllyTooltips();
            allyTestSnapshot = null;
            allyTestDirty = true;
        }

        private static void RefreshAllyTestControlsSafely()
        {
            if (allyTestFailed) return;
            try
            {
                if (!IsToolsSession())
                {
                    if (allyTestModel != null) DetachAllyTestControls();
                    return;
                }
                var panel = MainViewModel.Instance.HUDAlliesPanel;
                if (allyTestPanel != null && (!ReferenceEquals(panel, allyTestPanel) || !IsCurrentSession(allyTestSession)))
                    DetachAllyTestControls();
                if (panel == null || !panel.IsLoaded) return;
                if (allyTestPanel == null) AttachAllyTestControls(panel);
                if (!MainViewModel.Instance.AlliesPanelVisible) { OnAllyPanelClosed(); return; }
                allyWasOpen = true;
                if (!IsAllyTestMode) return;
                ApplyAllyTestPages();
                var snapshot = GameData.Instance.lastGameState;
                if (!allyTestDirty && ReferenceEquals(snapshot, allyTestSnapshot)) return;
                allyTestDirty = false; allyTestSnapshot = snapshot;
                RefreshAllyTestContents();
            }
            catch (Exception error)
            {
                DetachAllyTestControls();
                allyTestFailed = true;
                LogOnce("SPECTATOR_ALLIES_TEST_UNAVAILABLE: " + error);
            }
        }

        private static void AttachAllyTestControls(HUD_AlliesPanel panel)
        {
            if (allySelectedGood == null || allySelectedAmount == null || allyUpdateGoods == null ||
                allyUpdateGoods.ReturnType != typeof(void) || allyUpdateGoods.GetParameters().Length != 0)
                throw new MissingMemberException("Vanilla Allies amount/refresh contract changed.");
            var host = panel.FindName("SpectatorAllyTest") as Grid;
            if (host == null) throw new MissingMemberException("Spectator Allies XAML patch missing.");
            allyTestPanel = panel; allyTestSession = CurrentSessionId; allyTestHost = host;
            foreach (string name in new[] { "AllyTestBody", "AllyTestFooterExtras", "AllyTestToggle", "AllyTestClose", "AllyTestBack",
                "AllyTestTitle", "AllyTestActor", "AllyTestHomeActions", "AllyTestOrderActions", "AllyTestGoods", "AllyTestOrders",
                "AllyTestDefend", "AllyTestCancel", "AllyTestOrderIcon", "AllyTestOrderLabel", "AllyTestForce", "AllyTestDebit",
                "AllyVanillaGoods", "AllyVanillaCredit", "AllyVanillaReset", "AllyVanillaSummary", "AllyTestInfo", "AllyTestStatus" })
            {
                var element = panel.FindName(name) as FrameworkElement;
                if (element == null) throw new MissingMemberException("Missing Allies element: " + name);
                allyElements.Add(name, element);
            }
            allyTestBody = (Grid)allyElements["AllyTestBody"];
            allyGoodsFooter = (Grid)allyElements["AllyTestFooterExtras"];
            for (int i = 1; i <= 8; i++)
            {
                allyCards[i] = panel.FindName("AllyTestPlayer" + i) as Button;
                allyFaces[i] = panel.FindName("AllyTestFace" + i) as Image;
                foreach (string prefix in new[] { "AllyTestSlot", "AllyTestHuman" })
                    allyElements.Add(prefix + i, (FrameworkElement)panel.FindName(prefix + i));
                if (allyCards[i] == null || allyFaces[i] == null) throw new MissingMemberException("Allies player portrait " + i);
            }
            for (int i = 1; i < allyStockLabels.Length; i++)
                allyStockLabels[i] = panel.FindName("AllyStock" + i) as TextBlock;
            allyTestModel = new AllyTestViewModel(ExecuteAllyTestCommand);
            host.DataContext = allyTestModel;
            allyGoodsFooter.DataContext = allyTestModel;
            host.Visibility = Visibility.Visible;
            panel.PreviewMouseUp += OnAllyTestPointerUp;
            panel.IsVisibleChanged += OnAllyVisibilityChanged;

            SetAllyTip("AllyTestDefend", "Tools.DefendHelp");
            SetAllyTip("AllyTestCancel", "Tools.CancelHelp");
            SetAllyTip("AllyTestForce", "Tools.ForceHelp");
            SetAllyTip("AllyTestBack", "AlliesTest.Back");
            SetAllyTip("AllyTestClose", "AlliesTest.Close");
            allyTestDirty = true;
        }
        private static void SetAllyTip(string name, string key)
        { if (ToolTipService.GetToolTip(allyElements[name]) is ToolTip tip) tip.Content = SerpLocalization.Get(key); }

        private static void OnAllyVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (allyTestPanel?.IsVisible != true) OnAllyPanelClosed();
            else allyTestDirty = true;
        }
        private static void OnAllyTestPointerUp(object sender, MouseButtonEventArgs args) => allyTestDirty = true;

        private static void ExecuteAllyTestCommand(string command)
        {
            try { ExecuteAllyTestCommandCore(command); }
            catch (Exception error)
            {
                DetachAllyTestControls(); allyTestFailed = true;
                LogOnce("SPECTATOR_ALLIES_TEST_COMMAND_FAILED: " + error);
            }
        }
        private static void ExecuteAllyTestCommandCore(string command)
        {
            if (allyTestPanel == null || !IsCurrentSession(allyTestSession) || !IsToolsSession() ||
                MainViewModel.Instance.AlliesPanelVisible != true) return;
            var model = allyTestModel;
            if (command == "Toggle")
            {
                CloseAllyTooltips();
                SpectatorPerspectiveRuntime.CancelAllyRefresh();
                model.State.Toggle(humanSelectedOwner);
                model.Refresh(); model.Status = "";
                if (!model.State.Enabled)
                {
                    RestoreAllyTestOverrides();
                    HideAllyTestPages();
                    HUD_AlliesPanel.Open(true);
                }
                else
                {
                    // Only these exact foreign properties are borrowed. Never snapshot the entire visual tree.
                    allyTestOverrides.Add(new ToolbarProperty(allyElements["AllyVanillaCredit"], UIElement.IsEnabledProperty));
                    BorrowAllyProperty(allyElements["AllyVanillaCredit"], Button.CommandProperty, model.Command);
                    BorrowAllyProperty(allyElements["AllyVanillaCredit"], Button.CommandParameterProperty, "Credit");
                    BorrowAllyProperty(allyElements["AllyVanillaCredit"], UIElement.VisibilityProperty, Visibility.Visible);
                    BorrowAllyProperty(allyElements["AllyVanillaReset"], UIElement.IsEnabledProperty, true);
                    BorrowAllyProperty(allyElements["AllyVanillaReset"], Button.CommandProperty, model.Command);
                    BorrowAllyProperty(allyElements["AllyVanillaReset"], Button.CommandParameterProperty, "Reset");
                    BorrowAllyProperty(allyElements["AllyVanillaSummary"], FrameworkElement.MarginProperty, new Thickness(75,0,0,14));
                    BorrowAllyProperty(allyElements["AllyVanillaSummary"], FrameworkElement.WidthProperty, 390f);
                    // These additions live inside the original footer, outside the host resource scope.
                    var tipStyle = (Style)allyTestHost.Resources["AllyTooltip"];
                    foreach (string name in new[] { "AllyTestDebit", "AllyVanillaCredit", "AllyVanillaReset", "AllyVanillaSummary", "AllyTestStatus" })
                    {
                        var tip = new ToolTip { Style = tipStyle, Content = "" };
                        allyTestOverrides.Add(new ToolbarProperty(allyElements[name], ToolTipService.ToolTipProperty));
                        allyTestOverrides.Add(new ToolbarProperty(allyElements[name], ToolTipService.ShowDurationProperty));
                        ToolTipService.SetToolTip(allyElements[name], tip);
                        ToolTipService.SetShowDuration(allyElements[name], 60000);
                    }
                    for (int i = 1; i < allyStockLabels.Length; i++) if (allyStockLabels[i] != null)
                    {
                        var label = allyStockLabels[i];
                        allyTestOverrides.Add(new ToolbarProperty(label, TextBlock.TextProperty));
                        BorrowAllyProperty(label, UIElement.IsHitTestVisibleProperty, true);
                        BorrowAllyProperty(label, ToolTipService.ShowDurationProperty, 60000);
                        BorrowAllyProperty(label, ToolTipService.ToolTipProperty, new ToolTip { Style = tipStyle, Content = "" });
                    }
                    foreach (var element in AllyDescendants(allyElements["AllyVanillaGoods"]))
                    {
                        if (!(element is Button button) || !(button.CommandParameter is string key) ||
                            (key != "X5" && key != "X10" && key != "X25" && key != "X100" && key != "X500")) continue;
                        if (button.Command == null) throw new MissingMemberException("Vanilla amount command missing.");
                        BorrowAllyProperty(button, Button.CommandProperty, new CheckedAllyAmountCommand(button.Command,
                            () => (int)allySelectedAmount.GetValue(allyTestPanel),
                            value => allySelectedAmount.SetValue(allyTestPanel, value),
                            () => { allyUpdateGoods.Invoke(allyTestPanel, null); allyTestDirty = true; }));
                    }
                    var back = allyTestPanel.FindName("AllyVanillaGoodsBack") as Button;
                    if (back == null) throw new MissingMemberException("Vanilla goods Back missing.");
                    BorrowAllyProperty(back, Button.CommandProperty, model.Command);
                    BorrowAllyProperty(back, Button.CommandParameterProperty, "Back");
                    ApplyAllyTestPages();
                }
            }
            else if (!model.State.Enabled) return;
            else if (command == "Exit") MainViewModel.Instance.AlliesPanelVisible = false;
            else if (command == "Back") { model.State.Open(model.State.Page == "Protect" ? "Orders" : ""); model.Status = ""; }
            else if (command == "Protect") { if (model.State.Page != "Orders") return; model.State.Open("Protect"); model.Status = ""; }
            else if (command == "Reset")
            {
                if (model.State.Page != "Goods") return;
                allySelectedGood.SetValue(allyTestPanel, -1); allySelectedAmount.SetValue(allyTestPanel, -1);
                allyUpdateGoods.Invoke(allyTestPanel, null); model.Status = "";
            }
            else if (command == "Goods" || command == "Orders")
            {
                if (command == "Goods" && !IsToolsPlayer(model.State.Recipient, false)) return;
                if (command == "Orders" && (!IsToolsPlayer(model.State.Recipient, true) || !GamePlayerManagerAPI.Instance.IsAIPlayer(model.State.Recipient))) return;
                model.State.Open(command); model.Status = "";
                if (command == "Goods")
                {
                    allySelectedGood.SetValue(allyTestPanel, (int)eGoods.STORED_WOOD_PLANKS);
                    allySelectedAmount.SetValue(allyTestPanel, 0);
                    ApplyAllyTestPages();
                    allyUpdateGoods.Invoke(allyTestPanel, null);
                }
            }
            else if (command != null && command.StartsWith("Player", StringComparison.Ordinal) && int.TryParse(command.Substring(6), out int player))
            {
                if (!IsAllyOccupied(player)) return;
                if (model.State.Page == "Orders" || model.State.Page == "Protect")
                {
                    bool defend = model.State.Page == "Protect";
                    var state = GameData.Instance.lastGameState;
                    int actor = model.State.Recipient;
                    if (!IsToolsPlayer(actor, true) || !IsToolsPlayer(player, false) ||
                        (!defend && (actor == player || state.teams[actor] == state.teams[player]))) return;
                    model.State.SelectTarget(player);
                    QueueTestAction(new TestAction { Kind = defend ? TestActionKind.Defend : TestActionKind.Attack,
                        Actor = actor, Target = player, Requester = defend ? player : actor, Force = model.Force, AllyReply = model });
                    model.State.Open("");
                }
                else { model.State.Select(player); model.Status = ""; }
            }
            else if (command == "Credit" || command == "Debit")
            {
                if (model.State.Page != "Goods") return;
                int amount = (int)allySelectedAmount.GetValue(allyTestPanel);
                var good = (eGoods)(int)allySelectedGood.GetValue(allyTestPanel);
                if (amount <= 0 || !TestToolsViewModel.IsGrantable(good)) return;
                QueueTestAction(new TestAction { Kind = TestActionKind.Credit, Actor = model.State.Recipient,
                    Good = good, Amount = command == "Debit" ? -amount : amount, AllyReply = model });
            }
            else if (command == "Cancel")
            {
                if (model.State.Page != "Orders") return;
                QueueTestAction(new TestAction { Kind = TestActionKind.Cancel, Actor = model.State.Recipient,
                    Requester = model.State.Recipient, Force = model.Force, AllyReply = model });
                model.State.Open("");
            }
            allyTestDirty = true;
            if (model.State.Enabled && MainViewModel.Instance.AlliesPanelVisible) ApplyAllyTestPages();
        }

        private static void ApplyAllyTestPages()
        {
            var model = MainViewModel.Instance;
            string page = allyTestModel.State.Page;
            // Vanilla Update must not traverse its six-slot ally arrays while displaying the independent roster.
            model.Allies_MainViewVis = model.Allies_NoAlliesViewVis = model.Allies_OrdersViewVis = false;
            model.Allies_GoodsViewVis = page == "Goods";
            model.Allies_SendGoodsViewVis = page == "Goods";
            model.Allies_RequestGoodsViewVis = false;
            SetDockVisibility(allyTestBody, page != "Goods");
            SetDockVisibility(allyGoodsFooter, page == "Goods");
            SetDockVisibility(allyElements["AllyTestClose"], page == "");
            SetDockVisibility(allyElements["AllyTestBack"], page != "" && page != "Goods");
            SetDockVisibility(allyElements["AllyTestHomeActions"], page == "");
            SetDockVisibility(allyElements["AllyTestOrderActions"], page == "Orders");
            SetDockVisibility(allyElements["AllyTestInfo"], page == "");
            SetDockVisibility(allyElements["AllyTestForce"], page == "Orders" || page == "Protect");
        }

        private static unsafe void RefreshAllyTestContents()
        {
            var state = GameData.Instance.lastGameState;
            var model = allyTestModel;
            bool orders = model.State.Page == "Orders", protect = model.State.Page == "Protect";
            int actor = model.State.Recipient;
            int target = model.State.Target;
            bool valid = IsToolsPlayer(actor, false), ai = IsToolsPlayer(actor, true) && GamePlayerManagerAPI.Instance.IsAIPlayer(actor);
            Text("AllyTestTitle", protect ? SerpLocalization.Get("Tools.Defend") : orders ? SerpLocalization.Get("Tools.Orders") :
                Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, 1));
            Text("AllyTestActor", actor > 0 ? PlayerLabel(actor) : SerpLocalization.Get("Tools.InvalidTarget"));
            ((TextBlock)allyElements["AllyTestActor"]).Foreground = PlayerBrush(actor);
            var players = new List<int>();
            for (int i = 1; i <= 8; i++)
                if (IsAllyOccupied(i) && (!orders || (i != actor && state.teams[i] != state.teams[actor]))) players.Add(i);
            allyTestBody.Height = orders ? (players.Count > 6 ? 260 : 180) : protect ? 180 : 320;
            for (int i = 1; i <= 8; i++)
            {
                int index = players.IndexOf(i);
                var slot = allyElements["AllyTestSlot" + i];
                SetDockVisibility(slot, index >= 0);
                if (index < 0) continue;
                bool live = IsToolsPlayer(i, false);
                slot.Opacity = live ? (!orders && !protect && i != actor ? 0.5f : 1f) : 0.35f;
                slot.IsEnabled = !orders && !protect || live && ai && nativeOrder != null;
                float x = orders ? 170 + (index % (players.Count > 6 ? 4 : 6)) * 80 : players.Count > 6 ? 30 + index * 80 : 60 + index * 100;
                float y = orders ? 60 + (index / (players.Count > 6 ? 4 : 6)) * 80 : 60;
                slot.Margin = new Thickness(x, y, 0, 0);
                allyFaces[i].Source = orders || protect || i == actor ? MainViewModel.Instance.getAIFaceBackground(i) : null;
                var face = state.is_skirmish_player(i) ? MainViewModel.Instance.getAIFace(state.computer_register[i], true) :
                    Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(i));
                PropEx.SetSprite1(allyCards[i], face); PropEx.SetSprite2(allyCards[i], face);
                PropEx.SetSprite3(allyCards[i], face); PropEx.SetSprite4(allyCards[i], face);
                SetDockVisibility(allyElements["AllyTestHuman" + i], !state.is_skirmish_player(i));
                if (ToolTipService.GetToolTip(allyCards[i]) is ToolTip tip) tip.Content = PlayerLabel(i);
            }
            allyElements["AllyTestGoods"].IsEnabled = valid;
            allyElements["AllyTestOrders"].IsEnabled = ai;
            allyElements["AllyTestDefend"].IsEnabled = ai && nativeOrder != null;
            allyElements["AllyTestCancel"].IsEnabled = ai && nativeOrder != null;
            var order = PresentedAiOrder.Read(actor, state.skirmish_order, state.skirmish_order_player, state.skirmish_order_from_player);
            ((Image)allyElements["AllyTestOrderIcon"]).Source = OrderIcon(order);
            Text("AllyTestOrderLabel", SerpLocalization.Get(order.Mode == 1 ? "Tools.Attack" : order.Mode == 2 ? "Tools.Defend" : "Tools.NoOrder") +
                (order.Mode != 0 ? " · " + PlayerLabel(order.Target) : ""));
            if (ToolTipService.GetToolTip(allyElements["AllyTestStatus"]) is ToolTip statusTip) statusTip.Content = model.Status;
            if (model.State.Page != "Goods") return;
            int amount = (int)allySelectedAmount.GetValue(allyTestPanel);
            var good = (eGoods)(int)allySelectedGood.GetValue(allyTestPanel);
            bool canBook = valid && amount > 0 && TestToolsViewModel.IsGrantable(good);
            allyElements["AllyVanillaCredit"].IsEnabled = allyElements["AllyTestDebit"].IsEnabled = canBook;
            lock (engineLock)
            {
                for (int i = 1; i < allyStockLabels.Length; i++) if (allyStockLabels[i] != null)
                    allyBalances[i] = valid ? ReadDebitBalance(actor, (eGoods)i) : new GoodsBalance(0,0);
            }
            for (int i = 1; i < allyStockLabels.Length; i++) if (allyStockLabels[i] != null)
            {
                var balanceAtGood = allyBalances[i];
                if (ToolTipService.GetToolTip(allyStockLabels[i]) is ToolTip stockTip)
                    stockTip.Content = PlayerLabel(actor) + " · " + SerpLocalization.Get("AlliesTest.Balance")
                        .Replace("{Stored}", balanceAtGood.Stored.ToString()).Replace("{Incoming}", balanceAtGood.Incoming.ToString());
                string value = balanceAtGood.Stored.ToString();
                if (!allyStockValues.TryGetValue(allyStockLabels[i], out string previous) || value != previous)
                { allyStockLabels[i].Text = value; allyStockValues[allyStockLabels[i]] = value; }
            }
            GoodsBalance selected = valid && TestToolsViewModel.IsGrantable(good) ? allyBalances[(int)good] : new GoodsBalance(0,0);
            string balance = (actor > 0 ? PlayerLabel(actor) + " · " : "") + SerpLocalization.Get("AlliesTest.Balance")
                .Replace("{Stored}", selected.Stored.ToString()).Replace("{Incoming}", selected.Incoming.ToString());
            foreach (string name in new[] { "AllyVanillaSummary", "AllyVanillaCredit", "AllyVanillaReset", "AllyTestDebit" })
                if (ToolTipService.GetToolTip(allyElements[name]) is ToolTip tip)
                    tip.Content = name == "AllyTestDebit" ? SerpLocalization.Get("AlliesTest.Debit") + " · " + balance :
                        name == "AllyVanillaCredit" ? SerpLocalization.Get("AlliesTest.Credit") + " · " + balance :
                        name == "AllyVanillaReset" ? SerpLocalization.Get("AlliesTest.Reset") : balance;

        }

        private static void Text(string name, string value)
        { var label = (TextBlock)allyElements[name]; if (label.Text != value) label.Text = value; }
        private static void BorrowAllyProperty(DependencyObject target, DependencyProperty property, object value)
        { allyTestOverrides.Add(new ToolbarProperty(target, property)); target.SetValue(property, value); }
        private static void RestoreAllyTestOverrides()
        { foreach (var property in allyTestOverrides) property.Restore(); allyTestOverrides.Clear(); allyStockValues.Clear(); }
        private static void HideAllyTestPages()
        {
            if (allyTestBody == null) return;
            SetDockVisibility(allyTestBody, false); SetDockVisibility(allyGoodsFooter, false);
            SetDockVisibility(allyElements["AllyTestClose"], false); SetDockVisibility(allyElements["AllyTestBack"], false);
        }
        private static void CloseAllyTooltips()
        {
            if (allyTestHost == null) return;
            foreach (var root in new DependencyObject[] { allyTestHost, allyGoodsFooter, allyElements.ContainsKey("AllyVanillaGoods") ? allyElements["AllyVanillaGoods"] : null })
                if (root != null) foreach (var element in AllyDescendants(root))
                    if (ToolTipService.GetToolTip(element) is ToolTip tip) tip.IsOpen = false;
        }
        private static IEnumerable<DependencyObject> AllyDescendants(DependencyObject root)
        {
            yield return root;
            for (int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in AllyDescendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        private static void DetachAllyTestControls()
        {
            CloseAllyTooltips();
            RestoreAllyTestOverrides();
            bool wasEnabled = allyTestModel?.State.Enabled == true;
            if (allyTestHost != null) { allyTestHost.Visibility = Visibility.Collapsed; allyTestHost.DataContext = null; }
            if (allyTestPanel != null)
            {
                allyTestPanel.PreviewMouseUp -= OnAllyTestPointerUp;
                allyTestPanel.IsVisibleChanged -= OnAllyVisibilityChanged;
                if (wasEnabled && MainViewModel.viewModelLoaded && ReferenceEquals(MainViewModel.Instance.HUDAlliesPanel, allyTestPanel))
                    MainViewModel.Instance.AlliesPanelVisible = false;
            }
            if (allyGoodsFooter != null) { allyGoodsFooter.Visibility = Visibility.Collapsed; allyGoodsFooter.DataContext = null; }
            allyTestModel?.State.Reset();
            allyTestModel = null; allyTestPanel = null; allyTestHost = allyTestBody = allyGoodsFooter = null;
            allyTestSession = 0; allyWasOpen = false; allyTestSnapshot = null; allyTestDirty = true; allyTestFailed = false;
            allyElements.Clear();
            Array.Clear(allyCards, 0, 9); Array.Clear(allyFaces, 0, 9); Array.Clear(allyStockLabels, 0, allyStockLabels.Length);
        }
    }
}
