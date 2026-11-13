using System;
using System.ComponentModel;
using System.Globalization;
using APIShared;
using APIShared.Presentation;
using BepInEx.Configuration;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using Application = UnityEngine.Application;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private const string HiddenDateCanvasName = "SpectatorHiddenDateCanvas";
        private const int HiddenDateDiscoveryAttempts = 50;
        private static ConfigEntry<string> hiddenDatePosition;
        private static Canvas hiddenDateCanvas;
        private static TextBlock hiddenDate;
        private static MainViewModel hiddenDateView;
        private static IUiFrameWorkRegistration hiddenDateWork;
        private static int hiddenDateAttempts;
        private static Point hiddenDateAnchor = new Point(1, 1);
        private static Point hiddenDatePreview, hiddenDateDragOffset;
        private static bool hiddenDateDragging;

        internal static void ConfigureHiddenDate(ConfigFile config)
        {
            hiddenDatePosition = config.Bind("Interface", "HiddenHudDatePosition", "1,1",
                "Relative position of the date shown while the HUD is hidden.");
            string[] coordinates = hiddenDatePosition.Value.Split(',');
            if (coordinates.Length == 2 && TryParseDateAnchor(coordinates[0], out float x) &&
                TryParseDateAnchor(coordinates[1], out float y)) hiddenDateAnchor = new Point(x, y);
        }

        private static bool TryParseDateAnchor(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 1;

        private static void EnableHiddenDate()
        {
            // Keep discovery deadlines independent of the toolbar's continuous rendering.
            if (!ApiShared.ForMod(Guid).Ui.TryRegisterBeforeRender("hidden-hud-date", RefreshHiddenDate,
                out hiddenDateWork, out string reason))
            {
                log.LogError("SPECTATOR_HIDDEN_DATE_UI_UNAVAILABLE: " + reason);
                return;
            }
            settings.PropertyChanged += OnHiddenDateSettingChanged;
        }

        private static void RequestHiddenDateRefresh() => hiddenDateWork?.Request();

        private static void OnHiddenDateSettingChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(SpectatorViewModel.ShowDateWhenHudHidden) ||
                args.PropertyName == nameof(SpectatorViewModel.EnableClientFeatures)) RequestHiddenDateRefresh();
        }

        private static void OnHiddenDateViewChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case null: case "":
                case nameof(MainViewModel.Show_InGame):
                case nameof(MainViewModel.Show_InGameUI):
                case nameof(MainViewModel.OST_Date_Vis):
                case nameof(MainViewModel.OST_Date_Text):
                case nameof(MainViewModel.Show_BlackOut):
                case nameof(MainViewModel.Show_HUD_Briefing):
                case nameof(MainViewModel.Show_HUD_MissionOver):
                case nameof(MainViewModel.Show_HUD_IngameMenu):
                case nameof(MainViewModel.Show_HUD_Options):
                case nameof(MainViewModel.Show_HUD_Help):
                case nameof(MainViewModel.Show_HUD_FreebuildMenu):
                case nameof(MainViewModel.Show_HUD_WorkshopUploader):
                case nameof(MainViewModel.Show_HUD_Confirmation):
                case nameof(MainViewModel.Show_HUD_LoadSaveRequester):
                    RequestHiddenDateRefresh();
                    break;
            }
        }

        private static bool HiddenDateEnabled() => settings.EnableClientFeatures && settings.ShowDateWhenHudHidden &&
            IsCurrentSession(CurrentSessionId);

        private static bool CanShowHiddenDate() => HiddenDateEnabled() && hiddenDateView is MainViewModel view &&
            view.Show_InGame && !view.Show_InGameUI && MainControls.instance is MainControls controls && controls != null &&
            !controls.IsUIVisible && view.OST_Date_Vis && !string.IsNullOrWhiteSpace(view.OST_Date_Text) &&
            !view.Show_BlackOut && !view.Show_HUD_Briefing && !view.Show_HUD_MissionOver && !view.Show_HUD_IngameMenu &&
            !view.Show_HUD_Options && !view.Show_HUD_Help && !view.Show_HUD_FreebuildMenu && !view.Show_HUD_WorkshopUploader &&
            !view.Show_HUD_Confirmation && !view.Show_HUD_LoadSaveRequester;

        private static void RefreshHiddenDate()
        {
            if (!HiddenDateEnabled()) { DetachHiddenDate(); return; }

            if (hiddenDate == null && !TryAttachHiddenDate())
            {
                // The mission can be published before Noesis creates the patched surface.
                if (++hiddenDateAttempts < HiddenDateDiscoveryAttempts) hiddenDateWork.RequestAfter(presentationStatusInterval);
                return;
            }

            if (!CanShowHiddenDate())
            {
                EndHiddenDateDrag();
                hiddenDate.Visibility = Visibility.Collapsed;
                return;
            }

            if (hiddenDate.Text != hiddenDateView.OST_Date_Text) hiddenDate.Text = hiddenDateView.OST_Date_Text;
            hiddenDate.Visibility = Visibility.Visible;
            PositionHiddenDate();
        }

        private static bool TryAttachHiddenDate()
        {
            if (!MainViewModel.viewModelLoaded) return false;
            var view = MainViewModel.Instance;
            if (view?.HUDmain == null || GameXAMLManagerAPI.Instance.FindGlobalElement(HiddenDateCanvasName) is not Canvas canvas ||
                !canvas.IsLoaded || canvas.Children.Count != 1 || canvas.Children[0] is not TextBlock date) return false;

            hiddenDateView = view;
            hiddenDateCanvas = canvas;
            hiddenDate = date;
            hiddenDateAttempts = 0;
            view.PropertyChanged += OnHiddenDateViewChanged;
            canvas.Unloaded += OnHiddenDateUnloaded;
            canvas.SizeChanged += OnHiddenDateSizeChanged;
            date.SizeChanged += OnHiddenDateSizeChanged;
            date.IsVisibleChanged += OnHiddenDateVisibilityChanged;
            date.MouseLeftButtonDown += OnHiddenDateMouseDown;
            date.MouseLeftButtonUp += OnHiddenDateMouseUp;
            date.MouseMove += OnHiddenDateMouseMove;
            date.LostMouseCapture += OnHiddenDateCaptureLost;
            Application.focusChanged += OnHiddenDateFocusChanged;
            return true;
        }

        private static void ResetHiddenDate()
        {
            hiddenDateWork?.CancelPending();
            DetachHiddenDate();
        }

        private static void DetachHiddenDate()
        {
            hiddenDateAttempts = 0;
            if (hiddenDate == null) return;
            EndHiddenDateDrag();
            Application.focusChanged -= OnHiddenDateFocusChanged;
            hiddenDateView.PropertyChanged -= OnHiddenDateViewChanged;
            hiddenDateCanvas.Unloaded -= OnHiddenDateUnloaded;
            hiddenDateCanvas.SizeChanged -= OnHiddenDateSizeChanged;
            hiddenDate.SizeChanged -= OnHiddenDateSizeChanged;
            hiddenDate.IsVisibleChanged -= OnHiddenDateVisibilityChanged;
            hiddenDate.MouseLeftButtonDown -= OnHiddenDateMouseDown;
            hiddenDate.MouseLeftButtonUp -= OnHiddenDateMouseUp;
            hiddenDate.MouseMove -= OnHiddenDateMouseMove;
            hiddenDate.LostMouseCapture -= OnHiddenDateCaptureLost;
            hiddenDate.Visibility = Visibility.Collapsed;
            hiddenDate = null;
            hiddenDateCanvas = null;
            hiddenDateView = null;
        }

        private static void OnHiddenDateUnloaded(object sender, RoutedEventArgs args)
        {
            DetachHiddenDate();
            RequestHiddenDateRefresh();
        }

        private static void OnHiddenDateSizeChanged(object sender, SizeChangedEventArgs args) => PositionHiddenDate();
        private static void OnHiddenDateCaptureLost(object sender, MouseEventArgs args) => EndHiddenDateDrag();
        private static void OnHiddenDateFocusChanged(bool focused) { if (!focused) EndHiddenDateDrag(); }
        private static void OnHiddenDateVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        { if (hiddenDate?.IsVisible != true) EndHiddenDateDrag(); }

        private static void PositionHiddenDate()
        {
            if (hiddenDate?.Visibility != Visibility.Visible || hiddenDateCanvas.ActualWidth <= 0) return;
            if (hiddenDate.MaxWidth != hiddenDateCanvas.ActualWidth) hiddenDate.MaxWidth = hiddenDateCanvas.ActualWidth;
            var anchor = hiddenDateDragging ? hiddenDatePreview : hiddenDateAnchor;
            float width = Math.Max(0, hiddenDateCanvas.ActualWidth - hiddenDate.ActualWidth);
            float height = Math.Max(0, hiddenDateCanvas.ActualHeight - hiddenDate.ActualHeight);
            float insetX = Math.Min(8, width / 2), insetY = Math.Min(8, height / 2);
            Canvas.SetLeft(hiddenDate, insetX + anchor.X * (width - 2 * insetX));
            Canvas.SetTop(hiddenDate, insetY + anchor.Y * (height - 2 * insetY));
        }

        private static void OnHiddenDateMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (!CanShowHiddenDate() || !hiddenDate.CaptureMouse()) return;
            hiddenDateDragging = true;
            hiddenDatePreview = hiddenDateAnchor;
            hiddenDateDragOffset = args.GetPosition(hiddenDate);
            args.Handled = true;
        }

        private static void OnHiddenDateMouseMove(object sender, MouseEventArgs args)
        {
            if (!hiddenDateDragging) return;
            if (args.LeftButton != MouseButtonState.Pressed || !CanShowHiddenDate()) { EndHiddenDateDrag(); return; }
            var point = args.GetPosition(hiddenDateCanvas);
            hiddenDatePreview = new Point(
                DateAnchor(point.X - hiddenDateDragOffset.X, hiddenDateCanvas.ActualWidth - hiddenDate.ActualWidth, hiddenDatePreview.X),
                DateAnchor(point.Y - hiddenDateDragOffset.Y, hiddenDateCanvas.ActualHeight - hiddenDate.ActualHeight, hiddenDatePreview.Y));
            PositionHiddenDate();
            args.Handled = true;
        }

        private static float DateAnchor(float position, float space, float previous)
        {
            float inset = Math.Min(8, Math.Max(0, space) / 2);
            return space > 2 * inset ? Math.Max(0, Math.Min(1, (position - inset) / (space - 2 * inset))) : previous;
        }

        private static void OnHiddenDateMouseUp(object sender, MouseButtonEventArgs args)
        {
            if (!hiddenDateDragging) return;
            bool save = CanShowHiddenDate() && !hiddenDateAnchor.Equals(hiddenDatePreview);
            if (save) hiddenDateAnchor = hiddenDatePreview;
            EndHiddenDateDrag();
            args.Handled = true;
            if (save)
            {
                try
                {
                    hiddenDatePosition.Value = FormattableString.Invariant($"{hiddenDateAnchor.X:R},{hiddenDateAnchor.Y:R}");
                    if (!hiddenDatePosition.ConfigFile.SaveOnConfigSet) hiddenDatePosition.ConfigFile.Save();
                }
                catch (Exception error) { LogOnce("SPECTATOR_HIDDEN_DATE_POSITION_SAVE: " + error); }
            }
        }

        private static void EndHiddenDateDrag()
        {
            if (!hiddenDateDragging) return;
            hiddenDateDragging = false;
            if (hiddenDate?.IsMouseCaptured == true) hiddenDate.ReleaseMouseCapture();
            PositionHiddenDate();
        }
    }
}
