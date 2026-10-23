using System;
using Shared;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UnitCosts
{
    internal sealed class UnitCostOverrides
    {
        private readonly OwnedValueOverrides<eChimps, int> gold;
        private readonly OwnedValueOverrides<eStructs, int> tentGold;
        private readonly OwnedValueOverrides<eChimps, eGoods32>[] materials =
            new OwnedValueOverrides<eChimps, eGoods32>[4];

        internal UnitCostOverrides(Func<eChimps, int> readGold, Action<eChimps, int> writeGold,
            Func<eStructs, int> readTentGold, Action<eStructs, int> writeTentGold,
            Func<eChimps, UnitGoodCosts> readMaterials, Action<eChimps, UnitGoodCosts> writeMaterials)
        {
            gold = new OwnedValueOverrides<eChimps, int>(readGold, writeGold);
            tentGold = new OwnedValueOverrides<eStructs, int>(readTentGold, writeTentGold);
            for (int index = 0; index < materials.Length; index++)
            {
                int slot = index;
                materials[slot] = new OwnedValueOverrides<eChimps, eGoods32>(
                    unit => ReadSlot(readMaterials(unit), slot),
                    (unit, value) => writeMaterials(unit, WithSlot(readMaterials(unit), slot, value)));
            }
        }

        internal void ApplyGold(eChimps unit, int value)
        {
            if (value == -1) gold.Restore(unit);
            else gold.Apply(unit, value);
        }

        internal void ApplyTentGold(eStructs tent, int value)
        {
            if (value == -1) tentGold.Restore(tent);
            else tentGold.Apply(tent, value);
        }

        internal void ApplyMaterials(eChimps unit, bool noWeapons)
        {
            foreach (var slot in materials)
            {
                if (noWeapons) slot.Apply(unit, eGoods32.STORED_NULL);
                else slot.Restore(unit);
            }
        }

        internal void RestoreAll(Action<Exception> report, bool endSession = false)
        {
            gold.RestoreAll(report, endSession);
            tentGold.RestoreAll(report, endSession);
            foreach (var slot in materials) slot.RestoreAll(report, endSession);
        }

        private static eGoods32 ReadSlot(UnitGoodCosts costs, int slot)
        {
            switch (slot)
            {
                case 0: return costs.cost1;
                case 1: return costs.cost2;
                case 2: return costs.cost3;
                default: return costs.cost4;
            }
        }

        private static UnitGoodCosts WithSlot(UnitGoodCosts costs, int slot, eGoods32 value)
        {
            switch (slot)
            {
                case 0: costs.cost1 = value; break;
                case 1: costs.cost2 = value; break;
                case 2: costs.cost3 = value; break;
                default: costs.cost4 = value; break;
            }
            return costs;
        }
    }
}
