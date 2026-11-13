using System;
using System.IO;
using System.Runtime.InteropServices;
using APIShared.NativeSites;
using BepInEx;
using Iced.Intel;
using RedBird.X64.Hooks;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.PitchMoatLines
{
    internal sealed unsafe partial class NativePlacement
    {
        private const string OwnerGuid = BugfixesAndQoLPlugin.PluginGuid;
        private const string SiteName = "pitch-moat-line-native";
        private const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const string InlineBackendHash = "E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB";
        private const string AssemblerHash = "CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F";

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
        }

        private static T Bind<T>(CrusaderLibraryLoadContext context, int rva, string prologue) where T : Delegate
        {
            // Calls through live entries preserve compatible installed detours.
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

        private void RegisterNativeCalls()
        {
            // Batch execution no longer changes the local Vanilla brush width.
            var claim = NativeSiteRegistry.Register(OwnerGuid, SiteName, new[]
            {
                new NativeSiteSpan(0x77E60, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x5CA40, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0xEC130, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x6C990, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x6C9D0, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0xCC420, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x6D580, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x6FE90, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0xE3B90, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(FindPitchSlotRva, 1, NativeSiteMode.CallOnly),
                new NativeSiteSpan(0x107160, 1, NativeSiteMode.CallOnly)
            });
            if (!claim.Succeeded)
                throw new InvalidOperationException("Native line-call contract unavailable: " + claim);
        }
    }
}
