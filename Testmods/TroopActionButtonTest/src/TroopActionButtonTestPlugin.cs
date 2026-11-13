using System;
using System.Globalization;
using APIShared;
using APIShared.GameModes;
using APIShared.Presentation;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;

namespace TroopActionButtonTest
{
    [BepInPlugin(PluginGuid, "Troop Action Button Test", PluginVersion)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.31.0")]
    public sealed class TroopActionButtonTestPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "TroopActionButtonTest_Serp";
        public const string PluginVersion = "0.1.0";
        private static ButtonTestRuntime runtime;

        private void Awake()
        {
            // The process registry and static runtime survive destruction of the early plugin component.
            runtime = new ButtonTestRuntime(Logger);
            ButtonTestRuntime current = runtime;
            current.Info("BUTTON_TEST_LOADED version=" + PluginVersion);
            ApiShared.ForMod(PluginGuid).WhenReady(client => {
                if (!client.Ui.TryPost(current.Initialize)) current.Error("BUTTON_TEST_UI_DISPATCH_UNAVAILABLE");
            });
        }
    }

    internal sealed class ButtonTestRuntime
    {
        private readonly ManualLogSource log;
        private readonly ITroopActionButtonRegistration[] registrations = new ITroopActionButtonRegistration[6];
        private bool initialized, contextObserved;

        internal ButtonTestRuntime(ManualLogSource log) { this.log = log; }
        internal void Info(string message) => log.LogInfo(Stamp(message));
        internal void Error(string message) => log.LogError(Stamp(message));
        private static string Stamp(string message) =>
            "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + message;

        internal void Initialize()
        {
            if (initialized) return;
            try
            {
                for (int index = 0; index < registrations.Length; index++)
                {
                    int number = index + 1;
                    var command = new ButtonTestCommand(number, CaptureSelection, Info);
                    var definition = new TroopActionButtonDefinition(command.Id, command,
                        "Test button " + number.ToString(CultureInfo.InvariantCulture) + ": log one click.",
                        context => CreateButton(context, number), priority: 300 + index);
                    if (!TroopActionHud.TryRegisterButton(TroopActionButtonTestPlugin.PluginGuid, definition,
                        out registrations[index], out string reason))
                        throw new InvalidOperationException(command.Id + ": " + reason);
                }
                // Uses APIShared's existing selection/setup hook; no private hook or frame publisher.
                if (!TroopActionHud.TryRegister(TroopActionButtonTestPlugin.PluginGuid, "archer-selection",
                    RefreshVisibility, out string refreshReason)) throw new InvalidOperationException(refreshReason);
                initialized = true;
                Info("BUTTON_TEST_READY buttons=6 priorities=300..305");
            }
            catch (Exception error)
            {
                foreach (var registration in registrations) registration?.SetVisible(false);
                Error("BUTTON_TEST_REGISTRATION_FAILED " + error);
            }
        }

        private void RefreshVisibility(HUD_Troops panel)
        {
            ArcherSelection selection = CaptureSelection();
            foreach (var registration in registrations) registration?.SetVisible(selection.Eligible);
            if (selection.Eligible && !contextObserved)
            {
                contextObserved = true;
                Info(string.Format(CultureInfo.InvariantCulture,
                    "BUTTON_TEST_ARCHER_CONTEXT player={0} selection={1}", selection.PlayerId, selection.Count));
            }
        }

        private static ArcherSelection CaptureSelection()
        {
            if (!MainViewModel.viewModelLoaded) return default;
            MainViewModel main = MainViewModel.Instance;
            if (main?.HUDmain == null || main.HUDTroopPanel == null || !main.Show_HUD_Troops) return default;
            int player = GameModeHelper.IsMapEditor() ? EditorDirector.instance?.ActivePlayerID ?? -1
                : GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
            if (!LocalSelectionAPI.TryCapture(player, out LocalSelectionSnapshot selected)) return default;
            bool archer = false;
            for (int i = 0; i < selected.Count; i++)
                if (selected[i].UnitType == (int)eChimps.CHIMP_TYPE_ARCHER) { archer = true; break; }
            return new ArcherSelection(player, selected.Count, archer);
        }

        private static Button CreateButton(UnitHudActionButtonContext context, int number)
        {
            var style = context.Hud.TryFindResource("BTN_SH_Plain") as Style;
            object normal = context.Hud.TryFindResource("UI-Buttons L009");
            object hover = context.Hud.TryFindResource("UI-Buttons L010");
            if (style == null || normal == null || hover == null)
                throw new InvalidOperationException("Vanilla numbered-button resources are unavailable.");
            var button = new Button { Width = 36, Height = 37, Style = style };
            PropEx.SetSprite1(button, normal); PropEx.SetSprite2(button, hover);
            PropEx.SetSprite3(button, hover); PropEx.SetSprite4(button, normal);
            PropEx.SetTextA(button, number.ToString(CultureInfo.InvariantCulture));
            return button;
        }
    }
}
