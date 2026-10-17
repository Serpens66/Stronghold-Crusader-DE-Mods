using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.UnitCommands
{
    internal interface IFormationSpeechNative
    {
        int CaptureVoice(IntPtr tribeManager, int tribe);
        int CaptureRepresentative();
        void Emit(FormationSpeechRequest request);
    }

    // Callable adapters only. No executable patch, additional hook or movement stager.
    internal sealed class FormationSpeechNative : IFormationSpeechNative
    {
        internal const int SelectedVoiceRva = 0x11A1B0, RepresentativeRva = 0x18D460;
        internal const int SpeechRva = 0x3BC0, BusyRva = 0xE0E0;
        internal const int UnitManagerRva = 0x67E8400, CurrentUnitRva = 0x9302C4;
        internal const int DialogueManagerRva = 0x64C4490, SoundManagerRva = 0x88E72D0;
        private readonly IntPtr module;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SelectedVoiceDelegate(IntPtr manager, int tribe, IntPtr count);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RepresentativeDelegate(IntPtr manager, int firstId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int BusyDelegate(IntPtr manager);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void SpeechDelegate(IntPtr manager, int type, int response, int tile);
        private readonly SelectedVoiceDelegate selectedVoice;
        private readonly RepresentativeDelegate representative;
        private readonly BusyDelegate busy;
        private readonly SpeechDelegate speech;

        // Whole-instruction signatures; RIP operands are wildcarded for the executable-only scan.
        internal static readonly string[] Patterns = {
            "40 53 48 81 EC A0 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 90 01 00 00",
            "48 89 5C 24 18 55 48 8D 6C 24 80 48 81 EC 80 01 00 00 48 8B 05 ?? ?? ?? ??",
            "40 53 55 56 57 48 83 EC 78 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 60",
            "40 53 48 83 EC 20 48 63 05 ?? ?? ?? ?? 48 8B D9 48 69 D0 3C 58 00 00 48 8D 05 ?? ?? ?? ??"
        };
        internal static readonly int[] ReferenceRvas = { SelectedVoiceRva, RepresentativeRva, SpeechRva, BusyRva };

        internal FormationSpeechNative(IntPtr module, ReadOnlySpan<byte> image, Action<string> resolved)
        {
            // Fixed data addresses are valid only under FormationFeature's canonical hash gate.
            if (module == IntPtr.Zero || image.Length < SoundManagerRva + 0x43B0)
                throw new InvalidOperationException("Formation speech native image is unavailable.");
            this.module = module;
            var targets = new IntPtr[ReferenceRvas.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                int rva = Resolve(image, ReferenceRvas[index], Patterns[index], out string method);
                targets[index] = IntPtr.Add(module, rva);
                resolved($"Formation speech target 0x{ReferenceRvas[index]:X}: {method}, RVA=0x{rva:X}.");
            }
            selectedVoice = Marshal.GetDelegateForFunctionPointer<SelectedVoiceDelegate>(targets[0]);
            representative = Marshal.GetDelegateForFunctionPointer<RepresentativeDelegate>(targets[1]);
            speech = Marshal.GetDelegateForFunctionPointer<SpeechDelegate>(targets[2]);
            busy = Marshal.GetDelegateForFunctionPointer<BusyDelegate>(targets[3]);
        }

        public int CaptureVoice(IntPtr tribeManager, int tribe) => selectedVoice(tribeManager, tribe, IntPtr.Zero);
        public int CaptureRepresentative()
        {
            // 18D460 scans using the current-unit global. Its query must not leak that scratch write.
            IntPtr current = IntPtr.Add(module, CurrentUnitRva);
            int previous = Marshal.ReadInt32(current);
            try { return representative(IntPtr.Add(module, UnitManagerRva), 1); }
            finally { Marshal.WriteInt32(current, previous); }
        }
        public void Emit(FormationSpeechRequest request)
        {
            if (busy(IntPtr.Add(module, SoundManagerRva)) != 0) return;
            speech(IntPtr.Add(module, DialogueManagerRva), (int)request.VoiceType,
                request.Response, request.TargetTileId);
        }

        internal static int Resolve(ReadOnlySpan<byte> image, int referenceRva, string pattern, out string method)
        {
            string[] tokens = pattern.Split(' ');
            int[] bytes = Array.ConvertAll(tokens, token => token == "??" ? -1 : Convert.ToInt32(token, 16));
            if (image.Length < 0x40 || image[0] != 'M' || image[1] != 'Z')
                throw new InvalidOperationException("Invalid formation speech PE image.");
            int pe = ReadInt(image, 0x3C);
            if (pe < 0 || pe > image.Length - 24 || image[pe] != 'P' || image[pe + 1] != 'E' ||
                image[pe + 2] != 0 || image[pe + 3] != 0)
                throw new InvalidOperationException("Invalid formation speech PE header.");
            int count = ReadUShort(image, pe + 6);
            int table = checked(pe + 24 + ReadUShort(image, pe + 20));
            int found = -1;
            for (int section = 0; section < count; section++)
            {
                int header = checked(table + section * 40);
                if (header < 0 || header > image.Length - 40)
                    throw new InvalidOperationException("Invalid formation speech section table.");
                if ((ReadInt(image, header + 36) & 0x20000000) == 0) continue;
                int start = ReadInt(image, header + 12), length = ReadInt(image, header + 8);
                if (start < 0 || length < 0 || start > image.Length - length)
                    throw new InvalidOperationException("Invalid formation speech executable section.");
                for (int offset = start; offset <= start + length - bytes.Length; offset++)
                {
                    int index = 0;
                    while (index < bytes.Length && (bytes[index] < 0 || image[offset + index] == bytes[index])) index++;
                    if (index != bytes.Length) continue;
                    if (found >= 0) throw new InvalidOperationException("Ambiguous formation speech entry.");
                    found = offset;
                }
            }
            if (found < 0) throw new InvalidOperationException("Formation speech entry unavailable, including pattern fallback.");
            method = found == referenceRva ? "reference-rva" : "executable-pattern-fallback";
            return found;
        }
        private static int ReadInt(ReadOnlySpan<byte> image, int offset) =>
            BitConverter.ToInt32(image.Slice(offset, 4).ToArray(), 0);
        private static int ReadUShort(ReadOnlySpan<byte> image, int offset) =>
            BitConverter.ToUInt16(image.Slice(offset, 2).ToArray(), 0);
    }
}
