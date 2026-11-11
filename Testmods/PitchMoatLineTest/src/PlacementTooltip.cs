using System;
using System.Reflection;
using System.Text;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using SHCDESE.Interop;

namespace PitchMoatLineTest
{
    internal sealed class PlacementTooltip
    {
        // MainControls.CurrentAction for Vanilla mapper placement.
        private const int MapperPlacementAction = 5;
        private const int PitchMapper = (int)eMappers.MAPPER_PITCH_DITCH;
        private const int MoatMapper = (int)eMappers.MAPPER_MOAT;
        private const int DugMoatMapper = (int)eMappers.MAPPER_DUGMOAT;

        private readonly int unityThread = Environment.CurrentManagedThreadId;
        private readonly StatusText pitchText = new StatusText();
        private readonly StatusText moatText = new StatusText();
        private AccessTools.FieldRef<HUD_Main, int> hoverStruct, selectedStruct;
        private AccessTools.FieldRef<HUD_Main, string> selectedString;
        private AccessTools.FieldRef<HUD_Main, bool> otherVisible, otherPriority;
        private string titleLanguage, moatTitle, moatBaseline, lastMoatBase, lastMoatStatus, lastMoatText;

        internal static void Register(ManualLogSource log)
        {
            // APIShared owns both process-lived registrations and calls them after Vanilla presentation.
            var tooltip = new PlacementTooltip();
            if (!TooltipPresentation.TryRegisterText(Plugin.Guid, "PitchLinePlacement", TooltipTarget.BuildingCompact,
                TooltipPlacement.AfterMainLine, tooltip.PitchText, out string reason))
                log.LogWarning(Plugin.Stamp("PITCH_LINE_TOOLTIP_UNAVAILABLE: " + reason));

            try
            {
                tooltip.hoverStruct = ReadField<int>("HoverStruct");
                tooltip.selectedStruct = ReadField<int>("SelectedStruct");
                tooltip.selectedString = ReadField<string>("SelectedString");
                tooltip.otherVisible = ReadField<bool>("OtherVisible");
                tooltip.otherPriority = ReadField<bool>("OtherHighestPri");
                if (!PresentationEvents.TryRegister(PresentationOperation.BuildingRollover, Plugin.Guid,
                    "MoatLinePlacement", null, tooltip.AfterRollover, out reason))
                    log.LogWarning(Plugin.Stamp("MOAT_LINE_TOOLTIP_UNAVAILABLE: " + reason));
            }
            catch (Exception error)
            {
                log.LogWarning(Plugin.Stamp("MOAT_LINE_TOOLTIP_UNAVAILABLE: " + error));
            }
        }

        private static AccessTools.FieldRef<HUD_Main, T> ReadField<T>(string name)
        {
            var field = typeof(HUD_Main).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || !field.IsPrivate || field.FieldType != typeof(T))
                throw new MissingFieldException(typeof(HUD_Main).FullName, name);
            return AccessTools.FieldRefAccess<HUD_Main, T>(field);
        }

        private static int SelectedMapper()
        {
            var controls = MainControls.instance;
            return controls != null && controls.CurrentAction == MapperPlacementAction ? controls.CurrentSubAction : 0;
        }

        private string PitchText(TooltipContext context)
        {
            if (!context.IsActive || context.IsSymbolHover || context.BuildingType != (int)eStructs.STRUCT_PITCH_DITCH ||
                SelectedMapper() != PitchMapper || !MainViewModel.viewModelLoaded || MainViewModel.Instance.Show_HUD_Troops)
                return null;

            var snapshot = PlacementRuntime.GetPreview();
            if (snapshot?.Mapper != PitchMapper || (snapshot.InGame && snapshot.Player != context.LocalPlayerId))
                snapshot = null;
            return pitchText.Get(snapshot, PlacementRuntime.GetNotice(PitchMapper), true);
        }

