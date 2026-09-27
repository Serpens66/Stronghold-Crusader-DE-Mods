using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed class BuildingRepairHudRuntime
    {
        private const string ButtonName = "ExtraFeaturesBuildingRepairButton";
        private const string HoverHostName = "ExtraFeaturesBuildingRepairHoverHost";
        private const string HoverId = "ExtraFeatures.BuildingRepair.SmallButton";
        private delegate bool ShowRepairDelegate(HUD_Buildings self, int type, int panel);
        private delegate void HudUpdateDelegate(FatControler self);

        private readonly ManualLogSource log;
        private readonly IBuildingRepairCapability repair;
        private readonly Hook classifierHook;
        private readonly Hook hudHook;
        private readonly ShowRepairDelegate originalShowRepair;
        private readonly HudUpdateDelegate originalHudUpdate;
        private Grid lastHoverHost;
        private bool hudFailureLogged;
        private int active;

        private BuildingRepairHudRuntime(ManualLogSource log, IBuildingRepairCapability repair)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
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

        internal static BuildingRepairHudRuntime Install(ManualLogSource log, IBuildingRepairCapability repair) =>
            new BuildingRepairHudRuntime(log, repair);

        internal void SetActive(bool enabled)
        {
            Interlocked.Exchange(ref active, enabled ? 1 : 0);
            repair.SetActive(enabled);
            if (!enabled) repair.EndHover(HoverId);
        }

        private bool IsActive => Volatile.Read(ref active) != 0;

        private static MethodInfo FindMethod(Type owner, string name, BindingFlags flags, params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(name, flags, null, parameters, null);
            if (method == null) throw new MissingMethodException(owner.FullName, name);
            return method;
        }

        private bool ShowRepairHook(HUD_Buildings self, int type, int panel)
        {
            bool vanilla = originalShowRepair(self, type, panel);
            return vanilla || (IsActive && panel == (int)Enums.InBuildingModes.INSIDE_NULL &&
                type > (int)eStructs.STRUCT_NULL && type <= (int)eStructs.STRUCT_GARDEN_LARGE);
        }

        private void HudUpdateHook(FatControler self)
        {
            originalHudUpdate(self);
            try { UpdateButton(); }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref active, 0);
                try { repair.SetActive(false); repair.EndHover(HoverId); } catch { }
                try { if (lastHoverHost != null) lastHoverHost.Visibility = Visibility.Hidden; } catch { }
                if (hudFailureLogged) return;
                hudFailureLogged = true;
                Shared.DebugLogHelper.LogError(log, "Building repair HUD update failed; further errors are suppressed: " + ex);
            }
        }

        private void UpdateButton()
        {
            if (!MainViewModel.viewModelLoaded) return;
            MainViewModel view = MainViewModel.Instance;
            if (view?.HUDmain == null || view.HUDBuildingPanel == null) return;
            HUD_Buildings hud = view.HUDBuildingPanel;
            Button button = hud.FindName(ButtonName) as Button;
            Grid hoverHost = hud.FindName(HoverHostName) as Grid;
            if (button == null || hoverHost == null) return;
            if (!ReferenceEquals(hoverHost, lastHoverHost))
            {
                hoverHost.MouseEnter += OnButtonEnter;
                hoverHost.MouseLeave += OnButtonLeave;
                lastHoverHost = hoverHost;
            }
            if (!IsActive || !repair.TryGetSelectedQuote(out BuildingRepairQuote quote))
            {
                hoverHost.Visibility = Visibility.Hidden;
                button.IsEnabled = false;
                repair.EndHover(HoverId);
                return;
            }

            bool special = hud.RefBarracksPanel.Visibility == Visibility.Visible ||
                hud.RefMercPostPanel.Visibility == Visibility.Visible ||
                hud.RefBedouinStockadePanel.Visibility == Visibility.Visible;
            if (special)
            {
                hoverHost.Width = 20;
                hoverHost.Height = 20;
                hoverHost.Margin = new Thickness(0, 0, 235, 27);
            }
            else
            {
                hoverHost.Width = 24;
                hoverHost.Height = 24;
                hoverHost.Margin = new Thickness(0, 0, 262, 44);
            }
            hoverHost.Visibility = Visibility.Visible;
            button.IsEnabled = quote.CanRepair && quote.CurrentHealth < quote.MaxHealth &&
                quote.HasResources && hud.RefButtonRepair.IsEnabled;
            button.Opacity = button.IsEnabled ? 1.0f : 0.5f;
        }

        private void OnButtonEnter(object sender, MouseEventArgs args)
        {
            if (IsActive) repair.BeginHover(HoverId);
        }

        private void OnButtonLeave(object sender, MouseEventArgs args) => repair.EndHover(HoverId);
    }
}
