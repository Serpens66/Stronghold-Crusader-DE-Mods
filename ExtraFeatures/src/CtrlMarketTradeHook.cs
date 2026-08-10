// Feature: Native input and UI support for Ctrl single-unit market trades.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ExtraFeatures
{
    internal sealed unsafe class CtrlMarketTradeHook : IDisposable
    {
        internal const int NormalTradeMode = 0;
        internal const int SingleTradeMode = 2;

        private const int VanillaMarketTradeSoundId = 26;

        private const string MarketValidatorPattern =
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 85 D2 48 8D 2D";
        private const string MarketPacketTailPattern =
            "89 3D ?? ?? ?? ?? 89 35 ?? ?? ?? ?? 8B 84 29 E8 EB 12 00 48 8D 0D ?? ?? ?? ?? 89 05 ?? ?? ?? ?? E8 ?? ?? ?? ??";
        private const string MarketStorageCallPattern =
            "44 8B C1 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B C8 83 F8 FF 0F 85";
        private const string AutoMarketSellStatisticPattern =
            "43 01 94 8A ?? ?? ?? ?? 45 8B C8 43 01 94 14 ?? ?? ?? ?? 44 8B C6";
        private const int MarketValidatorRva = 0xD7080;
        private const int MarketPacketTailRva = 0xD7324;
        private const int MarketStorageCallRva = 0xD7119;
        private const int AutoMarketSellStatisticRva = 0xD0484;

        private delegate int GameActionDelegate(Enums.GameActionCommand command, int structureId, int state, int value2);
        private delegate void NoesisGuiUpdateDelegate(FatControler self);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MarketValidatorDelegate(int selling, int tradeMode, int goodValue);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetAvailableGoodStorageDelegate(
            NativePointer<GameBuildingManager> buildingManager,
            int playerId,
            eGoods good);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SendActionPacketDelegate(IntPtr packetContext, byte action);

        private static readonly Regex TrailingAmountRegex = new Regex(@"\d+\s*$", RegexOptions.Compiled);

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly ulong imageBase;
        private readonly bool referenceHashMatches;
        private HookTransaction nativeTransaction;
        private HookRef<X64ManagedFunctionDetourAOB<MarketValidatorDelegate>> marketValidatorHook =
            new HookRef<X64ManagedFunctionDetourAOB<MarketValidatorDelegate>>();
        private GetAvailableGoodStorageDelegate getAvailableGoodStorage;
        private SendActionPacketDelegate sendActionPacket;
        private IntPtr marketActionPacketContext;
        private int* marketActionSelling;
        private int* marketActionGood;
        private int* marketActionMode;
        private int* marketSellGoldStatistic;
        private Hook gameActionHook;
        private Hook uiUpdateHook;
        private GameActionDelegate gameActionTrampoline;
        private NoesisGuiUpdateDelegate uiUpdateTrampoline;
        private bool nativeReady;
        private bool disposed;

        public CtrlMarketTradeHook(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.");

            imageBase = unchecked((ulong)libraryHandle.ToInt64());
            this.referenceHashMatches = referenceHashMatches;

            try
            {
                InstallNativeValidator(memory);

                gameActionHook = new Hook(FindGameActionMethod(), (GameActionDelegate)GameActionHook);
                gameActionTrampoline = gameActionHook.GenerateTrampoline<GameActionDelegate>();
                uiUpdateHook = new Hook(FindUiUpdateMethod(), (NoesisGuiUpdateDelegate)NoesisGuiUpdateHook);
                uiUpdateTrampoline = uiUpdateHook.GenerateTrampoline<NoesisGuiUpdateDelegate>();

                nativeReady = true;
                Shared.DebugLogHelper.LogInfo(log, "Extra Features Ctrl single-unit market hooks installed.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            nativeReady = false;
            uiUpdateHook?.Undo();
            uiUpdateHook?.Dispose();
            gameActionHook?.Undo();
            gameActionHook?.Dispose();
            nativeTransaction?.Unload();
            nativeTransaction?.Dispose();
            Shared.DebugLogHelper.LogInfo(log, "Extra Features Ctrl single-unit market hooks disposed.");
        }

        internal void ExecuteSingleMarketTrade(PlayerMarketInteractionEventArgs args)
        {
            if (args == null ||
                args.Phase != EventHookPhase.Pre ||
                args.ShiftModifier != SingleTradeMode ||
                !IsFeatureUsable())
            {
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Single market execution entered: player={args.PlayerId}, selling={args.Selling}, " +
                $"good={(int)args.Good} ({args.Good}), mode={args.ShiftModifier}.");

            if (!CanExecuteSingleMarketTrade(args.PlayerId, args.Selling, args.Good))
            {
                Shared.DebugLogHelper.LogInfo(log, "Single market execution rejected by the execution-time guard.");
                return;
            }

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.TryGetPlayerResourcesById(args.PlayerId, out GamePlayerResources* resources))
            {
                Shared.DebugLogHelper.LogInfo(log, $"Single market execution could not resolve resources for player={args.PlayerId}.");
                return;
            }

            PackedGoodPrice price = playerApi.GetTradeBasePrice(args.Good);
            if (args.Selling)
            {
                int proceeds = price.SellPrice / 5;
                resources->r_TotalGoodsGold += (uint)proceeds;
                // This is the same resources field updated by Vanilla AutoMarket.
                resources->N00004513 += (uint)proceeds;
                marketSellGoldStatistic[args.PlayerId] += proceeds;
                playerApi.RemoveGood(args.PlayerId, args.Good, 1);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Single market sell executed: player={args.PlayerId}, good={(int)args.Good} ({args.Good}), " +
                    $"proceeds={proceeds}, goldAfter={resources->r_TotalGoodsGold}.");
                return;
            }

            int cost = Math.Max(1, price.BuyPrice / 5);
            // AutoMarket deducts gold only after the native storage operation succeeds.
            if (!playerApi.TryAddGood(args.PlayerId, args.Good, 1))
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Single market buy failed in native TryAddGood: player={args.PlayerId}, good={(int)args.Good} ({args.Good}).");
                return;
            }

            resources->r_TotalGoodsGold -= (uint)cost;
            resources->N00004513 -= (uint)cost;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Single market buy executed: player={args.PlayerId}, good={(int)args.Good} ({args.Good}), " +
                $"cost={cost}, goldAfter={resources->r_TotalGoodsGold}.");
        }

        private void InstallNativeValidator(ReadOnlySpan<byte> memory)
        {
            ulong validator = ResolveNativeDependencies(memory);

            nativeTransaction = new HookTransaction(
                memory,
                imageBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);
            nativeTransaction.AddDetour(
                ref marketValidatorHook,
                validator,
                MarketValidatorHook);
            nativeTransaction.Commit();

            if (!marketValidatorHook.Success)
                throw new InvalidOperationException("The Vanilla market validator signature was not found.");
        }

        private ulong ResolveNativeDependencies(ReadOnlySpan<byte> memory)
        {
            ulong validator = FindRequiredPattern(
                memory, MarketValidatorPattern, MarketValidatorRva, "market validator");
            ulong tail = FindRequiredPattern(
                memory, MarketPacketTailPattern, MarketPacketTailRva, "market packet tail");
            EnsureInsideValidator(validator, tail, "market packet tail");
            marketActionSelling = (int*)ResolveRipRelativeTarget(memory, tail, 2, 6, sizeof(int), "market selling field");
            marketActionMode = (int*)ResolveRipRelativeTarget(memory, tail + 6, 2, 6, sizeof(int), "market mode field");
            marketActionPacketContext = (IntPtr)ResolveRipRelativeTarget(memory, tail + 19, 3, 7, 1, "market packet context");
            marketActionGood = (int*)ResolveRipRelativeTarget(memory, tail + 26, 2, 6, sizeof(int), "market good field");
            ulong sender = ResolveRipRelativeTarget(memory, tail + 32, 1, 5, 1, "market packet sender");

            ulong storageCall = FindRequiredPattern(
                memory, MarketStorageCallPattern, MarketStorageCallRva, "market storage call");
            EnsureInsideValidator(validator, storageCall, "market storage call");
            ulong storageFunction = ResolveRipRelativeTarget(
                memory,
                storageCall + 10,
                1,
                5,
                1,
                "market storage function");

            // Resolve all delegates before the validator detour becomes active.
            getAvailableGoodStorage = Marshal.GetDelegateForFunctionPointer<GetAvailableGoodStorageDelegate>(
                (IntPtr)storageFunction);
            sendActionPacket = Marshal.GetDelegateForFunctionPointer<SendActionPacketDelegate>((IntPtr)sender);

            ulong statisticInstruction = FindRequiredPattern(
                memory,
                AutoMarketSellStatisticPattern,
                AutoMarketSellStatisticRva,
                "AutoMarket sell statistic update");
            uint statisticRva = *(uint*)(statisticInstruction + 4);
            marketSellGoldStatistic = (int*)ValidateModuleTarget(
                memory,
                imageBase + statisticRva,
                sizeof(int) * 9,
                "AutoMarket sell statistic array");

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Ctrl market native dependencies resolved by instruction patterns: " +
                $"validatorRva=0x{validator - imageBase:X}, packetTailRva=0x{tail - imageBase:X}, " +
                $"storageFunctionRva=0x{storageFunction - imageBase:X}, senderRva=0x{sender - imageBase:X}, " +
                $"sellStatisticRva=0x{statisticRva:X}.");
            return validator;
        }

        private static void EnsureInsideValidator(ulong validator, ulong instruction, string label)
        {
            if (instruction < validator || instruction - validator >= 0x400)
                throw new InvalidOperationException($"The native {label} does not belong to the resolved market validator.");
        }

        private ulong FindRequiredPattern(
            ReadOnlySpan<byte> memory,
            string pattern,
            int referenceRva,
            string label)
        {
            int rva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                referenceRva,
                referenceHashMatches,
                label,
                log).Rva;
            return imageBase + unchecked((ulong)rva);
        }

        private ulong ResolveRipRelativeTarget(
            ReadOnlySpan<byte> memory,
            ulong instruction,
            int displacementOffset,
            int instructionLength,
            int targetSize,
            string label)
        {
            ValidateModuleTarget(memory, instruction, instructionLength, label + " instruction");
            int displacement = *(int*)(instruction + unchecked((ulong)displacementOffset));
            ulong target = unchecked((ulong)((long)instruction + instructionLength + displacement));
            return ValidateModuleTarget(memory, target, targetSize, label);
        }

        private ulong ValidateModuleTarget(ReadOnlySpan<byte> memory, ulong target, int targetSize, string label)
        {
            ulong moduleEnd = imageBase + unchecked((ulong)memory.Length);
            if (target < imageBase || targetSize <= 0 || target > moduleEnd - unchecked((ulong)targetSize))
                throw new InvalidOperationException($"The resolved native {label} lies outside CrusaderDE.dll.");

            return target;
        }

        private static MethodInfo FindGameActionMethod()
        {
            MethodInfo method = typeof(EngineInterface).GetMethod(
                "GameAction",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                null);
            return method ?? throw new MissingMethodException(typeof(EngineInterface).FullName, "GameAction(GameActionCommand,int,int,int)");
        }

        private static MethodInfo FindUiUpdateMethod()
        {
            MethodInfo method = typeof(FatControler).GetMethod(
                "NoesisGUIUpdateChecksInGame",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            return method ?? throw new MissingMethodException(typeof(FatControler).FullName, "NoesisGUIUpdateChecksInGame");
        }

        private int GameActionHook(Enums.GameActionCommand command, int structureId, int state, int value2)
        {
            if (!IsFeatureUsable() || !IsMarketCommand(command))
                return gameActionTrampoline(command, structureId, state, value2);

            KeyManager keys = KeyManager.instance;
            if (keys == null || !keys.isCtrlDown())
                return gameActionTrampoline(command, structureId, state, value2);

            // Ctrl+Shift explicitly restores the normal five-unit mode.
            int tradeMode = keys.isShiftDown() ? NormalTradeMode : SingleTradeMode;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Ctrl market GameAction entering: command={command}, originalMode={structureId}, " +
                $"mappedMode={tradeMode}, good={state}, value2={value2}, shift={keys.isShiftDown()}.");
            int result = gameActionTrampoline(command, tradeMode, state, value2);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Ctrl market GameAction returned: command={command}, mappedMode={tradeMode}, good={state}, result={result}.");
            return result;
        }

        private void MarketValidatorHook(int selling, int tradeMode, int goodValue)
        {
            if (tradeMode != SingleTradeMode)
            {
                marketValidatorHook.Value.Hook.Trampoline(selling, tradeMode, goodValue);
                return;
            }

            if (!IsFeatureUsable())
                return;

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            int playerId = playerApi.GetLocalPlayerId();
            eGoods good = (eGoods)goodValue;
            bool validPlayer = playerApi.IsPlayerIdValid(playerId);
            bool validGood = goodValue >= 0 && goodValue < (int)eGoods.Count;
            int stock = validPlayer && validGood ? playerApi.GetGoodAmount(playerId, good) : -1;
            uint gold = validPlayer ? playerApi.GetPlayerGold(playerId) : 0;
            int storage = validPlayer && validGood ? GetAvailableGoodStorage(playerId, good) : -1;
            PackedGoodPrice price = validGood ? playerApi.GetTradeBasePrice(good) : default(PackedGoodPrice);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Single market validator entered: selling={selling}, mode={tradeMode}, goodValue={goodValue}, " +
                $"player={playerId}, validPlayer={validPlayer}, good={goodValue} ({good}), validGood={validGood}, " +
                $"stock={stock}, gold={gold}, storage={storage}, buyPrice={price.BuyPrice}, sellPrice={price.SellPrice}.");

            if (!CanExecuteSingleMarketTrade(playerId, selling == 1, good))
            {
                // A trade that fails for one unit must also fail for Vanilla's five units.
                // Let Vanilla produce its exact error code and matching Space_Warning speech.
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Single market validator delegated the rejected action to Vanilla's normal-mode error path.");
                marketValidatorHook.Value.Hook.Trampoline(selling, NormalTradeMode, goodValue);
                return;
            }

            // Keep Vanilla's existing synchronized market packet and only reserve payload mode 2.
            *marketActionSelling = selling;
            *marketActionGood = goodValue;
            *marketActionMode = tradeMode;
            sendActionPacket(marketActionPacketContext, 0x26);
            PlaySuccessfulMarketTradeSound();
            Shared.DebugLogHelper.LogInfo(log, "Single market validator submitted Vanilla packet 0x26.");
        }

        private void NoesisGuiUpdateHook(FatControler self)
        {
            uiUpdateTrampoline(self);

            try
            {
                UpdateCtrlTradeUi();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features Ctrl market UI update failed: {ex}");
            }
        }

        private void UpdateCtrlTradeUi()
        {
            KeyManager keys = KeyManager.instance;
            if (!IsFeatureUsable()
                || keys == null
                || !keys.isCtrlDown()
                || GameData.Instance?.lastGameState == null
                || GameData.Instance.lastGameState.app_sub_mode != 57)
            {
                return;
            }

            int goodId = GameData.Instance.lastGameState.trading_current_goods;
            if (goodId < 0 || goodId >= (int)eGoods.Count || MainViewModel.Instance?.HUDBuildingPanel == null)
                return;

            eGoods good = (eGoods)goodId;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            int playerId = playerApi.GetLocalPlayerId();
            PackedGoodPrice price = playerApi.GetTradeBasePrice(good);
            int amount = keys.isShiftDown() ? 5 : 1;
            int buyCost = amount == 1 ? Math.Max(1, price.BuyPrice / 5) : price.BuyPrice;
            int sellProceeds = amount == 1 ? price.SellPrice / 5 : price.SellPrice;

            // Reuse Vanilla's localized labels; Ctrl+Shift must undo Shift's displayed 25 too.
            MainViewModel.Instance.BuyText = ReplaceTrailingAmount(MainViewModel.Instance.BuyText, amount);
            MainViewModel.Instance.SellText = ReplaceTrailingAmount(MainViewModel.Instance.SellText, amount);
            MainViewModel.Instance.BuyPriceText = buyCost.ToString();
            MainViewModel.Instance.SellPriceText = sellProceeds.ToString();

            if (MainViewModel.Instance.HUDBuildingPanel.RefTradeBuyButton != null)
            {
                // A failed Ctrl click must reach Vanilla's validator so its normal warning can play.
                MainViewModel.Instance.HUDBuildingPanel.RefTradeBuyButton.IsEnabled = amount == 1
                    || (playerApi.GetPlayerGold(playerId) >= (uint)buyCost
                        && GetAvailableGoodStorage(playerId, good) >= amount);
            }

            if (MainViewModel.Instance.HUDBuildingPanel.RefTradeSellButton != null)
            {
                MainViewModel.Instance.HUDBuildingPanel.RefTradeSellButton.IsEnabled = amount == 1
                    || playerApi.GetGoodAmount(playerId, good) >= amount;
            }
        }

        private void PlaySuccessfulMarketTradeSound()
        {
            // Vanilla's native Buy/Sell validator queues SFX 26; its managed enum name is a misleading legacy label.
            GameSoundManagerAPI.Instance.PlayUnitySoundEx(VanillaMarketTradeSoundId);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Queued Vanilla successful market trade sound ID {VanillaMarketTradeSoundId}.");
        }

        private bool CanExecuteSingleMarketTrade(int playerId, bool selling, eGoods good)
        {
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || (int)good < 0 || (int)good >= (int)eGoods.Count)
                return false;

            if (selling)
                return playerApi.GetGoodAmount(playerId, good) >= 1;

            PackedGoodPrice price = playerApi.GetTradeBasePrice(good);
            int cost = Math.Max(1, price.BuyPrice / 5);
            return playerApi.GetPlayerGold(playerId) >= (uint)cost
                && GetAvailableGoodStorage(playerId, good) >= 1;
        }

        private int GetAvailableGoodStorage(int playerId, eGoods good)
        {
            if (getAvailableGoodStorage == null)
                return 0;

            return getAvailableGoodStorage(
                GameBuildingManagerAPI.Instance.GetBuildingManager(),
                playerId,
                good);
        }

        private bool IsFeatureUsable()
        {
            return nativeReady
                && settings.EnableMod
                && settings.EnableCtrlSingleMarketTrade;
        }

        private static bool IsMarketCommand(Enums.GameActionCommand command)
        {
            return command == Enums.GameActionCommand.BuyGoods
                || command == Enums.GameActionCommand.SellGoods;
        }

        private static string ReplaceTrailingAmount(string text, int amount)
        {
            string replacement = amount.ToString();
            if (string.IsNullOrEmpty(text))
                return replacement;

            return TrailingAmountRegex.IsMatch(text)
                ? TrailingAmountRegex.Replace(text, replacement)
                : text + " " + replacement;
        }
    }
}
