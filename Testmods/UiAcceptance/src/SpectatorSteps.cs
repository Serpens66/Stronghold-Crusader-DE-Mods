using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using SHCDESE.API;

namespace UiAcceptance
{
    // Spectator toolbar through its public view model (the same ActionCommand path a button uses),
    // the APIShared perspective writer and Vanilla HUD transitions/power buttons.
    internal static class SpectatorSteps
    {
        internal static void Register()
        {
            Runner.Register("Spectator.WaitToolbar", WaitToolbar);
            Runner.Register("Spectator.Cmd", Command);
            Runner.Register("Spectator.Amount", Amount);
            Runner.Register("Spectator.ExpectStatus", ExpectStatus);
            Runner.Register("Spectator.View", View);
            Runner.Register("Spectator.ExpectNoToolbar", ExpectNoToolbar);
            Runner.Register("Spectator.TooltipPixels", SpectatorTooltipPixels.Run);
            Runner.Register("Hud", Hud);
            Runner.Register("Power", s => GameSteps.Do(() => CrusaderDE.MainViewModel.Instance.ButtonExtremeCommand.Execute(s.Arg(0))));
            Runner.Register("ExpectGold", ExpectGold);
        }

        private static Noesis.FrameworkElement Panel() => GameXAMLManagerAPI.Instance.FindGlobalElement("SpectatorEditorHumanPanel");

        // Public type from Spectator.dll, resolved by name so the testmod works without it installed.
        internal static object Model()
        {
            object context = Panel()?.DataContext;
            return context != null && context.GetType().FullName == "Spectator.TestToolsViewModel" ? context : null;
        }

        private static bool Visible(Noesis.FrameworkElement element) =>
            element != null && element.Visibility == Noesis.Visibility.Visible && element.IsVisible && element.ActualWidth > 0;

        private static IEnumerable<Wait> WaitToolbar(Step step)
        {
            yield return new Until("visible Spectator toolbar with tools model", () => Visible(Panel()) && Model() != null);
        }

        private static IEnumerable<Wait> ExpectNoToolbar(Step step)
        {
            yield return new Delay(step.Int(0, 2000));
            Runner.Require(!Visible(Panel()), "Spectator toolbar is visible");
        }

        private static IEnumerable<Wait> Command(Step step)
        {
            object model = Model() ?? throw new StepFailure("no Spectator tools model");
            var command = (ICommand)model.GetType().GetProperty("ActionCommand").GetValue(model, null);
            for (int repeat = step.Int(1, 1); repeat > 0; repeat--)
            {
                Runner.Require(command.CanExecute(step.Arg(0)), "ActionCommand cannot execute " + step.Arg(0));
                command.Execute(step.Arg(0));
            }
            yield return NextFrame.Instance;
        }

        private static IEnumerable<Wait> Amount(Step step)
        {
            object model = Model() ?? throw new StepFailure("no Spectator tools model");
            PropertyInfo amount = model.GetType().GetProperty("Amount");
            amount.SetValue(model, step.Arg(0), null);
            Runner.Require((string)amount.GetValue(model, null) == step.Arg(0), "Amount rejected " + step.Arg(0));
            yield break;
        }

        private static IEnumerable<Wait> ExpectStatus(Step step)
        {
            object model = Model() ?? throw new StepFailure("no Spectator tools model");
            string status = null;
            yield return new Until("status containing '" + step.Arg(0) + "'", () =>
                (status = model.GetType().GetProperty("Status").GetValue(model, null) as string)?.IndexOf(step.Arg(0), StringComparison.OrdinalIgnoreCase) >= 0);
            AcceptLog.Info("STATUS " + status);
        }

        private static IEnumerable<Wait> View(Step step)
        {
            Runner.Require(APIShared.PlayerPerspectiveAPI.TrySetSpectatorView(step.Int(0)), "TrySetSpectatorView(" + step.Arg(0) + ") rejected");
            yield return new Delay(200);
        }

        private static IEnumerable<Wait> Hud(Step step)
        {
            var view = CrusaderDE.MainViewModel.Instance;
            switch (step.Arg(0))
            {
                case "building": view.InBuildingGameAction(); break;
                case "troops": view.TroopsSelectedGameAction(false); break;
                case "default": view.DefaultGameUIGameAction(true); break;
                default: throw new StepFailure("Hud expects building|troops|default");
            }
            yield return new Delay(200);
        }

        private static IEnumerable<Wait> ExpectGold(Step step)
        {
            int player = step.Int(0), expected = step.Int(1);
            int gold = -1;
            yield return new Until("gold of player " + player + " == " + expected, () => (gold = GamePlayerManagerAPI.Instance.GetPlayerGold(player)) == expected);
        }
    }
}
