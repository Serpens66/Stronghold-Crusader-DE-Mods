using System;
using System.Collections.Generic;
using BugfixesAndQoL;
using Noesis;
using SHCDESE.Interop;

// Isolated boundary doubles. Enum values were checked against the installed SHCDESE.dll;
// no native engine or Noesis rendering is exercised by this harness.
namespace SHCDESE.Interop
{
    public enum GM : ushort { GM_BODY_LORD = 100, GM_BODY_FIGHTING_MONK = 109, GM_BODY_ARABIC_LORD = 205, GM_BODY_TEMPLE_GUARD = 229 }
    public enum eChimps : ushort { CHIMP_TYPE_MONK = 37, CHIMP_TYPE_LORD = 55 }
}
namespace Noesis
{
    public enum Visibility { Visible, Collapsed }
    public enum MouseButton { Left, Middle }
    public class RoutedEventArgs : EventArgs { public bool Handled; }
    public class MouseButtonEventArgs : RoutedEventArgs { public MouseButton ChangedButton; public int ClickCount; }
    public class Geometry { }
    public struct Rect { public Rect(float x, float y, float w, float h) { } }
    public class RectangleGeometry : Geometry { public RectangleGeometry(Rect rect) { } }
    public class ImageSource { public readonly string Key; public ImageSource(string key) { Key = key; } }
    public class BitmapSource : ImageSource { public BitmapSource(string key) : base(key) { } }
    public struct Int32Rect
    {
        public readonly int X, Y, Width, Height;
        public Int32Rect(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
    }
    public sealed class CroppedBitmap : BitmapSource
    {
        public readonly ImageSource Source;
        public readonly Int32Rect Rect;
        public CroppedBitmap(BitmapSource source, Int32Rect rect) : base("crop") { Source = source; Rect = rect; }
    }
    public sealed class ResourceDictionary : Dictionary<string, object>
    {
        public bool Contains(string key) => ContainsKey(key);
    }
    public class Brush { }
    public struct Color { public static Color FromArgb(byte a, byte r, byte g, byte b) { return new Color(); } }
    public class SolidColorBrush : Brush { public SolidColorBrush(Color color) { } }
    public class FrameworkElement
    {
        public readonly Dictionary<string, object> Names = new Dictionary<string, object>();
        public bool IsLoaded = true, IsHitTestVisible = true;
        public float Opacity = 1;
        public Geometry Clip;
        public Visibility Visibility;
        public event EventHandler<MouseButtonEventArgs> PreviewMouseDown;
        public object FindName(string name) { object value; return Names.TryGetValue(name, out value) ? value : null; }
        public void PressMiddle() { PreviewMouseDown?.Invoke(this, new MouseButtonEventArgs { ChangedButton = MouseButton.Middle, ClickCount = 1 }); }
    }
    public class Grid : FrameworkElement { }
    public class Canvas : FrameworkElement { public static void SetTop(FrameworkElement element, float value) { } }
    public class Button : FrameworkElement
    {
        public readonly ImageSource[] Sprites = new ImageSource[4];
        public event EventHandler<RoutedEventArgs> Click;
        public void Press() { Click?.Invoke(this, new RoutedEventArgs()); }
    }
    public class TextBlock : FrameworkElement { public string Text; public Brush Foreground; }
    public static class GUI
    {
        public static ResourceDictionary Resources;
        public static int Reads;
        public static ResourceDictionary GetApplicationResources() { Reads++; return Resources; }
    }
}
namespace CrusaderDE
{
    public class HUD_Main : FrameworkElement { }
    public class HUD_Troops : FrameworkElement { }
    public class IngameUIScreens : FrameworkElement { }
    public class MainViewModel
    {
        public IngameUIScreens IngameUI = new IngameUIScreens();
        public HUD_Main HUDmain;
        public HUD_Troops HUDTroopPanel;
    }
    public static class PropEx
    {
        public static void SetSprite1(Button b, ImageSource s) { b.Sprites[0] = s; }
        public static void SetSprite2(Button b, ImageSource s) { b.Sprites[1] = s; }
        public static void SetSprite3(Button b, ImageSource s) { b.Sprites[2] = s; }
        public static void SetSprite4(Button b, ImageSource s) { b.Sprites[3] = s; }
    }
}
namespace APIShared
{
    public enum LordSkin { Unknown, Crusader, Arabic, Bedouin, Scribe, CrusaderFemale, Bessy, ArabicFemale, BedouinFemale }
    public static class LordSkins
    {
        public static bool TryGetFromMaterial(GM material, out LordSkin skin)
        {
            skin = material == GM.GM_BODY_LORD ? LordSkin.Crusader :
                material == GM.GM_BODY_ARABIC_LORD ? LordSkin.Arabic : LordSkin.Unknown;
            return skin != LordSkin.Unknown;
        }
    }
}
namespace BugfixesAndQoL
{
    internal readonly struct ForeignTroopPlayerColor
    {
        internal int PortraitColorId => 6;
        internal uint LabelArgb => 0xFF800080;
    }
    internal sealed class ForeignTroopPlayerColors
    {
        internal ForeignTroopPlayerColor Get(int owner) { return new ForeignTroopPlayerColor(); }
    }
    internal sealed class ForeignTroopCaptureHarness
    {
        private readonly Dictionary<int, ForeignTroopEntry> grouped = new Dictionary<int, ForeignTroopEntry>();
        private readonly List<ForeignTroopEntry> entries = new List<ForeignTroopEntry>();
        private readonly List<ForeignTroopEntry> entryPool = new List<ForeignTroopEntry>();
        private readonly ForeignTroopPlayerColors playerColors = new ForeignTroopPlayerColors();
        private int usedEntries;
        private struct Unit
        {
            internal GM r_GameMaterialIndex;
            internal uint r_CurrentHealth, r_MaxHealth;
        }
        private void Add(int owner, int type, Unit unit)
        {
            /* PRODUCTION_CAPTURE_BLOCK */
        }
        internal static void Run(Action<bool, string> check)
        {
            var capture = new ForeignTroopCaptureHarness();
            var monk = new Unit { r_GameMaterialIndex = GM.GM_BODY_FIGHTING_MONK, r_CurrentHealth = 100, r_MaxHealth = 200 };
            var guard = new Unit { r_GameMaterialIndex = GM.GM_BODY_TEMPLE_GUARD, r_CurrentHealth = 300, r_MaxHealth = 400 };
            capture.Add(6, 37, guard); capture.Add(6, 37, monk);
            ForeignTroopEntry first = capture.entries[0];
            check(capture.entries.Count == 1 && first.Material == guard.r_GameMaterialIndex, "First matching unit determines combined skin");
            check(first.Count == 2 && first.CurrentHealth == 400 && first.MaxHealth == 600, "Both skins retain combined count and health");
            capture.Add(5, 37, monk); capture.Add(6, 24, monk);
            check(capture.entries.Count == 3, "Owners and types remain separate groups");
            capture.grouped.Clear(); capture.entries.Clear(); capture.usedEntries = 0;
            capture.Add(6, 37, monk); capture.Add(6, 37, guard);
            ForeignTroopEntry reused = capture.entries[0];
            check(ReferenceEquals(first, reused) && reused.Material == monk.r_GameMaterialIndex, "Pooled entry adopts new first-unit skin");
            check(reused.Count == 2 && reused.CurrentHealth == 400 && reused.MaxHealth == 600, "Pooled counters reset before aggregation");
            check(reused.ColorId == 6 && reused.OwnerColorArgb == 0xFF800080, "Capture retains independent player color");
        }
    }
    internal static class SelectedUnitHealthSummary
    {
        internal static int GetBand(long current, long max) { return 0; }
        internal static long ScaleForDisplay(long value) { return value / 10; }
    }
    internal static class SelectedUnitHealthSlotViewModel
    {
        internal static Brush GetBandBrush(int band) { return new Brush(); }
    }
}
public static class ForeignTroopPortraitTests
{
    private static int checks;
    private static void Check(bool pass, string name) { checks++; if (!pass) throw new Exception(name); }
    private static MainViewModel Screen()
    {
        var main = new MainViewModel();
        var names = main.IngameUI.Names;
        names["ForeignTroopHudPanel"] = new Canvas();
        names["ForeignTroopPageText"] = new TextBlock();
        names["ForeignTroopPrevious"] = new Button();
        names["ForeignTroopNext"] = new Button();
        for (int i = 1; i <= 8; i++)
        {
            names["ForeignTroopSlot" + i] = new Canvas();
            names["ForeignTroopImage" + i] = new Button();
            foreach (string prefix in new[] { "ForeignTroopType", "ForeignTroopOwner", "ForeignTroopCount", "ForeignTroopCurrentHealth", "ForeignTroopMaxHealth" })
                names[prefix + i] = new TextBlock();
        }
        return main;
    }
    private static ForeignTroopEntry Entry(GM material, int color = 1)
    {
        return new ForeignTroopEntry { Owner = 6, Type = (int)eChimps.CHIMP_TYPE_MONK,
            Material = material, ColorId = color, OwnerColorArgb = 0xFF800080,
            Count = 5, CurrentHealth = 53250, MaxHealth = 93750 };
    }
    private static void Show(ForeignTroopHudView view, MainViewModel main, List<ForeignTroopEntry> entries)
    {
        string failure;
        Check(view.Show(main, entries, true, out failure), "View resolves: " + failure);
    }
    private static Button Portrait(MainViewModel main) { return (Button)main.IngameUI.Names["ForeignTroopImage1"]; }
    private static void ExpectPortrait(MainViewModel main, ImageSource expected)
    {
        foreach (ImageSource sprite in Portrait(main).Sprites) Check(ReferenceEquals(sprite, expected), "All button states use expected skin");
    }
    public static string Run()
    {
        ForeignTroopCaptureHarness.Run(Check);
        int[] pairs = { 22,23, 24,1, 26,3, 23,5, 25,25, 27,7, 28,27, 30,9, 37,11, 29,29, 5,31,
            39,13, 40,15, 59,17, 58,19, 60,33, 41,21, 61,35, 70,37, 71,39, 72,41,
            73,43, 74,45, 75,47, 76,49, 77,51, 78,53, 79,55, 80,57, 81,59, 82,61,
            83,63, 84,65, 85,67 };
        for (int i = 0; i < pairs.Length; i += 2)
            Check(ForeignTroopPortraits.GetResourceKey(pairs[i], GM.GM_BODY_FIGHTING_MONK) == "UI-Buttons K" + pairs[i + 1].ToString("000"), "Existing type mapping");
        Check(ForeignTroopPortraits.GetResourceKey(-1, 0) == null && ForeignTroopPortraits.GetResourceKey(89, 0) == null, "Unsupported type");
        Check(ForeignTroopPortraits.GetResourceKey(37, (GM)65535) == "UI-Buttons K011", "Unknown material keeps type portrait");
        Check(ForeignTroopPortraits.GetResourceKey(24, GM.GM_BODY_TEMPLE_GUARD) == "UI-Buttons K001", "Alternate material is scoped to monks");

        string[] suffixes = { "", " Orange", " Yellow", " Blue", " Black", " Purple", " Light Blue", " Green" };
        foreach (GM material in new[] { GM.GM_BODY_FIGHTING_MONK, GM.GM_BODY_TEMPLE_GUARD })
            for (int color = 1; color <= 8; color++)
            {
                string key = (material == GM.GM_BODY_TEMPLE_GUARD ? "UI-Buttons K011a" : "UI-Buttons K011") + suffixes[color - 1];
                var expected = new ImageSource(key);
                GUI.Resources = new ResourceDictionary { [key] = expected };
                var view = new ForeignTroopHudView();
                var main = Screen();
                Show(view, main, new List<ForeignTroopEntry> { Entry(material, color) });
                ExpectPortrait(main, expected);
                Check(((TextBlock)main.IngameUI.Names["ForeignTroopCount1"]).Text == "5", "Count retained");
                Check(((TextBlock)main.IngameUI.Names["ForeignTroopCurrentHealth1"]).Text == "5325", "Current health retained");
                Check(((TextBlock)main.IngameUI.Names["ForeignTroopMaxHealth1"]).Text == "9375", "Maximum health retained");
            }

        var monk = new ImageSource("monk"); var guard = new ImageSource("guard");
        GUI.Resources = new ResourceDictionary { ["UI-Buttons K011"] = monk, ["UI-Buttons K011a"] = guard };
        var changingView = new ForeignTroopHudView(); var changingMain = Screen();
        var entry = Entry(GM.GM_BODY_FIGHTING_MONK);
        var selection = new List<ForeignTroopEntry> { entry };
        Show(changingView, changingMain, selection); ExpectPortrait(changingMain, monk);
        int reads = GUI.Reads;
        Show(changingView, changingMain, selection);
        Check(GUI.Reads == reads, "Unchanged snapshot skips resource lookup");
        entry.CurrentHealth += 10;
        Show(changingView, changingMain, selection);
        Check(GUI.Reads == reads, "Health-only update retains the drawn skin without resource lookup");
        Check(((TextBlock)changingMain.IngameUI.Names["ForeignTroopCurrentHealth1"]).Text == "5326", "Health-only update still refreshes its label");
        entry.Material = GM.GM_BODY_TEMPLE_GUARD;
        Show(changingView, changingMain, selection); ExpectPortrait(changingMain, guard);
        entry.Material = GM.GM_BODY_FIGHTING_MONK;
        Show(changingView, changingMain, selection); ExpectPortrait(changingMain, monk);
        changingView.Hide(); entry.Material = GM.GM_BODY_TEMPLE_GUARD;
        Show(changingView, changingMain, selection); ExpectPortrait(changingMain, guard);
        changingView.ResetForMap(); changingMain = Screen();
        Show(changingView, changingMain, selection); ExpectPortrait(changingMain, guard);
        int centeredOwner = 0, centeredType = 0;
        changingView.CenterOnGroup = (owner, type) => { centeredOwner = owner; centeredType = type; };
        Portrait(changingMain).PressMiddle();
        Check(centeredOwner == entry.Owner && centeredType == entry.Type, "Middle-click keeps owner/type target");

        // A skin-only update must not reset the page or retain a pooled entry's old skin.
        selection.Clear();
        for (int i = 0; i < 9; i++) { var item = Entry(GM.GM_BODY_FIGHTING_MONK); item.Owner = i; selection.Add(item); }
        Show(changingView, changingMain, selection);
        ((Button)changingMain.IngameUI.Names["ForeignTroopNext"]).Press();
        selection[8].Material = GM.GM_BODY_TEMPLE_GUARD;
        Show(changingView, changingMain, selection); ExpectPortrait(changingMain, guard);
        Check(((TextBlock)changingMain.IngameUI.Names["ForeignTroopPageText"]).Text == "2/2", "Skin update preserves page");

        var fallbackView = new ForeignTroopHudView(); var fallbackMain = Screen();
        Show(fallbackView, fallbackMain, new List<ForeignTroopEntry> { Entry(GM.GM_BODY_TEMPLE_GUARD, 6) });
        ExpectPortrait(fallbackMain, guard); // Purple is absent: retain the same skin's base artwork.
        GUI.Resources = new ResourceDictionary();
        fallbackView.ResetForMap();
        Show(fallbackView, fallbackMain, new List<ForeignTroopEntry> { Entry(GM.GM_BODY_TEMPLE_GUARD) });
        Check(Portrait(fallbackMain).Visibility == Visibility.Collapsed, "Missing resource hides icon");
        Check(((TextBlock)fallbackMain.IngameUI.Names["ForeignTroopType1"]).Visibility == Visibility.Visible, "Missing resource retains type label");
        GUI.Resources = null; fallbackView.ResetForMap();
        Show(fallbackView, fallbackMain, new List<ForeignTroopEntry> { Entry(GM.GM_BODY_FIGHTING_MONK) });
        Check(Portrait(fallbackMain).Visibility == Visibility.Collapsed, "Missing dictionary remains safe");
        var atlas = new BitmapSource("Lord atlas");
        var crown = new ImageSource("crown");
        GUI.Resources = new ResourceDictionary { ["BugfixesAndQoL-LordSource"] = atlas, ["shield_crown"] = crown };
        for (int row = 0; row < 8; row++)
            for (int colour = 0; colour <= 8; colour++)
            {
                var skin = (LordSkin)(row + 1);
                var portrait = LordPortraits.Get(skin, colour) as CroppedBitmap;
                Check(portrait != null && portrait.Source == atlas && portrait.Rect.X == colour * 72 &&
                    portrait.Rect.Y == row * 123 && portrait.Rect.Width == 72 && portrait.Rect.Height == 123,
                    "Select the expected atlas cell");
                Check(ReferenceEquals(portrait, LordPortraits.Get(skin, colour)), "Reuses the atlas crop");
            }
        entry = Entry(GM.GM_BODY_LORD, 4);
        entry.Type = (int)eChimps.CHIMP_TYPE_LORD;
        entry.Count = 1;
        selection = new List<ForeignTroopEntry> { entry };
        Show(fallbackView, fallbackMain, selection);
        ExpectPortrait(fallbackMain, LordPortraits.Get(LordSkin.Crusader, 4));
        var countLabel = (TextBlock)fallbackMain.IngameUI.Names["ForeignTroopCount1"];
        Check(countLabel.Text == string.Empty, "A lone Lord has no count label");
        entry.Material = GM.GM_BODY_ARABIC_LORD;
        Show(fallbackView, fallbackMain, selection);
        ExpectPortrait(fallbackMain, LordPortraits.Get(LordSkin.Arabic, 4));
        entry.ColorId = 0;
        Show(fallbackView, fallbackMain, selection);
        ExpectPortrait(fallbackMain, LordPortraits.Get(LordSkin.Arabic, 0));
        entry.ColorId = 8;
        Show(fallbackView, fallbackMain, selection);
        ExpectPortrait(fallbackMain, LordPortraits.Get(LordSkin.Arabic, 8));
        entry.Material = GM.GM_BODY_TEMPLE_GUARD;
        Show(fallbackView, fallbackMain, selection);
        ExpectPortrait(fallbackMain, crown);
        entry.Type = (int)eChimps.CHIMP_TYPE_MONK;
        Show(fallbackView, fallbackMain, selection);
        Check(countLabel.Text == "1", "An ordinary unit in the same slot retains its count label");
        return "PASS: " + checks + " portrait/view checks (72 Lord atlas cells, other troop skins, redraw, pooling, pagination, camera and fallbacks).";
    }
}
