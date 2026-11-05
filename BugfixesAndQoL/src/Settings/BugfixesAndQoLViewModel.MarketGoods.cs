using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BepInEx.Logging;
using Noesis;
using UnityEngine;

namespace BugfixesAndQoL
{
    // Editor rows and icons for the HD market goods order setting.
    public sealed partial class BugfixesAndQoLViewModel
    {
        internal void InitializeMarketGoodsOrderEditor(ManualLogSource log)
        {
            marketGoodsLog = log;

            // MainViewModel.Instance constructs the game view model on first access. During
            // plugin startup that constructor is not ready yet, so build text-only rows first.
            foreach (int good in MarketGoodsOrderDefinition.CreateHdOrder())
            {
                marketGoodNames[good] = ((Enums.Goods)good).ToString();
                marketGoodIcons[good] = null;
            }

            while (MarketGoodsOrderItems.Count < MarketGoodsOrderDefinition.Count)
                MarketGoodsOrderItems.Add(new MarketGoodOrderItemViewModel(MoveMarketGood));

            RefreshMarketGoodsOrderItems();
            RefreshStatisticsTeamBadgePreviewVisual();
            RefreshMarketGoodsOrderVisuals();
        }

        internal void RefreshMarketGoodsOrderVisuals()
        {
            if (marketGoodsVisualsResolved)
                return;

            // Never touch Instance before the game confirms that its view model is constructed.
            // Its getter is a factory and can throw while the frontend is still initializing.
            if (!MainViewModel.viewModelLoaded)
            {
                LogMarketGoodsVisualsDeferred(
                    mainViewModelReady: false,
                    spriteCount: 0,
                    resolvedIconCount: 0,
                    resolvedNameCount: 0);
                return;
            }

            MainViewModel viewModel;
            try
            {
                viewModel = MainViewModel.Instance;
            }
            catch (Exception ex)
            {
                LogMarketGoodsVisualsDeferred(
                    mainViewModelReady: false,
                    spriteCount: 0,
                    resolvedIconCount: 0,
                    resolvedNameCount: 0,
                    detail: ex.GetType().Name);
                return;
            }

            if (viewModel?.GameSprites == null)
            {
                LogMarketGoodsVisualsDeferred(
                    mainViewModelReady: viewModel != null,
                    spriteCount: 0,
                    resolvedIconCount: 0,
                    resolvedNameCount: 0);
                return;
            }

            int resolvedIconCount = 0;
            int resolvedNameCount = 0;
            foreach (int good in MarketGoodsOrderDefinition.CreateHdOrder())
            {
                if (TryResolveMarketGoodName(good, out string name))
                    resolvedNameCount++;
                marketGoodNames[good] = name;

                if (TryResolveMarketGoodIcon(viewModel, good, out ImageSource icon))
                    resolvedIconCount++;
                marketGoodIcons[good] = icon;
            }

            RefreshMarketGoodsOrderItems();
            marketGoodsVisualsResolved =
                resolvedIconCount == MarketGoodsOrderDefinition.Count &&
                resolvedNameCount == MarketGoodsOrderDefinition.Count;

            int spriteCount = viewModel.GameSprites.Count;
            if (marketGoodsVisualsResolved)
            {
                if (!marketGoodsVisualsResolvedLogged)
                {
                    marketGoodsVisualsResolvedLogged = true;
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    {
                        // Keep captures inside the enabled scope; otherwise the compiler can allocate them before the guard.
                        var diagnosticResolvedIconCount = @resolvedIconCount;
                        var diagnosticResolvedNameCount = @resolvedNameCount;
                        var diagnosticSpriteCount = @spriteCount;
                        Shared.DebugLogHelper.LogDebug(
                            marketGoodsLog,
                            () =>
                                $"Bugfixes and QoL market-goods visuals resolved: icons={diagnosticResolvedIconCount}/{MarketGoodsOrderDefinition.Count}, " +
                                $"localizedNames={diagnosticResolvedNameCount}/{MarketGoodsOrderDefinition.Count}, gameSprites={diagnosticSpriteCount}.");
                    }
                }

                return;
            }

            LogMarketGoodsVisualsDeferred(
                mainViewModelReady: true,
                spriteCount,
                resolvedIconCount,
                resolvedNameCount);
        }

