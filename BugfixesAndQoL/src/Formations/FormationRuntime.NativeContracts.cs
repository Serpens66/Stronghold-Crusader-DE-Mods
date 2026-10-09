using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private void RequireMainThread(string contract)
        {
            int current = Environment.CurrentManagedThreadId;
            if (current != mainThreadId)
            {
                throw new InvalidOperationException(
                    $"Formation input callback '{contract}' ran on thread {current}; " +
                    $"expected main thread {mainThreadId}.");
            }
        }

        private static FieldInfo RequireEditorField(string name, Type type)
        {
            FieldInfo field = typeof(EditorDirector).GetField(name, InstanceFields);
            if (field == null || field.FieldType != type)
                throw new MissingFieldException(typeof(EditorDirector).FullName, name);
            return field;
        }

        private static void ValidateNativeContracts(ReadOnlySpan<byte> memory)
        {
            if (Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TargetTilePositionX)).ToInt32() !=
                    0xC4 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TargetTilePositionY)).ToInt32() !=
                    0xC6 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AttackMoveToTargetTileX)).ToInt32() !=
                    0x2D8 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AttackMoveToTargetTileY)).ToInt32() !=
                    0x2DA)
            {
                throw new InvalidOperationException(
                    "GameUnit target tile offsets no longer match the audited native layout.");
            }
            ValidateBytes(memory, StandardSelectorRva, StandardSelectorPrefix,
                "standard formation selector");
            ValidateUniqueEntry(memory, StandardSelectorRva, StandardSelectorPrefix,
                "standard formation selector");
            ValidateBytes(memory, AssassinSelectorRva, AssassinSelectorPrefix,
                "Assassin ground formation selector");
            ValidateUniqueEntry(memory, AssassinSelectorRva, AssassinSelectorPrefix,
                "Assassin ground formation selector");
            ValidateBytes(memory, CommonGroupMoveRva, CommonGroupMovePrefix,
                "common group movement path");
            ValidateUniqueEntry(memory, CommonGroupMoveRva, CommonGroupMovePrefix,
                "common group movement path");
            ValidateBytes(memory, UnitMoveTargetRva, UnitMoveTargetPrefix,
                "terminal unit movement target writer");
            ValidateUniqueEntry(memory, UnitMoveTargetRva, UnitMoveTargetPrefix,
                "terminal unit movement target writer");
            ValidateBytes(
                memory,
                GetGroupUnitIdRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x63, 0xC2,
                    0x45, 0x33, 0xC9, 0x48, 0x69, 0xD0, 0x88, 0x06,
                    0x00, 0x00
                },
                "group unit iterator");
        }

        private static void ValidateBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string name)
        {
            if (rva < 0 || expected == null || rva + expected.Length > memory.Length)
                throw new InvalidOperationException($"{name} lies outside the native image.");
            for (int index = 0; index < expected.Length; index++)
            {
                if (memory[rva + index] != expected[index])
                    throw new InvalidOperationException(
                        $"{name} byte mismatch at RVA 0x{rva + index:X}: " +
                        $"expected 0x{expected[index]:X2}, got 0x{memory[rva + index]:X2}.");
            }
        }

        private static void ValidateUniqueEntry(
            ReadOnlySpan<byte> memory,
            int expectedRva,
            byte[] expected,
            string name)
        {
            string[] bytes = new string[expected.Length];
            for (int index = 0; index < expected.Length; index++)
                bytes[index] = expected[index].ToString("X2");
            int resolved = APIShared.Internal.NativePatternResolver.FindUniquePattern(
                memory, string.Join(" ", bytes), name);
            if (resolved != expectedRva)
            {
                throw new InvalidOperationException(
                    $"{name} resolved to RVA 0x{resolved:X}, expected 0x{expectedRva:X}.");
            }
        }
    }
}
