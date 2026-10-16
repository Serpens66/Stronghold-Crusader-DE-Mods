using BepInEx;
using Iced.Intel;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PowerCall(IntPtr manager, int player, int power, int tile);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GateCall(int player, int building, int state, uint global);
        private static PowerCall nativePower;
        private static GateCall nativeGate, nativeBridge;
        private static long toolsImage;
        private static byte[] powerEntry, gateEntry, bridgeEntry;
        private const int PowerRva = 0xCD630, GateRva = 0xD5810, BridgeRva = 0xD57B0;
        private const int PowersAroundLordRva = 0x37EF96C;

        private static void InitializeNativeActions(CrusaderLibraryLoadContext context)
        {
            try
            {
                byte[] image = File.ReadAllBytes(Path.Combine(Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll"));
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(image)).Replace("-", "") != NativeSha256)
                        throw new InvalidDataException("Unknown native image; action/data contracts are hash-bound.");
                toolsImage = context.ModuleHandle.ToInt64();
                // Call-only adapters: never overwrite an entry, call site, automation hook or effect function.
                powerEntry = ReferenceEntry(image, PowerRva, 512);
                gateEntry = ReferenceEntry(image, GateRva, 96);
                bridgeEntry = ReferenceEntry(image, BridgeRva, 64);
                if (GameGlobalsManager.Instance.GamePlayerManagerVA == 0)
                    throw new InvalidDataException("Player manager is unavailable.");
                nativePower = (PowerCall)Marshal.GetDelegateForFunctionPointer(new IntPtr(toolsImage + PowerRva), typeof(PowerCall));
                nativeGate = (GateCall)Marshal.GetDelegateForFunctionPointer(new IntPtr(toolsImage + GateRva), typeof(GateCall));
                nativeBridge = (GateCall)Marshal.GetDelegateForFunctionPointer(new IntPtr(toolsImage + BridgeRva), typeof(GateCall));
                log.LogInfo("TEST_NATIVE_ACTIONS_READY powers=0xCD630 gate=0xD5810 bridge=0xD57B0 mode=call-only; live ownership checked per action");
            }
            catch (Exception error) { log.LogError("TEST_NATIVE_ACTIONS_DISABLED: " + error); }
        }

        private static byte[] ReferenceEntry(byte[] image, int rva, int length)
        {
            int pe = BitConverter.ToInt32(image, 60), count = BitConverter.ToUInt16(image, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
            for (int i = 0; i < count; i++)
            {
                int header = table + i * 40, start = BitConverter.ToInt32(image, header + 12);
                int size = BitConverter.ToInt32(image, header + 16), raw = BitConverter.ToInt32(image, header + 20);
                if (rva < start || (long)rva + length > (long)start + size ||
                    (BitConverter.ToUInt32(image, header + 36) & 0x20000000) == 0) continue;
                byte[] bytes = new byte[length];
                Buffer.BlockCopy(image, raw + rva - start, bytes, 0, length);
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
                int total = 0;
                while (total < length)
                {
                    Instruction instruction = decoder.Decode();
                    if (instruction.IsInvalid) throw new InvalidDataException("Invalid native action instruction.");
                    total += instruction.Length;
                }
                if (total != length) throw new InvalidDataException("Native action boundary changed.");
                var entry = new byte[20]; Array.Copy(bytes, entry, entry.Length); return entry;
            }
            throw new InvalidDataException("Action entry is outside executable PE sections.");
        }

        private static void RequireActionEntry(int rva, byte[] expected, bool allowExtremePowers)
        {
            if (toolsImage == 0 || expected == null) throw new InvalidOperationException("Native action is unavailable.");
            IntPtr address = new IntPtr(toolsImage + rva);
            byte[] live = new byte[expected.Length]; Marshal.Copy(address, live, 0, live.Length);
            if (Matches(live, 0, expected)) return;
            if (allowExtremePowers && OptionalExtremePowers.OwnsDispatcher(address)) return;
            throw new InvalidOperationException("Unknown native action entry owner at RVA 0x" + rva.ToString("X"));
        }
    }
}
