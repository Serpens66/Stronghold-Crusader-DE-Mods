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


        private readonly ManualLogSource log;
        private readonly IBuildingRepairCapability repair;
        private readonly Hook classifierHook;

        private readonly ShowRepairDelegate originalShowRepair;

        private Grid lastHoverHost;
        private HUD_Buildings lastHudPanel;
        private Button lastRepairButton;
        private bool hovered;
        private bool? lastSpecialLayout;
        private bool hudFailureLogged;
        private int active;

        private BuildingRepairHudRuntime(ManualLogSource log, IBuildingRepairCapability repair)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.repair = repair ?? throw new ArgumentNullException(nameof(repair));
            Hook candidateClassifier = null;

            try
            {
                candidateClassifier = new Hook(FindMethod(typeof(HUD_Buildings), "GetBuildingShowRepair",
                    BindingFlags.Public | BindingFlags.Instance, typeof(int), typeof(int)),
                    (ShowRepairDelegate)ShowRepairHook);
                originalShowRepair = candidateClassifier.GenerateTrampoline<ShowRepairDelegate>();
                classifierHook = candidateClassifier;

            }
            catch
            {
                // Only an unpublished initialization candidate can be rolled back.


                try { candidateClassifier?.Undo(); } catch { }
                try { candidateClassifier?.Dispose(); } catch { }
                throw;
            }
        }

        private void RegisterHudEvents()
        {
            if (!APIShared.Presentation.PresentationEvents.TryRegister(
                APIShared.Presentation.PresentationOperation.GuiChecks, ExtraFeaturesPlugin.PluginGuid,
                "BuildingRepairHud", null, args => {
                    if (args.OriginalCompleted) HudUpdateHook(args.Controller);
                }, out string reason)) throw new InvalidOperationException(reason);
        }
        internal static BuildingRepairHudRuntime Install(ManualLogSource log, IBuildingRepairCapability repair) =>
            Create(log, repair);

        private static BuildingRepairHudRuntime Create(ManualLogSource log, IBuildingRepairCapability repair)
        {
            var runtime = new BuildingRepairHudRuntime(log, repair);
            try { runtime.RegisterHudEvents(); }
            catch
            {
                // The classifier belongs to this failed, unpublished candidate.
                try { runtime.classifierHook.Undo(); } catch { }
                try { runtime.classifierHook.Dispose(); } catch { }
                throw;
            }
            return runtime;
        }

        internal void SetActive(bool enabled)
        {
            Interlocked.Exchange(ref active, enabled ? 1 : 0);
            repair.SetActive(enabled);
            if (!enabled)
            {
                hovered = false;
                repair.EndHover(HoverId);
            }
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
            if (!ReferenceEquals(hud, lastHudPanel))
            {
                if (hovered)
                    repair.EndHover(HoverId);
                if (lastHoverHost != null)
                {
                    lastHoverHost.MouseEnter -= OnButtonEnter;
                    lastHoverHost.MouseLeave -= OnButtonLeave;
                }
                lastHudPanel = hud;
                lastHoverHost = null;
                lastRepairButton = null;
                hovered = false;
                lastSpecialLayout = null;
            }
            if (!view.Show_HUD_Building)
            {
                if (lastHoverHost != null && lastHoverHost.Visibility != Visibility.Hidden)
                    lastHoverHost.Visibility = Visibility.Hidden;
                if (hovered)
                {
                    hovered = false;
                    repair.EndHover(HoverId);
                }
                return;
            }
            if (lastRepairButton == null)
                lastRepairButton = hud.FindName(ButtonName) as Button;
            Grid hoverHost = lastHoverHost ?? hud.FindName(HoverHostName) as Grid;
            Button button = lastRepairButton;
            if (button == null || hoverHost == null) return;
            if (!ReferenceEquals(hoverHost, lastHoverHost))
            {
                hoverHost.MouseEnter += OnButtonEnter;
                hoverHost.MouseLeave += OnButtonLeave;
                lastHoverHost = hoverHost;
            }
            if (!IsActive || !repair.TryGetSelectedQuote(out BuildingRepairQuote quote))
            {
                if (hoverHost.Visibility != Visibility.Hidden)
                    hoverHost.Visibility = Visibility.Hidden;
                if (button.IsEnabled)
                    button.IsEnabled = false;
                if (hovered)
                {
                    hovered = false;
                    repair.EndHover(HoverId);
                }
                return;
            }

            bool special = hud.RefBarracksPanel.Visibility == Visibility.Visible ||
                hud.RefMercPostPanel.Visibility == Visibility.Visible ||
                hud.RefBedouinStockadePanel.Visibility == Visibility.Visible;
            if (lastSpecialLayout != special)
            {
                hoverHost.Width = special ? 20 : 24;
                hoverHost.Height = special ? 20 : 24;
                hoverHost.Margin = special
                    ? new Thickness(0, 0, 235, 27)
                    : new Thickness(0, 0, 262, 44);
                lastSpecialLayout = special;
            }
            if (hoverHost.Visibility != Visibility.Visible)
                hoverHost.Visibility = Visibility.Visible;
            bool enabled = quote.CanRepair && quote.CurrentHealth < quote.MaxHealth &&
                quote.HasResources && hud.RefButtonRepair.IsEnabled;
            if (button.IsEnabled != enabled)
                button.IsEnabled = enabled;
            float opacity = enabled ? 1.0f : 0.5f;
            if (button.Opacity != opacity)
                button.Opacity = opacity;
        }

        private void OnButtonEnter(object sender, MouseEventArgs args)
        {
            if (IsActive)
            {
                hovered = true;
                repair.BeginHover(HoverId);
            }
        }

        private void OnButtonLeave(object sender, MouseEventArgs args)
        {
            hovered = false;
            repair.EndHover(HoverId);
        }
    }
}
