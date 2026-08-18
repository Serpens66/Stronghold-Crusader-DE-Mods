// Feature: Keep AI market decisions and transactions on Vanilla prices when configured.
using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace ExtraFeatures
{
    internal sealed class AIMarketVanillaPriceHook : IDisposable
    {
        private const int PolyHookMinimumJumpSize = 6;
        private const int ValidatedOverwriteLength = 10;
        private const ulong BuyPriceTableDisplacement = 0x1817B8;
        private const ulong SellPriceTableDisplacement = 0x1817BC;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MarketPriceDelegate(IntPtr playerManager, int playerId, int good, int amount);

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<MarketPriceDelegate>> buyPriceHook =
            new HookRef<X64ManagedFunctionDetourAOB<MarketPriceDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<MarketPriceDelegate>> sellPriceHook =
            new HookRef<X64ManagedFunctionDetourAOB<MarketPriceDelegate>>();
        // BEGIN TEMPORARY AI_MARKET_TEST_LOGGING - remove after the in-game price test succeeds.
        private int buyCallbackConfirmed;
        private int sellCallbackConfirmed;
        private int buyConfiguredPriceCallbackConfirmed;
        private int sellConfiguredPriceCallbackConfirmed;
        // END TEMPORARY AI_MARKET_TEST_LOGGING
        private int buyCallbackFailureLogged;
        private int sellCallbackFailureLogged;
        private bool disposed;

        public AIMarketVanillaPriceHook(
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

            Shared.NativeResolution buyResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AIMarketVanillaPricePolicy.BuyPriceFunctionPattern,
                AIMarketVanillaPricePolicy.BuyPriceFunctionRva,
                referenceHashMatches,
                "AI market buy-price helper",
                log);
            Shared.NativeResolution sellResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AIMarketVanillaPricePolicy.SellPriceFunctionPattern,
                AIMarketVanillaPricePolicy.SellPriceFunctionRva,
                referenceHashMatches,
                "AI market sell-price helper",
                log);

            ulong imageBase = unchecked((ulong)libraryHandle.ToInt64());
            ValidateHookPair(memory, imageBase, buyResolution.Rva, sellResolution.Rva);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features AI market hook spans validated before installation: " +
                $"buy=0x{buyResolution.Rva:X}-0x{buyResolution.Rva + ValidatedOverwriteLength:X} (3+7 bytes), " +
                $"sell=0x{sellResolution.Rva:X}-0x{sellResolution.Rva + ValidatedOverwriteLength:X} (3+7 bytes), " +
                $"nextInstructionLength=5, minimumDetourBytes={PolyHookMinimumJumpSize}, " +
                "ripRelative=false, incomingInteriorTargets=0, overlap=false.");

            HookTransaction pendingTransaction = null;
            try
            {
                pendingTransaction = new HookTransaction(
                    memory,
                    imageBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                pendingTransaction.AddDetour(
                    ref buyPriceHook,
                    imageBase + unchecked((ulong)buyResolution.Rva),
                    GetBuyPrice);
                pendingTransaction.AddDetour(
                    ref sellPriceHook,
                    imageBase + unchecked((ulong)sellResolution.Rva),
                    GetSellPrice);
                pendingTransaction.Commit();

                if (!buyPriceHook.Success || !sellPriceHook.Success)
                    throw new InvalidOperationException("The AI market-price hook transaction did not install both detours.");

                transaction = pendingTransaction;
                pendingTransaction = null;
            }
            catch
            {
                if (pendingTransaction != null)
                {
                    try
                    {
                        pendingTransaction.Unload();
                    }
                    catch
                    {
                    }
                    try
                    {
                        pendingTransaction.Dispose();
                    }
                    catch
                    {
                    }
                }
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features AI Vanilla market-price hooks installed atomically: " +
                $"buyMethod={buyResolution.Method}, buyRva=0x{buyResolution.Rva:X}, buySpan=0x{buyResolution.Rva:X}-0x{buyResolution.Rva + ValidatedOverwriteLength:X}; " +
                $"sellMethod={sellResolution.Method}, sellRva=0x{sellResolution.Rva:X}, sellSpan=0x{sellResolution.Rva:X}-0x{sellResolution.Rva + ValidatedOverwriteLength:X}; " +
                $"minimumDetourBytes={PolyHookMinimumJumpSize}.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private int GetBuyPrice(IntPtr playerManager, int playerId, int good, int amount)
        {
            bool confirmConfiguredAIPrice = false;
            try
            {
                if (ShouldUseVanillaPrice(playerManager, playerId, good, out confirmConfiguredAIPrice))
                {
                    PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)good);
                    int total = AIMarketVanillaPricePolicy.CalculateTradeTotal(price.BuyPrice, amount);
                    // TEMPORARY AI_MARKET_TEST_LOGGING
                    TryConfirmFirstCallback(ref buyCallbackConfirmed, "buy", playerManager, playerId, good, amount, price.BuyPrice, total);
                    return total;
                }
            }
            catch (Exception ex)
            {
                TryLogCallbackFailureOnce(ref buyCallbackFailureLogged, "buy", ex);
            }

            int trampolineResult = buyPriceHook.Value.Hook.Trampoline(playerManager, playerId, good, amount);
            TryConfirmConfiguredPriceCallback(
                ref buyConfiguredPriceCallbackConfirmed,
                confirmConfiguredAIPrice,
                true,
                playerManager,
                playerId,
                good,
                amount,
                trampolineResult);
            return trampolineResult;
        }

        private int GetSellPrice(IntPtr playerManager, int playerId, int good, int amount)
        {
            bool confirmConfiguredAIPrice = false;
            try
            {
                if (ShouldUseVanillaPrice(playerManager, playerId, good, out confirmConfiguredAIPrice))
                {
                    PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)good);
                    int total = AIMarketVanillaPricePolicy.CalculateTradeTotal(price.SellPrice, amount);
                    // TEMPORARY AI_MARKET_TEST_LOGGING
                    TryConfirmFirstCallback(ref sellCallbackConfirmed, "sell", playerManager, playerId, good, amount, price.SellPrice, total);
                    return total;
                }
            }
            catch (Exception ex)
            {
                TryLogCallbackFailureOnce(ref sellCallbackFailureLogged, "sell", ex);
            }

            int trampolineResult = sellPriceHook.Value.Hook.Trampoline(playerManager, playerId, good, amount);
            TryConfirmConfiguredPriceCallback(
                ref sellConfiguredPriceCallbackConfirmed,
                confirmConfiguredAIPrice,
                false,
                playerManager,
                playerId,
                good,
                amount,
                trampolineResult);
            return trampolineResult;
        }

        private bool ShouldUseVanillaPrice(
            IntPtr playerManager,
            int playerId,
            int good,
            out bool confirmConfiguredAIPrice)
        {
            confirmConfiguredAIPrice = false;
            bool validPlayer = playerManager != IntPtr.Zero && playerId >= 1 && playerId <= 8;
            bool validGood = good >= 0 && good < (int)eGoods.Count;
            bool modEnabled = settings.EnableMod;
            bool marketPricesAlsoForAI = settings.MarketPricesAlsoForAI;
            if (!modEnabled || !validPlayer || !validGood)
                return false;

            bool isAIPlayer = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            confirmConfiguredAIPrice = modEnabled && marketPricesAlsoForAI && isAIPlayer;
            return AIMarketVanillaPricePolicy.ShouldUseVanillaPrice(
                modEnabled,
                marketPricesAlsoForAI,
                validPlayer,
                validGood,
                isAIPlayer);
        }

        // BEGIN TEMPORARY AI_MARKET_TEST_LOGGING - remove this method and its call sites after the in-game test succeeds.
        private void TryConfirmFirstCallback(
            ref int alreadyLogged,
            string direction,
            IntPtr playerManager,
            int playerId,
            int good,
            int amount,
            int basePrice,
            int total)
        {
            if (Interlocked.Exchange(ref alreadyLogged, 1) != 0)
                return;

            try
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"[AI_MARKET_TEST_LOG] AI Vanilla-price route confirmed: direction={direction}, " +
                    $"playerManager=0x{unchecked((ulong)playerManager.ToInt64()):X}, player={playerId}, " +
                    $"good={good}, amount={amount}, vanillaBasePrice={basePrice}, total={total}.");
            }
            catch
            {
                // Diagnostics must never change the selected trade price.
            }
        }
        // END TEMPORARY AI_MARKET_TEST_LOGGING

        // BEGIN TEMPORARY AI_MARKET_TEST_LOGGING - remove this method and its call sites after the in-game test succeeds.
        private void TryConfirmConfiguredPriceCallback(
            ref int alreadyLogged,
            bool shouldLog,
            bool buying,
            IntPtr playerManager,
            int playerId,
            int good,
            int amount,
            int trampolineResult)
        {
            if (!shouldLog || Interlocked.Exchange(ref alreadyLogged, 1) != 0)
                return;

            try
            {
                eGoods typedGood = (eGoods)good;
                PackedGoodPrice activePrice = GamePlayerManagerAPI.Instance.GetTradeBasePrice(typedGood);
                PackedGoodPrice defaultPrice = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice(typedGood);
                int activeBasePrice = buying ? activePrice.BuyPrice : activePrice.SellPrice;
                int defaultBasePrice = buying ? defaultPrice.BuyPrice : defaultPrice.SellPrice;
                string direction = buying ? "buy" : "sell";
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"[AI_MARKET_TEST_LOG] AI configured-price trampoline route confirmed: direction={direction}, " +
                    $"playerManager=0x{unchecked((ulong)playerManager.ToInt64()):X}, player={playerId}, " +
                    $"good={good}, amount={amount}, activeBasePrice={activeBasePrice}, " +
                    $"vanillaBasePrice={defaultBasePrice}, trampolineTotal={trampolineResult}.");
            }
            catch (Exception ex)
            {
                try
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"[AI_MARKET_TEST_LOG] Configured-price confirmation logging failed without affecting the trade result: {ex}");
                }
                catch
                {
                }
            }
        }
        // END TEMPORARY AI_MARKET_TEST_LOGGING

        private void TryLogCallbackFailureOnce(ref int alreadyLogged, string direction, Exception failure)
        {
            if (Interlocked.Exchange(ref alreadyLogged, 1) != 0)
                return;

            try
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features AI Vanilla market-price {direction} callback failed; " +
                    $"this call uses the active global price through Vanilla: {failure}");
            }
            catch
            {
                // The trampoline below remains the independent safe fallback.
            }
        }

        private static void ValidateHookPair(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            int buyRva,
            int sellRva)
        {
            ValidateFunction(memory, imageBase, buyRva, BuyPriceTableDisplacement, "buy");
            ValidateFunction(memory, imageBase, sellRva, SellPriceTableDisplacement, "sell");

            int buyEnd = checked(buyRva + ValidatedOverwriteLength);
            int sellEnd = checked(sellRva + ValidatedOverwriteLength);
            if (buyRva < sellEnd && sellRva < buyEnd)
                throw new InvalidOperationException("The AI market-price hook spans overlap.");

            ValidateNoIncomingDirectBranchTargets(memory, buyRva, buyEnd, "buy");
            ValidateNoIncomingDirectBranchTargets(memory, sellRva, sellEnd, "sell");
        }

        private static void ValidateFunction(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            int rva,
            ulong expectedPriceTableDisplacement,
            string direction)
        {
            if (rva < 0 || rva > memory.Length - 15)
                throw new InvalidOperationException($"The AI market {direction} helper is outside the loaded image.");

            byte[] bytes = memory.Slice(rva, Math.Min(64, memory.Length - rva)).ToArray();
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = imageBase + unchecked((ulong)rva);

            decoder.Decode(out Instruction first);
            decoder.Decode(out Instruction second);
            int overwriteLength = checked(first.Length + second.Length);
            if (first.Code == Code.INVALID || second.Code == Code.INVALID ||
                first.Length != 3 || second.Length != 7 ||
                overwriteLength != ValidatedOverwriteLength ||
                overwriteLength < PolyHookMinimumJumpSize)
            {
                throw new InvalidOperationException(
                    $"The AI market {direction} helper has an unsafe detour span: " +
                    $"lengths={first.Length}+{second.Length}, required={PolyHookMinimumJumpSize}.");
            }

            if (first.Mnemonic != Mnemonic.Movsxd ||
                first.Op0Kind != OpKind.Register || first.Op0Register != Register.RAX ||
                first.Op1Kind != OpKind.Register || first.Op1Register != Register.R8D ||
                second.Mnemonic != Mnemonic.Mov ||
                second.Op0Kind != OpKind.Register || second.Op0Register != Register.ECX ||
                second.Op1Kind != OpKind.Memory ||
                second.MemoryBase != Register.RCX || second.MemoryIndex != Register.RAX ||
                second.MemoryIndexScale != 8 ||
                second.MemoryDisplacement64 != expectedPriceTableDisplacement)
            {
                throw new InvalidOperationException(
                    $"The AI market {direction} helper no longer matches the audited ABI/price-table semantics.");
            }

            if (first.IsIPRelativeMemoryOperand || second.IsIPRelativeMemoryOperand ||
                first.FlowControl != FlowControl.Next || second.FlowControl != FlowControl.Next)
            {
                throw new InvalidOperationException(
                    $"The AI market {direction} detour span contains a relative operand or control-flow instruction.");
            }

            decoder.Decode(out Instruction following);
            if (following.Code == Code.INVALID ||
                following.IP != imageBase + unchecked((ulong)(rva + ValidatedOverwriteLength)) ||
                following.Length != 5 || following.Mnemonic != Mnemonic.Mov ||
                following.Op0Kind != OpKind.Register || following.Op0Register != Register.EAX ||
                following.Op1Kind != OpKind.Immediate32 || following.Immediate32 != 0x66666667U)
            {
                throw new InvalidOperationException(
                    $"The instruction following the AI market {direction} detour span changed.");
            }
        }

        private static void ValidateNoIncomingDirectBranchTargets(
            ReadOnlySpan<byte> memory,
            int hookStart,
            int hookEnd,
            string direction)
        {
            foreach (Shared.NativeCodeRange range in Shared.NativePatternResolver.GetExecutableCodeRanges(memory))
            {
                int end = checked(range.Offset + range.Length);
                for (int source = range.Offset; source < end; source++)
                {
                    int instructionLength;
                    int displacement;
                    byte opcode = memory[source];
                    if ((opcode == 0xE8 || opcode == 0xE9) && source <= end - 5)
                    {
                        instructionLength = 5;
                        displacement = Shared.NativePatternResolver.ReadInt32(memory, source + 1);
                    }
                    else if ((opcode == 0xEB || (opcode >= 0x70 && opcode <= 0x7F) ||
                        (opcode >= 0xE0 && opcode <= 0xE3)) && source <= end - 2)
                    {
                        instructionLength = 2;
                        displacement = unchecked((sbyte)memory[source + 1]);
                    }
                    else if (opcode == 0x0F && source <= end - 6 &&
                        memory[source + 1] >= 0x80 && memory[source + 1] <= 0x8F)
                    {
                        instructionLength = 6;
                        displacement = Shared.NativePatternResolver.ReadInt32(memory, source + 2);
                    }
                    else
                    {
                        continue;
                    }

                    long target = (long)source + instructionLength + displacement;
                    bool sourceInsideSpan = source >= hookStart && source < hookEnd;
                    if (!sourceInsideSpan && target > hookStart && target < hookEnd)
                    {
                        throw new InvalidOperationException(
                            $"A direct branch at RVA 0x{source:X} targets the interior of the AI market " +
                            $"{direction} detour span at RVA 0x{target:X}.");
                    }
                }
            }
        }
    }
}
