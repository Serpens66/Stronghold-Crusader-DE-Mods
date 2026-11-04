using CrusaderDE;
using Noesis;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace Spectator
{
    public sealed class TestToolsViewModel : INotifyPropertyChanged
    {
        private readonly Action<string> execute;
        private readonly Func<string, bool> canExecute;
        private readonly Func<bool> prepareDockedTools;
        private string amount = "0", selectedGood = "";
        private readonly ToolbarFeedback feedback = new ToolbarFeedback();
        private readonly ToolbarMenuState menu = new ToolbarMenuState();
        private bool powerFlag, force, hasRecipient, ownBuildMenu;
        private bool expanded => menu.Expanded;
        private bool goods => menu.View == "Goods";
        private bool orders => menu.View == "Orders";
        private ToolPower[] powerItems = InitialPowers();
        private static ToolPower[] InitialPowers()
        {
            var result = new ToolPower[8];
            for (int i = 0; i < result.Length; i++) result[i] = new ToolPower { Id = i, Name = "", Help = "" };
            return result;
        }
        private eGoods good = eGoods.STORED_WOOD_PLANKS;
        internal int Target;
        private string locale;
        public event PropertyChangedEventHandler PropertyChanged;
        public ToolsText Text { get; } = new ToolsText();
        public ICommand ActionCommand { get; }
        public string Amount { get => amount; set { if (amount == value || !CreditInput.IsEditable(value)) return; amount = value; Changed(nameof(Amount)); Changed(nameof(CanCredit)); } }
        internal void SetActionStatus(TestActionKind kind, string value, string page = null)
        {
            SetStatus(value, kind == TestActionKind.Credit ? "Goods" :
                kind == TestActionKind.Attack || kind == TestActionKind.Defend || kind == TestActionKind.Cancel ? "Orders" : page ?? menu.View);
        }
        private void SetStatus(string value, string page)
        {
            if (feedback.ForPage(page) == value) return;
            feedback.Set(page, value);
            Changed(nameof(Status)); Changed(nameof(StatusVisibility)); Changed(nameof(GoodsStatusVisibility));
        }
        public string Status { get => feedback.ForPage(menu.View); internal set => SetStatus(value, menu.View); }
        public string SelectedGood { get => selectedGood; internal set { if (selectedGood == value) return; selectedGood = value; Changed(nameof(SelectedGood)); } }
        public bool ForceAcceptance { get => force; set { force = value; Changed(nameof(ForceAcceptance)); } }
        private bool hudAvailable;
        private bool details => menu.ShowDetails && hudAvailable;
        internal void SetHudAvailable(bool value) { if (hudAvailable == value) return; hudAvailable = value; RefreshViews(); }
        public Visibility ExpandedVisibility => details ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GoodsVisibility => details && goods ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OrdersVisibility => details && orders ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NavigationVisibility => details ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PlacementVisibility => menu.Placing ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ExpandButtonVisibility => menu.CanExpand ? Visibility.Visible : Visibility.Collapsed;
        public bool PlacementActive { get => menu.Placing; internal set { if (menu.Placing == value) return; menu.SetPlacement(value); RefreshViews(); } }
        public string ActiveViewKey => menu.View;
        public ToolPower[] Powers => powerItems;
        public bool PowersEnabled => powerFlag;
        public string PowersShortLabel => SerpLocalization.Get(powerFlag ? "Tools.PowersShortOn" : "Tools.PowersShortOff");
        public string PowersToggleLabel => SerpLocalization.Get(powerFlag ? "Tools.PowersSwitchOn" : "Tools.PowersSwitchOff");
        internal void RefreshPowerFlag(bool value)
        { if (value == powerFlag) return; powerFlag = value; Changed(nameof(PowersEnabled)); Changed(nameof(PowersToggleLabel)); Changed(nameof(PowersShortLabel)); }
        internal void SetPowers(ToolPower[] value)
        {
            bool changed = value.Length != powerItems.Length;
            for (int i = 0; !changed && i < value.Length; i++)
                changed = !value[i].SameContract(powerItems[i]) || value[i].Name != powerItems[i].Name ||
                    value[i].Help != powerItems[i].Help || value[i].Enabled != powerItems[i].Enabled ||
                    !ReferenceEquals(value[i].Icon, powerItems[i].Icon);
            if (!changed) return;
            powerItems = value; Changed(nameof(Powers));
        }
        public string SelectedGoodKey => "G" + ((int)good).ToString("00", CultureInfo.InvariantCulture);
        private bool classicTheme;
        private ToolbarPalette palette = ToolbarPalette.Paper;
        internal bool ClassicTheme => classicTheme;
        public Brush MenuForeground => palette.Ink;
        public Brush MenuButton => palette.Button;
        public Brush MenuHover => palette.Hover;
        public Brush MenuBorder => palette.Border;
        public Brush MenuActive => palette.Active;
        public Brush MenuInput => palette.Input;
        public Brush ForceBackground => palette.Force;
        public Brush MenuGrip => palette.Grip;
        public Brush MenuGripHover => palette.GripHover;
        public Brush MenuHighlight => palette.Highlight;
        public string DesignHelp => SerpLocalization.Get(classicTheme ? "Tools.DesignPaper" : "Tools.DesignClassic");
        internal void SetFloatingTheme(bool value)
        {
            if (classicTheme == value) return;
            classicTheme = value; palette = value ? ToolbarPalette.Classic : ToolbarPalette.Paper;
            foreach (string property in new[] { nameof(MenuForeground), nameof(MenuButton), nameof(MenuHover),
                nameof(MenuBorder), nameof(MenuActive), nameof(MenuInput), nameof(ForceBackground), nameof(MenuGrip), nameof(MenuGripHover), nameof(MenuHighlight), nameof(DesignHelp) }) Changed(property);
        }
        internal void SetFloating(bool value)
        {
            if (!menu.SetFloating(value)) return;
            Changed(nameof(ExpandLabel)); Changed(nameof(ExpandFontSize)); Changed(nameof(ExpandHelp));
        }
        public string ExpandLabel => menu.ControlLabel(SerpLocalization.Get("Tools.ToolsShort"), SerpLocalization.Get("Tools.BackShort"));
        public string ExpandHelp => SerpLocalization.Get(menu.ControlHelpKey(ownBuildMenu));
        internal void SetOwnBuildMenu(bool value)
        { if (ownBuildMenu == value) return; ownBuildMenu = value; Changed(nameof(ExpandHelp)); }
        public float ExpandFontSize => menu.ControlFontSize;
        public bool CanCredit => ToolsAvailable && hasRecipient && CreditMath.TryParse(amount, out _) && canExecute("Credit");
        public Visibility StatusVisibility => details && !string.IsNullOrEmpty(Status) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GoodsStatusVisibility => goods ? StatusVisibility : Visibility.Collapsed;
        internal void SetRecipientAvailable(bool available)
        { if (hasRecipient == available) return; hasRecipient = available; Changed(nameof(CanCredit)); }
        private bool canAttack, canDefend, canCancel;
        private string orderAvailability = "";
        public bool ToolsAvailable => menu.ToolsAvailable;
        public bool CanAttack => ToolsAvailable && canAttack;
        public bool CanDefend => ToolsAvailable && canDefend;
        public bool CanCancel => ToolsAvailable && canCancel;
        public string OrderAvailability => orderAvailability;
        public Visibility OrderAvailabilityVisibility => string.IsNullOrEmpty(orderAvailability) ? Visibility.Collapsed : Visibility.Visible;
        internal void SetAvailability(bool tools)
        {
            if (!menu.SetToolsAvailable(tools)) return;
            Changed(nameof(ToolsAvailable)); Changed(nameof(CanCredit));
            Changed(nameof(CanAttack)); Changed(nameof(CanDefend)); Changed(nameof(CanCancel));
            RefreshViews();
            ((ToolsCommand)ActionCommand).Refresh();
        }
        internal void SetOrderAvailability(bool attack, bool defend, bool cancel, string reason)
        {
            if (canAttack != attack) { canAttack = attack; Changed(nameof(CanAttack)); }
            if (canDefend != defend) { canDefend = defend; Changed(nameof(CanDefend)); }
            if (canCancel != cancel) { canCancel = cancel; Changed(nameof(CanCancel)); }
            string text = string.IsNullOrEmpty(reason) ? "" : SerpLocalization.Get("Tools." + reason);
            if (orderAvailability != text) { orderAvailability = text; Changed(nameof(OrderAvailability)); Changed(nameof(OrderAvailabilityVisibility)); }
        }
        internal bool Expanded => expanded;
        internal eGoods Good => good;

        internal TestToolsViewModel(Action<string> execute, Func<string, bool> canExecute, Func<bool> prepareDockedTools)
        {
            this.execute = execute; this.canExecute = canExecute; this.prepareDockedTools = prepareDockedTools;
            ActionCommand = new ToolsCommand(Invoke, CanInvoke);
            SelectGood(good);
        }

        private bool CanInvoke(string value) => canExecute(value) && menu.CanInvoke(value);

        private void Invoke(string value)
        {
            if (!CanInvoke(value)) return;
            switch (value)
            {
                case "Collapse": menu.ToggleExpanded(hudAvailable, prepareDockedTools); RefreshViews(); execute("CancelPower"); return;
                case "Goods": menu.ToggleView("Goods"); RefreshViews(); execute("StopCursor"); return;
                case "Orders": menu.ToggleView("Orders"); RefreshViews(); execute("StopCursor"); return;
                case "Placement": execute("Placement"); return;
                case "TogglePowers": if (ToolsAvailable) execute(powerFlag ? "DisablePowers" : "EnablePowers"); return;
                case "Credit": case "Debit": if (!CanCredit) return; break;
                case "Attack": if (!CanAttack) return; break;
                case "Defend": if (!CanDefend) return; break;
                case "Cancel": if (!CanCancel) return; break;
            }
            if (value != null && value.StartsWith("G", StringComparison.Ordinal) &&
                int.TryParse(value.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int selected) &&
                IsGrantable((eGoods)selected)) { SelectGood((eGoods)selected); return; }
            execute(value);
        }

        private void SelectGood(eGoods selected)
        {
            good = selected;
            SelectedGood = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, (int)good);
            Changed(nameof(SelectedGoodKey));
        }

        internal void RefreshLocale()
        {
            string current = SerpLocalization.GetActiveLocale();
            if (current == locale) return;
            locale = current;
            Changed(nameof(Text)); Changed(nameof(DesignHelp)); Changed(nameof(PowersToggleLabel)); Changed(nameof(PowersShortLabel)); Changed(nameof(ExpandLabel)); Changed(nameof(ExpandHelp));
            SelectGood(good);
        }

        private void RefreshViews()
        {
            Changed(nameof(ExpandedVisibility)); Changed(nameof(GoodsVisibility));
            Changed(nameof(OrdersVisibility)); Changed(nameof(PlacementActive));
            Changed(nameof(ExpandLabel)); Changed(nameof(ExpandHelp)); Changed(nameof(ActiveViewKey)); Changed(nameof(Status)); Changed(nameof(StatusVisibility)); Changed(nameof(GoodsStatusVisibility));
            Changed(nameof(NavigationVisibility)); Changed(nameof(PlacementVisibility)); Changed(nameof(ExpandButtonVisibility));
        }

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        // The actual Vanilla send-goods choices. Unsupported intermediates are never queued.
        internal static bool IsGrantable(eGoods good)
        {
            switch (good)
            {
                case eGoods.STORED_WOOD_PLANKS: case eGoods.STORED_RAW_HOPS: case eGoods.STORED_STONE_BLOCKS:
                case eGoods.STORED_IRON_INGOTS: case eGoods.STORED_PITCH_RAW: case eGoods.STORED_RAW_WHEAT:
                case eGoods.STORED_FOOD_BREAD: case eGoods.STORED_FOOD_CHEESE: case eGoods.STORED_FOOD_MEAT:
                case eGoods.STORED_FOOD_FRUIT: case eGoods.STORED_FOOD_ALE: case eGoods.STORED_GOLD:
                case eGoods.STORED_FLOUR: case eGoods.STORED_BOWS: case eGoods.STORED_CROSSBOWS:
                case eGoods.STORED_SPEARS: case eGoods.STORED_PIKES: case eGoods.STORED_MACES:
                case eGoods.STORED_SWORDS: case eGoods.STORED_LEATHER_ARMOUR: case eGoods.STORED_METAL_ARMOUR:
                    return true;
                default: return false;
            }
        }

        private sealed class ToolsCommand : ICommand
        {
            private readonly Action<string> execute;
            private readonly Func<string, bool> enabled;
            internal ToolsCommand(Action<string> execute, Func<string, bool> enabled) { this.execute = execute; this.enabled = enabled; }
            public event System.EventHandler CanExecuteChanged;
            internal void Refresh() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
            public bool CanExecute(object parameter) => enabled(parameter as string);
            public void Execute(object parameter) { if (enabled(parameter as string)) execute(parameter as string); }
        }
    }
}
