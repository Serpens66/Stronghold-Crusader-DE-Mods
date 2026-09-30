using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using Noesis;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnitCosts
{
    // Vanilla checks fixed weapon stocks here even when native recruitment costs have been cleared.
    internal static class RecruitmentWeaponUiIlContract
    {
        private sealed class PatchSite
        {
            internal PatchSite(Instruction instruction, eChimps unitType, bool buttonDisable)
            {
                Instruction = instruction;
                UnitType = unitType;
                ButtonDisable = buttonDisable;
            }

            internal Instruction Instruction { get; }
            internal eChimps UnitType { get; }
            internal bool ButtonDisable { get; }
        }

        private static readonly eChimps[] HoverUnits =
        {
            eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_SPEARMAN, eChimps.CHIMP_TYPE_PIKEMAN,
            eChimps.CHIMP_TYPE_MACEMAN, eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_KNIGHT
        };

        private static readonly eGoods[][] HoverGoods =
        {
            new[] { eGoods.STORED_BOWS },
            new[] { eGoods.STORED_CROSSBOWS, eGoods.STORED_LEATHER_ARMOUR },
            new[] { eGoods.STORED_SPEARS },
            new[] { eGoods.STORED_PIKES, eGoods.STORED_METAL_ARMOUR },
            new[] { eGoods.STORED_MACES, eGoods.STORED_LEATHER_ARMOUR },
            new[] { eGoods.STORED_SWORDS, eGoods.STORED_METAL_ARMOUR },
            new[] { eGoods.STORED_SWORDS, eGoods.STORED_METAL_ARMOUR }
        };

        private static readonly eGoods[] ButtonGoods =
        {
            eGoods.STORED_BOWS, eGoods.STORED_SPEARS, eGoods.STORED_MACES,
            eGoods.STORED_CROSSBOWS, eGoods.STORED_PIKES, eGoods.STORED_SWORDS,
            eGoods.STORED_METAL_ARMOUR, eGoods.STORED_LEATHER_ARMOUR
        };

        private static readonly eChimps[][] ButtonUnits =
        {
            new[] { eChimps.CHIMP_TYPE_ARCHER },
            new[] { eChimps.CHIMP_TYPE_SPEARMAN },
            new[] { eChimps.CHIMP_TYPE_MACEMAN },
            new[] { eChimps.CHIMP_TYPE_XBOWMAN },
            new[] { eChimps.CHIMP_TYPE_PIKEMAN },
            new[] { eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_KNIGHT },
            new[] { eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_KNIGHT, eChimps.CHIMP_TYPE_PIKEMAN },
            new[] { eChimps.CHIMP_TYPE_MACEMAN, eChimps.CHIMP_TYPE_XBOWMAN },
            new[] { eChimps.CHIMP_TYPE_KNIGHT }
        };

        internal static void Validate(MethodInfo method)
        {
            using (var definition = new DynamicMethodDefinition(method))
            using (var context = new ILContext(definition.Definition))
                FindAllSites(context);
        }

        internal static void Apply(ILContext context, Func<eChimps, bool> noWeapons)
        {
            if (noWeapons == null)
                throw new ArgumentNullException(nameof(noWeapons));

            // Validate the complete hover and button paths before editing either path.
            List<PatchSite> sites = FindAllSites(context);
            IList<Instruction> instructions = context.Body.Instructions;
            sites.Sort((left, right) => instructions.IndexOf(right.Instruction).CompareTo(instructions.IndexOf(left.Instruction)));
            foreach (PatchSite site in sites)
            {
                eChimps unitType = site.UnitType;
                int index = instructions.IndexOf(site.Instruction);
                var cursor = new ILCursor(context) { Index = site.ButtonDisable ? index : index + 1 };
                if (site.ButtonDisable)
                {
                    cursor.Remove();
                    cursor.Emit(OpCodes.Dup);
                    cursor.EmitDelegate<Func<ToggleButton, bool>>(button =>
                        noWeapons(unitType) ? button.IsEnabled : false);
                }
                else
                {
                    // Vanilla initializes its three material ceilings to 10000.
                    cursor.EmitDelegate<Func<int, int>>(available =>
                        noWeapons(unitType) ? 10000 : available);
                }
            }
        }

        private static List<PatchSite> FindAllSites(ILContext context)
        {
            var sites = new List<PatchSite>();
            sites.AddRange(FindHoverSites(context.Body.Instructions));
            sites.AddRange(FindButtonSites(context.Body.Instructions));
            if (sites.Count != 26)
                throw new InvalidOperationException($"Expected 26 European material UI sites, found {sites.Count}.");
            return sites;
        }

        private static List<PatchSite> FindHoverSites(IList<Instruction> instructions)
        {
            var matches = new List<List<PatchSite>>();
            for (int index = 5; index < instructions.Count; index++)
            {
                if (instructions[index].OpCode != OpCodes.Switch ||
                    instructions[index - 1].OpCode != OpCodes.Sub ||
                    !IsInt(instructions[index - 2], (int)eChimps.CHIMP_TYPE_ARCHER) ||
                    !IsField(instructions[index - 5], nameof(MainViewModel.lastTroopBuildChimp)))
                    continue;

                Array rawTargets = instructions[index].Operand as Array;
                if (rawTargets == null || rawTargets.Length != HoverUnits.Length)
                    continue;
                var targets = new int[HoverUnits.Length];
                bool valid = true;
                for (int unit = 0; unit < targets.Length; unit++)
                {
                    if (!TryTarget(rawTargets.GetValue(unit), out Instruction target))
                    {
                        valid = false;
                        break;
                    }
                    targets[unit] = instructions.IndexOf(target);
                    if (targets[unit] <= index)
                        valid = false;
                }
                if (!valid)
                    continue;

                var orderedTargets = new List<int>(targets);
                orderedTargets.Sort();
                for (int unit = 1; unit < orderedTargets.Count; unit++)
                    if (orderedTargets[unit] == orderedTargets[unit - 1])
                        valid = false;
                if (!valid)
                    continue;

                int commonEnd = -1;
                for (int scan = orderedTargets[0]; scan < orderedTargets[1]; scan++)
                {
                    if (IsUnconditionalBranch(instructions[scan]) &&
                        TryTarget(instructions[scan].Operand, out Instruction end))
                    {
                        commonEnd = instructions.IndexOf(end);
                        break;
                    }
                }
                if (commonEnd <= orderedTargets[orderedTargets.Count - 1])
                    continue;

                var candidate = new List<PatchSite>();
                for (int unit = 0; unit < targets.Length && valid; unit++)
                {
                    int orderIndex = orderedTargets.IndexOf(targets[unit]);
                    int end = orderIndex + 1 < orderedTargets.Count
                        ? orderedTargets[orderIndex + 1] : commonEnd;
                    var actualGoods = new List<eGoods>();
                    int horses = 0;
                    for (int scan = targets[unit]; scan < end; scan++)
                    {
                        if (instructions[scan].OpCode == OpCodes.Ldelem_I4 &&
                            scan >= 2 && scan + 1 < end &&
                            IsField(instructions[scan - 2], "resources") &&
                            TryInt(instructions[scan - 1], out int good) &&
                            IsStoreLocal(instructions[scan + 1]))
                        {
                            actualGoods.Add((eGoods)good);
                            candidate.Add(new PatchSite(instructions[scan], HoverUnits[unit], false));
                        }
                        if (IsField(instructions[scan], "total_horses_available") &&
                            scan + 1 < end && IsStoreLocal(instructions[scan + 1]))
                        {
                            horses++;
                            candidate.Add(new PatchSite(instructions[scan], HoverUnits[unit], false));
                        }
                    }
                    if (actualGoods.Count != HoverGoods[unit].Length ||
                        horses != (HoverUnits[unit] == eChimps.CHIMP_TYPE_KNIGHT ? 1 : 0))
                        valid = false;
                    for (int goodIndex = 0; valid && goodIndex < actualGoods.Count; goodIndex++)
                        valid = actualGoods[goodIndex] == HoverGoods[unit][goodIndex];
                }
                if (valid && candidate.Count == 13 &&
                    HasHoverCeilingConsumer(instructions, commonEnd))
                    matches.Add(candidate);
            }
            if (matches.Count != 1)
                throw new InvalidOperationException($"Expected one complete Vanilla European hover cost switch, found {matches.Count}.");
            return matches[0];
        }

        private static bool HasHoverCeilingConsumer(IList<Instruction> instructions, int start)
        {
            int end = Math.Min(start + 55, instructions.Count);
            for (int index = start; index < end; index++)
                if (IsField(instructions[index], nameof(MainViewModel.lastTroopsAmountToMakeMax)) &&
                    instructions[index].OpCode == OpCodes.Stfld)
                    return true;
            return false;
        }

        private static List<PatchSite> FindButtonSites(IList<Instruction> instructions)
        {
            var matches = new List<List<PatchSite>>();
            for (int start = 2; start < instructions.Count - 5; start++)
            {
                if (!IsField(instructions[start], "resources") ||
                    !IsInt(instructions[start + 1], (int)ButtonGoods[0]) ||
                    instructions[start + 2].OpCode != OpCodes.Ldelem_I4)
                    continue;

                int readIndex = start;
                var candidate = new List<PatchSite>();
                bool valid = true;
                for (int group = 0; group < ButtonUnits.Length && valid; group++)
                {
                    bool horse = group == ButtonGoods.Length;
                    if (horse)
                        valid = IsField(instructions[readIndex], "total_horses_available");
                    else
                        valid = IsField(instructions[readIndex], "resources") &&
                            IsInt(instructions[readIndex + 1], (int)ButtonGoods[group]) &&
                            instructions[readIndex + 2].OpCode == OpCodes.Ldelem_I4;
                    if (!valid)
                        break;

                    int loadIndex = readIndex + (horse ? 1 : 3);
                    int branchIndex = loadIndex + 1;
                    if (!IsLoadLocal(instructions[loadIndex]) ||
                        !IsGreaterEqualBranch(instructions[branchIndex]) ||
                        !TryTarget(instructions[branchIndex].Operand, out Instruction nextStart))
                    {
                        valid = false;
                        break;
                    }

                    int nextIndex = instructions.IndexOf(nextStart);
                    int expectedEnd = branchIndex + 1 + ButtonUnits[group].Length * 5;
                    if (nextIndex != expectedEnd || nextIndex + 2 >= instructions.Count)
                    {
                        valid = false;
                        break;
                    }
                    for (int unit = 0; unit < ButtonUnits[group].Length && valid; unit++)
                    {
                        int block = branchIndex + 1 + unit * 5;
                        valid = IsCall(instructions[block], "get_Instance", typeof(MainViewModel)) &&
                            IsField(instructions[block + 1], nameof(MainViewModel.HUDBuildingPanel)) &&
                            IsField(instructions[block + 2], GetButtonFieldName(ButtonUnits[group][unit])) &&
                            IsInt(instructions[block + 3], 0) &&
                            IsCall(instructions[block + 4], "set_IsEnabled", typeof(UIElement));
                        if (valid)
                            candidate.Add(new PatchSite(instructions[block + 3], ButtonUnits[group][unit], true));
                    }
                    if (group + 1 < ButtonUnits.Length)
                    {
                        valid = IsCall(instructions[nextIndex], "get_Instance", typeof(GameData)) &&
                            IsCall(instructions[nextIndex + 1], "get_lastGameState", typeof(GameData));
                        readIndex = nextIndex + 2;
                    }
                }
                if (valid && candidate.Count == 13)
                    matches.Add(candidate);
            }
            if (matches.Count != 1)
                throw new InvalidOperationException($"Expected one complete Vanilla European button material block, found {matches.Count}.");
            return matches[0];
        }

        private static string GetButtonFieldName(eChimps unitType)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_ARCHER: return nameof(HUD_Buildings.RefRecruitArcherButton);
                case eChimps.CHIMP_TYPE_XBOWMAN: return nameof(HUD_Buildings.RefRecruitXBowmanButton);
                case eChimps.CHIMP_TYPE_SPEARMAN: return nameof(HUD_Buildings.RefRecruitSpearmanButton);
                case eChimps.CHIMP_TYPE_PIKEMAN: return nameof(HUD_Buildings.RefRecruitPikemanButton);
                case eChimps.CHIMP_TYPE_MACEMAN: return nameof(HUD_Buildings.RefRecruitMacemanButton);
                case eChimps.CHIMP_TYPE_SWORDSMAN: return nameof(HUD_Buildings.RefRecruitSwordsmanButton);
                case eChimps.CHIMP_TYPE_KNIGHT: return nameof(HUD_Buildings.RefRecruitKnightButton);
                default: throw new ArgumentOutOfRangeException(nameof(unitType));
            }
        }

        private static bool IsField(Instruction instruction, string name) =>
            instruction.Operand is FieldReference field && field.Name == name;

        private static bool IsCall(Instruction instruction, string name, Type declaringType) =>
            (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
            instruction.Operand is MethodReference method && method.Name == name &&
            method.DeclaringType.FullName == declaringType.FullName;

        private static bool IsStoreLocal(Instruction instruction) =>
            instruction.OpCode == OpCodes.Stloc || instruction.OpCode == OpCodes.Stloc_S ||
            instruction.OpCode == OpCodes.Stloc_0 || instruction.OpCode == OpCodes.Stloc_1 ||
            instruction.OpCode == OpCodes.Stloc_2 || instruction.OpCode == OpCodes.Stloc_3;

        private static bool IsLoadLocal(Instruction instruction) =>
            instruction.OpCode == OpCodes.Ldloc || instruction.OpCode == OpCodes.Ldloc_S ||
            instruction.OpCode == OpCodes.Ldloc_0 || instruction.OpCode == OpCodes.Ldloc_1 ||
            instruction.OpCode == OpCodes.Ldloc_2 || instruction.OpCode == OpCodes.Ldloc_3;

        private static bool IsGreaterEqualBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Bge || instruction.OpCode == OpCodes.Bge_S;

        private static bool IsUnconditionalBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Br || instruction.OpCode == OpCodes.Br_S;

        private static bool TryTarget(object operand, out Instruction target)
        {
            target = operand is ILLabel label ? label.Target : operand as Instruction;
            return target != null;
        }

        private static bool IsInt(Instruction instruction, int value) =>
            TryInt(instruction, out int actual) && actual == value;

        private static bool TryInt(Instruction instruction, out int value)
        {
            value = 0;
            switch (instruction.OpCode.Code)
            {
                case Code.Ldc_I4_M1: value = -1; return true;
                case Code.Ldc_I4_0: value = 0; return true;
                case Code.Ldc_I4_1: value = 1; return true;
                case Code.Ldc_I4_2: value = 2; return true;
                case Code.Ldc_I4_3: value = 3; return true;
                case Code.Ldc_I4_4: value = 4; return true;
                case Code.Ldc_I4_5: value = 5; return true;
                case Code.Ldc_I4_6: value = 6; return true;
                case Code.Ldc_I4_7: value = 7; return true;
                case Code.Ldc_I4_8: value = 8; return true;
                case Code.Ldc_I4_S: value = (sbyte)instruction.Operand; return true;
                case Code.Ldc_I4: value = (int)instruction.Operand; return true;
                default: return false;
            }
        }
    }
}
