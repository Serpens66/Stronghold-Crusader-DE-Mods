using System;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;

namespace BugfixesAndQoL
{
    internal sealed class ForeignTroopHudView
    {
        private static readonly int[] PortraitCodes = CreatePortraitCodes();
        private readonly Canvas[] slots = new Canvas[8];
        private readonly Button[] portraits = new Button[8];
        private readonly TextBlock[] typeLabels = new TextBlock[8];
        private readonly TextBlock[] ownerLabels = new TextBlock[8];
        private readonly TextBlock[] counts = new TextBlock[8];
        private readonly TextBlock[] currentHealth = new TextBlock[8];
        private readonly TextBlock[] maxHealth = new TextBlock[8];
        private readonly List<ForeignTroopEntry> visibleEntries = new List<ForeignTroopEntry>();
        private readonly List<ForeignTroopEntry> visibleEntryPool = new List<ForeignTroopEntry>();
        private readonly ForeignTroopEntry[] drawnEntries = new ForeignTroopEntry[8];
        private static readonly SolidColorBrush[] PlayerBrushes = {
            Brush(210, 204, 188), Brush(235, 72, 56), Brush(238, 156, 55),
            Brush(237, 212, 72), Brush(87, 147, 229), Brush(163, 158, 155),
            Brush(171, 105, 203), Brush(107, 211, 223), Brush(103, 207, 92)
        };
        private Canvas panel;
        private TextBlock pageText;
        private Button previous;
        private Button next;
        private int page;
        private Grid mainRoot;
        private Grid troopRoot;
        private HUD_Main activeMainHud;
        private HUD_Troops activeTroopHud;
        private IngameUIScreens activeIngameUi;
        private Geometry originalMainClip;
        private Geometry originalTroopClip;
        private bool originalTroopHitTest;
        private FrameworkElement[] nativeControls;
        private float[] originalOpacities;
        private bool showHealth;
        internal bool IsVisible => panel != null && panel.Visibility == Visibility.Visible;

        private static readonly string[] NativeControlNames = {
            "TroopSelectionControls", "TroopSelectionNumbers", "ToggleControlGroups",
            "ButtonTroopPanelPage1", "ButtonTroopPanelPage2", "Leftpadding",
            "TroopsPanelRollover", "TroopsPanelRollover2", "StanceTabs", "UnitControls"
        };

        internal bool LordIconMissing { get; private set; }
        internal Action<int, int> CenterOnGroup { get; set; }

        internal void ResetForMap()
        {
            Hide();
            Detach();
            visibleEntries.Clear();
            page = 0;
        }

        internal void Hide()
        {
            if (panel != null) panel.Visibility = Visibility.Collapsed;
            RestoreVanilla();
        }

        internal bool ActivateVanilla(MainViewModel main, out string failure)
        {
            failure = null;
            if (mainRoot != null && troopRoot != null &&
                ReferenceEquals(activeMainHud, main.HUDmain) &&
                ReferenceEquals(activeTroopHud, main.HUDTroopPanel))
                return true;
            RestoreVanilla();
            if (main.HUDmain == null) { failure = "HUDmain"; return false; }
            if (main.HUDTroopPanel == null) { failure = "HUDTroopPanel"; return false; }
            Grid nextMainRoot = FindNamed<Grid>(main.HUDmain, "HUD_Main", "LayoutRoot", out failure);
            if (failure != null) return false;
            Grid nextTroopRoot = FindNamed<Grid>(main.HUDTroopPanel, "HUD_Troops", "LayoutRoot", out failure);
            if (failure != null) return false;
            var controls = new FrameworkElement[NativeControlNames.Length];
            var opacities = new float[controls.Length];
            for (int i = 0; i < controls.Length; i++)
            {
                controls[i] = FindNamed<FrameworkElement>(main.HUDTroopPanel, "HUD_Troops", NativeControlNames[i], out failure);
                if (failure != null) return false;
                opacities[i] = controls[i].Opacity;
            }
            mainRoot = nextMainRoot;
            troopRoot = nextTroopRoot;
            activeMainHud = main.HUDmain;
            activeTroopHud = main.HUDTroopPanel;
            nativeControls = controls;
            originalOpacities = opacities;
            originalMainClip = mainRoot.Clip;
            originalTroopClip = troopRoot.Clip;
            originalTroopHitTest = troopRoot.IsHitTestVisible;
            // Keep the radar frame and editor shields; hide only the building controls to their left.
            mainRoot.Clip = new RectangleGeometry(new Rect(660, 0, 254, 306));
            // Keep Vanilla's sword at the left; stop before the minimap and editor controls.
            troopRoot.Clip = new RectangleGeometry(new Rect(130, 0, 430, 155));
            troopRoot.IsHitTestVisible = false;
            foreach (FrameworkElement control in nativeControls) control.Opacity = 0;
            return true;
        }

