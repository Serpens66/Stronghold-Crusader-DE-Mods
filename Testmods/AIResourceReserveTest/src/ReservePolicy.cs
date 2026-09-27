using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace AIResourceReserveTest
{
    internal enum ReserveGood { Wood, Iron, Ale }

    internal sealed class ReserveOverrides
    {
        internal readonly int? MinWood;
        internal readonly int? MinIron;
        internal readonly int? MinAle;

        internal ReserveOverrides(int? minWood, int? minIron, int? minAle)
        {
            MinWood = minWood;
            MinIron = minIron;
            MinAle = minAle;
        }

        internal int GetTarget(ReserveGood good, int activeConsumers)
        {
            int? explicitTarget = good == ReserveGood.Wood ? MinWood :
                good == ReserveGood.Iron ? MinIron : MinAle;
            return explicitTarget ?? activeConsumers;
        }

        internal static bool TryParse(IReadOnlyDictionary<string, object> data,
            out ReserveOverrides overrides, out string error)
        {
            overrides = null;
            error = null;
            if (data == null)
            {
                error = "The mod namespace is empty.";
                return false;
            }
            if (data.TryGetValue("schemaVersion", out object version) &&
                (!TryNonnegativeInt(version, out int schema) || schema != 1))
            {
                error = "schemaVersion must be 1.";
                return false;
            }
            if (!TryOptional(data, "MinWood", out int? wood, out error) ||
                !TryOptional(data, "MinIron", out int? iron, out error) ||
                !TryOptional(data, "MinAle", out int? ale, out error))
                return false;
            overrides = new ReserveOverrides(wood, iron, ale);
            return true;
        }

        private static bool TryOptional(IReadOnlyDictionary<string, object> data, string key,
            out int? value, out string error)
        {
            value = null;
            error = null;
            if (!data.TryGetValue(key, out object raw)) return true;
            if (!TryNonnegativeInt(raw, out int parsed))
            {
                error = key + " must be a nonnegative 32-bit integer.";
                return false;
            }
            value = parsed;
            return true;
        }

        private static bool TryNonnegativeInt(object raw, out int value)
        {
            if (raw is int integer && integer >= 0) { value = integer; return true; }
            if (raw is long large && large >= 0 && large <= int.MaxValue)
            { value = (int)large; return true; }
            if (raw is ulong unsigned && unsigned <= int.MaxValue)
            { value = (int)unsigned; return true; }
            value = 0;
            return false;
        }
    }

    internal static class ReservePolicy
    {
        internal const int PurchaseLimit = 5;

        internal static int Proposal(int stock, int target, int pending)
        {
            if (stock < 0 || target <= stock || pending != 0) return 0;
            return Math.Min(PurchaseLimit, target - stock);
        }

        internal static eGoods Good(ReserveGood good) => good == ReserveGood.Wood
            ? eGoods.STORED_WOOD_PLANKS
            : good == ReserveGood.Iron ? eGoods.STORED_IRON_INGOTS : eGoods.STORED_FOOD_ALE;

        // Only a metal-producing armourer consumes iron; leather armourers do not.
        internal static bool Consumes(in GameBuilding building, ReserveGood good)
        {
            if (building.r_AliveState != AliveState.IsAlive || building.r_IsSleeping != 0)
                return false;
            switch (good)
            {
                case ReserveGood.Wood:
                    return building.r_BuildingType == eStructs.STRUCT_FLETCHERS_WORKSHOP ||
                        building.r_BuildingType == eStructs.STRUCT_POLETURNERS_WORKSHOP;
                case ReserveGood.Iron:
                    return building.r_BuildingType == eStructs.STRUCT_BLACKSMITHS_WORKSHOP ||
                        (building.r_BuildingType == eStructs.STRUCT_ARMOURERS_WORKSHOP &&
                         building.r_ProducedGoodId == eGoods.STORED_METAL_ARMOUR);
                default:
                    return building.r_BuildingType == eStructs.STRUCT_INN;
            }
        }
    }
}