        private void LogMarketGoodsVisualsDeferred(
            bool mainViewModelReady,
            int spriteCount,
            int resolvedIconCount,
            int resolvedNameCount,
            string detail = null)
        {
            if (marketGoodsVisualsDeferredLogged)
                return;

            marketGoodsVisualsDeferredLogged = true;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
            {
                // Keep captures inside the enabled scope; otherwise the compiler can allocate them before the guard.
                var diagnosticMainViewModelReady = @mainViewModelReady;
                var diagnosticSpriteCountValue = @spriteCount;
                var diagnosticResolvedIconCountValue = @resolvedIconCount;
                var diagnosticResolvedNameCountValue = @resolvedNameCount;
                var diagnosticDetail = @detail;
                Shared.DebugLogHelper.LogDebug(
                    marketGoodsLog,
                    () =>
                        $"Bugfixes and QoL deferred market-goods visuals until the game UI resources are ready: " +
                        $"mainViewModelReady={diagnosticMainViewModelReady}, gameSprites={diagnosticSpriteCountValue}, " +
                        $"icons={diagnosticResolvedIconCountValue}/{MarketGoodsOrderDefinition.Count}, " +
                        $"localizedNames={diagnosticResolvedNameCountValue}/{MarketGoodsOrderDefinition.Count}" +
                        (string.IsNullOrEmpty(diagnosticDetail) ? "." : $", detail={diagnosticDetail}."));
            }
        }

        private void MoveMarketGood(int good, int direction)
        {
            if (!CanEditClientSettings || !CanMutateSetting(nameof(MarketGoodsOrder)))
                return;

            MarketGoodsOrder = MarketGoodsOrderDefinition.SwapGoodWithNeighbor(
                marketGoodsOrder.Value,
                good,
                direction);
        }

        private void RestoreHdMarketOrder()
        {
            if (!CanEditClientSettings || !CanMutateSetting(nameof(MarketGoodsOrder)))
                return;

            MarketGoodsOrder = MarketGoodsOrderDefinition.CreateHdOrder();
        }

        private void RefreshMarketGoodsOrderItems()
        {
            if (MarketGoodsOrderItems.Count != MarketGoodsOrderDefinition.Count)
                return;

            int[] order = MarketGoodsOrderDefinition.CloneOrDefault(marketGoodsOrder.Value);
            for (int index = 0; index < order.Length; index++)
            {
                int good = order[index];
                marketGoodNames.TryGetValue(good, out string name);
                marketGoodIcons.TryGetValue(good, out ImageSource icon);
                MarketGoodsOrderItems[index].Update(
                    index,
                    good,
                    string.IsNullOrWhiteSpace(name) ? ((Enums.Goods)good).ToString() : name,
                    icon);
            }
        }

        private static bool TryResolveMarketGoodName(int good, out string name)
        {
            try
            {
                name = Translate.Instance?.lookUpText(Enums.eTextSections.TEXT_GOODS, good);
                if (!string.IsNullOrWhiteSpace(name))
                    return true;
            }
            catch
            {
            }

            name = ((Enums.Goods)good).ToString();
            return false;
        }

        private static bool TryResolveMarketGoodIcon(
            MainViewModel viewModel,
            int good,
            out ImageSource icon)
        {
            icon = null;
            try
            {
                if (viewModel == null || viewModel.GameSprites == null)
                    return false;

                int spriteId = (int)viewModel.goodsSpriteEnumFromGoodsEnum((Enums.Goods)good);
                if (spriteId < 0 || spriteId >= viewModel.GameSprites.Count)
                    return false;

                icon = viewModel.GameSprites[spriteId];
                return (BaseComponent)(object)icon != (BaseComponent)null;
            }
            catch
            {
                icon = null;
                return false;
            }
        }
    }
}
