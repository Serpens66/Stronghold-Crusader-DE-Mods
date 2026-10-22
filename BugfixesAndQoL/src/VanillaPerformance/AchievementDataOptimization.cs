using System;
using System.Threading;
using System.Linq;
using HarmonyLib;

namespace BugfixesAndQoL.VanillaPerformance
{
    internal static class AchievementDataOptimization
    {
        internal sealed class Values
        {
            internal readonly int Food, Wood, Weapons;
            internal Values(int food, int wood, int weapons) { Food = food; Wood = wood; Weapons = weapons; }
        }
        internal static void Install(Harmony harmony)
        {
            var target = typeof(EngineInterface).GetMethod(nameof(EngineInterface.SetAchData));
            if (target == null || !target.IsPublic || !target.IsStatic || target.ReturnType != typeof(void) ||
                !target.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {typeof(int), typeof(int), typeof(int)}))
                throw new MissingMethodException("SetAchData signature differs");
            EnginePresentationBridge.ValidateLockedCall(target, nameof(EngineInterface.DLL_SetAchValues));
            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(AchievementDataOptimization), nameof(BeforeSetAchData)),
                finalizer: new HarmonyMethod(typeof(AchievementDataOptimization), nameof(FinishSetAchData)));
        }
        private static bool BeforeSetAchData(int food, int wood, int weapons, out bool __state)
        {
            __state = false;
            var session = EnginePresentationBridge.Current;
            if (!EnginePresentationBridge.Active || !EnginePresentationBridge.IsMainThread || session == null)
            {
                // A synchronous foreign writer must not be overwritten later by
                // an older queued Main-thread tuple. Retain its ordering under
                // the very same lock through the original method/finalizer.
                if (session != null && Volatile.Read(ref session.Pending) != null)
                {
                    Monitor.Enter(EnginePresentationBridge.Gate, ref __state);
                    Flush(session);
                }
                return true;
            }
            // Publish the complete original Int32 tuple; preserve the native
            // Int16 conversion and ordering in DLL_SetAchValues itself.
            Publish(session, food, wood, weapons);
            bool taken = false;
            try
            {
                Monitor.TryEnter(EnginePresentationBridge.Gate, ref taken);
                if (taken) Flush(session);
            }
            finally { if (taken) Monitor.Exit(EnginePresentationBridge.Gate); }
            return false;
        }
        internal static void Publish(EnginePresentationBridge.Session session, int food, int wood, int weapons)
            => Interlocked.Exchange(ref session.Pending, new Values(food, wood, weapons));
        private static Exception FinishSetAchData(bool __state, Exception __exception)
        {
            if (__state) Monitor.Exit(EnginePresentationBridge.Gate);
            return __exception;
        }
        internal static void Flush(EnginePresentationBridge.Session session)
        {
            if (session == null || !ReferenceEquals(session, EnginePresentationBridge.Current)) return;
            if (!Monitor.IsEntered(EnginePresentationBridge.Gate)) throw new SynchronizationLockException("Achievement flush requires engine lock");
            Values value = Interlocked.Exchange(ref session.Pending, null);
            if (value == null) return;
            try { EngineInterface.DLL_SetAchValues(value.Food, value.Wood, value.Weapons); }
            catch
            {
                // A newer complete tuple already supersedes the failed write;
                // otherwise retain this one for retry/save failure reporting.
                Interlocked.CompareExchange(ref session.Pending, value, null); throw;
            }
        }
    }
}
