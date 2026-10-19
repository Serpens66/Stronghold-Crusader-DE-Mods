using BepInEx;
using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL.DrawbridgeDestructionFix
{
    internal sealed unsafe class FixRuntime
    {
        private readonly ManualLogSource log;
        private readonly HookHandle<X64InlineHook> handle = new HookHandle<X64InlineHook>();
        private readonly ThreadLocal<Stack<Probe>> probes = new ThreadLocal<Stack<Probe>>(() => new Stack<Probe>(4));
        private HookTransaction transaction;
        private IDisposable damageSubscription;
        private IntPtr state;
        private bool published;
        private int confirmed, errorLogged, lastEnabled = -1;

        internal FixRuntime(ManualLogSource log) { this.log = log; }

        internal void Install(CrusaderLibraryLoadContext context, bool enableMod, bool enableFix)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero) throw new InvalidOperationException("Native context unavailable.");
            NativeContract.ValidateLayouts();
            byte[] reference = NativeContract.ReadReferenceFunction(Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll"));
            string pattern = BitConverter.ToString(NativeContract.Signature).Replace('-', ' ');
            var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory, pattern, NativeContract.HookRva,
                true, "drawbridge nonlethal gatehouse damage", log);
            if (resolution.Rva != NativeContract.HookRva || !NativeContract.Matches(context.Memory, resolution.Rva))
                throw new InvalidOperationException("Hook is outside the audited native contract.");
            // Extender owns the entry. No unreviewed interior hook is accepted.
            if (!context.Memory.Slice(NativeContract.FunctionRva + 64, reference.Length - 64).SequenceEqual(new ReadOnlySpan<byte>(reference, 64, reference.Length - 64)))
                throw new InvalidOperationException("Audited damage function body differs from the native image.");
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            ulong target = imageBase + NativeContract.HookRva;
            using (var probe = new X64InlineHook(target, NativeContract.SpanLength))
                if (probe.DisplacedByteCount != NativeContract.SpanLength) throw new InvalidOperationException("Inline span differs from 15.");
            state = Marshal.AllocHGlobal(12);
            for (int n = 0; n < 3; n++) Marshal.WriteInt32(state, n * 4, 0);
            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
                transaction.AddInline(handle, HookTarget.FromAddress(target),
                    (asm, original, continuation) => NativeContract.Emit(asm, original, continuation,
                        imageBase + NativeContract.SkipRva, unchecked((ulong)state.ToInt64())),
                    hookSize: NativeContract.SpanLength, name: "BugfixesAndQoL_DrawbridgeNonlethalGateHit");
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !handle.Success || handle.Hook.DisplacedByteCount != NativeContract.SpanLength || handle.Hook.TargetAddress != target)
                    throw new InvalidOperationException("Drawbridge hook commit/span failed: " + result);
                byte[] installed = new byte[NativeContract.SpanLength];
                Marshal.Copy(new IntPtr(unchecked((long)target)), installed, 0, installed.Length);
                if (installed[0] != 0xFF || installed[1] != 0x25 || BitConverter.ToInt32(installed, 2) != 0 ||
                    BitConverter.ToInt64(installed, 6) != handle.Hook.StubAddress.ToInt64() || installed[14] != 0x90)
                    throw new InvalidOperationException("Committed inline patch does not point to its verified stub.");
                damageSubscription = BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnDamage);
                published = true;
                Info("DRAWBRIDGE_FIX_INSTALLED hash=" + NativeContract.Hash + "; method=" + resolution.Method +
                    "; rva=0x7EE6B; span=15; end=0x7EE7A; skip=0x7EF87");
                SetEnabled(enableMod, enableFix);
            }
            catch
            {
                if (!published)
                {
                    damageSubscription?.Dispose();
                    transaction?.Dispose(); // Roll back only an unpublished initialization candidate.
                    Marshal.FreeHGlobal(state); state = IntPtr.Zero;
                }
                throw;
            }
        }

        internal void SetEnabled(bool enableMod, bool enableFix)
        {
            if (!published || state == IntPtr.Zero) return;
            int value = NativeContract.IsEnabled(enableMod, enableFix) ? 1 : 0;
            Volatile.Write(ref *(int*)state, value);
            if (Interlocked.Exchange(ref lastEnabled, value) != value) Info("DRAWBRIDGE_FIX_ACTIVE enabled=" + (value != 0));
        }

        private int Entered => Volatile.Read(ref *((int*)state + 1));
        private int Suppressed => Volatile.Read(ref *((int*)state + 2));
        private void OnDamage(BuildingTileTakeDamageEventArgs args)
        {
            // After the one confirmation, no stack, SDK lookup or formatting occurs on this observer path.
            Shared.DevDiag.Count("drawbridge.damageEvents");
            if (!published || Volatile.Read(ref confirmed) != 0) return;
            if (args.Phase == EventHookPhase.Pre)
            {
                Probe capture = default;
                try
                {
                    if (Volatile.Read(ref *(int*)state) != 0 && args.Unknown3 == 1 && GameTileManagerAPI.Instance.IsValidTileId(args.TileId))
                    {
                        int id = GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);
                        if (id > 0 && GameBuildingManagerAPI.Instance.IsValidId(id) && GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) &&
                            building->r_AliveState == AliveState.IsAlive &&
                            (building->r_BuildingType == eStructs.STRUCT_GATE_MAIN || building->r_BuildingType == eStructs.STRUCT_GATE_INNER))
                        {
                            capture = new Probe { Id = id, Global = building->r_GlobalId, Hp = building->r_CurrentHealth,
                                X = building->r_TilePositionXBegin, Y = building->r_TilePositionYBegin, Entered = Entered, Suppressed = Suppressed };
                        }
                    }
                }
                catch (Exception error) { Error("DRAWBRIDGE_FIX_DIAGNOSTIC_ERROR " + error.Message); }
                probes.Value.Push(capture);
                return;
            }
            if (args.Phase != EventHookPhase.Post) return;
            try
            {
                var stack = probes.Value;
                if (stack.Count == 0) { Error("DRAWBRIDGE_FIX_DIAGNOSTIC_ERROR: unmatched Post"); return; }
                Probe capture = stack.Pop();
                // Exactly one native entry excludes ambiguous nested attribution. Counters prove the guard ran.
                if (capture.Id == 0 || Entered - capture.Entered != 1 || Suppressed - capture.Suppressed != 1) return;
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(capture.Id, out GameBuilding* building) ||
                    building->r_GlobalId != capture.Global || building->r_AliveState != AliveState.IsAlive || building->r_CurrentHealth <= 0) return;
                if (Interlocked.Exchange(ref confirmed, 1) == 0)
                    Info("DRAWBRIDGE_FIX_CONFIRMED building=" + capture.Id + "/g" + capture.Global + "; xy=" + capture.X + "," + capture.Y +
                        "; hp=" + capture.Hp + "->" + building->r_CurrentHealth + "; nativeProtectedHit=true; afterStartupCleanup=true");
            }
            catch (Exception error) { Error("DRAWBRIDGE_FIX_DIAGNOSTIC_ERROR " + error.Message); }
        }
        private void Info(string message) { try { Shared.DebugLogHelper.LogInfo(log, message); } catch { } }
        private void Error(string message)
        {
            if (Interlocked.Exchange(ref errorLogged, 1) == 0)
                try { log.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message); } catch { }
        }
        private struct Probe { internal int Id, Hp, X, Y, Entered, Suppressed; internal uint Global; }
    }
}
