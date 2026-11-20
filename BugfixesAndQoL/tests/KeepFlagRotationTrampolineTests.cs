using System;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal static class KeepFlagRotationTrampolineTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Invoke();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Mark(int player);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr bytes, uint allocationType, uint protection);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

        internal static void Run(Action<bool, string> check)
        {
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (page == IntPtr.Zero)
            {
                check(false, "allocate private executable page for the two main flag RedBird fixtures");
                return;
            }

            try
            {
                for (int path = 0; path < KeepFlagRotationNative.OriginalBytes.Length; path++)
                    CheckPath(page, path, check);
            }
            finally
            {
                VirtualFree(page, UIntPtr.Zero, 0x8000);
            }
        }

        private static void CheckPath(IntPtr page, int path, Action<bool, string> check)
        {
            IntPtr entry = page + 0x100;
            IntPtr camera = page + 0x200;
            IntPtr active = page + 0x204;
            IntPtr observed = page + 0x208;
            int markedPlayer = 0;
            Mark mark = player => markedPlayer = player;
            var prefix = new Assembler(64);
            prefix.push(rsi);
            prefix.push(r12);
            prefix.push(r13);
            prefix.mov(esi, 3);
            prefix.mov(r12d, 19);
            prefix.mov(r13d, 29);
            prefix.mov(r8d, 39);
            prefix.mov(r9d, 49);
            prefix.mov(edx, 59);
            prefix.mov(ecx, 69);
            prefix.jmp(Address(entry));
            Write(prefix, page);

            byte[] bytes = (byte[])KeepFlagRotationNative.OriginalBytes[path].Clone();
            BitConverter.GetBytes(checked((int)(camera.ToInt64() - (entry.ToInt64() + 6)))).CopyTo(bytes, 2);
            Marshal.Copy(bytes, 0, entry, bytes.Length);

            var continuation = new Assembler(64);
            continuation.mov(r11, Address(observed));
            continuation.mov(__dword_ptr[r11], edx);
            continuation.mov(__dword_ptr[r11 + 4], ecx);
            continuation.mov(__dword_ptr[r11 + 8], r8d);
            continuation.mov(__dword_ptr[r11 + 12], r9d);
            continuation.mov(__dword_ptr[r11 + 16], r12d);
            continuation.pushfq();
            continuation.pop(r10);
            continuation.mov(__dword_ptr[r11 + 20], r10d);
            continuation.pop(r13);
            continuation.pop(r12);
            continuation.pop(rsi);
            continuation.ret();
            Write(continuation, entry + bytes.Length);

            var invoke = Marshal.GetDelegateForFunctionPointer<Invoke>(page);
            Marshal.WriteInt32(active, 0);
            using (var hook = new X64InlineHook(Address(entry), bytes.Length, name: $"MainFlag{path}"))
            {
                check(hook.DisplacedByteCount == bytes.Length, $"main flag path {path} displaces the full audited span");
                hook.Generate((assembler, original, continuationAddress) =>
                    KeepFlagRotationNative.Emit(assembler, original, Address(active), Address(Marshal.GetFunctionPointerForDelegate(mark))));
                hook.Enable();
                check(Marshal.ReadByte(entry) == 0xFF && Marshal.ReadByte(entry, 1) == 0x25 &&
                    Marshal.ReadInt32(entry, 2) == 0 && Marshal.ReadInt64(entry, 6) == hook.StubAddress.ToInt64() &&
                    Marshal.ReadByte(entry, 14) == 0x90, $"main flag path {path} uses the expected RedBird jump");

                foreach (int view in new[] { 0, 2, 4, 6 })
                {
                    Marshal.WriteInt32(camera, view);
                    Marshal.WriteInt32(active, 0);
                    markedPlayer = 0;
                    check(invoke() == view && markedPlayer == 0 && HasExpectedState(path, view, observed),
                        $"main flag path {path} preserves Vanilla for camera {view} when inactive");

                    Marshal.WriteInt32(active, 1);
                    check(invoke() == 0 && markedPlayer == 3 && HasExpectedState(path, 0, observed),
                        $"main flag path {path} uses view 0 for camera {view} and preserves the native continuation");
                    check(Marshal.ReadInt32(camera) == view, $"main flag path {path} leaves the local camera unchanged");
                }
            }
            GC.KeepAlive(mark);
        }

        private static bool HasExpectedState(int path, int view, IntPtr observed)
        {
            bool zero = (Marshal.ReadInt32(observed, 20) & 0x40) != 0;
            return Marshal.ReadInt32(observed, 0) == 59 &&
                (path == 0
                    ? Marshal.ReadInt32(observed, 4) == 29 && Marshal.ReadInt32(observed, 8) == 58 &&
                      Marshal.ReadInt32(observed, 12) == 49 && Marshal.ReadInt32(observed, 16) == 19 && zero == (view == 2)
                    : Marshal.ReadInt32(observed, 4) == 69 && Marshal.ReadInt32(observed, 8) == 39 &&
                      Marshal.ReadInt32(observed, 12) == 7 && Marshal.ReadInt32(observed, 16) == 0 && zero);
        }

        private static ulong Address(IntPtr pointer) => unchecked((ulong)pointer.ToInt64());

        private static void Write(Assembler asm, IntPtr destination)
        {
            using (var stream = new MemoryStream())
            {
                asm.Assemble(new StreamCodeWriter(stream), Address(destination));
                byte[] bytes = stream.ToArray();
                Marshal.Copy(bytes, 0, destination, bytes.Length);
            }
        }
    }
}
