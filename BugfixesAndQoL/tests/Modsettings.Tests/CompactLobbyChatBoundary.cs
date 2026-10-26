// Only game/Noesis/logging boundaries are doubled. The formatter, ownership,
// transaction and IL transform are compiled directly from production sources.
// Native Noesis measurement, text wrapping and painting still require game acceptance.
using System;
using System.Collections.Generic;
namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace Shared
{
    internal static class DebugLogHelper
    {
        internal static bool FailLogging;
        internal static void LogInfo(object log, string text) { }
        internal static void LogErrorLimited(object log, string key, string text)
        { if (FailLogging) throw new InvalidOperationException("Injected logging failure"); }
    }
}
namespace BugfixesAndQoL
{
    internal sealed class BugfixesAndQoLViewModel
    {
        public bool EnableClientFeatures { get; set; } = true;
        public bool EnableCompactLobbyChat { get; set; } = true;
    }
}
namespace Noesis
{
    public class Inline { public float FontSize { get; set; } }
    public sealed class Run : Inline { public string Text { get; set; } public Run(string text) { Text = text; } }
    public sealed class LineBreak : Inline { }
    public sealed class InlineUIContainer : Inline { public object Child { get; set; } }
    public sealed class Image { public float Width { get; set; } public float Height { get; set; } }
    public enum TextWrapping { NoWrap, Wrap, WrapWithOverflow }
    public struct Thickness
    {
        private readonly float a,b,c,d;
        public Thickness(float a,float b,float c,float d) { this.a=a;this.b=b;this.c=c;this.d=d; }
        public override bool Equals(object value) => value is Thickness t && a==t.a && b==t.b && c==t.c && d==t.d;
        public override int GetHashCode() => a.GetHashCode();
        public static bool operator ==(Thickness a,Thickness b) => a.Equals(b);
        public static bool operator !=(Thickness a,Thickness b) => !a.Equals(b);
    }
    public sealed class InlineList : List<Inline>
    {
        public bool FailNextAdd;
        public new void Add(Inline item)
        {
            if (FailNextAdd) { FailNextAdd=false; throw new InvalidOperationException("Injected external collection failure"); }
            base.Add(item);
        }
    }
    public sealed class TextBlock
    {
        public string Text { get; set; }
        public float Width { get; set; } = float.NaN;
        public float Height { get; set; } = float.NaN;
        public float FontSize { get; set; }
        public TextWrapping TextWrapping { get; set; }
        public Thickness Margin { get; set; }
        public InlineList Inlines { get; } = new InlineList();
    }
}
namespace CrusaderDE
{
    public sealed class FRONT_Multiplayer
    {
        public Noesis.TextBlock Chat;
        public object FindName(string name) => Chat;
    }
    public sealed class FRONT_CoopTrail1 { public static FRONT_CoopTrail1 Instance; public Noesis.TextBlock RefMP_ChatDisplay; }
    public sealed class FRONT_CoopTrail2 { public static FRONT_CoopTrail2 Instance; public Noesis.TextBlock RefMP_ChatDisplay; }
    public sealed class FRONT_CoopTrail3 { public static FRONT_CoopTrail3 Instance; public Noesis.TextBlock RefMP_ChatDisplay; }
    public sealed class FRONT_CoopTrail4 { public static FRONT_CoopTrail4 Instance; public Noesis.TextBlock RefMP_ChatDisplay; }
    public sealed class MainViewModel
    {
        public static bool viewModelLoaded;
        public static MainViewModel Instance;
        public FRONT_Multiplayer FRONTMultiplayer;
    }
}
