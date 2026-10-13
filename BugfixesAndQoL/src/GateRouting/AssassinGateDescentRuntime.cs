using APIShared.GateRouting.Native;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
namespace BugfixesAndQoL.GateRouting
{
    // Functional descent rule; APIShared owns 185D55..185D69 and the optional clear observation.
    // No lifecycle teardown; rooted until process exit. All memory writes target activation data.
    internal sealed unsafe class AssassinGateDescentRuntime
    {
        internal const int Site = 0x185D55, Length = 20, Continuation = 0x185934;
        private static readonly System.Collections.Generic.List<AssassinGateDescentRuntime> Roots = new System.Collections.Generic.List<AssassinGateDescentRuntime>();
        private readonly ulong module;
        private readonly Func<RouteTilePolicySnapshot> policy;
        private readonly int* active = (int*)Marshal.AllocHGlobal(4);
        private readonly int* diagnostics = (int*)Marshal.AllocHGlobal(4);
        private readonly CapturerComparisonCallback decide, before, after;
        private Action<IntPtr, bool> observeClear;
        private Action<int> observeException;
        private long allowed, preserved, errors, observerErrors;
        internal AssassinGateDescentRuntime(CrusaderLibraryLoadContext context, GateRoutingNativeHooks nativeHooks, Func<RouteTilePolicySnapshot> policy)
        {
            module = (ulong)context.ModuleHandle.ToInt64(); this.policy = policy;
            *active = *diagnostics = 0;
            decide = Decide;
            before = p => ObserveClear(p, false);
            after = p => ObserveClear(p, true);
            nativeHooks.InstallDescent(decide,before,after,(IntPtr)active,(IntPtr)diagnostics);
            lock (Roots) Roots.Add(this);
        }
        internal void SetActive(bool value, Action<IntPtr, bool> clearObserver, Action<int> exceptionObserver)
        {
            observeClear = clearObserver; observeException = exceptionObserver;
            if (value) allowed = preserved = errors = observerErrors = 0;
            Volatile.Write(ref *diagnostics, value && (observeClear != null || observeException != null) ? 1 : 0);
            Volatile.Write(ref *active, value ? 1 : 0);
        }
        internal string Summary => "Assassin gate descent: exceptionsGranted=" + Interlocked.Read(ref allowed) +
            ",nativeAbortPreserved=" + Interlocked.Read(ref preserved) + ",guardErrors=" + Interlocked.Read(ref errors) +
            ",observerErrors=" + Interlocked.Read(ref observerErrors);
        private void ObserveClear(IntPtr pointer, bool completed)
        {
            try { if (Volatile.Read(ref *diagnostics) != 0) observeClear?.Invoke(pointer, completed); }
            catch (Exception) { Interlocked.Increment(ref observerErrors); }
        }
        private void Decide(IntPtr pointer)
        {
            var c = (X64SmartCPUContext*)pointer; c->R11 = 0;
            try
            {
                if (!Allows(c)) { Interlocked.Increment(ref preserved); return; }
                c->R11 = 1; Interlocked.Increment(ref allowed);
            }
            catch (Exception) { Interlocked.Increment(ref errors); return; }
            // Observation failure must not revoke a functional permission.
            try { if (Volatile.Read(ref *diagnostics) != 0) observeException?.Invoke((int)c->RBX); }
            catch (Exception) { Interlocked.Increment(ref observerErrors); }
        }
        private bool Allows(X64SmartCPUContext* c)
        {
            int id = (int)c->RBX, direction = (int)c->R14;
            if (Volatile.Read(ref *active) == 0 || c->RDI != module + 0x67E8400 || id < 1 || id > 10000 ||
                c->R12 != (ulong)id * 0x490 || (c->RBP & 255) != 0 ||
                !AssassinPathAPI.DirectGatehouseClimbingEnabled ||
                !UnitAccess.TryGetById(id, out GameUnit* u, out _) || u == null || !UnitAccess.IsReallyAlive(in *u) ||
                (ulong)u != c->RDI + c->R12 + 0x65C || u->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                u->r_ControllableForPlayerId < 1 || u->r_ControllableForPlayerId > 8 || u->r_GlobalId == 0 ||
                (uint)direction > 7 || u->r_CurrentTilePositionY >= 800) return false;
            uint global = u->r_GlobalId; ushort player = u->r_ControllableForPlayerId;
            int tile = (int)u->r_CurrentPositionTileId;
            if ((uint)tile >= 320800) return false;
            int target = tile + *(int*)(module + 0x405EDB0 + (uint)(direction + u->r_CurrentTilePositionY * 8) * 4UL);
            if ((uint)target >= 320800 || target == tile) return false;
            RouteTilePolicySnapshot snapshot = policy();
            int gate = *(short*)(module + 0x4B6AA50 + (uint)tile * 2UL);
            if (snapshot == null || !snapshot.GateIdentities.TryGetValue(gate, out var identity) || identity.Global == 0 ||
                !GameBuildingManagerAPI.Instance.IsValidId(gate) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(gate, out GameBuilding* b) || b == null ||
                b->r_GlobalId != identity.Global || b->r_PlayerIdOwner != identity.Owner || b->r_CapturedByPlayerId != identity.Capturer ||
                b->r_GateState != 2 || !AssassinPathAPI.IsDirectGatehouseClimbEndpoint(tile)) return false;
            // Audited raw WORDs: path mode +90, alternate step +36C. R9w is the special-step state.
            bool permitted = AssassinGateDescentRule.Allows(direction, (int)u->r_MovementDirection,
                u->r_CurrentPathPlanIndex, u->r_PathPlanLength, (int)c->RDX, (ushort)c->R9,
                *(short*)((byte*)u + 0x36C), u->r_AIState, *(short*)((byte*)u + 0x90) == 0,
                *(byte*)(module + 0x51890D0 + (uint)tile), *(byte*)(module + 0x51890D0 + (uint)target),
                *(byte*)(module + 0x312620 + (uint)direction), *(byte*)(module + 0x312620 + (uint)(direction ^ 4)),
                *(uint*)(module + 0x48F71B0 + (uint)tile * 4UL), *(uint*)(module + 0x48F71B0 + (uint)target * 4UL));
            return permitted && Volatile.Read(ref *active) != 0 && ReferenceEquals(snapshot, policy()) &&
                u->r_GlobalId == global && u->r_ControllableForPlayerId == player && u->r_CurrentPositionTileId == tile &&
                b->r_GlobalId == identity.Global && b->r_PlayerIdOwner == identity.Owner && b->r_CapturedByPlayerId == identity.Capturer && b->r_GateState == 2;
        }
    }
}
