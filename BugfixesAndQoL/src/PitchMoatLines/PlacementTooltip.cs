using System;
using System.Reflection;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.PitchMoatLines
{
    internal sealed class PlacementTooltip
    {
        // MainControls.CurrentAction for Vanilla mapper placement.
        private const int MapperPlacementAction = 5;
        private const int PitchMapper = (int)eMappers.MAPPER_PITCH_DITCH;
        private const int PitchStructure = (int)eStructs.STRUCT_PITCH_DITCH;
        private const int PitchSprite = (int)Enums.eUISprites.SPRITE_GOODS_SMALL_PITCH;

        private readonly int unityThread = Environment.CurrentManagedThreadId;
        private readonly AccessTools.FieldRef<HUD_Main, int> hoverStruct = ReadField<int>("HoverStruct");
        private readonly AccessTools.FieldRef<HUD_Main, int> selectedStruct = ReadField<int>("SelectedStruct");
        private readonly AccessTools.FieldRef<HUD_Main, string> selectedString = ReadField<string>("SelectedString");
        private readonly AccessTools.FieldRef<HUD_Main, bool> otherVisible = ReadField<bool>("OtherVisible");
        private readonly AccessTools.FieldRef<HUD_Main, bool> otherPriority = ReadField<bool>("OtherHighestPri");
        private CostText costText;

        private CostText GetCostText(int brush, int vanillaStock, LineQuote quote)
        {
            if (costText == null || !costText.Matches(brush, vanillaStock, quote))
                costText = new CostText(brush, vanillaStock, quote);
            return costText;
        }

        internal static void Register(ManualLogSource log)
        {
            try
            {
                // The APIShared publisher keeps this callback alive after the plugin's startup cleanup.
                var tooltip = new PlacementTooltip();
                if (!PresentationEvents.TryRegister(PresentationOperation.BuildingRollover, BugfixesAndQoLPlugin.PluginGuid,
                    "PitchLinePlacement", null, tooltip.AfterRollover, out string reason, order: int.MaxValue - 1))
                    log.LogWarning(PitchMoatLinesFeature.Stamp("PITCH_LINE_TOOLTIP_UNAVAILABLE: " + reason));
            }
            catch (Exception error)
            {
                log.LogWarning(PitchMoatLinesFeature.Stamp("PITCH_LINE_TOOLTIP_UNAVAILABLE: " + error));
            }
        }

        private static AccessTools.FieldRef<HUD_Main, T> ReadField<T>(string name)
        {
            var field = typeof(HUD_Main).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || !field.IsPrivate || field.FieldType != typeof(T))
                throw new MissingFieldException(typeof(HUD_Main).FullName, name);
            return AccessTools.FieldRefAccess<HUD_Main, T>(field);
        }

        private void AfterRollover(PresentationPostEventArgs args)
        {
            if (!args.OriginalCompleted || Environment.CurrentManagedThreadId != unityThread || !MainViewModel.viewModelLoaded)
                return;

            var preview = PlacementRuntime.GetPreview();
            if (preview == null || !preview.Available || !preview.InGame || preview.Mapper != PitchMapper)
                return;

            var controls = MainControls.instance;
            var director = EditorDirector.instance;
            if (controls == null || controls.CurrentAction != MapperPlacementAction || controls.CurrentSubAction != PitchMapper ||
                director == null || !director.IsActive || director.ActivePlayerID != preview.Player)
                return;

            var hud = args.BuildingHud;
            var main = MainViewModel.Instance;
            if (hud == null || !ReferenceEquals(hud, main.HUDmain) || main.Show_HUD_Troops ||
                main.RolloverBuilding_TooltipVis || hoverStruct(hud) != 0 || !string.IsNullOrEmpty(hud.HoverString) ||
                selectedStruct(hud) != PitchStructure || (otherVisible(hud) && otherPriority(hud)))
                return;

            string selected = selectedString(hud);
            string title = main.RollOverText;
            if (string.IsNullOrEmpty(selected) || title == null || title.Length != selected.Length + 2 ||
                !title.StartsWith("  ", StringComparison.Ordinal) || string.CompareOrdinal(title, 2, selected, 0, selected.Length) != 0)
                return;

            var game = GameData.Instance;
            var state = game?.lastGameState;
            // Match Vanilla's cost-row visibility, including its siege-builder exception.
            if (state == null || (main.IsMapEditorMode && game.game_type != (int)eGameTypeModes.GAMETYPE_SIEGE_THAT_BUILDER) ||
                main.GameSprites == null || main.GameSprites.Count <= PitchSprite || main.GameSprites[PitchSprite] == null)
                return;

            var image = main.GameSprites[PitchSprite];
            bool pitch1 = ReferenceEquals(main.RollOverText_GoodsImage1, image);
            bool pitch2 = ReferenceEquals(main.RollOverText_GoodsImage2, image);
            if (pitch1 && pitch2)
                return;

            var quote = preview.Quote;
            if (pitch1 || pitch2)
            {
                int wood = 0, stone = 0, iron = 0, pitch = 0, gold = 0;
                GameData.getStructureCosts(PitchStructure, ref wood, ref stone, ref iron, ref pitch, ref gold);
                if (pitch <= 0)
                    return;
                int vanillaStock = state.resources[(int)eGoods.STORED_PITCH_RAW];
                var text = GetCostText(pitch, vanillaStock, quote);
                // Only substitute a complete Vanilla slot. Earlier HUD providers can own different output.
                if (pitch1 && main.RollOverText_AmountReq1 == text.Brush && main.RollOverText_AmountGot1 == text.VanillaStock)
                {
                    main.RollOverText_AmountReq1 = text.Cost;
                    main.RollOverText_AmountGot1 = text.Stock;
                }
                else if (pitch2 && main.RollOverText_AmountReq2 == text.Brush && main.RollOverText_AmountGot2 == text.VanillaStockSecond)
                {
                    main.RollOverText_AmountReq2 = text.Cost;
                    main.RollOverText_AmountGot2 = text.StockSecond;
                }
                return;
            }

            // A positive debit can use an empty Vanilla cost slot, with no extra row or displaced good.
            if (quote.TotalCost <= 0 || main.RollOverText_GoodsImage2 != null ||
                main.RollOverText_AmountReq2 != "" || main.RollOverText_AmountGot2 != "  ")
                return;
            var fallbackText = GetCostText(0, 0, quote);
            if (main.RollOverText_GoodsImage1 == null)
            {
                if (main.RollOverText_AmountReq1 != "" || main.RollOverText_AmountGot1 != "")
                    return;
                main.RollOverText_AmountReq1 = fallbackText.Cost;
                main.RollOverText_AmountGot1 = fallbackText.Stock;
                main.RollOverText_GoodsImage1 = image;
            }
            else
            {
                main.RollOverText_AmountReq2 = fallbackText.Cost;
                main.RollOverText_AmountGot2 = fallbackText.StockSecond;
                main.RollOverText_GoodsImage2 = image;
            }
        }

        private sealed class CostText
        {
            private readonly int brush, vanillaStock, cost, stock;
            internal readonly string Brush, VanillaStock, VanillaStockSecond, Cost, Stock, StockSecond;

            internal CostText(int brush, int vanillaStock, LineQuote quote)
            {
                this.brush = brush;
                this.vanillaStock = vanillaStock;
                cost = quote.TotalCost;
                stock = quote.Stock;
                Brush = $"   {brush} ";
                VanillaStock = $"({vanillaStock})";
                VanillaStockSecond = VanillaStock + "  ";
                Cost = $"   {cost} ";
                Stock = stock == vanillaStock ? VanillaStock : $"({stock})";
                StockSecond = Stock + "  ";
            }

            internal bool Matches(int brush, int vanillaStock, LineQuote quote) =>
                this.brush == brush && this.vanillaStock == vanillaStock && cost == quote.TotalCost && stock == quote.Stock;
        }
    }
}
