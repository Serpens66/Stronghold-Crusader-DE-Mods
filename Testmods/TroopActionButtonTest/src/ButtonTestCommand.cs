using System;
using System.Globalization;
using System.Windows.Input;

namespace TroopActionButtonTest
{
    // Only a summary of the current completed selection, never a retained native pointer.
    internal readonly struct ArcherSelection
    {
        internal ArcherSelection(int playerId, int count, bool hasArcher)
        { PlayerId = playerId; Count = count; HasArcher = hasArcher; }
        internal int PlayerId { get; }
        internal int Count { get; }
        internal bool HasArcher { get; }
        internal bool Eligible => PlayerId > 0 && Count > 0 && HasArcher;
    }

    internal sealed class ButtonTestCommand : ICommand
    {
        private readonly Func<ArcherSelection> selection;
        private readonly Action<string> log;
        internal ButtonTestCommand(int number, Func<ArcherSelection> selection, Action<string> log)
        {
            if (number < 1 || number > 6) throw new ArgumentOutOfRangeException(nameof(number));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            Id = "test-" + number.ToString(CultureInfo.InvariantCulture);
        }
        internal string Id { get; }
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => selection().Eligible;
        public void Execute(object parameter)
        {
            // The caller's earlier CanExecute/visibility check does not authorize a later click.
            ArcherSelection current = selection();
            if (!current.Eligible) return;
            log(string.Format(CultureInfo.InvariantCulture, "BUTTON_TEST_CLICK id={0} player={1} selection={2}",
                Id, current.PlayerId, current.Count));
        }
    }
}