        private void RestoreVanilla()
        {
            if (nativeControls != null)
                for (int i = 0; i < nativeControls.Length; i++)
                    if (nativeControls[i] != null)
                        nativeControls[i].Opacity = originalOpacities[i];
            if (troopRoot != null)
            {
                troopRoot.Clip = originalTroopClip;
                troopRoot.IsHitTestVisible = originalTroopHitTest;
            }
            if (mainRoot != null) mainRoot.Clip = originalMainClip;
            mainRoot = null;
            troopRoot = null;
            activeMainHud = null;
            activeTroopHud = null;
            nativeControls = null;
            originalOpacities = null;
            originalMainClip = null;
            originalTroopClip = null;
        }

        internal bool Show(MainViewModel main, List<ForeignTroopEntry> entries, bool healthEnabled, out string failure)
        {
            if (!Resolve(main, out failure)) return false;
            bool healthChanged = showHealth != healthEnabled;
            showHealth = healthEnabled;
            bool selectionChanged = entries.Count != visibleEntries.Count;
            bool displayChanged = selectionChanged;
            if (!selectionChanged)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Owner != visibleEntries[i].Owner || entries[i].Type != visibleEntries[i].Type)
                    { selectionChanged = true; break; }
            if (!displayChanged)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Owner != visibleEntries[i].Owner ||
                        entries[i].Type != visibleEntries[i].Type ||
                        entries[i].ColorId != visibleEntries[i].ColorId ||
                        entries[i].Count != visibleEntries[i].Count ||
                        entries[i].CurrentHealth != visibleEntries[i].CurrentHealth ||
                        entries[i].MaxHealth != visibleEntries[i].MaxHealth)
                    { displayChanged = true; break; }
            if (selectionChanged) page = 0;
            if (displayChanged || healthChanged)
            {
                visibleEntries.Clear();
                foreach (ForeignTroopEntry entry in entries)
                {
                    int index = visibleEntries.Count;
                    ForeignTroopEntry copy = index < visibleEntryPool.Count ? visibleEntryPool[index] : new ForeignTroopEntry();
                    if (index == visibleEntryPool.Count) visibleEntryPool.Add(copy);
                    copy.Owner = entry.Owner;
                    copy.Type = entry.Type;
                    copy.ColorId = entry.ColorId;
                    copy.Count = entry.Count;
                    copy.CurrentHealth = entry.CurrentHealth;
                    copy.MaxHealth = entry.MaxHealth;
                    visibleEntries.Add(copy);
                }
                page = Math.Min(page, (visibleEntries.Count - 1) / 8);
                Draw(healthChanged);
            }
            panel.Visibility = Visibility.Visible;
            return true;
        }

        private bool Resolve(MainViewModel main, out string failure)
        {
            failure = null;
            IngameUIScreens screen = main.IngameUI;
            if (screen == null) { failure = "IngameUI:not-found"; return false; }
            if (panel != null && panel.IsLoaded && ReferenceEquals(activeIngameUi, screen)) return true;
            Canvas nextCanvas = FindNamed<Canvas>(screen, "IngameUI", "ForeignTroopHudPanel", out failure);
            if (failure != null) return false;
            TextBlock nextPageText = FindNamed<TextBlock>(screen, "IngameUI", "ForeignTroopPageText", out failure);
            if (failure != null) return false;
            Button nextPrevious = FindNamed<Button>(screen, "IngameUI", "ForeignTroopPrevious", out failure);
            if (failure != null) return false;
            Button nextNext = FindNamed<Button>(screen, "IngameUI", "ForeignTroopNext", out failure);
            if (failure != null) return false;
            var nextSlots = new Canvas[8];
            var nextPortraits = new Button[8];
            var nextTypeLabels = new TextBlock[8];
            var nextOwnerLabels = new TextBlock[8];
            var nextCounts = new TextBlock[8];
            var nextCurrentHealth = new TextBlock[8];
            var nextMaxHealth = new TextBlock[8];
            for (int i = 0; i < 8; i++)
            {
                string number = (i + 1).ToString();
                nextSlots[i] = FindNamed<Canvas>(screen, "IngameUI", "ForeignTroopSlot" + number, out failure);
                if (failure != null) return false;
                nextPortraits[i] = FindNamed<Button>(screen, "IngameUI", "ForeignTroopImage" + number, out failure);
                if (failure != null) return false;
                nextTypeLabels[i] = FindNamed<TextBlock>(screen, "IngameUI", "ForeignTroopType" + number, out failure);
                if (failure != null) return false;
                nextOwnerLabels[i] = FindNamed<TextBlock>(screen, "IngameUI", "ForeignTroopOwner" + number, out failure);
                if (failure != null) return false;
                nextCounts[i] = FindNamed<TextBlock>(screen, "IngameUI", "ForeignTroopCount" + number, out failure);
                if (failure != null) return false;
                nextCurrentHealth[i] = FindNamed<TextBlock>(screen, "IngameUI", "ForeignTroopCurrentHealth" + number, out failure);
                if (failure != null) return false;
                nextMaxHealth[i] = FindNamed<TextBlock>(screen, "IngameUI", "ForeignTroopMaxHealth" + number, out failure);
                if (failure != null) return false;
            }
            Detach();
            activeIngameUi = screen;
            panel = nextCanvas;
            pageText = nextPageText;
            previous = nextPrevious;
            next = nextNext;
            Array.Copy(nextSlots, slots, 8);
            Array.Copy(nextPortraits, portraits, 8);
            Array.Copy(nextTypeLabels, typeLabels, 8);
            Array.Copy(nextOwnerLabels, ownerLabels, 8);
            Array.Copy(nextCounts, counts, 8);
            Array.Copy(nextCurrentHealth, currentHealth, 8);
            Array.Copy(nextMaxHealth, maxHealth, 8);
            // Resolved controls may retain their visual state from the previous map.
            panel.Visibility = Visibility.Collapsed;
            for (int i = 0; i < slots.Length; i++) slots[i].Visibility = Visibility.Collapsed;
            pageText.Text = string.Empty;
            previous.Visibility = Visibility.Collapsed;
            next.Visibility = Visibility.Collapsed;
            page = 0;
            for (int i = 0; i < portraits.Length; i++)
            {
                portraits[i].PreviewMouseDown += OnPortraitMouseDown;
                typeLabels[i].PreviewMouseDown += OnPortraitMouseDown;
            }
            previous.Click += OnPrevious;
            next.Click += OnNext;
            return true;
        }

        private static T FindNamed<T>(FrameworkElement host, string control, string name, out string failure)
            where T : FrameworkElement
        {
            object found = host.FindName(name);
            if (found is T match) { failure = null; return match; }
            failure = control + "." + name + (found == null ? ":not-found" :
                ":wrong-type:actual=" + found.GetType().FullName + ",expected=" + typeof(T).FullName) +
                ",hostLoaded=" + host.IsLoaded;
            return null;
        }

        private void Detach()
        {
            for (int i = 0; i < portraits.Length; i++)
            {
                if (portraits[i] != null) portraits[i].PreviewMouseDown -= OnPortraitMouseDown;
                if (typeLabels[i] != null) typeLabels[i].PreviewMouseDown -= OnPortraitMouseDown;
            }
            if (previous != null) previous.Click -= OnPrevious;
            if (next != null) next.Click -= OnNext;
            visibleEntries.Clear();
            visibleEntryPool.Clear();
            Array.Clear(drawnEntries, 0, drawnEntries.Length);
            panel = null;
            activeIngameUi = null;
            pageText = null;
            previous = null;
            next = null;
            Array.Clear(slots, 0, slots.Length);
            Array.Clear(portraits, 0, portraits.Length);
            Array.Clear(typeLabels, 0, typeLabels.Length);
            Array.Clear(ownerLabels, 0, ownerLabels.Length);
            Array.Clear(counts, 0, counts.Length);
            Array.Clear(currentHealth, 0, currentHealth.Length);
            Array.Clear(maxHealth, 0, maxHealth.Length);
        }

        private void OnPrevious(object sender, RoutedEventArgs args)
        {
            if (page > 0) { page--; Draw(); }
            args.Handled = true;
        }

        private void OnNext(object sender, RoutedEventArgs args)
        {
            if ((page + 1) * 8 < visibleEntries.Count) { page++; Draw(); }
            args.Handled = true;
        }

        private void OnPortraitMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (args == null || panel == null || panel.Visibility != Visibility.Visible)
                return;
            if (args.ChangedButton != MouseButton.Middle)
            {
                args.Handled = true;
                return;
            }
            if (args.ClickCount != 1) return;
            int slot = Array.IndexOf(portraits, sender);
            if (slot < 0) slot = Array.IndexOf(typeLabels, sender);
            int index = page * 8 + slot;
            if (slot < 0 || index >= visibleEntries.Count) return;
            ForeignTroopEntry entry = visibleEntries[index];
            CenterOnGroup?.Invoke(entry.Owner, entry.Type);
            args.Handled = true;
        }

        private void Draw(bool healthChanged = false)
        {
            LordIconMissing = false;
            for (int i = 0; i < 8; i++)
            {
                int entryIndex = page * 8 + i;
                if (entryIndex >= visibleEntries.Count)
                {
                    slots[i].Visibility = Visibility.Collapsed;
                    drawnEntries[i] = null;
                    continue;
                }
                ForeignTroopEntry entry = visibleEntries[entryIndex];
                ForeignTroopEntry drawn = drawnEntries[i];
                bool identityChanged = drawn == null || drawn.Owner != entry.Owner ||
                    drawn.Type != entry.Type || drawn.ColorId != entry.ColorId;
                if (drawn == null) slots[i].Visibility = Visibility.Visible;
                if (identityChanged)
                {
                    ownerLabels[i].Foreground = PlayerBrush(entry.ColorId);
                    ownerLabels[i].Text = "P" + entry.Owner;
                    int portraitCode = entry.Type < PortraitCodes.Length ? PortraitCodes[entry.Type] : 0;
                    ImageSource source = entry.Type == 55 ? Shared.LordPortraitPalette.Get(entry.ColorId) :
                        portraitCode == 0 ? null : FindPortrait(portraitCode, entry.ColorId);
                    if (entry.Type == 55 && source == null) LordIconMissing = true;
                    Canvas.SetTop(portraits[i], entry.Type == 55 ? 65f : 53f);
                    Canvas.SetTop(typeLabels[i], entry.Type == 55 ? 65f : 53f);
                    PropEx.SetSprite1(portraits[i], source);
                    PropEx.SetSprite2(portraits[i], source);
                    PropEx.SetSprite3(portraits[i], source);
                    PropEx.SetSprite4(portraits[i], source);
                    portraits[i].Visibility = source == null ? Visibility.Collapsed : Visibility.Visible;
                    typeLabels[i].Text = "Typ " + entry.Type;
                    typeLabels[i].Visibility = source == null ? Visibility.Visible : Visibility.Collapsed;
                }
                if (drawn == null || drawn.Count != entry.Count) counts[i].Text = entry.Count.ToString();
                if (drawn == null || drawn.CurrentHealth != entry.CurrentHealth || healthChanged)
                    currentHealth[i].Text = showHealth ? ScaleHealth(entry.CurrentHealth) : string.Empty;
                if (drawn == null || drawn.MaxHealth != entry.MaxHealth || healthChanged)
                    maxHealth[i].Text = showHealth ? ScaleHealth(entry.MaxHealth) : string.Empty;
                if (showHealth && (drawn == null || healthChanged ||
                    drawn.CurrentHealth != entry.CurrentHealth || drawn.MaxHealth != entry.MaxHealth))
                    currentHealth[i].Foreground = HealthBrush(entry);
                if (drawn == null) drawnEntries[i] = drawn = new ForeignTroopEntry();
                drawn.Owner = entry.Owner;
                drawn.Type = entry.Type;
                drawn.ColorId = entry.ColorId;
                drawn.Count = entry.Count;
                drawn.CurrentHealth = entry.CurrentHealth;
                drawn.MaxHealth = entry.MaxHealth;
            }
            int pages = (visibleEntries.Count + 7) / 8;
            pageText.Text = pages > 1 ? (page + 1) + "/" + pages : "";
            previous.Visibility = page > 0 ? Visibility.Visible : Visibility.Collapsed;
            next.Visibility = (page + 1) * 8 < visibleEntries.Count ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Brush HealthBrush(ForeignTroopEntry entry) =>
            SelectedUnitHealthSlotViewModel.GetBandBrush(SelectedUnitHealthSummary.GetBand(
                (long)Math.Min(entry.CurrentHealth, (ulong)long.MaxValue),
                (long)Math.Min(entry.MaxHealth, (ulong)long.MaxValue)));

        private static string ScaleHealth(ulong value) =>
            SelectedUnitHealthSummary.ScaleForDisplay((long)Math.Min(value, (ulong)long.MaxValue)).ToString();

        private static ImageSource FindPortrait(int code, int colorId)
        {
            string key = "UI-Buttons K" + code.ToString("000");
            string suffix = ColorSuffix(colorId);
            var resources = GUI.GetApplicationResources();
            if (resources == null) return null;
            if (!string.IsNullOrEmpty(suffix))
            {
                try
                {
                    ImageSource colored = resources[key + " " + suffix] as ImageSource;
                    if (colored != null) return colored;
                }
                catch { /* Some unit portraits have no variant for this colour. */ }
            }
            try
            {
                return resources[key] as ImageSource;
            }
            catch { return null; }
        }

        private static string ColorSuffix(int colorId)
        {
            switch (colorId)
            {
                case 1: return "";
                case 2: return "Orange";
                case 3: return "Yellow";
                case 4: return "Blue";
                case 5: return "Black";
                case 6: return "Purple";
                case 7: return "Light Blue";
                case 8: return "Green";
                default: return null;
            }
        }

        private static SolidColorBrush PlayerBrush(int colorId)
        {
            return colorId >= 1 && colorId < PlayerBrushes.Length ? PlayerBrushes[colorId] : PlayerBrushes[0];
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b) =>
            new SolidColorBrush(Noesis.Color.FromArgb(255, r, g, b));

        private static int[] CreatePortraitCodes()
        {
            int[] codes = new int[89];
            int[] pairs = {
                22,23, 24,1, 26,3, 23,5, 25,25, 27,7, 28,27, 30,9, 37,11, 29,29, 5,31,
                39,13, 40,15, 59,17, 58,19, 60,33, 41,21, 61,35, 70,37, 71,39, 72,41,
                73,43, 74,45, 75,47, 76,49, 77,51, 78,53, 79,55, 80,57, 81,59, 82,61,
                83,63, 84,65, 85,67
            };
            for (int i = 0; i < pairs.Length; i += 2) codes[pairs[i]] = pairs[i + 1];
            return codes;
        }
    }
}
