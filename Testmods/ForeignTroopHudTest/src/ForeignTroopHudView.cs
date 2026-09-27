using System;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;

namespace ForeignTroopHudTest
{
    internal sealed class ForeignTroopHudView
    {
        private static readonly int[] PortraitCodes = CreatePortraitCodes();
        private readonly Canvas[] slots = new Canvas[8];
        private readonly Button[] portraits = new Button[8];
        private static readonly SolidColorBrush HealthyBrush = Brush(102, 204, 102);
        private static readonly SolidColorBrush WoundedBrush = Brush(255, 214, 102);
        private static readonly SolidColorBrush CriticalBrush = Brush(255, 102, 102);
        private readonly TextBlock[] typeLabels = new TextBlock[8];
        private readonly TextBlock[] ownerLabels = new TextBlock[8];
        private readonly TextBlock[] counts = new TextBlock[8];
        private readonly TextBlock[] currentHealth = new TextBlock[8];
        private readonly TextBlock[] maxHealth = new TextBlock[8];
        private readonly List<ForeignTroopEntry> visibleEntries = new List<ForeignTroopEntry>();
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

        private static readonly string[] NativeControlNames = {
            "TroopSelectionControls", "TroopSelectionNumbers", "ToggleControlGroups",
            "ButtonTroopPanelPage1", "ButtonTroopPanelPage2", "Leftpadding",
            "TroopsPanelRollover", "TroopsPanelRollover2", "StanceTabs", "UnitControls"
        };

        internal bool LordIconMissing { get; private set; }

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
            // Include Vanilla's sword at the left and its complete radar frame at the right.
            troopRoot.Clip = new RectangleGeometry(new Rect(130, 0, 670, 155));
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

        internal bool Show(MainViewModel main, List<ForeignTroopEntry> entries, out string failure)
        {
            if (!Resolve(main, out failure)) return false;
            bool selectionChanged = entries.Count != visibleEntries.Count;
            if (!selectionChanged)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Owner != visibleEntries[i].Owner || entries[i].Type != visibleEntries[i].Type)
                    { selectionChanged = true; break; }
            if (selectionChanged) page = 0;
            visibleEntries.Clear();
            foreach (ForeignTroopEntry entry in entries)
                visibleEntries.Add(new ForeignTroopEntry
                {
                    Owner = entry.Owner,
                    Type = entry.Type,
                    ColorId = entry.ColorId,
                    Count = entry.Count,
                    CurrentHealth = entry.CurrentHealth,
                    MaxHealth = entry.MaxHealth
                });
            page = Math.Min(page, (visibleEntries.Count - 1) / 8);
            Draw();
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
            if (previous != null) previous.Click -= OnPrevious;
            if (next != null) next.Click -= OnNext;
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

        private void Draw()
        {
            LordIconMissing = false;
            for (int i = 0; i < 8; i++)
            {
                int entryIndex = page * 8 + i;
                if (entryIndex >= visibleEntries.Count)
                {
                    slots[i].Visibility = Visibility.Collapsed;
                    continue;
                }
                ForeignTroopEntry entry = visibleEntries[entryIndex];
                slots[i].Visibility = Visibility.Visible;
                SolidColorBrush playerBrush = PlayerBrush(entry.ColorId);
                ownerLabels[i].Foreground = playerBrush;
                ownerLabels[i].Text = "P" + entry.Owner;
                counts[i].Text = entry.Count.ToString();
                currentHealth[i].Text = ScaleHealth(entry.CurrentHealth);
                maxHealth[i].Text = ScaleHealth(entry.MaxHealth);
                currentHealth[i].Foreground = HealthBrush(entry);
                int portraitCode = entry.Type < PortraitCodes.Length ? PortraitCodes[entry.Type] : 0;
                ImageSource source = entry.Type == 55 ? FindLordPortrait() :
                    portraitCode == 0 ? null : FindPortrait(portraitCode, entry.ColorId);
                if (entry.Type == 55 && source == null) LordIconMissing = true;
                PropEx.SetSprite1(portraits[i], source);
                PropEx.SetSprite2(portraits[i], source);
                PropEx.SetSprite3(portraits[i], source);
                PropEx.SetSprite4(portraits[i], source);
                portraits[i].Visibility = source == null ? Visibility.Collapsed : Visibility.Visible;
                typeLabels[i].Text = "Typ " + entry.Type;
                typeLabels[i].Visibility = source == null ? Visibility.Visible : Visibility.Collapsed;
            }
            int pages = (visibleEntries.Count + 7) / 8;
            pageText.Text = pages > 1 ? (page + 1) + "/" + pages : "";
            previous.Visibility = page > 0 ? Visibility.Visible : Visibility.Collapsed;
            next.Visibility = (page + 1) * 8 < visibleEntries.Count ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string ScaleHealth(ulong health) =>
            ((long)Math.Round(health / 10m, 0, MidpointRounding.AwayFromZero)).ToString();

        private static SolidColorBrush HealthBrush(ForeignTroopEntry entry) =>
            entry.MaxHealth == 0 ? CriticalBrush :
            (decimal)entry.CurrentHealth >= (decimal)entry.MaxHealth * 0.75m ? HealthyBrush :
            (decimal)entry.CurrentHealth >= (decimal)entry.MaxHealth * 0.40m ? WoundedBrush : CriticalBrush;

        private static ImageSource FindLordPortrait()
        {
            try { return GUI.GetApplicationResources()?["BugfixesAndQoL-LordIcon"] as ImageSource; }
            catch { return null; }
        }

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
            switch (colorId)
            {
                case 1: return Brush(235, 72, 56);
                case 2: return Brush(238, 156, 55);
                case 3: return Brush(237, 212, 72);
                case 4: return Brush(87, 147, 229);
                case 5: return Brush(163, 158, 155);
                case 6: return Brush(171, 105, 203);
                case 7: return Brush(107, 211, 223);
                case 8: return Brush(103, 207, 92);
                default: return Brush(210, 204, 188);
            }
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
