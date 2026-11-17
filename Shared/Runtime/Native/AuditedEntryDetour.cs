using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;

namespace Shared
{
    /// <summary>
    /// Permanent RedBird detour on an audited native function entry (NativeX64 backend, Indirect scheme).
    /// Replaces MonoMod NativeDetour, whose trampoline rewrites the entry before and after every call of the
    /// original: that costs two code writes per call and silently removes any patch another mod placed on
    /// the entry later. Here the displaced entry runs relocated in a trampoline and the entry is written
    /// exactly once.
    /// Contract (AGENTS/native-hooks.md): the entry bytes are checked in the unchanged load image, the
    /// candidate (scheme, displaced length, trampoline replay and continuation) before Enable, the live
    /// span by NativePatchGuard (foreign patches), and the patch form after Enable. No live probes; the
    /// offline test runs the installed backend on the real entries.
    /// </summary>
    internal static class AuditedEntryDetour
    {
        private static readonly object Gate = new object();
        // Published detours and their callbacks stay rooted for the process lifetime (never unpatched).
        private static readonly List<object> Roots = new List<object>();

        /// <summary>
        /// Creates and checks an unpublished candidate; nothing is written to the game code. The live entry is
        /// compared with the load snapshot of NativePatchGuard first, so RedBird never decodes a foreign patch
        /// as game code (an Error names the other patch). On failure nothing is left behind.
        /// </summary>
        internal static NativeDetour<T> Prepare<T>(ReadOnlySpan<byte> loadImage, ulong moduleBase, int rva,
            byte[] entry, T callback, string feature) where T : Delegate
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (entry == null || entry.Length < 6) throw new ArgumentException(feature + ": the audited entry must cover the 6-byte Indirect patch.", nameof(entry));
            if (rva <= 0 || rva > loadImage.Length - entry.Length)
                throw new InvalidOperationException(feature + ": entry RVA 0x" + rva.ToString("X") + " is outside the load image.");
            for (int index = 0; index < entry.Length; index++)
                if (loadImage[rva + index] != entry[index])
                    throw new InvalidOperationException(feature + ": load image differs from the audited entry at RVA 0x" +
                        rva.ToString("X") + "+" + index + ".");
            ulong target = moduleBase + (ulong)rva;
            Instruction[] audited = DecodeEntry(entry, target, feature);
            NativePatchGuard.RequireUnpatched(target, entry.Length, feature, feature);

            var request = new DetourRequest<T> { Name = feature, TargetAddress = target, Callback = callback };
            var candidate = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<T>;
            try
            {
                if (candidate == null || candidate.Scheme.ToString() != "Indirect" ||
                    candidate.DisplacedByteCount != entry.Length || candidate.TargetAddress != target ||
                    candidate.IsInstalled || candidate.ChainDepth != 1)
                    throw new InvalidOperationException(feature + ": detour candidate differs from the audited Indirect/" +
                        entry.Length + " contract (scheme=" + candidate?.Scheme + ", displaced=" + candidate?.DisplacedByteCount + ").");
                VerifyTrampoline(candidate.TrampolineAddress, candidate.TrampolineSize, entry, audited, target, feature);
            }
            catch
            {
                candidate?.Dispose(); // Unpublished candidate only.
                throw;
            }
            return candidate;
        }

