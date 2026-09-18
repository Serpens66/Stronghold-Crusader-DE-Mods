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

        private CastleRotationOptions(
            IReadOnlyList<string> displayTexts,
            Dictionary<string, int> nativeRotations)
        {
            DisplayTexts = displayTexts;
            this.nativeRotations = nativeRotations;
        }

        public IReadOnlyList<string> DisplayTexts { get; }
        public string DefaultDisplayText => DisplayTexts[0];

        public static CastleRotationOptions Create(
            Func<string, string> vanillaTextResolver)
        {
            if (vanillaTextResolver == null)
                throw new ArgumentNullException(nameof(vanillaTextResolver));

            var displayTexts = new List<string>(Definitions.Length);
            var nativeRotations = new Dictionary<string, int>(
                Definitions.Length,
                StringComparer.Ordinal);
            foreach (Definition definition in Definitions)
            {
                string direction = ResolveDirection(
                    vanillaTextResolver,
                    definition.VanillaTextKey,
                    definition.EnglishDirection);
                string displayText = $"{direction} ({definition.Degrees}°)";
                displayTexts.Add(displayText);
                nativeRotations.Add(displayText, definition.NativeRotation);
            }

            return new CastleRotationOptions(
                new ReadOnlyCollection<string>(displayTexts),
                nativeRotations);
        }

        public int GetNativeRotation(string displayText)
        {
            return displayText != null &&
                nativeRotations.TryGetValue(displayText, out int nativeRotation)
                ? nativeRotation
                : 0;
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
}
