// Feature: Keep AI market decisions and transactions on Vanilla prices when configured.
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;

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
        private readonly DetourHandle<MarketPriceDelegate> buyPriceHook =
            new DetourHandle<MarketPriceDelegate>();
        private readonly DetourHandle<MarketPriceDelegate> sellPriceHook =
            new DetourHandle<MarketPriceDelegate>();
        private int buyCallbackFailureLogged;
        private int sellCallbackFailureLogged;
        private bool disposed;

        public AIMarketVanillaPriceHook(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            IntPtr libraryHandle,
            ScanRegion region,
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
                pendingTransaction = ExtraFeaturesHookInfrastructure.CreateOwnedTransaction(region);
                pendingTransaction.AddDetour(
                    buyPriceHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)buyResolution.Rva)),
                    GetBuyPrice);
                pendingTransaction.AddDetour(
                    sellPriceHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)sellResolution.Rva)),
                    GetSellPrice);
                CommitResult commitResult = pendingTransaction.Commit();

                if (!commitResult.IsCompleteSuccess || !buyPriceHook.Success || !sellPriceHook.Success)
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
            transaction?.Dispose();
            transaction = null;
        }

        private int GetBuyPrice(IntPtr playerManager, int playerId, int good, int amount)
        {
            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter(
                    "AiMarketBuyPrice",
                    playerId,
                    good,
                    amount,
                    playerManager.ToInt64()))
            {
            try
            {
                if (ShouldUseVanillaPrice(playerManager, playerId, good))
                {
                    PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)good);
                    int result = AIMarketVanillaPricePolicy.CalculateTradeTotal(price.BuyPrice, amount);
                    diagnostic.Complete(1);
                    return result;
                }
            }
            catch (Exception ex)
            {
                TryLogCallbackFailureOnce(ref buyCallbackFailureLogged, "buy", ex);
            }

            int vanillaResult = buyPriceHook.Original(playerManager, playerId, good, amount);
            diagnostic.Complete(0);
            return vanillaResult;
            }
        }

        private int GetSellPrice(IntPtr playerManager, int playerId, int good, int amount)
        {
            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter(
                    "AiMarketSellPrice",
                    playerId,
                    good,
                    amount,
                    playerManager.ToInt64()))
            {
            try
            {
                if (ShouldUseVanillaPrice(playerManager, playerId, good))
                {
                    PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)good);
                    int result = AIMarketVanillaPricePolicy.CalculateTradeTotal(price.SellPrice, amount);
                    diagnostic.Complete(1);
                    return result;
                }
            }
            catch (Exception ex)
            {
                TryLogCallbackFailureOnce(ref sellCallbackFailureLogged, "sell", ex);
            }

            int vanillaResult = sellPriceHook.Original(playerManager, playerId, good, amount);
            diagnostic.Complete(0);
            return vanillaResult;
            }
        }

        private bool ShouldUseVanillaPrice(IntPtr playerManager, int playerId, int good)
        {
            bool validPlayer = playerManager != IntPtr.Zero && playerId >= 1 && playerId <= 8;
            bool validGood = good >= 0 && good < (int)eGoods.Count;
            bool modEnabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
            bool marketPricesAlsoForAI = settings.MarketPricesAlsoForAI;
            if (!modEnabled || !validPlayer || !validGood)
                return false;

            bool isAIPlayer = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            return AIMarketVanillaPricePolicy.ShouldUseVanillaPrice(
                modEnabled,
                marketPricesAlsoForAI,
                validPlayer,
                validGood,
                isAIPlayer);
        }

        private void TryLogCallbackFailureOnce(ref int alreadyLogged, string direction, Exception failure)
        {
            Shared.CrashBreadcrumbDiagnostics.Record(
                "AiMarketCallbackFailure",
                direction == "buy" ? 1 : 2,
                outcome: -1);
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