        /// <summary>
        /// Writes the prepared detour. A foreign patch on the span throws before anything is written.
        /// Once Enable has been attempted the detour is rooted and never rolled back; the caller must have
        /// set its original delegate before this call so its callback can always reach Vanilla.
        /// </summary>
        internal static void Publish<T>(NativeDetour<T> candidate, T callback, string feature) where T : Delegate
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            NativePatchGuard.RequireUnpatched(candidate, feature);
            lock (Gate) { Roots.Add(candidate); Roots.Add(callback); } // From here on WasPublished is true.
            candidate.Enable();
            VerifyPatch(candidate, feature);
            NativePatchGuard.Watch(candidate, feature);
        }

        /// <summary>
        /// True once <see cref="Publish"/> has attempted Enable for this candidate. Such a detour stays rooted
        /// and must never be disposed; a candidate for which this is false wrote nothing and may be disposed.
        /// </summary>
        internal static bool WasPublished(object candidate)
        {
            lock (Gate) return Roots.Contains(candidate);
        }

        // The audited entry must decode into whole instructions that end exactly at the displaced length.
        // Plain instructions must be replayed with the same meaning; a conditional branch must keep its target.
        private static Instruction[] DecodeEntry(byte[] entry, ulong target, string feature)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(entry), target);
            var instructions = new List<Instruction>();
            while (decoder.IP < target + (ulong)entry.Length)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid)
                    throw new InvalidOperationException(feature + ": audited entry does not decode.");
                bool plain = instruction.FlowControl == FlowControl.Next && !instruction.IsIPRelativeMemoryOperand;
                bool branch = instruction.FlowControl == FlowControl.ConditionalBranch &&
                    instruction.Op0Kind == OpKind.NearBranch64 &&
                    (instruction.NearBranchTarget < target || instruction.NearBranchTarget >= target + (ulong)entry.Length);
                if (!plain && !branch)
                    throw new InvalidOperationException(feature + ": audited entry contains an unsupported instruction " +
                        instruction.Code + ".");
                instructions.Add(instruction);
            }
            if (decoder.IP != target + (ulong)entry.Length)
                throw new InvalidOperationException(feature + ": audited entry crosses an instruction boundary.");
            return instructions.ToArray();
        }

        private static void VerifyTrampoline(IntPtr trampoline, int size, byte[] entry, Instruction[] audited,
            ulong target, string feature)
        {
            // The trampoline may be shorter than the entry: RedBird re-encodes relocated instructions (for
            // example push rbx 40 53 -> 53), so instructions are compared by meaning, not by bytes.
            if (trampoline == IntPtr.Zero || size <= 0)
                throw new InvalidOperationException(feature + ": detour trampoline is missing.");
            var bytes = new byte[Math.Min(size, entry.Length + 32)]; // Never read past the trampoline.
            Marshal.Copy(trampoline, bytes, 0, bytes.Length);
            ulong start = unchecked((ulong)trampoline.ToInt64());
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes), start);
            foreach (Instruction original in audited)
            {
                Instruction relocated = decoder.Decode();
                if (decoder.LastError != DecoderError.None || relocated.IsInvalid ||
                    checked((int)(relocated.IP - start)) + relocated.Length > bytes.Length)
                    throw new InvalidOperationException(feature + ": detour trampoline does not decode.");
                if (original.FlowControl == FlowControl.Next)
                {
                    if (relocated.FlowControl != FlowControl.Next || relocated.Code != original.Code ||
                        relocated.IsIPRelativeMemoryOperand || Format(relocated) != Format(original))
                        throw new InvalidOperationException(feature + ": detour trampoline does not replay the entry (" +
                            Format(original) + " -> " + Format(relocated) + ").");
                }
                else if (relocated.FlowControl != FlowControl.ConditionalBranch ||
                    relocated.ConditionCode != original.ConditionCode ||
                    relocated.Op0Kind != OpKind.NearBranch64 ||
                    relocated.NearBranchTarget != original.NearBranchTarget)
                {
                    throw new InvalidOperationException(feature + ": relocated branch of the entry changed its condition or target.");
                }
            }
            Instruction jump = decoder.Decode();
            if (decoder.LastError != DecoderError.None || jump.IsInvalid ||
                checked((int)(jump.IP - start)) + jump.Length > bytes.Length)
                throw new InvalidOperationException(feature + ": detour trampoline has no continuation jump.");
            ulong continuation;
            if (jump.FlowControl == FlowControl.UnconditionalBranch && jump.Op0Kind == OpKind.NearBranch64)
                continuation = jump.NearBranchTarget;
            else if (jump.FlowControl == FlowControl.IndirectBranch && jump.IsIPRelativeMemoryOperand)
                continuation = unchecked((ulong)Marshal.ReadInt64((IntPtr)(long)jump.IPRelativeMemoryAddress));
            else throw new InvalidOperationException(feature + ": detour trampoline has no continuation jump.");
            if (continuation != target + (ulong)entry.Length)
                throw new InvalidOperationException(feature + ": detour trampoline continues at the wrong address.");
        }

        // Operands and mnemonic of a non-IP-relative instruction, independent of its encoding.
        private static string Format(Instruction instruction)
        {
            var output = new StringOutput();
            new NasmFormatter().Format(instruction, output);
            return output.ToStringAndReset();
        }

        private static void VerifyPatch<T>(NativeDetour<T> detour, string feature) where T : Delegate
        {
            ulong target = detour.TargetAddress;
            if (!detour.IsInstalled || detour.Scheme.ToString() != "Indirect" ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero || detour.TrampolineAddress == IntPtr.Zero ||
                detour.ChainDepth != 1)
                throw new InvalidOperationException(feature + ": installed detour contract differs.");
            var patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException(feature + ": installed detour patch differs.");
        }
    }
}
