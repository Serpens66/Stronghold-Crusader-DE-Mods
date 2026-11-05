using System;
using System.Reflection;
using HarmonyLib;

namespace PlayerControlRelayTest
{
    // Vanilla's per-pass troop gesture input: released left button, right button and the unit
    // IDs of a selection box or under the cursor (all owners). TroopSelection's mousePosX/Y are
    // screen pixels, so the order target comes from preDLLCallActions' mouseOverX/Y: the same
    // tile DLL_RunTick receives, already -1 over GUI. Both run in one engine pass on one thread.
    // Observes only: Vanilla receives unchanged arguments and keeps handling its own units. The
    // postfixes share the methods' existing Harmony chains and stay installed for the process.
    internal static class RelayInput
    {
        // EditorDirector's left-button engine state for a release.
        private const int ReleasedLeftButton = 3;
        private static Harmony harmony;
        private static Action<bool, int[], bool, int[], int, int> gesture;
        private static Action<Exception> failure;
        private static bool pending, pendingRight, pendingBox;
        private static int[] pendingBoxed, pendingUnderCursor;

        internal static void Initialize(Action<bool, int[], bool, int[], int, int> onGesture, Action<Exception> onFailure)
        {
            if (harmony != null) return;
            MethodInfo input = typeof(EngineInterface).GetMethod(nameof(EngineInterface.TroopSelection),
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(int), typeof(bool), typeof(bool), typeof(int[]), typeof(bool),
                    typeof(bool), typeof(int[]), typeof(int), typeof(int), typeof(bool), typeof(int[]) }, null);
            MethodInfo caller = typeof(EditorDirector).GetMethod(nameof(EditorDirector.preDLLCallActions),
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() }, null);
            MethodInfo capture = typeof(RelayInput).GetMethod(nameof(AfterTroopSelection), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo dispatch = typeof(RelayInput).GetMethod(nameof(AfterPreDll), BindingFlags.NonPublic | BindingFlags.Static);
            if (input == null || input.ReturnType != typeof(void) || caller == null || capture == null || dispatch == null)
                throw new InvalidOperationException("Vanilla troop input contract changed.");
            gesture = onGesture; failure = onFailure;
            var candidate = new Harmony(PlayerControlRelayPlugin.Guid + ".Input");
            candidate.Patch(input, postfix: new HarmonyMethod(capture));
            candidate.Patch(caller, postfix: new HarmonyMethod(dispatch));
            harmony = candidate;
        }

        // Called every simulation pass; the common path returns without allocating. Always
        // overwritten, so a pass that threw before its dispatch cannot leak a stale gesture.
        private static void AfterTroopSelection(int __0, bool __1, bool __2, int[] __3, bool __4, int[] __6)
        {
            pendingRight = __1 || __2;
            pending = __0 == ReleasedLeftButton || pendingRight;
            if (!pending) return;
            pendingBoxed = __3; pendingBox = __4; pendingUnderCursor = __6;
        }

        private static void AfterPreDll(ref int mouseOverX, ref int mouseOverY)
        {
            if (!pending) return;
            pending = false;
            int[] boxed = pendingBoxed, underCursor = pendingUnderCursor;
            pendingBoxed = pendingUnderCursor = null;
            try { gesture(pendingRight, boxed, pendingBox, underCursor, mouseOverX, mouseOverY); }
            catch (Exception error) { failure(error); }
        }
    }
}
