using APIShared.NativeSites;
using APIShared.Patching;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Iced.Intel;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;
using Instruction = Iced.Intel.Instruction;
using FlowControl = Iced.Intel.FlowControl;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // Optional compatibility owner for the relevant audited Fixes contracts.
    // Gatehouse-farmer and mounted-keep recovery remain outside this replacement.
    internal sealed unsafe class FixesIntegration
    {
        private readonly string OwnerGuid;
        private const string SiteName = "TrappedUnitRescue.FixesWorkerDeletionGuard";
        private const string MoveFunctionHash = "D81EEBC55A1FB0CFEEB25D0B0D1CCEDE5C9F545E3CCCCAC5119A556C7B43E9E1";
        private const int MoveFunctionRva = 0x196280, MoveFunctionLength = 1293;
        internal const int WorkerGuardRva = 0x196342;
        internal const int WorkerGuardLength = 16;
        internal const int WorkerGuardResumeRva = WorkerGuardRva + WorkerGuardLength;
        // Fixes requests fourteen bytes: MOV ECX (6) + TEST EAX (2) + MOV R11D (6)
        // end exactly at 196360. The following four-byte MOVSX remains live Vanilla.
        private const int FixesWorkerLength = 14, UnitManagerRva = 0x67E8400;
        internal static readonly byte[] WorkerGuardBytes = {
            0x49,0x8B,0xCF,0x41,0x0F,0xBF,0xC4,0x89,0x44,0x24,0x70,0xE8,0xEE,0x04,0,0 };

        private readonly ManualLogSource log;
        private readonly NativeRelocation native;
        // Every published callback, generated buffer and activation cell survives until exit.
        private static FixesIntegration processOwner;
        private SafeIlHook wallHook;
        private X64InlineHook workerHook;
        private IntPtr activation;
        private bool attempted;
        private bool ready;
        private bool workerDeletionInstalled;

        internal FixesIntegration(string ownerGuid, ManualLogSource logger, NativeRelocation auditedNative)
        {
            if (string.IsNullOrWhiteSpace(ownerGuid)) throw new ArgumentException("A stable caller GUID is required.", nameof(ownerGuid));
            OwnerGuid = ownerGuid;
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            native = auditedNative ?? throw new ArgumentNullException(nameof(auditedNative));
        }
        internal bool Ready => ready;
        // Installation is immutable after startup; config may be changed later by other mods.
        internal bool WorkerDeletionInstalled => ready ? workerDeletionInstalled :
            throw new InvalidOperationException("Fixes worker installation has not been validated.");
        internal bool Active => activation != IntPtr.Zero && Volatile.Read(ref *(int*)activation) != 0;

        internal void SetEnabled(bool enabled)
        {
            if (activation != IntPtr.Zero) Volatile.Write(ref *(int*)activation, ready && enabled ? 1 : 0);
        }

        internal void RecheckPublishedHooks()
        {
            // Includes editor starts. Never repair a published span; keep the replacement
            // dormant permanently if a later writer invalidates its native contract.
            if (Shared.NativePatchGuard.Recheck() == 0) return;
            ready = false;
            SetEnabled(false);
        }

        internal void Initialize(CrusaderLibraryLoadContext context)
        {
            if (attempted) return;
            attempted = true;
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (processOwner != null && !ReferenceEquals(processOwner, this))
            {
                Log("FIXES_INTEGRATION_INACTIVE: a process-lifetime compatibility owner already exists.");
                return;
            }
            try
            {
                if (context.ModuleHandle != native.ImageBase)
                    throw new InvalidOperationException("Fixes compatibility context differs from the already audited native image.");
                if (!Chainloader.PluginInfos.TryGetValue("fixes", out PluginInfo info))
                {
                    ready = true;
                    Log("Fixes absent; no compatibility suppression required.");
                    return;
                }
                if (info.Instance == null) throw new InvalidOperationException("Fixes startup has not completed.");
                Assembly assembly = info.Instance.GetType().Assembly;
                FixesManagedContract.Validate(assembly);
                MethodInfo wall = RequireWallMethod(assembly);
                ProbeWallPatch(wall);
                HookHandle<X64InlineHook> worker = ReadWorkerHandle(assembly);
                ulong image = Address(context.ModuleHandle);
                bool needsWorker = worker != null && worker.Hook != null;
                if (needsWorker) ValidateNativeAndFixesWorker(context, worker, image);

                activation = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(activation, 0);
                processOwner = this;
                // Both callbacks remain dormant until every required owner is installed.
                if (!SafeIlHook.TryInstall(OwnerGuid, "TrappedUnitRescue.FixesWallPre", wall,
                    PatchWall, log, out wallHook, out string failure))
                    throw new InvalidOperationException("Fixes wall guard: " + failure);
                if (needsWorker) InstallWorkerGuard(context, image);
                workerDeletionInstalled = needsWorker;
                ready = true;
                Log("Fixes compatibility ready; wall Pre and installed moat-worker deletion are logically gated. Gatehouse-farmer and mounted-keep recovery retained.");
            }
            catch (Exception error)
            {
                SetEnabled(false);
                ready = false;
                // A published IL hook or attempted native Enable is never removed on failure.
                // Its common activation cell remains zero and is retained with this owner.
                processOwner = this;
                log.LogError("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] FIXES_INTEGRATION_INACTIVE: " + error);
            }
        }

        private static MethodInfo RequireWallMethod(Assembly assembly)
        {
            Type type = assembly.GetType("Fixes.Events.FixesAIEvents", true);
            MethodInfo method = type.GetMethod("OnAIBuildWall", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(AIBuildWallEventArgs) }, null);
            if (method == null || method.ReturnType != typeof(void) || method.GetMethodBody() == null)
                throw new MissingMethodException(type.FullName, "OnAIBuildWall");
            using (var definition = new DynamicMethodDefinition(method))
            {
                var code = definition.Definition.Body.Instructions;
                if (!code.Any(i => i.Operand is MethodReference m && m.Name == "get_Phase") ||
                    !code.Any(i => i.Operand is MethodReference m && m.Name == "BuildSafePositionMap") ||
                    !code.Any(i => i.Operand is MethodReference m && m.Name == "SetTileHeight"))
                    throw new InvalidOperationException("Fixes wall Pre/Post IL contract changed.");
            }
            return method;
        }

        private void ProbeWallPatch(MethodInfo method)
        {
            using (var definition = new DynamicMethodDefinition(method))
            using (var il = new ILContext(definition.Definition))
            {
                il.ReferenceBag = RuntimeILReferenceBag.Instance;
                il.Invoke(PatchWall);
                definition.Generate();
            }
        }

        private void PatchWall(ILContext il)
        {
            var cursor = new ILCursor(il);
            ILLabel original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<AIBuildWallEventArgs, bool>>(SuppressWallPre);
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        }

        private bool SuppressWallPre(AIBuildWallEventArgs args) => Active && args != null && args.Phase == EventHookPhase.Pre;

        private static HookHandle<X64InlineHook> ReadWorkerHandle(Assembly assembly)
        {
            Type owner = assembly.GetType("Fixes.Detours.FixesUnitDetours", true);
            FieldInfo field = owner.GetField("c_game_unit_issueorder_movehere_hook", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(HookHandle<X64InlineHook>))
                throw new InvalidOperationException("Fixes worker hook handle contract changed.");
            var handle = (HookHandle<X64InlineHook>)field.GetValue(null);
            if (handle == null || handle.Failure != null)
                throw new InvalidOperationException("Fixes worker hook initialization failed.");
            return handle;
        }

        private static void ValidateNativeAndFixesWorker(CrusaderLibraryLoadContext context, HookHandle<X64InlineHook> handle, ulong image)
        {
            // NativeRelocation already validated the canonical full-file hash; Initialize
            // binds this pristine snapshot to that immutable module identity before use.
            if (context.Memory.Length < MoveFunctionRva + MoveFunctionLength ||
                Hash(context.Memory.Slice(MoveFunctionRva, MoveFunctionLength).ToArray()) != MoveFunctionHash)
                throw new InvalidOperationException("Pristine full unit-move body differs from the audited control-flow closure.");
            ValidateNoInteriorEdges(context.Memory.Slice(MoveFunctionRva, MoveFunctionLength).ToArray(), image);
            Shared.NativePatchGuard.RequireUnpatched(image + WorkerGuardRva, WorkerGuardLength,
                "TrappedUnitRescue.FixesWorkerGuard", SiteName);
            X64InlineHook hook = handle.Hook;
            if (!handle.IsInstalled || !hook.IsInstalled || hook.DisplacedByteCount != FixesWorkerLength ||
                handle.ResolvedAddress != image + WorkerGuardResumeRva || hook.TargetAddress != image + WorkerGuardResumeRva ||
                hook.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException("Fixes worker hook target/span/publication differs: handleInstalled=" + handle.IsInstalled +
                    "; hookInstalled=" + hook.IsInstalled + "; displaced=" + hook.DisplacedByteCount +
                    "; resolved=0x" + handle.ResolvedAddress.ToString("X") + "; target=0x" + hook.TargetAddress.ToString("X") +
                    "; expectedTarget=0x" + (image + WorkerGuardResumeRva).ToString("X") + "; stub=0x" + Address(hook.StubAddress).ToString("X"));
            var original = Decode(context.Memory.Slice(WorkerGuardResumeRva, FixesWorkerLength).ToArray(), image + WorkerGuardResumeRva);
            ulong stub = Address(hook.StubAddress);
            var assembler = new Assembler(64);
            EmitExpectedFixesWorker(assembler, original, image + UnitManagerRva);
            assembler.AddUnrestrictedJmp(image + WorkerGuardResumeRva + FixesWorkerLength);
            CheckLive(stub, Assemble(assembler, stub));
            CheckInlineEntry(image + WorkerGuardResumeRva, stub, FixesWorkerLength);
        }

        // The whole hash-bound original function and all recorded external xrefs were audited.
        // Recheck every direct local edge at runtime; no indirect branch/table is accepted here.
        private static void ValidateNoInteriorEdges(byte[] body, ulong image)
        {
            foreach (Instruction i in Decode(body, image + MoveFunctionRva))
            {
                if (i.FlowControl == FlowControl.IndirectBranch || i.FlowControl == FlowControl.IndirectCall)
                    throw new InvalidOperationException("Unknown indirect unit-move control flow.");
                if (i.OpCount > 0 && (i.Op0Kind == OpKind.NearBranch16 || i.Op0Kind == OpKind.NearBranch32 || i.Op0Kind == OpKind.NearBranch64) &&
                    i.NearBranchTarget > image + WorkerGuardRva && i.NearBranchTarget < image + WorkerGuardResumeRva)
                    throw new InvalidOperationException("A unit-move edge enters the worker-guard interior.");
            }
        }

        private void InstallWorkerGuard(CrusaderLibraryLoadContext context, ulong image)
        {
            bool claimed = false, enableAttempted = false;
            X64InlineHook candidate = null;
            try
            {
                Shared.NativePatchGuard.RequireUnpatched(image + WorkerGuardRva, WorkerGuardLength,
                    "TrappedUnitRescue.FixesWorkerGuard", SiteName);
                candidate = new X64InlineHook(image + WorkerGuardRva, 14, name: "TrappedUnitRescue.FixesWorkerGuard");
                if (candidate.DisplacedByteCount != WorkerGuardLength || candidate.IsInstalled)
                    throw new InvalidOperationException("Worker guard backend span differs from 16 bytes.");
                candidate.Generate((a, original, resume) => EmitWorkerGuard(a, original, Address(activation), resume));
                var expected = new Assembler(64);
                EmitWorkerGuard(expected, Decode(WorkerGuardBytes, image + WorkerGuardRva), Address(activation), image + WorkerGuardResumeRva);
                expected.AddUnrestrictedJmp(image + WorkerGuardResumeRva);
                CheckLive(Address(candidate.StubAddress), Assemble(expected, Address(candidate.StubAddress)));
                var claim = NativeSiteRegistry.Register(OwnerGuid, SiteName, new[] {
                    new NativeSiteSpan(WorkerGuardRva, WorkerGuardLength, NativeSiteMode.Exclusive, Hash(WorkerGuardBytes)),
                    new NativeSiteSpan(0x196840, 1, NativeSiteMode.CallOnly) });
                if (!claim.Succeeded) throw new InvalidOperationException("Worker guard native-site refusal: " + claim.Reason);
                claimed = true;
                CheckLive(image + WorkerGuardRva, WorkerGuardBytes);
                workerHook = candidate; // Root before any attempt to publish executable code.
                Shared.NativePatchGuard.RequireUnpatched(candidate, SiteName);
                enableAttempted = true;
                candidate.Enable();
                CheckInlineEntry(image + WorkerGuardRva, Address(candidate.StubAddress), WorkerGuardLength);
                if (!candidate.IsInstalled) throw new InvalidOperationException("Worker guard was not published.");
                Shared.NativePatchGuard.Watch(candidate, SiteName);
                Log("Worker guard installed at RVA 0x196342, displaced 16 bytes, resumes at the validated adjacent Fixes hook.");
            }
            catch
            {
                if (!enableAttempted)
                {
                    candidate?.Dispose();
                    if (claimed) NativeSiteRegistry.Unregister(OwnerGuid, SiteName);
                }
                throw;
            }
        }

        // A pure native gate is required: ContextHook wrappers modify flags before their
        // callback. EAX must retain the real moat probe result for Vanilla's later TEST.
        internal static void EmitWorkerGuard(Assembler a, ReadOnlySpan<Instruction> original, ulong activationAddress, ulong resume)
        {
            if (original.Length != 4 || original[0].Mnemonic != Mnemonic.Mov || original[1].Mnemonic != Mnemonic.Movsx ||
                original[2].Mnemonic != Mnemonic.Mov || original[3].Mnemonic != Mnemonic.Call ||
                original[0].IP + WorkerGuardLength != resume)
                throw new InvalidOperationException("Worker guard displaced instruction contract changed.");
            foreach (Instruction instruction in original.CloneInstructionsWithoutIP()) a.AddInstruction(instruction);
            // Original CALL runs with original RSP/arguments. No CALL is made while saved.
            a.pushfq(); a.push(rax);
            a.mov(rax, activationAddress);
            a.cmp(__byte_ptr[rax], 0);
            a.pop(rax);
            var inactive = a.CreateLabel();
            var finish = a.CreateLabel();
            a.je(inactive);
            a.popfq();
            a.cmp(eax, eax); // ZF=1 skips only Fixes deletion; EAX is unchanged.
            a.jmp(finish);
            a.Label(ref inactive);
            a.popfq(); // Inactive mode preserves exactly the flags returned by the live probe.
            a.Label(ref finish);
            a.nop(); // Distinct Iced labels require a real shared instruction.
            // X64InlineHook appends the sole continuation, preserving the final flags.
        }

        internal static void EmitExpectedFixesWorker(Assembler a, ReadOnlySpan<Instruction> original, ulong units)
        {
            var skip = a.CreateLabel(); var inRange = a.CreateLabel(); var low = a.CreateLabel();
            var done = a.CreateLabel(); var illegal = a.CreateLabel();
            a.jz(skip); a.push(r8); a.push(r9); a.mov(r8, units); a.add(r8, 0x6E6);
            a.imul(r9, rsi, 0x490); a.add(r8, r9); a.movsx(r8, __word_ptr[r8]);
            a.cmp(r8d, 70); a.jb(inRange); a.clc(); a.jmp(done);
            a.Label(ref inRange); a.cmp(r8d, 64); a.jb(low);
            a.sub(r8d, 64); a.mov(r9d, 0x3F); a.bt(r9d, r8d); a.jmp(done);
            a.Label(ref low); a.mov(r9, 0x82780C1F803FFFFEUL); a.bt(r9, r8);
            a.Label(ref done); a.jc(illegal); a.pop(r9); a.pop(r8); a.jmp(skip);
            a.Label(ref illegal); a.mov(r8, units); a.add(r8, 0x6E4); a.imul(r9, rsi, 0x490);
            a.add(r8, r9); a.mov(__word_ptr[r8], (short)AliveState.MarkedForDeletion); a.pop(r9); a.pop(r8);
            a.Label(ref skip); a.AddInstructions(original);
        }

        internal static Instruction[] Decode(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
            var items = new System.Collections.Generic.List<Instruction>();
            ulong end = address + (uint)bytes.Length;
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || instruction.NextIP > end) throw new InvalidOperationException("Invalid native instruction extent.");
                items.Add(instruction);
            }
            return items.ToArray();
        }

        internal static byte[] Assemble(Assembler a, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                if (!a.TryAssemble(new StreamCodeWriter(stream), address, out string error, out _))
                    throw new InvalidOperationException("Worker gate assembly failed: " + error);
                return stream.ToArray();
            }
        }

        private static void CheckInlineEntry(ulong entry, ulong stub, int length)
        {
            byte[] expected = Enumerable.Repeat((byte)0x90, length).ToArray();
            expected[0] = 0xFF; expected[1] = 0x25;
            expected[2] = expected[3] = expected[4] = expected[5] = 0;
            Array.Copy(BitConverter.GetBytes(stub), 0, expected, 6, 8);
            CheckLive(entry, expected);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryInfo
        {
            internal IntPtr BaseAddress, AllocationBase;
            internal uint AllocationProtect;
            internal UIntPtr RegionSize;
            internal uint State, Protect, Type;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr VirtualQuery(IntPtr address, out MemoryInfo info, UIntPtr size);

        private static void CheckLive(ulong address, byte[] expected)
        {
            if (address == 0 || VirtualQuery(new IntPtr((long)address), out MemoryInfo info,
                (UIntPtr)Marshal.SizeOf(typeof(MemoryInfo))) == UIntPtr.Zero)
                throw new InvalidOperationException("Native code memory query failed.");
            ulong start = Address(info.BaseAddress), count = info.RegionSize.ToUInt64();
            uint access = info.Protect & 0xFF;
            if (info.State != 0x1000 || (info.Protect & 0x100) != 0 ||
                (access != 0x20 && access != 0x40 && access != 0x80) || address < start ||
                address - start > count || (ulong)expected.Length > count - (address - start))
                throw new InvalidOperationException("Native code memory is unreadable or crosses a region boundary.");
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(new IntPtr((long)address), actual, 0, actual.Length);
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException("Unexpected native/Fixes code at 0x" + address.ToString("X"));
        }

        private static ulong Address(IntPtr pointer) => unchecked((ulong)pointer.ToInt64());
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
        private void Log(string message) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + message);
    }
}
