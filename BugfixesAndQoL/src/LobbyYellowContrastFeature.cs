// Feature: Improve the yellow lobby team brush and yellow player shields everywhere.
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class LobbyYellowContrastFeature
    {
        private const string GoldNormalResourceKey =
            "BugfixesAndQoL-LobbyGoldShieldNormal";
        private const string GoldHoverResourceKey =
            "BugfixesAndQoL-LobbyGoldShieldHover";
        private const string GoldSelectedResourceKey =
            "BugfixesAndQoL-LobbyGoldShieldSelected";
        private const string GoldMapResourceKey =
            "BugfixesAndQoL-LobbyGoldShieldMap";

        private static readonly string[] VanillaShieldResourceKeys =
        {
            "UI-Buttons H013",
            "UI-Buttons H014",
            "UI-Buttons H015",
            "UI-Buttons H033",
        };

        private static readonly string[] GoldShieldResourceKeys =
        {
            GoldNormalResourceKey,
            GoldHoverResourceKey,
            GoldSelectedResourceKey,
            GoldMapResourceKey,
        };

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly FieldInfo teamYellowBarColourField;
        private readonly SolidColorBrush baselineTeamYellowBrush;
        private readonly SolidColorBrush goldTeamYellowBrush;

        private ShieldMutation[] shieldMutations;
        private bool resourcesReady;
        private bool faulted;
        private bool failureLogged;

        internal LobbyYellowContrastFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            teamYellowBarColourField = RequireField(
                typeof(FRONT_Multiplayer),
                "teamYellowBarColour",
                BindingFlags.Static | BindingFlags.NonPublic,
                typeof(SolidColorBrush));

            baselineTeamYellowBrush =
                teamYellowBarColourField.GetValue(null) as SolidColorBrush;
            if (baselineTeamYellowBrush == null)
            {
                throw new InvalidOperationException(
                    "Vanilla yellow lobby-team brush is unavailable.");
            }

            goldTeamYellowBrush = new SolidColorBrush(Color.FromArgb(
                LobbyYellowContrastPolicy.TeamBrushAlpha,
                LobbyYellowContrastPolicy.GoldRed,
                LobbyYellowContrastPolicy.GoldGreen,
                LobbyYellowContrastPolicy.GoldBlue));

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL high-contrast yellow presentation initialized.");
        }

        internal void ApplySetting()
        {
            if (faulted || !IsActive)
            {
                RestoreVanillaPresentation();
                return;
            }

            if (!resourcesReady && !TryResolveResources())
                return;

            ApplyGoldPresentation();
        }

        private bool IsActive => LobbyYellowContrastPolicy.IsEnabled(
            settings.EnableMod,
            settings.EnableClientFeatures,
            settings.ImproveYellowLobbyContrast);

        private bool TryResolveResources()
        {
            try
            {
                ResourceDictionary resources = GUI.GetApplicationResources();
                if (resources == null)
                {
                    throw new InvalidOperationException(
                        "Noesis application resources are unavailable.");
                }

                var resolved = new ShieldMutation[VanillaShieldResourceKeys.Length];
                for (int index = 0; index < resolved.Length; index++)
                {
                    CroppedBitmap vanilla = RequireCroppedBitmap(
                        resources,
                        VanillaShieldResourceKeys[index]);
                    CroppedBitmap gold = RequireCroppedBitmap(
                        resources,
                        GoldShieldResourceKeys[index]);
                    if (vanilla.Source == null || gold.Source == null)
                    {
                        throw new InvalidOperationException(
                            $"Shield resource pair {VanillaShieldResourceKeys[index]}/" +
                            $"{GoldShieldResourceKeys[index]} has no bitmap source.");
                    }

                    resolved[index] = new ShieldMutation(
                        VanillaShieldResourceKeys[index],
                        vanilla,
                        gold);
                }

                for (int left = 0; left < resolved.Length; left++)
                {
                    for (int right = left + 1; right < resolved.Length; right++)
                    {
                        if (ReferenceEquals(resolved[left].Target, resolved[right].Target))
                        {
                            throw new InvalidOperationException(
                                "Vanilla yellow shield resources unexpectedly alias each other.");
                        }
                    }
                }

                shieldMutations = resolved;
                resourcesReady = true;
                return true;
            }
            catch (Exception ex)
            {
                DisableFailClosed("resource resolution", ex);
                return false;
            }
        }

        private void ApplyGoldPresentation()
        {
            try
            {
                // Validate every target before the first write. A foreign owner on any
                // target leaves the complete presentation untouched.
                foreach (ShieldMutation mutation in shieldMutations)
                {
                    if (!LobbyYellowContrastPolicy.CanApplyGold(
                        mutation.GetState()))
                    {
                        throw new InvalidOperationException(
                            $"Yellow shield resource '{mutation.ResourceKey}' was changed " +
                            "by another owner; Bugfixes and QoL will not overwrite it.");
                    }
                }

                object currentTeamBrush = teamYellowBarColourField.GetValue(null);
                if (!ReferenceEquals(currentTeamBrush, baselineTeamYellowBrush) &&
                    !ReferenceEquals(currentTeamBrush, goldTeamYellowBrush))
                {
                    throw new InvalidOperationException(
                        "The yellow lobby-team brush was changed by another owner; " +
                        "Bugfixes and QoL will not overwrite it.");
                }

                foreach (ShieldMutation mutation in shieldMutations)
                    mutation.ApplyGold();

                teamYellowBarColourField.SetValue(null, goldTeamYellowBrush);
            }
            catch (Exception ex)
            {
                DisableFailClosed("resource mutation", ex);
            }
        }

        private void DisableFailClosed(string stage, Exception error)
        {
            faulted = true;
            RestoreVanillaPresentation(false);
            if (failureLogged)
                return;

            failureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL high-contrast yellow presentation failed during {stage}; " +
                $"The previously active visuals remain in place: {error}");
        }

        private void RestoreVanillaPresentation(bool reportConflict = true)
        {
            Exception restoreError = null;
            if (resourcesReady && shieldMutations != null)
            {
                foreach (ShieldMutation mutation in shieldMutations)
                {
                    try
                    {
                        LobbyYellowResourceState state = mutation.GetState();
                        if (LobbyYellowContrastPolicy.ShouldRestoreBaseline(state))
                        {
                            mutation.RestoreBaseline();
                        }
                        else if (state == LobbyYellowResourceState.Foreign &&
                            restoreError == null)
                        {
                            restoreError = new InvalidOperationException(
                                $"Yellow shield resource '{mutation.ResourceKey}' was changed " +
                                "by another owner and was left untouched.");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (restoreError == null)
                            restoreError = ex;
                    }
                }
            }

            object currentTeamBrush = teamYellowBarColourField.GetValue(null);
            if (ReferenceEquals(currentTeamBrush, goldTeamYellowBrush))
            {
                try
                {
                    teamYellowBarColourField.SetValue(null, baselineTeamYellowBrush);
                }
                catch (Exception ex)
                {
                    if (restoreError == null)
                        restoreError = ex;
                }
            }
            else if (!ReferenceEquals(currentTeamBrush, baselineTeamYellowBrush) &&
                restoreError == null)
            {
                restoreError = new InvalidOperationException(
                    "The yellow lobby-team brush was changed by another owner and was left untouched.");
            }

            if (restoreError != null)
            {
                faulted = true;
                if (reportConflict && !failureLogged)
                {
                    failureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Bugfixes and QoL high-contrast yellow presentation stopped " +
                        $"fail-closed while restoring its resources: {restoreError}");
                }
            }
        }

        private static CroppedBitmap RequireCroppedBitmap(
            ResourceDictionary resources,
            string key)
        {
            CroppedBitmap bitmap = resources[key] as CroppedBitmap;
            if (bitmap == null)
            {
                throw new InvalidOperationException(
                    $"Required CroppedBitmap resource is unavailable: {key}");
            }

            return bitmap;
        }

        private static FieldInfo RequireField(
            Type owner,
            string name,
            BindingFlags flags,
            Type expectedType)
        {
            FieldInfo field = owner.GetField(name, flags);
            if (field == null || field.FieldType != expectedType)
                throw new MissingFieldException(owner.FullName, name);
            return field;
        }

        private sealed class ShieldMutation
        {
            private readonly BitmapSource baselineSource;
            private readonly Int32Rect baselineSourceRect;
            private readonly BitmapSource goldSource;
            private readonly Int32Rect goldSourceRect;

            internal ShieldMutation(
                string resourceKey,
                CroppedBitmap target,
                CroppedBitmap gold)
            {
                ResourceKey = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
                Target = target ?? throw new ArgumentNullException(nameof(target));
                if (gold == null)
                    throw new ArgumentNullException(nameof(gold));

                baselineSource = target.Source;
                baselineSourceRect = target.SourceRect;
                goldSource = gold.Source;
                goldSourceRect = gold.SourceRect;
            }

            internal string ResourceKey { get; }
            internal CroppedBitmap Target { get; }

            internal LobbyYellowResourceState GetState()
            {
                BitmapSource currentSource = Target.Source;
                Int32Rect currentSourceRect = Target.SourceRect;
                return LobbyYellowContrastPolicy.ClassifyResourceState(
                    ReferenceEquals(currentSource, baselineSource),
                    currentSourceRect.Equals(baselineSourceRect),
                    ReferenceEquals(currentSource, goldSource),
                    currentSourceRect.Equals(goldSourceRect));
            }

            internal void ApplyGold()
            {
                LobbyYellowResourceState state = GetState();
                if (state == LobbyYellowResourceState.Gold)
                    return;
                if (state != LobbyYellowResourceState.Baseline)
                    throw new InvalidOperationException(
                        $"Cannot apply gold to foreign resource '{ResourceKey}'.");

                // The gold rectangle fits both the large Vanilla atlas and the compact
                // custom source. Change the rectangle first so no transient crop is invalid.
                Target.SourceRect = goldSourceRect;
                Target.Source = goldSource;
            }

            internal void RestoreBaseline()
            {
                if (GetState() != LobbyYellowResourceState.Gold)
                    throw new InvalidOperationException(
                        $"Cannot restore unowned resource '{ResourceKey}'.");

                // Restore the large Vanilla source before its atlas-relative rectangle.
                Target.Source = baselineSource;
                Target.SourceRect = baselineSourceRect;
            }
        }
    }
}