        private void AfterRollover(PresentationPostEventArgs args)
        {
            if (!args.OriginalCompleted || Environment.CurrentManagedThreadId != unityThread || !MainViewModel.viewModelLoaded)
                return;
            int mapper = SelectedMapper();
            if (mapper != MoatMapper && mapper != DugMoatMapper)
                return;

            var snapshot = PlacementRuntime.GetPreview();
            if (snapshot?.Mapper != mapper)
                snapshot = null;
            var notice = PlacementRuntime.GetNotice(mapper);
            if (snapshot == null && notice == PlacementNotice.None)
                return;

            var hud = args.BuildingHud;
            var main = MainViewModel.Instance;
            if (hud == null || !ReferenceEquals(hud, main.HUDmain) || main.Show_HUD_Troops ||
                main.RolloverBuilding_TooltipVis || hoverStruct(hud) != 0 || !string.IsNullOrEmpty(hud.HoverString) ||
                selectedStruct(hud) != 0 || GameData.Instance?.lastGameState == null)
                return;

            // Mapper 106 has only a selected title; the editor's mapper 45 has no title.
            // Match the completed Vanilla rollover, including OtherVisible's empty-text/priority rule.
            string selected = selectedString(hud);
            if (otherVisible(hud) && (string.IsNullOrEmpty(selected) || otherPriority(hud)))
                return;
            string baseline = main.RollOverText;
            string title = MoatTitle();
            if (mapper == MoatMapper)
            {
                if (string.IsNullOrEmpty(title) || !string.Equals(selected, title, StringComparison.Ordinal) ||
                    !string.Equals(baseline, moatBaseline, StringComparison.Ordinal))
                    return;
            }
            else
            {
                if (!string.IsNullOrEmpty(selected) || !string.IsNullOrEmpty(baseline) || string.IsNullOrEmpty(title))
                    return;
                baseline = moatBaseline;
            }

            string status = moatText.Get(snapshot, notice, false);
            if (!string.Equals(lastMoatBase, baseline, StringComparison.Ordinal) || !ReferenceEquals(lastMoatStatus, status))
            {
                lastMoatBase = baseline;
                lastMoatStatus = status;
                lastMoatText = baseline + "\n  " + status.Replace("\n", "\n  ");
            }
            main.RollOverText = lastMoatText;
        }

        private string MoatTitle()
        {
            string language = FatControler.locale;
            if (moatTitle == null || !string.Equals(titleLanguage, language, StringComparison.Ordinal))
            {
                titleLanguage = language;
                moatTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.BHELP_TEXT_MOAT);
                moatBaseline = "  " + moatTitle;
            }
            return moatTitle;
        }

        private sealed class StatusText
        {
            private PreviewSnapshot lastSnapshot;
            private PlacementNotice lastNotice;
            private bool lastGerman;
            private string text;

            internal string Get(PreviewSnapshot snapshot, PlacementNotice notice, bool pitch)
            {
                if (snapshot == null && notice == PlacementNotice.None)
                {
                    lastSnapshot = null;
                    return null;
                }

                bool german = FatControler.locale == Translate.locale_german;
                if (!ReferenceEquals(lastSnapshot, snapshot) || lastNotice != notice || lastGerman != german)
                {
                    lastSnapshot = snapshot;
                    lastNotice = notice;
                    lastGerman = german;
                    text = Format(snapshot, notice, pitch, german);
                }
                return text;
            }

            private static string Format(PreviewSnapshot snapshot, PlacementNotice notice, bool pitch, bool german)
            {
                if (snapshot == null)
                    return notice == PlacementNotice.Busy
                        ? (german ? "Die vorige Linie wird noch gebaut" : "Still placing the previous line")
                        : (german ? "Platzierung vorzeitig beendet" : "Placement stopped early");
                if (!snapshot.Available)
                    return german ? "Bauprüfung nicht verfügbar" : "Placement check unavailable";

                var quote = snapshot.Quote;
                var label = new StringBuilder();
                if (pitch)
                    label.Append(german ? $"Pechbedarf {quote.TotalCost} · Vorrat {quote.Stock}" :
                        $"Pitch cost {quote.TotalCost} · Stock {quote.Stock}");
                else
                    label.Append(german ? "Keine Materialkosten" : "No material cost");
                label.Append('\n').Append(german ? $"{quote.BuildableCount}/{snapshot.Tiles.Length} Felder baubar" :
                    $"{quote.BuildableCount}/{snapshot.Tiles.Length} tiles buildable");
                AppendCount(label, quote.ExistingCount, german ? "vorhanden" : "existing");
                AppendCount(label, quote.BlockedCount, german ? "blockiert" : "blocked");
                AppendCount(label, quote.MaterialCount, german ? "ohne Material" : "short on materials");
                AppendCount(label, quote.CapacityCount, german ? "über dem Baulimit" : "over the limit");
                AppendCount(label, quote.OverlapUnavailableCount, german ? "wegen Überlagerung übersprungen" : "skipped because overlap is unavailable");
                return label.ToString();
            }

            private static void AppendCount(StringBuilder label, int count, string text)
            {
                if (count > 0)
                    label.Append('\n').Append(count).Append(' ').Append(text);
            }
        }
    }
}
