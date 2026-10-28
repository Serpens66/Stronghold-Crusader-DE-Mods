using System;
using System.ComponentModel;
using System.Windows.Input;
using Noesis;
namespace Spectator
{
    public sealed class AllyTestViewModel : INotifyPropertyChanged
    {
        private readonly Action<string> execute;
        internal readonly AllyTestState State = new AllyTestState();
        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand Command { get; }
        public AllyTestViewModel(Action<string> action) { execute = action; Command = new ActionCommand(action); }
        public bool Enabled { get => State.Enabled; set { if (value != Enabled) execute("Toggle"); } }
        public bool Force { get; set; }
        private string status = "";
        public string Status { get => status; internal set { if (status == value) return; status = value; Changed(nameof(Status)); } }
        public string ToggleText => SerpLocalization.Get("AlliesTest.Mode");
        public string Help => SerpLocalization.Get("AlliesTest.Help");
        public string CreditText => SerpLocalization.Get("AlliesTest.Credit");
        public string DebitText => SerpLocalization.Get("AlliesTest.Debit");
        public string GoodsText => SerpLocalization.Get("Tools.Goods");
        public string OrdersText => SerpLocalization.Get("Tools.Orders");
        public string ForceText => SerpLocalization.Get("Tools.Force");
        internal void Refresh() { Changed(nameof(Enabled)); Changed(nameof(Status)); }
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private sealed class ActionCommand : ICommand
        {
            private readonly Action<string> execute;
            internal ActionCommand(Action<string> action) { execute = action; }
            public event System.EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => execute(parameter as string);
        }
    }
}
