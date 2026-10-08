using BepInEx;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ExtraFeatures
{
    internal static unsafe class HealerNativeContract
    {
        internal const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int ListBuilderRva = 0x181340;
        internal const int UnitManagerRva = 0x67E8400;
        internal const int CountRva = 0x379F66C;
        internal const int IdListRva = 0x3945748;
        internal const int GlobalIdListRva = 0x3971668;
        internal const int PlayerRecordStride = 0x583C;
        internal const int IdListStride = 20000;
        internal const int GlobalIdListStride = 40000;
        internal const int ListCapacity = 10000;
        internal const int PlayerSlots = 9;
        internal const int ExpectedDisplacedBytes = 10;

        // The first two complete instructions are five bytes each. This exact entry is not
        // an Extender or Fixes hook site in the audited native build.
        private static readonly byte[] EntryBytes = {
            0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x6C, 0x24, 0x20,
            0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
            0x48, 0x83, 0xEC, 0x30
        };

        internal static void VerifyInstalledBinary(ulong imageBase)
        {
            string path = Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!string.Equals(actual, NativeSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Installed CrusaderDE.dll differs from the audited native build: " + actual);
            }

            if (typeof(NativeDetour<>).Assembly.GetName().Version != new Version(1, 5, 0, 0))
                throw new InvalidOperationException("Installed RedBird NativeX64 backend version differs from 1.5.0.0.");

            byte[] live = Capture(imageBase + ListBuilderRva, EntryBytes.Length);
            for (int index = 0; index < EntryBytes.Length; index++)
                if (live[index] != EntryBytes[index])
                    throw new InvalidOperationException("Healer list-builder entry differs at byte " + index + ".");

            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(live));
            int displaced = 0;
            while (displaced < ExpectedDisplacedBytes)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid || instruction.Length <= 0)
                    throw new InvalidOperationException("Invalid healer list-builder entry instruction.");
                displaced += instruction.Length;
            }
            if (displaced != ExpectedDisplacedBytes)
                throw new InvalidOperationException("The healer list-builder prologue changed its instruction boundaries.");

            if (Marshal.SizeOf<GameUnit>() != GameUnitManager.UnitRecordByteSize ||
                Marshal.OffsetOf<GameUnitManager>(nameof(GameUnitManager.GameUnitArray)).ToInt32() != 0x65C ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AliveState)).ToInt32() != 0x88 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitChimp)).ToInt32() != 0x8A ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentWorldPositionX)).ToInt32() != 0xB2 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentWorldPositionY)).ToInt32() != 0xB4 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SelectionAllowed)).ToInt32() != 0x2A0 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentHealth)).ToInt32() != 0x3C4 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_MaxHealth)).ToInt32() != 0x3C8 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.N000000DD)).ToInt32() != 0x450 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.N000000DE)).ToInt32() != 0x458)
                throw new InvalidOperationException("Script Extender unit layout differs from the audited native layout.");
        }

        internal static void ValidateInstalledDetour<T>(NativeDetour<T> detour, ulong expectedTarget)
            where T : Delegate
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != expectedTarget ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != ExpectedDisplacedBytes ||
                detour.ChainDepth != 1 || detour.TrampolineAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.HookEntryPointAddress == IntPtr.Zero || detour.PointerSlot == IntPtr.Zero)
                throw new InvalidOperationException("RedBird did not install the audited indirect 10-byte healer-list detour.");

            byte[] patch = Capture(expectedTarget, 6);
            if (patch[0] != 0xFF || patch[1] != 0x25)
                throw new InvalidOperationException("Healer-list detour is not an indirect FF 25 jump.");
            long slotAddress = checked((long)expectedTarget + 6 + BitConverter.ToInt32(patch, 2));
            if (slotAddress != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Healer-list detour pointer slot does not lead to its hook entry.");
        }

        internal static void ProbeBackend<T>(ulong entryAddress, T callback) where T : Delegate
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            byte[] entry = Capture(entryAddress, 64);
            NativeDetour<T> probe = null;
            try
            {
                Marshal.Copy(entry, 0, copy, entry.Length);
                var request = new DetourRequest<T>
                {
                    Name = "ExtraFeatures healer copied-entry probe",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = callback
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<T>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != ExpectedDisplacedBytes || probe.IsInstalled ||
                    probe.TargetAddress != unchecked((ulong)copy.ToInt64()) ||
                    probe.PointerSlot == IntPtr.Zero || probe.HookEntryPointAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Installed RedBird backend rejects the copied healer entry contract.");
                probe.Enable();
                ValidateInstalledDetour(probe, unchecked((ulong)copy.ToInt64()));
            }
            finally
            {
                probe?.Dispose();
                byte[] restored = new byte[entry.Length];
                Marshal.Copy(copy, restored, 0, restored.Length);
                for (int index = 0; index < entry.Length; index++)
                    if (restored[index] != entry[index])
                        throw new InvalidOperationException("Copied RedBird backend probe did not restore its entry.");
                Marshal.FreeHGlobal(copy);
                GC.KeepAlive(callback);
            }
        }

        internal static byte[] Capture(ulong address, int length)
        {
            var bytes = new byte[length];
            Marshal.Copy(unchecked((IntPtr)(long)address), bytes, 0, length);
            return bytes;
        }
    }
}
