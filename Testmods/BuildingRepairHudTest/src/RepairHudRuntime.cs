using APIShared;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;

namespace BuildingRepairHudTest
{
    internal sealed class RepairHudRuntime
    {
        private const string ButtonName = "BuildingRepairHudTestButton";
        private const string HoverId = "BuildingRepairHudTest.SmallButton";
        private delegate bool ShowRepairDelegate(HUD_Buildings self, int type, int panel);
        private delegate void HudUpdateDelegate(FatControler self);

        private readonly IBuildingRepairCapability repair;
        private readonly Hook classifierHook;
        private readonly Hook hudHook;
        private readonly ShowRepairDelegate originalShowRepair;
        private readonly HudUpdateDelegate originalHudUpdate;
        private Button lastButton;
        private HUD_Buildings lastHud;
        private bool postStartupLogged;

        private RepairHudRuntime(IBuildingRepairCapability repair)
        {
            this.repair = repair ?? throw new ArgumentNullException(nameof(repair));
            Hook candidateClassifier = null;
            Hook candidateHud = null;
            try
            {
                candidateClassifier = new Hook(FindMethod(typeof(HUD_Buildings), "GetBuildingShowRepair",
                    BindingFlags.Public | BindingFlags.Instance, typeof(int), typeof(int)),
                    (ShowRepairDelegate)ShowRepairHook);
                originalShowRepair = candidateClassifier.GenerateTrampoline<ShowRepairDelegate>();
                candidateHud = new Hook(FindMethod(typeof(FatControler), "NoesisGUIUpdateChecksInGame",
                    BindingFlags.Public | BindingFlags.Instance),
                    (HudUpdateDelegate)HudUpdateHook);
                originalHudUpdate = candidateHud.GenerateTrampoline<HudUpdateDelegate>();
                classifierHook = candidateClassifier;
                hudHook = candidateHud;
            }
            catch
            {
                // Only an unpublished initialization candidate can be rolled back.
                try { candidateHud?.Undo(); } catch { }
                try { candidateHud?.Dispose(); } catch { }
                try { candidateClassifier?.Undo(); } catch { }
                try { candidateClassifier?.Dispose(); } catch { }
                throw;
            }
        }

        internal static RepairHudRuntime Install(IBuildingRepairCapability repair) => new RepairHudRuntime(repair);

        private static MethodInfo FindMethod(Type owner, string name, BindingFlags flags, params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(name, flags, null, parameters, null);
            if (method == null) throw new MissingMethodException(owner.FullName, name);
            return method;
        }

        private bool ShowRepairHook(HUD_Buildings self, int type, int panel)
        {
            bool vanilla = originalShowRepair(self, type, panel);
            // BugfixesAndQoL asks about panel zero for Shift repair. Actual HUD panels keep Vanilla.
            return vanilla || (panel == (int)Enums.InBuildingModes.INSIDE_NULL &&
                type > (int)eStructs.STRUCT_NULL && type <= (int)eStructs.STRUCT_GARDEN_LARGE);
        }

        private void HudUpdateHook(FatControler self)
        {
            originalHudUpdate(self);
            try { UpdateButton(); }
            catch (Exception ex) { BuildingRepairHudTestPlugin.LogError("Repair HUD update failed: " + ex); }
        }

        private void UpdateButton()
        {
            if (!MainViewModel.viewModelLoaded) return;
            MainViewModel view = MainViewModel.Instance;
            if (view?.HUDmain == null || view.HUDBuildingPanel == null) return;
            HUD_Buildings hud = view.HUDBuildingPanel;
            if (!postStartupLogged || !ReferenceEquals(hud, lastHud))
            {
                BuildingRepairHudTestPlugin.LogInfo("Repair HUD runtime executed after startup cleanup; HUD instance=" + hud.GetHashCode() + ".");
                postStartupLogged = true;
                lastHud = hud;
            }

            Button button = hud.FindName(ButtonName) as Button;
            if (button == null) return;
            if (!ReferenceEquals(button, lastButton))
            {
                button.MouseEnter += OnButtonEnter;
                button.MouseLeave += OnButtonLeave;
                lastButton = button;
            }
            if (!repair.TryGetSelectedQuote(out BuildingRepairQuote quote))
            {
                button.Visibility = Visibility.Hidden;
                button.IsEnabled = false;
                repair.EndHover(HoverId);
                return;
            }

            bool special = hud.RefBarracksPanel.Visibility == Visibility.Visible ||
                hud.RefMercPostPanel.Visibility == Visibility.Visible ||
                hud.RefBedouinStockadePanel.Visibility == Visibility.Visible;
            if (special)
            {
                button.Width = 20;
                button.Height = 20;
                button.Margin = new Thickness(0, 0, 235, 27);
            }
            else
            {
                button.Width = 24;
                button.Height = 24;
                button.Margin = new Thickness(0, 0, 262, 44);
            }
            button.Visibility = Visibility.Visible;
            button.IsEnabled = quote.CanRepair && quote.CurrentHealth < quote.MaxHealth &&
                quote.HasResources && hud.RefButtonRepair.IsEnabled;
            button.Opacity = button.IsEnabled ? 1.0f : 0.5f;
        }

        private void OnButtonEnter(object sender, MouseEventArgs args) => repair.BeginHover(HoverId);
        private void OnButtonLeave(object sender, MouseEventArgs args) => repair.EndHover(HoverId);
    }
}
