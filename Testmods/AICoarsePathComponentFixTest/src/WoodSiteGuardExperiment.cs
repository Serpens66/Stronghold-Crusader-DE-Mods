using APIShared;
using BepInEx.Logging;
using Iced.Intel;
using R3;
using RedBird.X64.Assembly;
using RedBird.X64.Extensions;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace AICoarsePathComponentFixTest
{
    // Opt-in native experiment. The one-time patch stays installed for process life;
    // session activation is logical and the inactive callback preserves Vanilla.
    internal sealed class WoodSiteGuardExperiment
    {
        private const int SiteRva = 0x58B26;
        private const int FallbackRva = 0x58B2E;
        private const int SuccessRva = 0x58BAE;
        private const int StubCapacity = 0x1000;
        private const string CopySaveName = "test_canari_nowoodcutters_probe.sav";
        private const string RatMapName = "spezialist 3vs5.map";
        private static readonly byte[] Original = { 0x84, 0xDB, 0x0F, 0x84, 0x80, 0, 0, 0 };

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int Predicate(int coarseX, int coarseY);

        private static readonly Predicate Callback = ShouldSkip;
        private static WoodSiteGuardExperiment current;
        private static int active;
        private static long examined, noContext, unavailable, zeroAnchor, parcel;
        private static long session;

        private readonly ManualLogSource log;
        private readonly ulong module;
        private readonly string scope;
        private readonly IDisposable started;
        private readonly IDisposable ended;
        private readonly IntPtr stub;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtection, out uint oldProtection);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        internal WoodSiteGuardExperiment(ManualLogSource logger, ulong moduleBase, string activationScope)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            module = moduleBase;
            if (activationScope != "CopyOnly" && activationScope != "RatControl")
                throw new InvalidOperationException("Unknown wood guard activation scope: " + activationScope);
            scope = activationScope;
            if (current != null) throw new InvalidOperationException("Wood guard is already installed.");
            if (IsOldOverlayEnabled())
                throw new InvalidOperationException("NearbyWoodTest.Enabled is true. Disable the old overlay before arming the wood guard.");
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, "AI wood site guard",
                    requireCurrentVersion: true) ||
                !string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The wood guard requires the audited native DLL.");
            IntPtr site = new IntPtr(checked((long)(module + SiteRva)));
            ValidateSite(site);
            IntPtr candidate = AllocateNear(module + SiteRva);
            bool published = false;
            bool patchAttempted = false;
            try
            {
                byte[] body = BuildStub(unchecked((ulong)candidate.ToInt64()),
                    module + FallbackRva, module + SuccessRva,
                    unchecked((ulong)Marshal.GetFunctionPointerForDelegate(Callback).ToInt64()));
                if (body.Length > StubCapacity)
                    throw new InvalidOperationException("Wood guard stub exceeds allocated page.");
                Marshal.Copy(body, 0, candidate, body.Length);
                if (!FlushInstructionCache(GetCurrentProcess(), candidate, new UIntPtr((uint)body.Length)))
                    throw new InvalidOperationException("Wood guard stub instruction cache flush failed.");
                byte[] jump = BuildSiteJump(module + SiteRva, unchecked((ulong)candidate.ToInt64()));
                patchAttempted = true;
                WriteInitialPatch(site, jump);
                if (!BytesMatch(site, jump))
                    throw new InvalidOperationException("Wood guard site verification failed.");
                stub = candidate;
                current = this;
                published = true;
                started = Shared.MissionEvents.Started.Subscribe(OnStarted);
                ended = Shared.MissionEvents.Ended.Subscribe(OnEnded);
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_WOOD_SITE_GUARD_READY: nativeRva=0x{SiteRva:X}; displaced=8; " +
                    $"stub=0x{candidate.ToInt64():X}; callback={Marshal.GetFunctionPointerForDelegate(Callback)}; " +
                    $"scope={scope}; logicalActivation=MissionEvents.Started; permanentPatch=True; coarseWrites=0.");
            }
            catch
            {
                if (patchAttempted && !published)
                {
                    try
                    {
                        WriteInitialPatch(site, Original); // unpublished candidate only
                        if (!BytesMatch(site, Original)) published = true;
                    }
                    catch { published = true; } // retain the stub if any branch may still target it
                }
                throw;
            }
            finally
            {
                if (!published && !VirtualFree(candidate, UIntPtr.Zero, 0x8000))
                    Shared.DebugLogHelper.LogError(log, "AI_WOOD_SITE_GUARD_SCRATCH_FREE_FAILED: " + Marshal.GetLastWin32Error());
            }
        }

        internal void OnTick(int tick)
        {
            if (tick != 1 && tick % 500 != 0) return;
            LogState("tick=" + tick);
        }

        private void OnStarted(MissionLifecycleNotification notification)
        {
            Interlocked.Increment(ref session);
            MissionContext context = notification?.Context;
            string file = Path.GetFileName(context?.FilePath ?? "");
            bool arm = scope == "CopyOnly"
                ? context != null && context.IsSave &&
                  string.Equals(file, CopySaveName, StringComparison.OrdinalIgnoreCase)
                : context != null && !context.IsSave &&
                  string.Equals(file, RatMapName, StringComparison.OrdinalIgnoreCase);
            Volatile.Write(ref active, arm ? 1 : 0);
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_WOOD_SITE_GUARD_SESSION: scope={scope}; file={file}; " +
                $"isSave={context?.IsSave}; active={arm}.");
            LogState("started");
        }

        private void OnEnded(MissionLifecycleNotification notification)
        {
            Volatile.Write(ref active, 0);
            LogState("ended");
        }

        private void LogState(string phase) => Shared.DebugLogHelper.LogInfo(log,
            $"AI_WOOD_SITE_GUARD_STATUS: session={Interlocked.Read(ref session)}; phase={phase}; " +
            $"active={Volatile.Read(ref active) != 0}; examined={Interlocked.Read(ref examined)}; " +
            $"zeroAnchor={Interlocked.Read(ref zeroAnchor)}; parcel={Interlocked.Read(ref parcel)}; " +
            $"noContext={Interlocked.Read(ref noContext)}; unavailable={Interlocked.Read(ref unavailable)}.");

        private static int ShouldSkip(int coarseX, int coarseY)
        {
            try
            {
                if (Volatile.Read(ref active) == 0 || !AiBuildDiagnostic.HasObserver ||
                    !AiBuildDiagnostic.TryGetCurrentWoodAttempt(out _, out int playerId) ||
                    playerId < 1 || playerId > 8)
                { Interlocked.Increment(ref noContext); return 0; }
                Interlocked.Increment(ref examined);
                if ((uint)coarseX >= 160 || (uint)coarseY >= 160)
                { Interlocked.Increment(ref unavailable); return 0; }
                var tiles = AiBuildDiagnostic.CaptureTiles(coarseX * 5, coarseY * 5, 3, 3);
                if (tiles.Count != 9)
                { Interlocked.Increment(ref unavailable); return 0; }
                bool bit4 = false;
                for (int i = 0; i < 9; i++)
                {
                    AiPathTileSample tile = tiles[i];
                    if (tile.Status != "ok" || tile.NativeComponent != tile.ApiComponent)
                    { Interlocked.Increment(ref unavailable); return 0; }
                    if ((tile.PropertyFlags & 4u) != 0) bit4 = true;
                }
                if (tiles[0].NativeComponent == 0)
                { Interlocked.Increment(ref zeroAnchor); return 1; }
                if (bit4)
                { Interlocked.Increment(ref parcel); return 1; }
                return 0;
            }
            catch
            { Interlocked.Increment(ref unavailable); return 0; }
        }

        private static byte[] BuildStub(ulong ip, ulong fallback, ulong success, ulong callback)
        {
            var a = new Assembler(64);
            Label vanillaFallback = a.CreateLabel();
            Label denied = a.CreateLabel();
            a.test(bl, bl);
            a.jne(vanillaFallback);
            a.pushfq();
            a.push(rax); a.push(rcx); a.push(rdx); a.push(r8);
            a.push(r9); a.push(r10); a.push(r11);
            a.sub(rsp, 0x20); // Win64 shadow space; original RSP is 16-byte aligned here.
            a.mov(ecx, r11d); a.mov(edx, r10d);
            a.mov(rax, callback); a.call(rax);
            a.add(rsp, 0x20);
            a.test(eax, eax);
            a.jne(denied);
            EmitRestore(a);
            a.AddUnrestrictedJmp(success);
            a.Label(ref denied);
            EmitRestore(a);
            a.AddUnrestrictedJmp(fallback);
            a.Label(ref vanillaFallback);
            a.AddUnrestrictedJmp(fallback);
            using (var output = new MemoryStream())
            {
                a.Assemble(new StreamCodeWriter(output), ip);
                byte[] body = output.ToArray();
                var boundaries = new HashSet<ulong>();
                var localBranches = new List<ulong>();
                int fallbackJumps = 0, successJumps = 0;
                for (int offset = 0; offset < body.Length;)
                {
                    boundaries.Add(ip + (ulong)offset);
                    byte[] remaining = new byte[body.Length - offset];
                    Buffer.BlockCopy(body, offset, remaining, 0, remaining.Length);
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(remaining), ip + (ulong)offset);
                    Instruction instruction = decoder.Decode();
                    if (instruction.IsInvalid)
                        throw new InvalidOperationException("Wood guard stub contains an invalid instruction.");
                    if (remaining.Length >= 14 && remaining[0] == 0xFF && remaining[1] == 0x25 &&
                        BitConverter.ToInt32(remaining, 2) == 0)
                    {
                        ulong target = BitConverter.ToUInt64(remaining, 6);
                        if (instruction.Length != 6 || instruction.FlowControl != FlowControl.IndirectBranch ||
                            (target != success && target != fallback))
                            throw new InvalidOperationException("Wood guard absolute jump or target is invalid.");
                        if (target == success) successJumps++; else fallbackJumps++;
                        offset += 14;
                    }
                    else
                    {
                        if (instruction.FlowControl == FlowControl.ConditionalBranch ||
                            instruction.FlowControl == FlowControl.UnconditionalBranch)
                            localBranches.Add(instruction.NearBranchTarget);
                        offset += instruction.Length;
                    }
                }
                foreach (ulong target in localBranches)
                    if (!boundaries.Contains(target))
                        throw new InvalidOperationException("Wood guard local branch misses an instruction boundary.");
                if (successJumps != 1 || fallbackJumps != 2 || localBranches.Count != 2)
                    throw new InvalidOperationException("Wood guard branch graph differs from the audited paths.");
                return body;
            }
        }

        private static void EmitRestore(Assembler a)
        {
            a.pop(r11); a.pop(r10); a.pop(r9); a.pop(r8);
            a.pop(rdx); a.pop(rcx); a.pop(rax); a.popfq();
        }

        private static byte[] BuildSiteJump(ulong site, ulong target)
        {
            long distance = checked((long)target - checked((long)site + 5));
            if (distance < int.MinValue || distance > int.MaxValue)
                throw new InvalidOperationException("Wood guard stub is not reachable by a near jump.");
            byte[] result = { 0xE9, 0, 0, 0, 0, 0x90, 0x90, 0x90 };
            Buffer.BlockCopy(BitConverter.GetBytes((int)distance), 0, result, 1, 4);
            return result;
        }

        private static void ValidateSite(IntPtr site)
        {
            if (!BytesMatch(site, Original))
                throw new InvalidOperationException("Wood guard site bytes differ from the audited eight-byte branch.");
        }

        private static bool BytesMatch(IntPtr address, byte[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
                if (Marshal.ReadByte(address, i) != expected[i]) return false;
            return true;
        }

        private static void WriteInitialPatch(IntPtr site, byte[] bytes)
        {
            if (!VirtualProtect(site, new UIntPtr((uint)bytes.Length), 0x40, out uint old))
                throw new InvalidOperationException("Wood guard could not make the initial site writable.");
            try
            {
                Marshal.Copy(bytes, 0, site, bytes.Length);
                if (!FlushInstructionCache(GetCurrentProcess(), site, new UIntPtr((uint)bytes.Length)))
                    throw new InvalidOperationException("Wood guard site instruction cache flush failed.");
            }
            finally
            {
                if (!VirtualProtect(site, new UIntPtr((uint)bytes.Length), old, out _))
                    throw new InvalidOperationException("Wood guard site protection restore failed.");
            }
        }

        private static IntPtr AllocateNear(ulong site)
        {
            const long stride = 0x1000000;
            const long granularity = 0x10000;
            long aligned = ((long)site + granularity - 1) & ~(granularity - 1);
            for (int distance = 1; distance <= 120; distance++)
                foreach (int direction in new[] { 1, -1 })
                {
                    long address = aligned + direction * distance * stride;
                    if (address <= 0 || Math.Abs(address - (long)site) >= 0x70000000) continue;
                    IntPtr candidate = VirtualAlloc(new IntPtr(address), new UIntPtr((uint)StubCapacity), 0x3000, 0x40);
                    if (candidate != IntPtr.Zero) return candidate;
                }
            throw new InvalidOperationException("No near executable page is available for the wood guard.");
        }

        private static bool IsOldOverlayEnabled()
        {
            string path = Path.Combine(BepInEx.Paths.ConfigPath, "AIBuildDiagnoseTest_Serp.cfg");
            if (!File.Exists(path)) return false;
            bool section = false;
            foreach (string line in File.ReadAllLines(path))
            {
                string value = line.Trim();
                if (value.StartsWith("[", StringComparison.Ordinal))
                    section = string.Equals(value, "[NearbyWoodTest]", StringComparison.OrdinalIgnoreCase);
                else if (section && value.StartsWith("Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    int equals = value.IndexOf('=');
                    if (equals > 0 && bool.TryParse(value.Substring(equals + 1).Trim(), out bool enabled))
                        return enabled;
                    throw new InvalidOperationException("NearbyWoodTest.Enabled could not be parsed; guard remains unavailable.");
                }
            }
            return false;
        }
    }
}
