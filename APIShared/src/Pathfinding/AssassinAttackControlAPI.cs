using System;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.Interop;

namespace APIShared
{
    /// <summary>Synchronous, read-only movement view. Never publishes a native path.</summary>
    public interface IAssassinTraversalView
    {
        /// <summary>Whether map, settings and route policy still match this view.</summary>
        bool IsCurrent { get; }
        /// <summary>Compares all movement-relevant native data with the captured snapshot.</summary>
        bool ValidateTopology();
        /// <summary>Checks one physical transition and returns its movement/climb cost.</summary>
        bool TryGetEdgeCost(int x, int y, int nextX, int nextY, out int cost);
    }

    /// <summary>Optional process-owned Assassin update guard. No consumer owns a native hook.</summary>
    public static unsafe class AssassinAttackControlAPI
    {
        private static IntPtr module;
        private static ScanRegion region;
        private static ManualLogSource log;
        private static bool supported;
        private static HookTransaction transaction;
        private static DetourHandle<AssassinAttackNativeContract.UpdateDelegate> hook;
        private static Func<int, bool> guard;
        private static Func<int, int, IAssassinTraversalView> traversal;
        private static Func<bool> traversalAvailable;
        private static string owner;
        private static readonly object Sync = new object();
        private static int errorLogged;
        internal static void Initialize(IntPtr nativeModule, ScanRegion nativeRegion, string hash, ManualLogSource logger)
        {
            module = nativeModule; region = nativeRegion; log = logger;
            supported = string.Equals(hash, AssassinPathAPI.ReferenceHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Registers the mainmod's rooted read-only traversal provider once.</summary>
        public static void RegisterTraversal(Func<int, int, IAssassinTraversalView> provider, Func<bool> isAvailable)
        {
            if (provider == null || isAvailable == null) throw new ArgumentNullException(nameof(provider));
            if (Interlocked.CompareExchange(ref traversal, provider, null) != null)
                throw new InvalidOperationException("Assassin traversal provider is already registered.");
            Volatile.Write(ref traversalAvailable,isAvailable);
        }

        /// <summary>Whether the registered mainmod traversal is currently enabled.</summary>
        public static bool IsTraversalAvailable => Volatile.Read(ref traversalAvailable)?.Invoke()==true;

        /// <summary>Captures a synchronous movement view, or null when unavailable.</summary>
        public static IAssassinTraversalView CaptureTraversal(int playerId, int speedDelay) =>
            Volatile.Read(ref traversal)?.Invoke(playerId, speedDelay);

        /// <summary>Return true only to end the current obstacle attack. Callback must not issue native commands.</summary>
        public static void RegisterGuard(string ownerGuid, Func<int, bool> callback)
        {
            if (string.IsNullOrEmpty(ownerGuid) || callback == null) throw new ArgumentException("Owner and callback required.");
            lock (Sync)
            {
                if (owner != null) throw new InvalidOperationException("Assassin attack guard already belongs to " + owner);
                EnsureInstalled();
                owner = ownerGuid;
                Volatile.Write(ref guard, callback);
            }
        }

        private static void DispatchAssassinUpdate()
        {
            try
            {
                int unitId = *(int*)((byte*)module + 0x9302C4);
                if (Volatile.Read(ref guard)?.Invoke(unitId) == true &&
                    UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) &&
                    UnitAccess.IsReallyAlive(in *unit) && (unit->r_AIState == 101 || unit->r_AIState == 107))
                {
                    // Exact Vanilla obstacle-completion cleanup: 16D573..16D6DA.
                    unit->r_AI_ContextTargetBuildingTileId = 0;
                    unit->r_AIState = 0;
                    unit->r_AnimationTimer = 0;
                }
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref errorLogged, 1) == 0)
                    log?.LogError("Assassin attack guard failed; original update retained: " + ex);
            }
            hook.Original(); // Full original housekeeping and state machine, exactly once.
        }

        private static void EnsureInstalled()
        {
            if (hook != null) return;
            if (!supported || module == IntPtr.Zero || region == null)
                throw new InvalidOperationException("Assassin update requires the audited native image.");
            byte[] expected = { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10 };
            IntPtr target = module + 0x16CD70;
            for (int i=0;i<expected.Length;i++)
                if (Marshal.ReadByte(target,i) != expected[i]) throw new InvalidOperationException("Assassin update prologue changed or occupied.");
            var candidate = new DetourHandle<AssassinAttackNativeContract.UpdateDelegate>();
            HookTransaction pending = null;
            try
            {
                pending = new HookTransaction(region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true, Backend = AssassinAttackNativeContract.Backend });
                pending.AddDetour(candidate, HookTarget.FromAddress(unchecked((ulong)target.ToInt64())), (AssassinAttackNativeContract.UpdateDelegate)DispatchAssassinUpdate);
                if (!pending.Commit().IsCompleteSuccess || !candidate.Success)
                    throw new InvalidOperationException("Assassin update transaction failed.");
                AssassinAttackNativeContract.Validate(candidate.Hook, target);
                transaction = pending;
                hook = candidate; // Publication: process lifetime; no reachable teardown.
                log?.LogInfo("Assassin attack guard installed; APIShared owner, RVA=0x16CD70, Indirect/10, continuation=0x16CD7A.");
            }
            catch
            {
                if (hook == candidate) throw;
                pending?.Dispose(); // Unpublished failed candidate only.
                throw;
            }
        }
    }
}
