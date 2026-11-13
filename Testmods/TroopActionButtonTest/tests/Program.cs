using TroopActionButtonTest;

var lines = new List<string>();
ArcherSelection selected = default;
var command = new ButtonTestCommand(3, () => selected, lines.Add);
void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }

Check(!command.CanExecute(null), "No selection must not enable the action.");
command.Execute(null);
Check(lines.Count == 0, "No click log without a selection.");
selected = new ArcherSelection(1, 12, true);
Check(command.CanExecute(null), "An archer in a mixed selection enables the action.");
// A real stale-menu regression: selection changes after CanExecute but before Execute.
selected = new ArcherSelection(1, 12, false);
command.Execute(null);
Check(lines.Count == 0, "Execute must recheck the archer condition.");
selected = new ArcherSelection(0, 1, true);
command.Execute(null);
selected = new ArcherSelection(1, 0, true);
command.Execute(null);
Check(lines.Count == 0, "Invalid player or empty selection must not log clicks.");
selected = new ArcherSelection(1, 12, true);
for (int number = 1; number <= 6; number++) new ButtonTestCommand(number, () => selected, lines.Add).Execute(null);
Check(lines.Count == 6 && lines.Distinct().Count() == 6, "Each of the six buttons logs once with its own ID.");
Check(lines[2] == "BUTTON_TEST_CLICK id=test-3 player=1 selection=12", "Click records the current selection and ID.");
command.Execute(null);
Check(lines.Count == 7, "Repeated accepted clicks are not suppressed.");
Console.WriteLine("PASS: mixed archer selection, stale-click recheck, invalid context, six distinct IDs and exactly one log per click.");
