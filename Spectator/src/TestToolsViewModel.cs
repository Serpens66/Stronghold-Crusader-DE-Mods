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
        private readonly Func<bool> canExecute;
        private string amount = "0", status = "", selectedGood = "", selectedPlayer = "", selectedTarget = "";
        private bool expanded, goods, orders, powerFlag, force, hasRecipient;
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
        public string Status { get => status; internal set { if (status == value) return; status = value; Changed(nameof(Status)); Changed(nameof(StatusVisibility)); } }
        public string SelectedGood { get => selectedGood; internal set { if (selectedGood == value) return; selectedGood = value; Changed(nameof(SelectedGood)); } }
        public string SelectedPlayer { get => selectedPlayer; internal set { if (selectedPlayer == value) return; selectedPlayer = value; Changed(nameof(SelectedPlayer)); } }
        public string SelectedTarget { get => selectedTarget; internal set { if (selectedTarget == value) return; selectedTarget = value; Changed(nameof(SelectedTarget)); } }
        public bool ForceAcceptance { get => force; set { force = value; Changed(nameof(ForceAcceptance)); } }
        public Visibility ExpandedVisibility => expanded ? Visibility.Visible : Visibility.Collapsed;
        public Visibility GoodsVisibility => expanded && goods ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OrdersVisibility => expanded && orders ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PlacementVisibility => expanded && !goods && !orders ? Visibility.Visible : Visibility.Collapsed;
        public string ActiveViewKey => goods ? "Goods" : orders ? "Orders" : "Placement";
        public ToolPower[] Powers => powerItems;
        public bool PowersEnabled { get => powerFlag; set { if (value != powerFlag) execute(value ? "EnablePowers" : "DisablePowers"); } }
        internal void RefreshPowerFlag(bool value)
        { if (value == powerFlag) return; powerFlag = value; Changed(nameof(PowersEnabled)); }
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
        public string ExpandLabel => expanded ? "−" : "+";
        public bool CanCredit => toolsAvailable && hasRecipient && CreditMath.TryParse(amount, out _) && canExecute();
        public Visibility StatusVisibility => expanded && !string.IsNullOrEmpty(status) ? Visibility.Visible : Visibility.Collapsed;
        internal void SetRecipientAvailable(bool available)
        { if (hasRecipient == available) return; hasRecipient = available; Changed(nameof(CanCredit)); }
        private bool toolsAvailable, canAttack, canDefend, canCancel;
        private string orderAvailability = "";
        public bool ToolsAvailable => toolsAvailable;
        public bool CanAttack => canAttack;
        public bool CanDefend => canDefend;
        public bool CanCancel => canCancel;
        public string OrderAvailability => orderAvailability;
        internal void SetAvailability(bool tools)
        { if (toolsAvailable != tools) { toolsAvailable = tools; Changed(nameof(ToolsAvailable)); Changed(nameof(CanCredit)); } }
        internal void SetOrderAvailability(bool attack, bool defend, bool cancel, string reason)
        {
            if (canAttack != attack) { canAttack = attack; Changed(nameof(CanAttack)); }
            if (canDefend != defend) { canDefend = defend; Changed(nameof(CanDefend)); }
            if (canCancel != cancel) { canCancel = cancel; Changed(nameof(CanCancel)); }
            string text = string.IsNullOrEmpty(reason) ? "" : SerpLocalization.Get("Tools." + reason);
            if (orderAvailability != text) { orderAvailability = text; Changed(nameof(OrderAvailability)); }
        }
        internal bool Expanded => expanded;
        internal eGoods Good => good;

        internal TestToolsViewModel(Action<string> execute, Func<bool> canExecute)
        {
            this.execute = execute; this.canExecute = canExecute;
            ActionCommand = new ToolsCommand(Invoke, canExecute);
            SelectGood(good);
        }

        private void Invoke(string value)
        {
            if (!canExecute()) return;
            switch (value)
            {
                case "Collapse": expanded = !expanded; RefreshViews(); execute("CancelPower"); return;
                case "Goods": expanded = goods = true; orders = false; RefreshViews(); execute("StopCursor"); return;
                case "Orders": expanded = orders = true; goods = false; RefreshViews(); execute("StopCursor"); return;
                case "Placement": expanded = true; goods = orders = false; RefreshViews(); execute("CancelPower"); return;
                case "Credit": if (!CanCredit) return; break;
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
            Changed(nameof(Text));
            SelectGood(good);
        }

        private void RefreshViews()
        {
            Changed(nameof(ExpandedVisibility)); Changed(nameof(GoodsVisibility));
            Changed(nameof(OrdersVisibility)); Changed(nameof(PlacementVisibility));
            Changed(nameof(ExpandLabel)); Changed(nameof(ActiveViewKey)); Changed(nameof(StatusVisibility));
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
            private readonly Func<bool> enabled;
            internal ToolsCommand(Action<string> execute, Func<bool> enabled) { this.execute = execute; this.enabled = enabled; }
            public event System.EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => enabled();
            public void Execute(object parameter) { if (enabled()) execute(parameter as string); }
        }
    }
}
