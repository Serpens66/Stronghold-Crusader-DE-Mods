using System;
using System.Windows.Input;
namespace Spectator
{
    // Forward into the original command (including BFQ). Only repair Vanilla's signed-int wraparound.
    internal sealed class CheckedAllyAmountCommand : ICommand
    {
        private readonly ICommand original;
        private readonly Func<int> read;
        private readonly Action<int> write;
        private readonly Action refresh;
        internal CheckedAllyAmountCommand(ICommand command, Func<int> get, Action<int> set, Action changed)
        { original = command; read = get; write = set; refresh = changed; }
        public event EventHandler CanExecuteChanged { add { original.CanExecuteChanged += value; } remove { original.CanExecuteChanged -= value; } }
        public bool CanExecute(object parameter) => original.CanExecute(parameter);
        public void Execute(object parameter)
        {
            int before = read();
            original.Execute(parameter);
            // Known positive amount buttons add at most 500 without BFQ; BFQ itself saturates safely.
            if (before > int.MaxValue - 500 && read() < 0) write(int.MaxValue);
            refresh();
        }
    }
}
