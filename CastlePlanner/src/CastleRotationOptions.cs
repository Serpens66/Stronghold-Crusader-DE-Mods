using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CastlePlanner
{
    internal sealed class CastleRotationOptions
    {
        private sealed class Definition
        {
            public Definition(
                string vanillaTextKey,
                string englishDirection,
                int degrees,
                int nativeRotation)
            {
                VanillaTextKey = vanillaTextKey;
                EnglishDirection = englishDirection;
                Degrees = degrees;
                NativeRotation = nativeRotation;
            }

            public string VanillaTextKey { get; }
            public string EnglishDirection { get; }
            public int Degrees { get; }
            public int NativeRotation { get; }
        }

        private static readonly Definition[] Definitions =
        {
            new Definition("TEXT_CUSTOMISATION_017", "South", 0, 0),
            new Definition("TEXT_CUSTOMISATION_016", "East", 90, 2),
            new Definition("TEXT_CUSTOMISATION_015", "North", 180, 4),
            new Definition("TEXT_CUSTOMISATION_018", "West", 270, 6)
        };

        private readonly Dictionary<string, int> nativeRotations;
        private readonly Dictionary<int, string> unchangedDisplayTexts;

        private CastleRotationOptions(
            IReadOnlyList<string> displayTexts,
            Dictionary<string, int> nativeRotations,
            Dictionary<int, string> unchangedDisplayTexts)
        {
            DisplayTexts = displayTexts;
            this.nativeRotations = nativeRotations;
            this.unchangedDisplayTexts = unchangedDisplayTexts;
        }

        public IReadOnlyList<string> DisplayTexts { get; }
        public string DefaultDisplayText => DisplayTexts[0];

        public static CastleRotationOptions Create(
            Func<string, string> vanillaTextResolver,
            string unchangedDisplayText = "Unchanged")
        {
            if (vanillaTextResolver == null)
                throw new ArgumentNullException(nameof(vanillaTextResolver));

            var displayTexts = new List<string>(Definitions.Length + 1) { unchangedDisplayText };
            var nativeRotations = new Dictionary<string, int>(
                Definitions.Length,
                StringComparer.Ordinal);
            var unchangedDisplayTexts = new Dictionary<int, string>(Definitions.Length);
            foreach (Definition definition in Definitions)
            {
                string direction = ResolveDirection(
                    vanillaTextResolver,
                    definition.VanillaTextKey,
                    definition.EnglishDirection);
                string displayText = $"{direction} ({definition.Degrees}°)";
                displayTexts.Add(displayText);
                nativeRotations.Add(displayText, definition.NativeRotation);
                unchangedDisplayTexts.Add(definition.NativeRotation,
                    $"{unchangedDisplayText} ({direction}, {definition.Degrees}°)");
            }

            return new CastleRotationOptions(
                new ReadOnlyCollection<string>(displayTexts),
                nativeRotations,
                unchangedDisplayTexts);
        }

        public string FormatUnchanged(int? capturedRotation) =>
            capturedRotation.HasValue && unchangedDisplayTexts.TryGetValue(capturedRotation.Value, out string text)
                ? text : DefaultDisplayText;

        public string ToDisplayText(string selection, int? capturedRotation, bool allowRotation) =>
            !allowRotation || PreservesKeepRotation(selection)
                ? FormatUnchanged(capturedRotation) : selection;

        // Collection replacement may temporarily clear SelectedItem. Both that
        // value and the decorated Unchanged label retain the stable default choice.
        public string FromDisplayText(string displayText, bool allowRotation) =>
            allowRotation && displayText != null && nativeRotations.ContainsKey(displayText)
                ? displayText : DefaultDisplayText;

        public int GetNativeRotation(string displayText)
        {
            return displayText != null &&
                nativeRotations.TryGetValue(displayText, out int nativeRotation)
                ? nativeRotation
                : 0;
        }

        public bool PreservesKeepRotation(string displayText) =>
            string.Equals(displayText, DefaultDisplayText, StringComparison.Ordinal);

        public static string GetDirection(int degrees, Func<string, string> vanillaTextResolver)
        {
            if (vanillaTextResolver == null)
                throw new ArgumentNullException(nameof(vanillaTextResolver));

            foreach (Definition definition in Definitions)
            {
                if (definition.Degrees == degrees)
                    return ResolveDirection(
                        vanillaTextResolver,
                        definition.VanillaTextKey,
                        definition.EnglishDirection);
            }

            return string.Empty;
        }

        private static string ResolveDirection(
            Func<string, string> vanillaTextResolver,
            string vanillaTextKey,
            string englishDirection)
        {
            try
            {
                string localized = vanillaTextResolver(vanillaTextKey);
                if (!string.IsNullOrWhiteSpace(localized) &&
                    !string.Equals(localized, vanillaTextKey, StringComparison.Ordinal))
                {
                    return localized.Trim();
                }
            }
            catch
            {
                // Translation availability must not prevent castle selection.
            }

            return englishDirection;
        }
    }

    internal static class KeepRotationPolicy
    {
        // Native Keep constructors retain 15 in the sprite-variation field but
        // use the same orientation as 0. Never forward 15 to AIV placement.
        public static bool TryNormalize(int variation, out int rotation)
        {
            rotation = variation == 15 ? 0 : variation;
            return rotation == 0 || rotation == 2 || rotation == 4 || rotation == 6;
        }

        public static void ApplyToNativeArgument(
            bool preserveKeepRotation, int rotation, ref int nativeArgument)
        {
            if (!TryNormalize(rotation, out int normalized) || normalized != rotation)
                throw new InvalidOperationException("Invalid explicit Keep rotation.");
            if (!preserveKeepRotation)
                nativeArgument = rotation;
        }

    }
}
