using APIShared;
using BugfixesAndQoL.UnitCommands;
using BepInEx.Logging;
using Noesis;
using System;
using System.ComponentModel;

namespace BugfixesAndQoL
{
    // The shared service owns layout and input. This consumer owns only formation content and preferences.
    internal static class FormationHudButton
    {
        private static FormationMenuViewModel menu;
        private static IUnitHudActionButtonRegistration registration;
        private static bool ownContext;

        internal static void Configure(FormationMenuViewModel model, ManualLogSource log)
        {
            menu = model;
            menu.OwnHudAvailable = () => ownContext;
            menu.PropertyChanged += OnMenuChanged;
            ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid).WhenReady(client =>
                Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => {
                    if (!client.TryGetUnitHudPresentation(out var hud, out var diagnostic) ||
                        !(hud is IUnitHudActionButtonsCapability buttons))
                    {
                        Shared.DebugLogHelper.LogWarning(log, "FORMATION_ACTION_BUTTON_UNAVAILABLE: " + diagnostic?.Reason);
                        return;
                    }
                    var definition = new UnitHudActionButtonDefinition("formation", menu.ToggleMenuCommand,
                        SerpLocalization.Get("BugfixesAndQoL.ArrangementTooltip"), CreateContent,
                        contextChanged: available => { ownContext = available; menu.RefreshHostState(); });
                    if (!buttons.TryRegisterActionButton(definition, out registration, out diagnostic))
                        Shared.DebugLogHelper.LogWarning(log, "FORMATION_ACTION_BUTTON_FAILED: " + diagnostic.Reason);
                    else RefreshVisibility();
                }));
        }

        internal static void HideForForeignHud()
        {
            menu?.CloseMenu();
        }

        private static void OnMenuChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(FormationMenuViewModel.FeatureAvailable)) RefreshVisibility();
            if (args.PropertyName == nameof(FormationMenuViewModel.IsVanilla)) registration?.RequestContentRefresh();
        }

        private static void RefreshVisibility()
        {
            registration?.SetVisible(menu.FeatureAvailable);
            registration?.SetTooltip(SerpLocalization.Get("BugfixesAndQoL.ArrangementTooltip"));
        }

        private static FrameworkElement CreateContent(UnitHudActionButtonContext context)
        {
            var shield = (Grid)GUI.ParseXaml(
                "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Width=\"35\" Height=\"35\">" +
                "<Path Data=\"M 3,2 L 32,2 L 31,18 Q 29,27 17.5,33 Q 6,27 4,18 Z\" Stroke=\"#FF181914\" StrokeThickness=\"2\">" +
                "<Path.Fill><LinearGradientBrush StartPoint=\"0,0\" EndPoint=\"1,1\"><GradientStop Color=\"#FFB7B9A7\" Offset=\"0\"/>" +
                "<GradientStop Color=\"#FF767C6F\" Offset=\"0.45\"/><GradientStop Color=\"#FF444B3F\" Offset=\"1\"/></LinearGradientBrush></Path.Fill></Path>" +
                "<Path Data=\"" + FormationHudIconLayout.InnerShieldPath + "\" Fill=\"Transparent\"><Path.Style><Style TargetType=\"{x:Type Path}\">" +
                "<Setter Property=\"Stroke\" Value=\"#FFADA16A\"/><Setter Property=\"StrokeThickness\" Value=\"0.7\"/>" +
                "<Style.Triggers><DataTrigger Binding=\"{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType={x:Type Button}}}\" Value=\"True\">" +
                "<Setter Property=\"Stroke\" Value=\"#FFFFE4A0\"/><Setter Property=\"StrokeThickness\" Value=\"1.2\"/>" +
                "</DataTrigger></Style.Triggers></Style></Path.Style></Path>" +
                "</Grid>");
            var dots = new Canvas { Width = 35, Height = 35, IsHitTestVisible = false };
            shield.Children.Add(dots);
            int colour = context.PlayerColour;
            var fill = new SolidColorBrush(Color.FromArgb(255, FormationHudIconLayout.ColourChannel(colour, 0), FormationHudIconLayout.ColourChannel(colour, 1), FormationHudIconLayout.ColourChannel(colour, 2)));
            var stroke = new SolidColorBrush(Color.FromArgb(255, 26, 29, 20));
            foreach (var point in FormationHudIconLayout.Points(menu.SelectedKind))
            {
                var dot = new Ellipse { Width = FormationHudIconLayout.PointSize, Height = FormationHudIconLayout.PointSize, Fill = fill, Stroke = stroke, StrokeThickness = FormationHudIconLayout.Stroke };
                Canvas.SetLeft(dot, FormationHudIconLayout.Left + point[0] * FormationHudIconLayout.PointScale);
                Canvas.SetTop(dot, FormationHudIconLayout.Top + point[1] * FormationHudIconLayout.PointScale);
                dots.Children.Add(dot);
            }
            return shield;
        }

    }
}
