using APIShared;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace APISharedTests
{
    internal static class MarkedSelectionHarmonyTests
    {
        private static int originalCalls;
        private static int[] originalUnderCursor;
        private static int[] originalOnScreen;
        private static Hook extenderHook;
        private static SelectionCall extenderOriginal;
        private delegate void SelectionCall(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished,
            int[] underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps);

        internal static void Run(Action<bool, string> assert)
        {
            Type api = typeof(MarkedUnitSelectionAPI);
            MethodInfo mismatch = api.GetMethod("NeedsPositiveMismatchScan", BindingFlags.NonPublic | BindingFlags.Static);
            bool Scan(int count, int selected, int checkedCount, int revision, int checkedRevision) =>
                (bool)mismatch.Invoke(null, new object[] { count, selected, checkedCount, revision, checkedRevision });
            assert(!Scan(0, 41, -1, 0, -1) && !Scan(-1, 41, -1, 0, -1),
                "Transient zero and negative hover counts never trigger a full scan.");
            assert(Scan(5, 4, -1, 0, -1) && !Scan(5, 4, 5, 0, 0) &&
                Scan(5, 4, 5, 1, 0) && Scan(6, 4, 5, 0, 0),
                "Positive mismatch scans once per count and selection revision.");
            assert(!Scan(5, 5, 4, 1, 0), "Matching positive count does not trigger a scan.");
            MethodInfo postfix = api.GetMethod("OnTroopSelection", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo installed = api.GetField("installed", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo pendingInput = api.GetField("pendingInput", BindingFlags.NonPublic | BindingFlags.Static);
            var candidates = (HashSet<int>)api.GetField("candidates", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            bool wasInstalled = (bool)installed.GetValue(null);
            var fog = new Harmony("APISharedTests.FogPrefix");
            var shared = new Harmony("APISharedTests.MarkedSelectionPostfix");
            var fogPrefix = new HarmonyMethod(typeof(MarkedSelectionHarmonyTests).GetMethod(
                nameof(FogPrefix), BindingFlags.NonPublic | BindingFlags.Static));
            var sharedPostfix = new HarmonyMethod(postfix);

            try
            {
                installed.SetValue(null, true);
                TestOrder(nameof(FogFirst), true, fog, shared, fogPrefix, sharedPostfix,
                    candidates, pendingInput, assert);
                TestOrder(nameof(SharedFirst), false, fog, shared, fogPrefix, sharedPostfix,
                    candidates, pendingInput, assert);

                MethodInfo extenderTarget = GetTarget(nameof(WithExtender));
                extenderHook = new Hook(extenderTarget, (SelectionCall)ExtenderOverride,
                    new HookConfig { ManualApply = true, ID = "APISharedTests.ExtenderSelection" });
                extenderOriginal = extenderHook.GenerateTrampoline<SelectionCall>();
                extenderHook.Apply();
                fog.Patch(extenderTarget, prefix: fogPrefix);
                shared.Patch(extenderTarget, postfix: sharedPostfix);
                Reset(candidates, pendingInput);
                WithExtender(1, false, false, new[] { 5 }, false, false,
                    new[] { 7, 8 }, 0, 0, false, new[] { 9, 10 });
                assert(originalCalls == 1 && originalUnderCursor.SequenceEqual(new[] { 7 }) &&
                    originalOnScreen.SequenceEqual(new[] { 9 }),
                    "Fog filtering and the Script Extender style trampoline preserve one Vanilla call.");
                assert(candidates.Contains(11) && !candidates.Contains(8) && !candidates.Contains(10),
                    "APIShared observes the effective Script Extender selection and Fog-filtered candidates.");

                MethodInfo noFog = GetTarget(nameof(WithoutFog));
                shared.Patch(noFog, postfix: sharedPostfix);
                Reset(candidates, pendingInput);
                WithoutFog(1, false, false, new[] { 5 }, false, false,
                    new[] { 7, 8 }, 0, 0, false, new[] { 9, 10 });
                assert(candidates.SetEquals(new[] { 5, 7, 8, 9, 10 }),
                    "APIShared preserves the unfiltered candidate set without Fog.");
                assert(originalCalls == 1 && originalUnderCursor.SequenceEqual(new[] { 7, 8 }) &&
                    originalOnScreen.SequenceEqual(new[] { 9, 10 }),
                    "Vanilla receives the unchanged lists exactly once without Fog.");
                Reset(candidates, pendingInput);
                WithoutFog(0, false, false, new[] { 5 }, false, false,
                    new[] { 7 }, 0, 0, false, new[] { 9 });
                assert(candidates.Count == 0 && !(bool)pendingInput.GetValue(null),
                    "Idle input preserves the existing no-work path.");
            }
            finally
            {
                installed.SetValue(null, wasInstalled);
                Reset(candidates, pendingInput);
            }
        }

        private static void TestOrder(string name, bool fogFirst, Harmony fog, Harmony shared,
            HarmonyMethod fogPrefix, HarmonyMethod sharedPostfix, HashSet<int> candidates,
            FieldInfo pendingInput, Action<bool, string> assert)
        {
            MethodInfo target = GetTarget(name);
            if (fogFirst)
            {
                fog.Patch(target, prefix: fogPrefix);
                shared.Patch(target, postfix: sharedPostfix);
            }
            else
            {
                shared.Patch(target, postfix: sharedPostfix);
                fog.Patch(target, prefix: fogPrefix);
            }
            Reset(candidates, pendingInput);
            target.Invoke(null, new object[] { 1, false, false, new[] { 5 }, false, false,
                new[] { 7, 8 }, 0, 0, false, new[] { 9, 10 } });
            assert(originalCalls == 1 && originalUnderCursor.SequenceEqual(new[] { 7 }) &&
                originalOnScreen.SequenceEqual(new[] { 9 }),
                "Fog filters both Vanilla candidate lists before the original in " + name + ".");
            assert(candidates.SetEquals(new[] { 5, 7, 9 }) && (bool)pendingInput.GetValue(null),
                "APIShared observes Fog-filtered candidates after the original in " + name + ".");
        }

        private static MethodInfo GetTarget(string name) => typeof(MarkedSelectionHarmonyTests)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        private static void Reset(HashSet<int> candidates, FieldInfo pendingInput)
        {
            candidates.Clear();
            pendingInput.SetValue(null, false);
            originalCalls = 0;
            originalUnderCursor = null;
            originalOnScreen = null;
        }

        private static void FogPrefix(ref int[] __6, ref int[] __10)
        {
            __6 = __6.Where(id => id != 8).ToArray();
            __10 = __10.Where(id => id != 10).ToArray();
        }

        private static void ExtenderOverride(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished,
            int[] underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps)
        {
            extenderOriginal(mouseState, rightDown, rightUp, new[] { 11 }, selectionOn,
                selectionEstablished, underCursorChimps, mousePosX, mousePosY, overTopHalf, onScreenChimps);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WithExtender(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished,
            int[] underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps)
            => RecordOriginal(underCursorChimps, onScreenChimps);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FogFirst(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished,
            int[] underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps)
            => RecordOriginal(underCursorChimps, onScreenChimps);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SharedFirst(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished,
            int[] underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps)
            => RecordOriginal(underCursorChimps, onScreenChimps);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WithoutFog(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished,
            int[] underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps)
            => RecordOriginal(underCursorChimps, onScreenChimps);

        private static void RecordOriginal(int[] underCursor, int[] onScreen)
        {
            originalCalls++;
            originalUnderCursor = underCursor;
            originalOnScreen = onScreen;
        }
    }
}
