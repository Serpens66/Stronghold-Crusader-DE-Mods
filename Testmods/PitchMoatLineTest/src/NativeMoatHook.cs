using System;
using System.IO;
using System.Runtime.InteropServices;
using APIShared.NativeSites;
using BepInEx;
using Iced.Intel;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace PitchMoatLineTest
{
    internal sealed unsafe partial class NativePlacement
    {
        private const string OwnerGuid = "PitchMoatLineTest_Serp";
        private const string SiteName = "pitch-moat-line-native";
        private const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const string InlineBackendHash = "E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB";
        private const string AssemblerHash = "CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F";
        private const int WidthRva = 0x923AB;
        private const int WidthLength = 18;
        private const int WidthValueRva = 0x60AD458;
        private const int RejectionRva = 0x91FEB;
        private const string WidthOriginal = "890DA7B0010681FA1F0300000F872EFCFFFF";

        [ThreadStatic] private static NativePlacement widthScope;
        [ThreadStatic] private static LineTile scopedTile;
        [ThreadStatic] private static int scopedPlayer;
        [ThreadStatic] private static bool widthApplied;

        private X64InlineHook widthHook;
        private ContextHookDelegate widthCallback;
        private bool widthReady;

        private static void VerifyImage(CrusaderLibraryLoadContext context)
        {
            string nativeFile = Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            if (Shared.DebugLogHelper.GetFileSha256(nativeFile) != NativeHash ||
                Shared.DebugLogHelper.GetFileSha256(typeof(X64InlineHook).Assembly.Location) != InlineBackendHash ||
                Shared.DebugLogHelper.GetFileSha256(typeof(Assembler).Assembly.Location) != AssemblerHash)
                throw new InvalidOperationException("Unreviewed game or RedBird/Iced image.");
            if (context.ModuleHandle == IntPtr.Zero || context.Memory.Length <= 0x88E3D7C ||
                !NativeSiteRegistry.IsImageBound)
                throw new InvalidOperationException("The loaded native image or its site registry is unavailable.");
            RequireOriginal(context, WidthRva, WidthOriginal, true);
            RequireOriginal(context, WidthRva + WidthLength, "4863050CFA9703", false);
            RequireOriginal(context, RejectionRva, "32C0", false);
        }

        private static T Bind<T>(CrusaderLibraryLoadContext context, int rva, string prologue) where T : Delegate
        {
            // Calls through the live entry preserve installed SE and other compatible validators.
            RequireOriginal(context, rva, prologue, false);
            return Marshal.GetDelegateForFunctionPointer<T>(IntPtr.Add(context.ModuleHandle, rva));
        }

        private static void RequireOriginal(CrusaderLibraryLoadContext context, int rva, string hex, bool live)
        {
            int length = hex.Length / 2;
            if (rva < 0 || rva > context.Memory.Length - length)
                throw new InvalidOperationException("Native RVA outside the loaded image: 0x" + rva.ToString("X"));
            for (int i = 0; i < length; i++)
            {
                byte expected = Convert.ToByte(hex.Substring(2 * i, 2), 16);
                if (context.Memory[rva + i] != expected ||
                    (live && Marshal.ReadByte(context.ModuleHandle, rva + i) != expected))
                    throw new InvalidOperationException("Native contract or hook conflict at RVA 0x" + (rva + i).ToString("X"));
            }
        }

        private void InstallMoatWidthHook(CrusaderLibraryLoadContext context)
        {
            var claim = NativeSiteRegistry.Register(OwnerGuid, SiteName, new[]
            {
                new NativeSiteSpan(WidthRva, WidthLength, NativeSiteMode.Exclusive),
                new NativeSiteSpan(0x77E60, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x5CA40, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0xEC130, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x6C990, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x6C9D0, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(FindPitchSlotRva, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x107160, 1, NativeSiteMode.CallOnly)
            });
            if (!claim.Succeeded)
                throw new InvalidOperationException("Moat-width site unavailable: " + claim);

            bool publishing = false;
            try
            {
                RequireOriginal(context, WidthRva, WidthOriginal, true);
                ulong target = unchecked((ulong)Address(WidthRva).ToInt64());
                widthCallback = registers =>
                {
                    if (!widthReady || widthScope != this || widthApplied || !IsMoat(Read(ActiveMapperRva)) ||
                        scopedPlayer != Read(NativePlayerRva) || scopedTile.X != Read(MapPlacementX) ||
                        scopedTile.Y != Read(MapPlacementY))
                        return;
                    // The next instruction stores ECX. EDX is the coordinate for
                    // the displaced CMP, which establishes all flags for the JA.
                    registers.Pointer->RCX = BrushWidth;
                    widthApplied = true;
                };
                var options = new ContextHookOptions
                {
                    HookSize = WidthLength,
                    Registers = X64SmartCPUContextRegs.All,
                    Placement = OverwrittenInstructionPlacement.AfterCallback
                };
                widthHook = new X64InlineHook(target, WidthLength, NullLogger.Instance, "single-tile line moat brush");
                if (widthHook.TargetAddress != target || widthHook.DisplacedByteCount != WidthLength)
                    throw new InvalidOperationException("RedBird selected an unexpected moat-width span.");
                widthHook.Generate(ContextStub.CreateGenerator(widthHook,
                    ContextStub.Guard(widthCallback, options, target, NullLogger.Instance, SiteName), options));
                VerifyEmittedWidthStub(target);

                // Keep the callback and the published code rooted for the process.
                publishing = true;
                widthHook.Enable();
                if (!widthHook.IsInstalled ||
                    Marshal.ReadByte(Address(WidthRva), 0) != 0xFF ||
                    Marshal.ReadByte(Address(WidthRva), 1) != 0x25 ||
                    Marshal.ReadInt32(Address(WidthRva), 2) != 0 ||
                    Marshal.ReadInt64(Address(WidthRva), 6) != widthHook.StubAddress.ToInt64())
                    throw new InvalidOperationException("Published moat-width jump does not match the prepared stub.");
                for (int i = 14; i < WidthLength; i++)
                    if (Marshal.ReadByte(Address(WidthRva), i) != 0x90)
                        throw new InvalidOperationException("Unexpected published moat-width padding.");
                widthReady = true;
            }
            catch
            {
                if (!publishing)
                {
                    widthHook?.Dispose();
                    NativeSiteRegistry.Unregister(OwnerGuid, SiteName);
                }
                throw;
            }
        }

        private void VerifyEmittedWidthStub(ulong target)
        {
            if (widthHook.State != HookState.Prepared || widthHook.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException("RedBird did not prepare a moat-width stub.");
            var bytes = new byte[512];
            Marshal.Copy(widthHook.StubAddress, bytes, 0, bytes.Length);
            ulong stub = unchecked((ulong)widthHook.StubAddress.ToInt64());
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes), stub);
            ulong widthAddress = unchecked((ulong)Address(WidthValueRva).ToInt64());
            while (decoder.IP < stub + (ulong)bytes.Length - 32)
            {
                Instruction store = decoder.Decode();
                if (store.IsInvalid)
                    break;
                if (store.Mnemonic != Mnemonic.Mov || store.Op0Kind != OpKind.Memory ||
                    store.Op1Register != Register.ECX || !store.IsIPRelativeMemoryOperand ||
                    store.IPRelativeMemoryAddress != widthAddress)
                    continue;
                Instruction compare = decoder.Decode();
                Instruction reject = decoder.Decode();
                Instruction resume = decoder.Decode();
                if (compare.Mnemonic != Mnemonic.Cmp || compare.Op0Register != Register.EDX ||
                    compare.GetImmediate(1) != GameTileManagerAPI.MAX_WIDTH - 1 ||
                    reject.Mnemonic != Mnemonic.Ja ||
                    reject.NearBranchTarget != unchecked((ulong)Address(RejectionRva).ToInt64()) ||
                    !JumpsTo(resume, target + WidthLength))
                    break;
                return;
            }
            throw new InvalidOperationException("Relocated moat-width store, compare, or branch changed.");
        }

        private static bool JumpsTo(Instruction instruction, ulong address) =>
            instruction.Mnemonic == Mnemonic.Jmp &&
            (instruction.NearBranchTarget == address ||
                (instruction.IsIPRelativeMemoryOperand && instruction.IPRelativeMemoryAddress == instruction.NextIP &&
                    unchecked((ulong)Marshal.ReadInt64(new IntPtr((long)instruction.NextIP))) == address));
    }
}
