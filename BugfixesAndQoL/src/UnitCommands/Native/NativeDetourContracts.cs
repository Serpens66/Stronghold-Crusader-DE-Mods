using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        // NativeX64 Indirect needs six bytes rounded to complete instructions.
        // FBCB9319 full feature audit; inline-hook lengths are a separate contract.
        private static readonly Dictionary<int, string> nativeDetourPrefixes = new Dictionary<int, string> {
            { 0x12BF0, "40534883EC30" }, // Chore 36, 6 bytes
            { 0x11C3A0, "4C635C24284C63D2" }, // waypoint append, 8 bytes
            { 0x3E700, "48896C241856" }, // 6 bytes
            { 0x59210, "48895C240848896C2410" }, // 10 bytes
            { 0x61E70, "81FAFFF90000" }, // 6 bytes
            { 0x639C0, "48895C241856" }, // 6 bytes
            { 0x69470, "48896C24104889742418" }, // 10 bytes
            { 0x69560, "4863C20FB78441F023EA01" }, // 11 bytes
            { 0x69D60, "448944241889542410" }, // 9 bytes
            { 0x6AF60, "44894C242053" }, // 6 bytes
            { 0x6B910, "40534883EC20" }, // 6 bytes
            { 0x6C490, "48895C240848897C2410" }, // 10 bytes
            { 0x725E0, "48895C240848896C2410" }, // 10 bytes
            { 0x8C5F0, "44884C24204488442418" }, // 10 bytes
            { 0xB70C0, "48895C240855" }, // 6 bytes
            { 0xB72C0, "48895C240848896C2410" }, // 10 bytes
            { 0xC0270, "48895C24088D42FF" }, // 8 bytes
            { 0xC07C0, "4863C24869D02C030000" }, // 10 bytes
            { 0xC3E50, "48895C240857" }, // 6 bytes
            { 0xC4BF0, "48896C24184889742420" }, // 10 bytes
            { 0xC5040, "4883EC2883B97498310001" }, // 11 bytes
            { 0xD86F0, "48895C242055" }, // 6 bytes
            { 0xD8CE0, "48895C240848896C2418" }, // 10 bytes
            { 0xD90D0, "48895C240848896C2410" }, // 10 bytes
            { 0xD9C40, "48895C240848896C2418" }, // 10 bytes
            { 0xDA020, "48894C240853" }, // 6 bytes
            { 0xDA590, "48895C242056" }, // 6 bytes
            { 0xDAA50, "48895C24084889742410" }, // 10 bytes
            { 0xDAAC0, "48895C242055" }, // 6 bytes
            { 0xDAFD0, "48895C242055" }, // 6 bytes
            { 0xDB650, "48895C240848896C2418" }, // 10 bytes
            { 0xDBC60, "44894C242053" }, // 6 bytes
            { 0xDC3C0, "48895C242055" }, // 6 bytes
            { 0xDCD60, "4863C24C8D1D9632F2FF" }, // 10 bytes
            { 0xDCE60, "48895C241048896C2418" }, // 10 bytes
            { 0xDE6A0, "48895C240848896C2410" }, // 10 bytes
            { 0xDF720, "405741544155" }, // 6 bytes
            { 0xE04B0, "48895C241857" }, // 6 bytes
            { 0xE0530, "4863C24C8D1DC6FAF1FF" }, // 10 bytes
            { 0xE06F0, "4C8D1509F9F1FF" }, // 7 bytes
            { 0xE0770, "4C8D1589F8F1FF" }, // 7 bytes
            { 0xE0970, "48895C240848896C2410" }, // 10 bytes
            { 0xE0AC0, "48895C240848896C2418" }, // 10 bytes
            { 0xE1110, "405341574883EC38" }, // 8 bytes
            { 0xE1640, "48894C240853" }, // 6 bytes
            { 0xE1D30, "48895C240848896C2418" }, // 10 bytes
            { 0xE2610, "405541544155" }, // 6 bytes
            { 0xE2CA0, "48895C240848896C2410" }, // 10 bytes
            { 0xE2F60, "40555741574881EC80090000" }, // 12 bytes
            { 0xE32B0, "40534883EC30" }, // 6 bytes
            { 0xE3B90, "40535556574883EC28" }, // 9 bytes
            { 0xE49D0, "405341574883EC58" }, // 8 bytes
            { 0xE4E90, "40564883EC10" }, // 6 bytes
            { 0xE62D0, "488991605F1500" }, // 7 bytes
            { 0xE7C40, "448944241889542410" }, // 9 bytes
            { 0xE7F60, "448944241889542410" }, // 9 bytes
            { 0xE9D90, "405355574154" }, // 6 bytes
            { 0xE9FF0, "44894C24204489442418" }, // 10 bytes
            { 0xF0030, "895424105355" }, // 6 bytes
            { 0xF03C0, "895424105356" }, // 6 bytes
            { 0xF3060, "48895C241048896C2418" }, // 10 bytes
            { 0xF32B0, "48895C240848896C2410" }, // 10 bytes
            { 0xF4630, "4883EC0883B9C000000000" }, // 11 bytes
            { 0xF4930, "48895C240848896C2410" }, // 10 bytes
            { 0x107160, "4863C2488D15A66E9C04" }, // 10 bytes
            { 0x110740, "48895C24084889742410" }, // 10 bytes
            { 0x117820, "48895C240848896C2410" }, // 10 bytes
            { 0x1178D0, "48895C240848896C2410" }, // 10 bytes
            { 0x117BC0, "48895C240848896C2410" }, // 10 bytes
            { 0x117C70, "48895C240848896C2410" }, // 10 bytes
            { 0x118310, "405556574157" }, // 6 bytes
            { 0x118E00, "48895C240848896C2410" }, // 10 bytes
            { 0x119E30, "48895C241048896C2418" }, // 10 bytes
            { 0x119EE0, "48895C241048896C2418" }, // 10 bytes
            { 0x119F90, "48895C24084863C2" }, // 8 bytes
            { 0x11A980, "48895C240848896C2418" }, // 10 bytes
            { 0x11B520, "48895C240889542410" }, // 9 bytes
            { 0x11D7C0, "48895C240848896C2410" }, // 10 bytes
            { 0x11D8D0, "4C8BDC554154" }, // 6 bytes
            { 0x11DD10, "48895C24104489442418" }, // 10 bytes
            { 0x11E960, "44894C24204489442418" }, // 10 bytes
            { 0x122800, "48895C240848896C2410" }, // 10 bytes
            { 0x123090, "48895C24084889742410" }, // 10 bytes
            { 0x1232E0, "48895C240857" }, // 6 bytes
            { 0x123460, "48895C240848896C2410" }, // 10 bytes
            { 0x1243D0, "48895C240848896C2410" }, // 10 bytes
            { 0x124740, "48895C240848896C2410" }, // 10 bytes
            { 0x13F540, "48895C241855" }, // 6 bytes
            { 0x143400, "48895C241048896C2418" }, // 10 bytes
            { 0x145030, "48895C240848896C2410" }, // 10 bytes
            { 0x146A70, "48895C240848896C2410" }, // 10 bytes
            { 0x14AED0, "48895C241048896C2418" }, // 10 bytes
            { 0x169B70, "48895C240848896C2410" }, // 10 bytes
            { 0x174F90, "48895C240848896C2410" }, // 10 bytes
            { 0x178350, "48895C241855" }, // 6 bytes
            { 0x17B540, "48895C240848896C2410" }, // 10 bytes
            { 0x17CF50, "48895C242055" }, // 6 bytes
            { 0x180230, "4863C24C69C090040000" }, // 10 bytes
            { 0x1811A0, "448B15C92B7608" }, // 7 bytes
            { 0x181890, "48895C24084889742410" }, // 10 bytes
            { 0x182B00, "48895C240848896C2410" }, // 10 bytes
            { 0x1853F0, "40534883EC20" }, // 6 bytes
            { 0x1855A0, "48895C241048896C2418" }, // 10 bytes
            { 0x186AD0, "4863C24C69C090040000" }, // 10 bytes
            { 0x187200, "4863C24C69C090040000" }, // 10 bytes
            { 0x188340, "4863C24869D090040000" }, // 10 bytes
            { 0x18BC00, "4863C2488D1586AC1A00" }, // 10 bytes
            { 0x18BE50, "4863C24C69C890040000" }, // 10 bytes
            { 0x18D460, "48895C241855" }, // 6 bytes
            { 0x18DC40, "448B1529617508" }, // 7 bytes
            { 0x18E1E0, "40535556574154" }, // 7 bytes
            { 0x191C00, "83B98005000000" }, // 7 bytes
            { 0x1946A0, "48895C241048896C2418" }, // 10 bytes
            { 0x195E30, "48895C241048896C2418" }, // 10 bytes
            { 0x196100, "4883EC484863C2" }, // 7 bytes
            { 0x196280, "48895C242055" }, // 6 bytes
            { 0x196810, "4863C24C69C090040000" }, // 10 bytes
            { 0x196840, "4863C24869D090040000" }, // 10 bytes
            { 0x196870, "83B9BC05000000" }, // 7 bytes
            { 0x196CF0, "8B442428488D0D25D63D08" }, // 11 bytes
            { 0x1976C0, "48895C24084889742410" }, // 10 bytes
            { 0x197950, "4863C24869D090040000" }, // 10 bytes
            { 0x198620, "40574863FA4C69CF90040000" }, // 12 bytes
            { 0x1988F0, "48895C242057" }, // 6 bytes
            { 0x198C40, "48894C240853" }, // 6 bytes
            { 0x199CD0, "40534883EC20" }, // 6 bytes
            { 0x19B1B0, "4053488D1D474EE6FF" }, // 9 bytes
            { 0x19B260, "48895C240848896C2410" }, // 10 bytes
        };
        private readonly Dictionary<HookTransaction, List<Action>> nativePublicationChecks =
            new Dictionary<HookTransaction, List<Action>>();
        private void PrepareNativeDetour<T>(HookTransaction transaction, RedBirdDetour<T> detour,
            ulong address, T callback, IDetourIntermediaryFactory intermediary) where T : Delegate
        {
            int rva = checked((int)(address - (unchecked((ulong)nativePathManager.ToInt64()) - NativePathManagerRva)));
            if (!nativeDetourPrefixes.TryGetValue(rva, out string prefix))
                throw new InvalidOperationException("Unaudited command detour entry: " + rva.ToString("X"));
            int count = prefix.Length / 2;
            for (int i = 0; i < count; i++)
                if (Marshal.ReadByte(new IntPtr(unchecked((long)address)), i) != Convert.ToByte(prefix.Substring(i * 2, 2), 16))
                    throw new InvalidOperationException("Command detour prefix changed: " + rva.ToString("X"));
            var request = new DetourRequest<T> {
                Name = "UnitCommandPathAPI prepublication contract", TargetAddress = address,
                Callback = callback, IntermediaryFactory = intermediary };
            var candidate = APIShared.AssassinPathAPI.Backend.CreateDetour(in request) as NativeDetour<T>;
            if (candidate == null) throw new InvalidOperationException("NativeX64 command backend unavailable.");
            using (candidate) ValidateCommandNativeDetour(candidate, address, count, false);
            if (!nativePublicationChecks.TryGetValue(transaction, out List<Action> checks))
                nativePublicationChecks.Add(transaction, checks = new List<Action>());
            checks.Add(() => ValidateCommandNativeDetour(detour.Handle.Hook as NativeDetour<T>, address, count, true));
        }
        private static void ValidateCommandNativeDetour<T>(NativeDetour<T> hook, ulong address, int count, bool installed) where T : Delegate
        {
            if (hook == null || hook.Scheme != DetourScheme.Indirect || hook.TargetAddress != address ||
                hook.DisplacedByteCount != count || hook.IsInstalled != installed ||
                hook.TrampolineAddress == IntPtr.Zero || hook.PointerSlot == IntPtr.Zero ||
                hook.HookEntryPointAddress == IntPtr.Zero || hook.OriginalEntryPointAddress != hook.TrampolineAddress)
                throw new InvalidOperationException("NativeX64 command detour contract changed.");
            if (!installed) return;
            var target = new IntPtr(unchecked((long)address));
            if (Marshal.ReadByte(target) != 0xFF || Marshal.ReadByte(target, 1) != 0x25 ||
                IntPtr.Add(target, 6 + Marshal.ReadInt32(target, 2)) != hook.PointerSlot ||
                Marshal.ReadInt64(hook.PointerSlot) != hook.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("NativeX64 command detour patch/slot contract changed.");
        }
        private void ValidatePublishedCommandHooks(HookTransaction transaction)
        {
            if (nativePublicationChecks.TryGetValue(transaction, out List<Action> checks))
                foreach (Action check in checks) check();
        }
    }
}
