using APIShared.NativeSites;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.API.LowLevel;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace BugfixesAndQoL.LordTeleportGuard
{
    internal sealed unsafe partial class LordTeleportGuardRuntime
    {
        private const string Site = "Lord.HomeTeleportGuard";
        private readonly ManualLogSource log;
        private readonly X64InlineHook hook;
        private readonly IntPtr enabled;
        private readonly IntPtr trace = IntPtr.Zero;
        private readonly ulong image;
        private bool runtimeLogged;
        private bool ready;
        // Retain an unpublished candidate if rollback cannot prove that no code still enters it.
        private static object retainedFailedCandidate;

        internal LordTeleportGuardRuntime(ManualLogSource logger, CrusaderLibraryLoadContext context, bool modEnabled, bool optionEnabled)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            if (context == null) throw new ArgumentNullException(nameof(context));
            image = unchecked((ulong)context.ModuleHandle.ToInt64());
            if (image == 0 || Shared.DebugLogHelper.CurrentNativeSha256 != LordTeleportNative.NativeHash)
                throw new InvalidOperationException("Native identity contract mismatch.");
            LordTeleportNative.ValidateLayouts();
            var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory,
                BitConverter.ToString(LordTeleportNative.EntrySignature).Replace('-', ' '),
                LordTeleportNative.EntryRva, true, "Lord teleport helper", log);
            if (resolution.Rva != LordTeleportNative.EntryRva)
                throw new InvalidOperationException("Teleport helper moved outside its hash-bound contract.");
            ValidateAnchors(context.Memory);
            ulong entry = image + LordTeleportNative.EntryRva;
            CheckLive(entry, LordTeleportNative.EntryBytes);
            enabled = Marshal.AllocHGlobal(4); Marshal.WriteInt32(enabled, 0);
#if SERP_DEVDIAG
            trace = Marshal.AllocHGlobal(LordTeleportNative.TraceBytes);
            Marshal.Copy(new byte[LordTeleportNative.TraceBytes], 0, trace, LordTeleportNative.TraceBytes);
#endif
            X64InlineHook candidate = null;
            bool claimed = false;
            try
            {
                candidate = new X64InlineHook(entry, 14, name: "BugfixesAndQoL.LordHomeTeleport");
                if (candidate.DisplacedByteCount != LordTeleportNative.DisplacedLength || candidate.IsInstalled)
                    throw new InvalidOperationException("Installed RedBird inline span differs from 16 bytes.");
                candidate.Generate((a, original, resume) =>
                {
                    LordTeleportNative.ValidateInstructions(original, resume);
                    LordTeleportNative.Emit(a, original, Address(enabled),
                        image + LordTeleportNative.UnitsRva, image + LordTeleportNative.AiLineUpRva, Address(trace));
                });
                // Compare the actual installed-backend generator, including its one automatic continuation.
                var assembler = new Assembler(64);
                LordTeleportNative.Emit(assembler, LordTeleportNative.Decode(entry), Address(enabled),
                    image + LordTeleportNative.UnitsRva, image + LordTeleportNative.AiLineUpRva, Address(trace));
                assembler.AddUnrestrictedJmp(image + LordTeleportNative.ResumeRva);
                CheckLive(Address(candidate.StubAddress), LordTeleportNative.Assemble(assembler, Address(candidate.StubAddress)));
                string bytesHash;
                using (var sha = SHA256.Create()) bytesHash = BitConverter.ToString(sha.ComputeHash(LordTeleportNative.EntryBytes)).Replace("-", "");
                var result = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Site,
                    new[] {
                        new NativeSiteSpan(LordTeleportNative.EntryRva, 16, NativeSiteMode.Exclusive, bytesHash),
                        new NativeSiteSpan(0x15AB83, 7, NativeSiteMode.Observe),
                        new NativeSiteSpan(0x28A7D, 7, NativeSiteMode.Observe),
                        new NativeSiteSpan(LordTeleportNative.StandingRescueCallRva, 5, NativeSiteMode.Observe) });
                if (!result.Succeeded) throw new InvalidOperationException("Native-site refusal: " + result.Reason);
                claimed = true;
                CheckLive(entry, LordTeleportNative.EntryBytes);
                candidate.Enable();
                byte[] expectedPatch = new byte[16]; expectedPatch[0] = 0xFF; expectedPatch[1] = 0x25;
                Array.Copy(BitConverter.GetBytes(Address(candidate.StubAddress)), 0, expectedPatch, 6, 8);
                expectedPatch[14] = expectedPatch[15] = 0x90;
                CheckLive(entry, expectedPatch);
                if (!candidate.IsInstalled || candidate.DisplacedByteCount != 16)
                    throw new InvalidOperationException("Published backend contract mismatch.");
                hook = candidate;
                // Publication is the last operation that can affect the hook. Never dispose it afterwards.
                ready = true;
                SetEnabled(modEnabled, optionEnabled);
            }
            catch
            {
                Volatile.Write(ref *(int*)enabled, 0);
                candidate?.Dispose(); // Unpublished initialization candidate only.
                if (Matches(entry, LordTeleportNative.EntryBytes))
                {
                    if (claimed) NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Site);
                    Marshal.FreeHGlobal(enabled);
                    if (trace != IntPtr.Zero) Marshal.FreeHGlobal(trace);
                }
                else retainedFailedCandidate = new object[] { candidate, enabled, trace };
                throw;
            }
        }

        internal ManualLogSource LogSource => log;

        internal void SetEnabled(bool modEnabled, bool optionEnabled)
        {
            if (enabled != IntPtr.Zero)
                Volatile.Write(ref *(int*)enabled, LordTeleportNative.ShouldEnable(ready, modEnabled, optionEnabled) ? 1 : 0);
        }

        internal void ReportInstalled()
        {
            Shared.DebugLogHelper.LogInfo(log,
                "LORD_TELEPORT_GUARD_READY: rva=0x19BA40; span=[0x19BA40,0x19BA50); displaced=16; " +
                "permanent=True; nativePredicate=True; humanControl=AILineUp; allModes=True; standingTileRescue=Vanilla; " +
                "unitManager=0x67E8400; unitHeader=0x65C; stride=0x490; ids=1..9999; " +
                "homeOffsets=0x336/0x338; nativeHash=" + LordTeleportNative.NativeHash);
        }

        internal void OnTick(int tick)
        {
            if (!runtimeLogged)
            {
                runtimeLogged = true;
                Shared.DebugLogHelper.LogInfo(log, "LORD_TELEPORT_GUARD_RUNTIME_ALIVE: tick=" + tick +
                    "; nativeHook=" + hook.IsInstalled + "; startupCleanupIndependent=True.");
            }
            FlushDiagnostics(tick);
        }

        partial void FlushDiagnostics(int tick);

        private void ValidateAnchors(ReadOnlySpan<byte> memory)
        {
            if (memory.Length <= LordTeleportNative.AiLineUpRva + 9 * 4 ||
                memory.Length <= LordTeleportNative.UnitsRva + LordTeleportNative.RecordOrigin +
                    SHCDESE.Interop.GameUnitManager.NativeUnitSlotCount * LordTeleportNative.Stride)
                throw new InvalidOperationException("Native data ranges exceed the captured image.");
            LordTeleportNative.ValidateStandingRescueCall(memory.Slice(LordTeleportNative.StandingRescueCallRva, 5), image);
            CheckRipLea(memory, 0x15AB83, Register.RBP, image + LordTeleportNative.UnitsRva);
            CheckRipLea(memory, 0x28A7D, Register.RAX, image + LordTeleportNative.AiLineUpRva);
        }

        private void CheckRipLea(ReadOnlySpan<byte> memory, int rva, Register destination, ulong target)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(memory.Slice(rva, 7).ToArray())); decoder.IP = image + (uint)rva;
            Instruction i = decoder.Decode();
            if (i.Length != 7 || i.Mnemonic != Mnemonic.Lea || i.Op0Register != destination ||
                !i.IsIPRelativeMemoryOperand || i.IPRelativeMemoryAddress != target)
                throw new InvalidOperationException("Native data anchor mismatch at 0x" + rva.ToString("X"));
        }

        private static ulong Address(IntPtr p) => unchecked((ulong)p.ToInt64());
        private static bool Matches(ulong address, byte[] expected)
        {
            byte[] actual = new byte[expected.Length]; Marshal.Copy(new IntPtr(unchecked((long)address)), actual, 0, actual.Length);
            return expected.SequenceEqual(actual);
        }
        private static void CheckLive(ulong address, byte[] expected)
        {
            if (!Matches(address, expected)) throw new InvalidOperationException("Unexpected code bytes at 0x" + address.ToString("X"));
        }
    }
}
