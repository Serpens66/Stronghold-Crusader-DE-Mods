using System;
using System.Threading;

namespace AIBuildDiagnoseTest
{
    // Testmod-private cooperation. No hook ownership or memory mutation lives here.
    internal readonly struct WoodCounterPoint
    {
        internal readonly long Session;
        internal readonly int Tick, UnitId, BuildingId;
        internal readonly short NativeAfter;
        internal readonly uint UnitGlobal, BuildingGlobal;
        internal WoodCounterPoint(long session, int tick, int unit, uint unitGlobal, int building, uint buildingGlobal, short after)
        { Session = session; Tick = tick; UnitId = unit; BuildingId = building; NativeAfter = after; UnitGlobal = unitGlobal; BuildingGlobal = buildingGlobal; }
    }
    internal readonly struct WoodCounterCorrection
    {
        internal readonly bool Applied;
        internal readonly uint UnitGlobal, BuildingGlobal;
        internal WoodCounterCorrection(uint unit, uint building)
        { Applied = true; UnitGlobal = unit; BuildingGlobal = building; }
    }
    internal interface IWoodSafetyIntervention
    {
        Action EnterWoodBuild(int player);
        WoodCounterCorrection CorrectDeathCounter(WoodCounterPoint point);
    }
    internal static class WoodSafetyBridge
    {
        private static IWoodSafetyIntervention consumer;
        private static string nativeHash;
        private static int counterReady;
        private static long failures;
        internal static string NativeHash => Volatile.Read(ref nativeHash);
        internal static bool CounterReady => Volatile.Read(ref counterReady) != 0;
        internal static long CallbackFailures => Interlocked.Read(ref failures);
        internal static void SetNativeHash(string hash) => Volatile.Write(ref nativeHash, hash);
        internal static void SetCounterReady(bool ready) => Volatile.Write(ref counterReady, ready ? 1 : 0);
        internal static bool TryRegister(string owner, IWoodSafetyIntervention target, out string reason)
        {
            reason = null;
            if (owner != "AIWoodSafetyTest_Serp" || target == null) { reason = "Invalid wood safety consumer"; return false; }
            var prior = Interlocked.CompareExchange(ref consumer, target, null);
            if (prior != null && !ReferenceEquals(prior, target)) { reason = "Wood safety consumer already registered"; return false; }
            return true;
        }
        internal static Action EnterWoodBuild(int player)
        {
            try { return Volatile.Read(ref consumer)?.EnterWoodBuild(player); }
            catch { Interlocked.Increment(ref failures); return null; }
        }
        internal static void ExitWoodBuild(Action restore)
        {
            try { restore?.Invoke(); }
            catch { Interlocked.Increment(ref failures); }
        }
        internal static WoodCounterCorrection CorrectDeathCounter(WoodCounterPoint point)
        {
            try { return Volatile.Read(ref consumer)?.CorrectDeathCounter(point) ?? default; }
            catch { Interlocked.Increment(ref failures); return default; }
        }
    }
}
