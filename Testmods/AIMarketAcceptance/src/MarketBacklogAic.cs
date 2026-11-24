using System;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;

namespace AIMarketOverbuyDiagnostic
{
    // Scenario-owned profile edits, called only inside AutomationRun.InSimulation.
    // Native profile identity and the SDK array base are proved equal before writing.
    internal static unsafe class MarketBacklogAic
    {
        internal static Action<string> Reporter;

        private const int RecordSize = 0x5E4;
        private const int NativeProfileRootRva = 0x404C950;
        private static readonly Owned[] owned = new Owned[GamePlayerManagerAPI.MAX_PLAYERS + 1];
        private static int count;
        private static long module;

        static MarketBacklogAic()
        {
            // Public replayable event; do not consume the Extender's internal handle.
            CrusaderLibrary.Instance.LibraryLoaded += context => module = context.ModuleHandle.ToInt64();
        }

        private struct Owned
        {
            internal int Index, Bread, Cheese, Apples, Amount, ExpectedAmount, WeaponBuy, WeaponSell, TradeMinimum;
            internal long Address;
        }

        internal static void Configure(int player)
        {
            if (!Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                throw new InvalidOperationException("Backlog AIC requires the audited native image.");
            ValidateLayout();
            if (!CanTestEquipmentBuyer(player))
                throw new InvalidOperationException("Backlog equipment scenario requires a custom profile with native weapon purchasing enabled.");
            var players = GamePlayerManagerAPI.Instance;
            if (!players.IsPlayerIdValid(player) || !players.IsAIPlayer(player) ||
                !players.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null)
                throw new InvalidOperationException("Backlog AIC player unavailable.");
            long nativeId = resources->r_AILordMinusOne;
            if (nativeId <= 0) throw new InvalidOperationException("Backlog AIC native profile identity unavailable.");
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            long sdkBase = aics.GetArrayAddress().ToInt64();
            if (module == 0) throw new InvalidOperationException("Audited native module is unavailable.");
            long target = checked(module + NativeProfileRootRva + (nativeId - 1) * RecordSize);
            long delta = target - sdkBase;
            if (sdkBase == 0 || delta < 0 || delta % RecordSize != 0 || delta / RecordSize >= aics.Length)
                throw new InvalidOperationException("Native profile does not map to the public SDK AIC array.");
            int index = checked((int)(delta / RecordSize));
            if (index <= 0 || !Enum.IsDefined(typeof(Enums.AILords), index) || sdkBase + (long)index * RecordSize != target)
                throw new InvalidOperationException("Backlog AIC SDK profile index is not writable.");
            for (int i = 0; i < count; i++)
                if (owned[i].Index == index)
                {
                    if (owned[i].Address != target) throw new InvalidOperationException("Backlog AIC profile moved.");
                    return; // Multiple players can share the same process-wide profile.
                }
            if (count == owned.Length) throw new InvalidOperationException("Backlog AIC ownership capacity exceeded.");
            InternalAIC current = aics.GetValue(index);
            var state = new Owned { Index = index, Address = target, Bread = current.buy_bread_at,
                Cheese = current.buy_cheese_at, Apples = current.buy_apples_at, Amount = current.buy_food_amount, ExpectedAmount = 1,
                WeaponBuy = current.buy_weapons, WeaponSell = current.trade_weapons_at, TradeMinimum = current.trade_minimum };
            InternalAIC updated = current;
            updated.buy_bread_at = updated.buy_cheese_at = updated.buy_apples_at = updated.buy_food_amount = 1;
            updated.buy_weapons = 5;
            updated.trade_weapons_at = 5;
            updated.trade_minimum = 2;
            // Own the seven fields before calling the wrapper, so a failed readback can
            // still be cooperatively restored by the scenario's finally block.
            owned[count++] = state;
            WriteVerified(index, target, updated);
            Reporter?.Invoke("MARKET_BACKLOG_AIC configured player=" + player + " lineupLord=" + (int)players.GetAILord(player) +
                " nativeId=" + nativeId + " sdkIndex=" + index + " nativeSdkAddressEqual=True" +
                " foodBefore=" + state.Bread + "/" + state.Cheese + "/" + state.Apples + "/" + state.Amount +
                " foodAfter=1/1/1/1 weaponBefore=" + state.WeaponBuy + "/" + state.WeaponSell + "/" + state.TradeMinimum +
                " weaponAfterBuySellMinimum=5/5/2 fullRecordReadback=True profileWide=True pendingWrites=0");
        }

        // Selection is based on the unmodified native profile, before Configure.
        internal static bool CanTestEquipmentBuyer(int player)
        {
            ValidateLayout();
            var players = GamePlayerManagerAPI.Instance;
            if (module == 0 || !players.IsPlayerIdValid(player) || !players.IsAIPlayer(player) ||
                !players.TryGetPlayerResourcesById(player, out GamePlayerResources* r) || r == null || r->r_AILordMinusOne == 0)
                return false;
            Enums.AILords lord = players.GetAILord(player);
            if (lord < Enums.AILords.SK_X1 || lord >= Enums.AILords.SK_TEMP) return false;
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            long sdkBase = aics.GetArrayAddress().ToInt64();
            long target = checked(module + NativeProfileRootRva + ((long)r->r_AILordMinusOne - 1) * RecordSize);
            long delta = target - sdkBase;
            if (sdkBase == 0 || delta < 0 || delta % RecordSize != 0 || delta / RecordSize >= aics.Length) return false;
            int index = checked((int)(delta / RecordSize));
            return index > 0 && Enum.IsDefined(typeof(Enums.AILords), index) &&
                sdkBase + (long)index * RecordSize == target && aics.GetValue(index).buy_weapons > 0;
        }

        // Numeric eligibility only: safe to repeat inside the simulation producer.
        internal static bool ReadEligibility(int player)
        {
            var players = GamePlayerManagerAPI.Instance;
            if (module == 0 || !players.IsPlayerIdValid(player) || !players.IsAIPlayer(player) ||
                !players.TryGetPlayerResourcesById(player, out GamePlayerResources* r) || r == null || r->r_AILordMinusOne == 0)
                return false;
            long target = module + NativeProfileRootRva + ((long)r->r_AILordMinusOne - 1) * RecordSize;
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            for (int i = 0; i < count; i++)
            {
                Owned state = owned[i];
                if (state.Address != target) continue;
                if (state.Index <= 0 || state.Index >= aics.Length ||
                    aics.GetArrayAddress().ToInt64() + (long)state.Index * RecordSize != target) return false;
                InternalAIC current = aics.GetValue(state.Index);
                return r->N0000535B != 0 && (long)r->r_CurrentPopularity >= current.vlow_popularity && r->r_AILastStandPhase == 0 &&
                    current.buy_bread_at == 1 && current.buy_cheese_at == 1 && current.buy_apples_at == 1 &&
                    current.buy_food_amount == state.ExpectedAmount && current.buy_weapons == 5 &&
                    current.trade_weapons_at == 5 && current.trade_minimum == 2;
            }
            return false;
        }

        internal static void ValidateEligibility(int player)
        {
            if (!ReadEligibility(player))
                throw new InvalidOperationException("Backlog AIC ownership or normal purchase gates are not eligible.");
        }

        internal static void SetBatchAmount(int amount)
        {
            if (amount != 1 && amount != 8) throw new ArgumentOutOfRangeException(nameof(amount));
            if (count == 0) throw new InvalidOperationException("No owned backlog AIC profiles.");
            ValidateLayout();
            for (int i = 0; i < count; i++)
            {
                Owned state = owned[i];
                var aics = GameAIManagerAPI.Instance.GetAICArray();
                if (state.Index <= 0 || state.Index >= aics.Length ||
                    aics.GetArrayAddress().ToInt64() + (long)state.Index * RecordSize != state.Address)
                    throw new InvalidOperationException("Backlog AIC batch target moved.");
                InternalAIC current = aics.GetValue(state.Index);
                if (current.buy_food_amount != state.ExpectedAmount || current.buy_bread_at != 1 ||
                    current.buy_cheese_at != 1 || current.buy_apples_at != 1 || current.buy_weapons != 5 ||
                    current.trade_weapons_at != 5 || current.trade_minimum != 2)
                    throw new InvalidOperationException("Foreign change to backlog AIC fields; batch change refused.");
                current.buy_food_amount = amount;
                try { WriteVerified(state.Index, state.Address, current); }
                finally
                {
                    // If the wrapper wrote the amount but byte readback failed on a
                    // different field, cleanup must still recognize our last amount.
                    if (aics.GetValue(state.Index).buy_food_amount == amount)
                    {
                        state.ExpectedAmount = amount;
                        owned[i] = state;
                    }
                }
                Reporter?.Invoke("MARKET_BACKLOG_AIC batch sdkIndex=" + state.Index +
                    " amount=" + amount + " fullRecordReadback=True pendingWrites=0");
            }
        }

        internal static void RestoreAll()
        {
            ValidateLayout();
            // Keep ownership until each verified restore succeeds. An exception leaves
            // remaining entries available for a bounded caller-side cleanup attempt.
            while (count > 0)
            {
                Owned state = owned[count - 1];
                var aics = GameAIManagerAPI.Instance.GetAICArray();
                if (state.Index <= 0 || state.Index >= aics.Length ||
                    aics.GetArrayAddress().ToInt64() + (long)state.Index * RecordSize != state.Address)
                    throw new InvalidOperationException("Backlog AIC array changed before restore.");
                InternalAIC current = aics.GetValue(state.Index);
                int foreign = 0;
                if (current.buy_bread_at == 1) current.buy_bread_at = state.Bread; else foreign++;
                if (current.buy_cheese_at == 1) current.buy_cheese_at = state.Cheese; else foreign++;
                if (current.buy_apples_at == 1) current.buy_apples_at = state.Apples; else foreign++;
                if (current.buy_food_amount == state.ExpectedAmount) current.buy_food_amount = state.Amount; else foreign++;
                if (current.buy_weapons == 5) current.buy_weapons = state.WeaponBuy; else foreign++;
                if (current.trade_weapons_at == 5) current.trade_weapons_at = state.WeaponSell; else foreign++;
                if (current.trade_minimum == 2) current.trade_minimum = state.TradeMinimum; else foreign++;
                WriteVerified(state.Index, state.Address, current);
                count--;
                owned[count] = default;
                Reporter?.Invoke("MARKET_BACKLOG_AIC restored sdkIndex=" + state.Index +
                    " foreignOwnedFieldsPreserved=" + foreign + " otherFieldsPreserved=True");
            }
        }

        private static void WriteVerified(int index, long address, InternalAIC value)
        {
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            if (index <= 0 || index >= aics.Length || aics.GetArrayAddress().ToInt64() + (long)index * RecordSize != address)
                throw new InvalidOperationException("Backlog AIC write target changed.");
            var bytes = new ReadOnlySpan<byte>(&value, RecordSize);
            GameAIManagerAPI.Instance.SetAICFromBytes((Enums.AILords)index, bytes);
            InternalAIC verified = aics.GetValue(index);
            if (!new ReadOnlySpan<byte>(&verified, RecordSize).SequenceEqual(bytes))
                throw new InvalidOperationException("Backlog AIC full-record readback failed.");
        }

        private static void ValidateLayout()
        {
            if (sizeof(InternalAIC) != RecordSize ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.buy_apples_at)).ToInt32() != 0x80 ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.buy_cheese_at)).ToInt32() != 0x84 ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.buy_bread_at)).ToInt32() != 0x88 ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.buy_food_amount)).ToInt32() != 0x94 ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.buy_weapons)).ToInt32() != 0x98 ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.trade_weapons_at)).ToInt32() != 0xB8 ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.trade_minimum)).ToInt32() != 0xC4 ||
                Marshal.OffsetOf(typeof(InternalAIC), nameof(InternalAIC.vlow_popularity)).ToInt32() != 0x18 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_AILordMinusOne)).ToInt32() != 0x2300 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N0000535B)).ToInt32() != 0x3974 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_CurrentPopularity)).ToInt32() != 0x60 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_AILastStandPhase)).ToInt32() != 0x38F0)
                throw new InvalidOperationException("Backlog AIC audited layout changed.");
        }
    }
}
