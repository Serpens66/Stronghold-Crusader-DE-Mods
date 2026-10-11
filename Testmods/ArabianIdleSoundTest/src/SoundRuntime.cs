using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ArabianIdleSoundTest
{
    internal sealed unsafe class SoundRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void UnitTick();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void Emit(IntPtr manager, short sound, short x, short y, short flag);
        [ThreadStatic] private static int scopeId;
        private readonly ManualLogSource log;
        private NativeDetour<UnitTick> unitHook;
        private NativeDetour<Emit> emitHook;
        private ILHook managedHook;
        private UnitTick unitCallback;
        private Emit emitCallback;
        private bool publicationStarted;
        private int* currentUnit;
        private int confirmed, reduced, error;
        // DLL_RunTick receives short[14,700,000]. The pending native buffer is
        // even larger; this conservative capacity covers every normal tick.
        internal const int MaxRecords = 14700000 / 21;

        internal SoundRuntime(ManualLogSource log) { this.log = log; }
        internal void Install(CrusaderLibraryLoadContext context)
        {
            var files = new InstalledAudioFiles(BepInEx.Paths.GameRootPath);
            files.Validate();
            Shared.DebugLogHelper.LogInfo(log, "Installed audio binary identities verified from BepInEx game root: " + BepInEx.Paths.GameRootPath);
            var live = new ReadOnlySpan<byte>(context.ModuleHandle.ToPointer(), context.Memory.Length);
            var unit = Shared.NativePatternResolver.ResolveUnique(live, NativeContract.ArabianPattern,
                NativeContract.ArabianRva, true, "Arabian idle handler", log);
            var emitter = Shared.NativePatternResolver.ResolveUnique(live, NativeContract.EmitPattern,
                NativeContract.EmitRva, true, "Spatial sound emitter", log);
            // Fixed semantic/layout contracts remain hash-bound even if a byte
            // search succeeds elsewhere. Never detour an unknown duplicate.
            if (unit.Rva != NativeContract.ArabianRva || emitter.Rva != NativeContract.EmitRva)
                throw new InvalidOperationException("Resolved function moved outside the audited semantic boundary.");
            int unitGlobal = Shared.NativePatternResolver.ResolveRelativeTarget(live, unit.Rva + 0x1F, unit.Rva + 0x23);
            int managerGlobal = Shared.NativePatternResolver.ResolveRelativeTarget(live, unit.Rva + 0x26, unit.Rva + 0x2A);
            if (unitGlobal != NativeContract.CurrentUnitRva || managerGlobal != 0x67E8400 || unitGlobal > live.Length - 4)
                throw new InvalidOperationException("Unit manager/global derivation changed.");
            currentUnit = (int*)((byte*)context.ModuleHandle.ToPointer() + unitGlobal);
            Shared.DebugLogHelper.LogInfo(log, "Resolved Arabian handler: method=" + unit.Method + ",rva=0x" + unit.Rva.ToString("X") +
                "; emitter: method=" + emitter.Method + ",rva=0x" + emitter.Rva.ToString("X") +
                "; gameId=1-based,stride=0x490,slot1=manager+0xAEC,chimp=+0x8A,state=+0x2BC,currentUnit=0x9302C4.");
            var method = typeof(GameMap).GetMethod("processTestMap", new[] { typeof(short[]), typeof(int), typeof(EngineInterface.PlayState), typeof(byte[]) });
            if (method == null || !method.IsPublic) throw new InvalidOperationException("Public processTestMap signature changed.");
            SoundIlContract.Validate(method);
            try
            {
                unitCallback = OnUnit;
                emitCallback = OnEmit;
                var unitRequest = new DetourRequest<UnitTick> { Name = "ArabianIdleSoundTest.Unit", TargetAddress = Address(context, unit.Rva), Callback = unitCallback };
                var emitRequest = new DetourRequest<Emit> { Name = "ArabianIdleSoundTest.Emit", TargetAddress = Address(context, emitter.Rva), Callback = emitCallback };
                unitHook = (NativeDetour<UnitTick>)NativeContract.Backend(3879).CreateDetour(in unitRequest);
                emitHook = (NativeDetour<Emit>)NativeContract.Backend(137).CreateDetour(in emitRequest);
                NativeContract.Validate(unitHook, 15, unitRequest.TargetAddress);
                NativeContract.Validate(emitHook, 14, emitRequest.TargetAddress);
                managedHook = new ILHook(method, SoundIlContract.Apply, new ILHookConfig { ManualApply = true, ID = Plugin.Guid });
            }
            catch { RollbackUnpublished(); throw; }
            SoundIlContract.Reduced = OnReduced;
            // No published hook is ever reverted. All callbacks stay logically
            // inactive until the complete chain has been installed and checked.
            publicationStarted = true;
            managedHook.Apply();
            unitHook.Enable();
            emitHook.Enable();
            if (!unitHook.IsInstalled || !emitHook.IsInstalled || !managedHook.IsApplied)
                throw new InvalidOperationException("Hook publication incomplete.");
            NativeContract.Validate(unitHook, 15, Address(context, NativeContract.ArabianRva));
            NativeContract.Validate(emitHook, 14, Address(context, NativeContract.EmitRva));
            SoundPolicy.SetReady(true);
            Shared.DebugLogHelper.LogInfo(log, "Idle sword-draw hooks installed: NativeX64/Absolute; unit span=0x16F590..0x16F59F (5+5+5),next=push rdi; emitter span=0x1A41E0..0x1A41EE (4+3+4+3),next=mov word [r10+rax+2],r8w; factor=0.25; callbacks rooted for process lifetime.");
        }
        private static ulong Address(CrusaderLibraryLoadContext context, int rva) => unchecked((ulong)context.ModuleHandle.ToInt64() + (uint)rva);

        private void RollbackUnpublished()
        {
            if (publicationStarted) throw new InvalidOperationException("Published hook rollback forbidden.");
            if ((unitHook != null && unitHook.IsInstalled) || (emitHook != null && emitHook.IsInstalled) ||
                (managedHook != null && managedHook.IsApplied)) throw new InvalidOperationException("Installed hook rollback forbidden.");
            managedHook?.Dispose();
            emitHook?.Dispose();
            unitHook?.Dispose();
        }

        private void OnUnit()
        {
            int previous = scopeId;
            scopeId = 0;
            try
            {
                if (SoundPolicy.Active)
                {
                    try
                    {
                        int id = *currentUnit;
                        if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out var failure))
                            throw new InvalidOperationException("Arabian handler current unit ID invalid: " + id + "; reason=" + failure);
                        if ((int)unit->r_UnitChimp != 75) throw new InvalidOperationException("Arabian handler type mismatch.");
                        scopeId = id;
                        if (Interlocked.Exchange(ref confirmed, 1) == 0)
                            Shared.DebugLogHelper.LogInfo(log, "hook confirmed after startup cleanup: unitId=" + id +
                                ",type=75,state=" + unit->r_AIState + ",soundPosition=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY + "; field map verified by installed public GameUnit view.");
                    }
                    catch (Exception ex) { Fail(ex); }
                }
                unitHook.Original();
            }
            finally { scopeId = previous; }
        }

        private void OnEmit(IntPtr manager, short sound, short x, short y, short flag)
        {
            bool eligible = false;
            short* buffer = null;
            int before = -1;
            try
            {
                if (SoundPolicy.Active && scopeId > 0 && sound == SoundPolicy.SwordDraw && flag == 0)
                {
                    int id = *currentUnit;
                    if (id == scopeId && APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _))
                        eligible = SoundPolicy.Eligible(scopeId, id, (int)unit->r_UnitChimp, unit->r_AIState, sound, flag);
                    if (eligible)
                    {
                        if (manager == IntPtr.Zero) throw new InvalidOperationException("Null sound manager.");
                        buffer = *(short**)manager.ToPointer();
                        before = *(int*)((byte*)manager.ToPointer() + 8);
                        if (buffer == null || before < 0 || before >= MaxRecords) throw new InvalidOperationException("Sound buffer/count outside audited capacity.");
                    }
                }
            }
            catch (Exception ex) { eligible = false; Fail(ex); }
            emitHook.Original(manager, sound, x, y, flag);
            if (!eligible || !SoundPolicy.Active) return;
            try
            {
                if (*(short**)manager.ToPointer() != buffer || *(int*)((byte*)manager.ToPointer() + 8) != before + 1)
                    throw new InvalidOperationException("Spatial emitter did not append exactly one record.");
                var record = new Span<short>(buffer + before * SoundPolicy.RecordWords, SoundPolicy.RecordWords);
                if (!SoundPolicy.TryMark(record, true)) throw new InvalidOperationException("Appended spatial sword-draw record changed.");
            }
            catch (Exception ex) { Fail(ex); }
        }

        private void OnReduced(int index, float before, float after)
        {
            if (Interlocked.Exchange(ref reduced, 1) == 0)
                Shared.DebugLogHelper.LogInfo(log, "idle sword-draw playback confirmed after startup cleanup: sound=180,record=" + index +
                    ",factor=0.25,volumeBefore=" + before + ",volumeAfter=" + after + "; no global sound volume changes.");
        }
        private void Fail(Exception ex)
        {
            SoundPolicy.SetReady(false);
            if (Interlocked.Exchange(ref error, 1) == 0)
                Shared.DebugLogHelper.LogError(log, "Idle sword-draw context failed; callbacks remain installed but inactive: " + ex);
        }
    }
}
