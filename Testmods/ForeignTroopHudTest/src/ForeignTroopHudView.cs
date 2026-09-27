using System;
using System.Collections.Generic;
using Noesis;
using SHCDESE.API;

namespace ForeignTroopHudTest
{
    internal sealed class ForeignTroopHudView
    {
        private static readonly int[] PortraitCodes = CreatePortraitCodes();
        private readonly Canvas[] slots = new Canvas[8];
        private readonly Image[] portraits = new Image[8];
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
        }

        internal bool Show(List<ForeignTroopEntry> entries)
        {
            if (!Resolve()) return false;
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

        private bool Resolve()
        {
            if (panel != null && panel.IsLoaded) return true;
            Canvas nextCanvas = GameXAMLManagerAPI.Instance?.FindGlobalElement("ForeignTroopHudPanel") as Canvas;
            if (nextCanvas == null) { Hide(); return false; }
            if (ReferenceEquals(nextCanvas, panel)) return true;
            Detach();
            panel = nextCanvas;
            pageText = Find<TextBlock>("ForeignTroopPageText");
            previous = Find<Button>("ForeignTroopPrevious");
            next = Find<Button>("ForeignTroopNext");
            if (panel == null || pageText == null || previous == null || next == null) { Detach(); return false; }
            for (int i = 0; i < 8; i++)
            {
                string number = (i + 1).ToString();
                slots[i] = Find<Canvas>("ForeignTroopSlot" + number);
                portraits[i] = Find<Image>("ForeignTroopImage" + number);
                typeLabels[i] = Find<TextBlock>("ForeignTroopType" + number);
                ownerLabels[i] = Find<TextBlock>("ForeignTroopOwner" + number);
                counts[i] = Find<TextBlock>("ForeignTroopCount" + number);
                currentHealth[i] = Find<TextBlock>("ForeignTroopCurrentHealth" + number);
                maxHealth[i] = Find<TextBlock>("ForeignTroopMaxHealth" + number);
                if (slots[i] == null || portraits[i] == null || typeLabels[i] == null || ownerLabels[i] == null ||
                    counts[i] == null || currentHealth[i] == null || maxHealth[i] == null)
                { Detach(); return false; }
            }
            previous.Click += OnPrevious;
            next.Click += OnNext;
            return true;
        }

        private T Find<T>(string name) where T : FrameworkElement =>
            GameXAMLManagerAPI.Instance.FindElementByName(panel, name) as T;

        private void Detach()
        {
            if (previous != null) previous.Click -= OnPrevious;
            if (next != null) next.Click -= OnNext;
            panel = null;
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
                currentHealth[i].Text = entry.CurrentHealth.ToString();
                maxHealth[i].Text = entry.MaxHealth.ToString();
                int portraitCode = entry.Type < PortraitCodes.Length ? PortraitCodes[entry.Type] : 0;
                ImageSource source = portraitCode == 0 ? null : FindPortrait(portraitCode, entry.ColorId);
                portraits[i].Source = source;
                typeLabels[i].Text = "Typ " + entry.Type;
                typeLabels[i].Visibility = source == null ? Visibility.Visible : Visibility.Collapsed;
            }
            int pages = (visibleEntries.Count + 7) / 8;
            pageText.Text = pages > 1 ? (page + 1) + "/" + pages : "";
            previous.Visibility = page > 0 ? Visibility.Visible : Visibility.Collapsed;
            next.Visibility = (page + 1) * 8 < visibleEntries.Count ? Visibility.Visible : Visibility.Collapsed;
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
