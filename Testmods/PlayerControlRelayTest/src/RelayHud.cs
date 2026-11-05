using System;
using System.Reflection;
using System.Threading;
using CrusaderDE;
using HarmonyLib;

namespace PlayerControlRelayTest
{
    // Shows Vanilla's own troop HUD for a private allied selection. EngineInterface.CopyPlayStateStruct
    // builds the managed copy that GameData, HUD_Troops, KeyManager and FatControler read; this postfix
    // only extends that copy. The simulation and the native selection never see it, so Vanilla's
    // buttons and hotkeys reach RelayRuntime through GameAction. BugfixesAndQoL's foreign troop HUD
    // leaves by itself at app_mode 14, sub-mode 61. The hooks stay installed for the process and do
    // nothing without a published selection.
    internal sealed class RelayHudSelection
    {
        internal RelayHudSelection(RelayUnit[] units, bool patrolArmed) { Units = units; PatrolArmed = patrolArmed; }
        internal RelayUnit[] Units { get; }
        internal bool PatrolArmed { get; }
    }

    internal static class RelayHud
    {
        private const int TroopAppMode = 14, TroopSubMode = 61, AlternateTroopSubMode = 62;
        // 0x19D960 writes 3 to 5 for ordinary fighting units; HUD_Troops shows Attack Here for them.
        private const byte AttackHereVisible = 5;
        private static readonly int[] StanceCounts = new int[RelayValidation.StanceCount];
        private static Harmony harmony;
        private static RelayHudSelection current;
        private static Action<Exception> failure;
        private static Func<bool> armPatrol;

        internal static void Initialize(Action<Exception> onFailure, Func<bool> onPatrolKey)
        {
            if (harmony != null) return;
            MethodInfo copy = typeof(EngineInterface).GetMethod(nameof(EngineInterface.CopyPlayStateStruct),
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo place = typeof(EditorDirector).GetMethod(nameof(EditorDirector.placeBuilding),
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Enums.eMappers) }, null);
            if (copy == null || copy.ReturnType != typeof(EngineInterface.PlayState) || place == null)
                throw new InvalidOperationException("Vanilla troop HUD contract changed.");
            failure = onFailure; armPatrol = onPatrolKey;
            var candidate = new Harmony(PlayerControlRelayPlugin.Guid + ".Hud");
            candidate.Patch(copy, postfix: new HarmonyMethod(typeof(RelayHud), nameof(AfterCopy)));
            candidate.Patch(place, prefix: new HarmonyMethod(typeof(RelayHud), nameof(BeforePlaceBuilding)));
            harmony = candidate;
        }

        internal static void Publish(RelayHudSelection selection) => Volatile.Write(ref current, selection);

        private static void AfterCopy(EngineInterface.PlayState __result)
        {
            var selection = Volatile.Read(ref current);
            if (selection == null || __result == null || __result.app_mode != TroopAppMode) return;
            try { if (Monitor.IsEntered(RelayWorld.EngineGate)) Apply(__result, selection); }
            catch (Exception error) { failure(error); }
        }

        private static void Apply(EngineInterface.PlayState state, RelayHudSelection selection)
        {
            bool own = state.numSelectedChimps > 0 &&
                (state.app_sub_mode == TroopSubMode || state.app_sub_mode == AlternateTroopSubMode);
            int count = state.numSelectedChimps, added = 0, soldiers = 0;
            Array.Clear(StanceCounts, 0, StanceCounts.Length);
            foreach (var unit in selection.Units)
            {
                if (count >= state.selectedChimps.Length || !RelayWorld.TryGetHudUnit(unit, out int type, out int stance)) continue;
                state.selectedChimps[count] = unit.Slot;
                state.selectedChimpTypes[count] = type;
                count++; added++;
                if (!RelayWorld.IsLord(unit.Slot)) soldiers++;
                if (stance >= 0 && stance < StanceCounts.Length) StanceCounts[stance]++;
            }
            if (added == 0) return;
            state.numSelectedChimps = count;
            if (own) return; // Vanilla's own values describe the own units of a mixed selection.
            state.app_sub_mode = TroopSubMode;
            state.lordOnlySelected = (byte)(soldiers == 0 ? 1 : 0);
            state.troops_show_disband = (byte)(soldiers > 0 ? 1 : 0);
            state.troops_show_attack_here_and_type = AttackHereVisible;
            state.troops_show_stance = (byte)RelayValidation.MajorityStance(StanceCounts);
            state.troops_show_patrol = 1;
            state.troops_patrol_mode = (byte)(selection.PatrolArmed ? 1 : 0);
        }

        // Vanilla's patrol hotkey goes straight to the placement cursor, without GameAction.
        private static bool BeforePlaceBuilding(Enums.eMappers buildingType)
        {
            if (buildingType != Enums.eMappers.MAPPER_PATROL || Volatile.Read(ref current) == null) return true;
            try { return !armPatrol(); }
            catch (Exception error) { failure(error); return true; }
        }
    }
}
