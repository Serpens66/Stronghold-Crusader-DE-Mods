using System;
using System.Reflection;
using System.Threading;
using MonoMod.RuntimeDetour;

namespace PlayerControlRelayTest
{
    // The modal proxy surface is GUI. Vanilla's mouse handling uses this public
    // query before producing troop/placement gestures. Preserve its original once,
    // including hover bookkeeping, then claim the world surface while proxying.
    internal static class RelayInputGuard
    {
        private delegate bool Original(FatControler self);
        private delegate bool Wrapper(Original original, FatControler self);
        private static Hook hook;
        private static Hook keyboardHook;
        private static int active;
        private static int releasePending;
        private static int mainThread;
        internal static void Initialize()
        {
            if (hook != null) return;
            mainThread = Thread.CurrentThread.ManagedThreadId;
            var method = typeof(FatControler).GetMethod(nameof(FatControler.overNoesisGUI), BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(bool)) throw new InvalidOperationException("Vanilla GUI query contract changed.");
            hook = new Hook(method, (Wrapper)Dispatch); // Process lifetime; never toggled/uninstalled.
            var keyboard = typeof(FatControler).GetProperty(nameof(FatControler.NoesisHasKeyboard), BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();
            if (keyboard == null || keyboard.ReturnType != typeof(bool)) throw new InvalidOperationException("Vanilla keyboard ownership contract changed.");
            keyboardHook = new Hook(keyboard, (Wrapper)Dispatch);
        }
        internal static void SetActive(bool value)
        {
            if (!value && Interlocked.Exchange(ref active, 0) != 0) Volatile.Write(ref releasePending, 1);
            else if (value) Volatile.Write(ref active, 1);
        }
        private static bool Dispatch(Original original, FatControler self)
        {
            bool result = original(self);
            // Closing with Escape/a held hotkey must not synthesize a fresh Vanilla
            // key-down before the same physical press has been released.
            if (Volatile.Read(ref releasePending) != 0 && Thread.CurrentThread.ManagedThreadId == mainThread && !UnityEngine.Input.anyKey)
                Volatile.Write(ref releasePending, 0);
            return result || Volatile.Read(ref active) != 0 || Volatile.Read(ref releasePending) != 0;
        }
    }
}
