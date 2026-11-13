using APIShared.NativeSites;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed unsafe class MatureTreeHarvestRuntime
    {
        private const string Registration = "MatureTreeHarvest";
        private IntPtr enabled;

        internal static MatureTreeHarvestRuntime Install(CrusaderLibraryLoadContext context, bool referenceHashMatches, ManualLogSource log)
        {
            if (!ExtraFeaturesHookInfrastructure.InitialLibraryLoad || !referenceHashMatches ||
                !string.Equals(Shared.DebugLogHelper.InstalledNativeSha256(), MatureTreeHarvestNative.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mature tree harvesting requires the audited native image and library load window.");

            ValidateLayout();
            var runtime = new MatureTreeHarvestRuntime();
            ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
            var sites = MatureTreeHarvestNative.Sites;
            var spans = new NativeSiteSpan[sites.Length];
            var handles = new HookHandle<X64InlineHook>[sites.Length];
            var instructions = new Instruction[sites.Length][];
            ulong worker = image + MatureTreeHarvestNative.CurrentWorkerRva;
            using (var sha = SHA256.Create())
            {
                for (int i = 0; i < sites.Length; i++)
                {
                    var site = sites[i];
                    var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory,
                        BitConverter.ToString(site.Original).Replace('-', ' '), site.Rva, true,
                        Registration + " " + site.Kind, log, Shared.NativePatternSearchScope.ExecutableSections);
                    if (resolution.Rva != site.Rva)
                        throw new InvalidOperationException("Mature tree hook moved: " + site.Kind);
                    ValidateBytes(context.Memory, image, site.Rva, site.Original);
                    ValidateBytes(context.Memory, image, site.Rva + site.Original.Length, site.Following);
                    ValidateBytes(context.Memory, image, site.Reject, site.RejectBytes);
                    ulong address = image + (uint)site.Rva;
                    instructions[i] = MatureTreeHarvestNative.DecodeAndValidate(site, address, image);
                    using (var probe = new X64InlineHook(address, site.Original.Length))
                        if (probe.IsInstalled || probe.DisplacedByteCount != site.Original.Length)
                            throw new InvalidOperationException("Mature tree hook span changed: " + site.Kind);
                    spans[i] = new NativeSiteSpan(site.Rva, site.Original.Length, NativeSiteMode.Exclusive,
                        BitConverter.ToString(sha.ComputeHash(site.Original)).Replace("-", ""));
                    handles[i] = new HookHandle<X64InlineHook>();
                }
            }

            var claim = NativeSiteRegistry.Register(ExtraFeaturesPlugin.PluginGuid, Registration, spans);
            if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);

            HookTransaction transaction = null;
            bool commitAttempted = false;
            try
            {
                runtime.enabled = Marshal.AllocHGlobal(sizeof(int));
                runtime.SetEnabled(false);
                ulong flag = unchecked((ulong)runtime.enabled.ToInt64());
                transaction = ExtraFeaturesHookInfrastructure.CreateOwnedTransaction(context.Region);
                for (int i = 0; i < sites.Length; i++)
                {
                    var site = sites[i];
                    ulong address = image + (uint)site.Rva;
                    ulong reject = image + (uint)site.Reject;
                    var assembler = new Assembler(64);
                    MatureTreeHarvestNative.Emit(assembler, site.Kind, flag, worker, reject, instructions[i]);
                    MatureTreeHarvestNative.Assemble(assembler, address);
                    transaction.AddInline(handles[i], HookTarget.FromAddress(address),
                        (a, original, _) => MatureTreeHarvestNative.Emit(a, site.Kind, flag, worker, reject, original),
                        hookSize: site.Original.Length, name: Registration + "_" + site.Kind);
                }

                ExtraFeaturesHookInfrastructure.RootBeforeCommit(runtime, transaction);
                commitAttempted = true;
                var result = transaction.Commit();
                if (!result.IsCompleteSuccess)
                    throw new InvalidOperationException("Mature tree hook transaction failed: " + result);

                for (int i = 0; i < sites.Length; i++)
                {
                    var site = sites[i];
                    ulong address = image + (uint)site.Rva;
                    var handle = handles[i];
                    if (!handle.Success || !handle.Hook.IsInstalled || handle.Hook.TargetAddress != address ||
                        handle.Hook.DisplacedByteCount != site.Original.Length)
                        throw new InvalidOperationException("Committed mature tree hook span differs: " + site.Kind);
                    byte* live = (byte*)address;
                    if (live[0] != 0xFF || live[1] != 0x25 || *(uint*)(live + 2) != 0 ||
                        *(ulong*)(live + 6) != unchecked((ulong)handle.Hook.StubAddress.ToInt64()))
                        throw new InvalidOperationException("Committed mature tree hook jump differs: " + site.Kind);
                }
                Shared.DebugLogHelper.LogInfo(log, "Mature tree harvesting hooks ready (search, arrival, strike).");
                return runtime;
            }
            catch
            {
                if (!commitAttempted)
                {
                    try { transaction?.Dispose(); }
                    finally
                    {
                        if (runtime.enabled != IntPtr.Zero) Marshal.FreeHGlobal(runtime.enabled);
                        NativeSiteRegistry.Unregister(ExtraFeaturesPlugin.PluginGuid, Registration);
                    }
                }
                // A commit attempt may leave a stub installed. Its rooted flag stays zero.
                throw;
            }
        }

        internal void SetEnabled(bool value) => Volatile.Write(ref *(int*)enabled, value ? 1 : 0);

        private static void ValidateBytes(ReadOnlySpan<byte> memory, ulong image, int rva, byte[] expected)
        {
            var live = new ReadOnlySpan<byte>((void*)(image + (uint)rva), expected.Length);
            if (!memory.Slice(rva, expected.Length).SequenceEqual(expected) || !live.SequenceEqual(expected))
                throw new InvalidOperationException("Mature tree hook bytes differ at 0x" + rva.ToString("X"));
        }

        private static void ValidateLayout()
        {
            if (Marshal.SizeOf(typeof(GameVegetation)) != MatureTreeHarvestNative.Stride ||
                (int)AliveState.IsAlive != 2 || (int)TreeGrowthStage.Stage3 != 3 ||
                (int)VegetationType.ChestnutTree != 1 || (int)VegetationType.OakTree != 2 ||
                (int)VegetationType.PineTree != 3 || (int)VegetationType.BirchTree != 4)
                throw new InvalidOperationException("Mature tree vegetation layout changed.");

            ValidateField(nameof(GameVegetation.r_AliveState), MatureTreeHarvestNative.Alive);
            ValidateField(nameof(GameVegetation.r_VegetationType), MatureTreeHarvestNative.Species);
            ValidateField(nameof(GameVegetation.r_Health), MatureTreeHarvestNative.Health);
            ValidateField(nameof(GameVegetation.N00004890), MatureTreeHarvestNative.MaxHealth);
            ValidateField(nameof(GameVegetation.r_IsFelled), MatureTreeHarvestNative.Felled);
            ValidateField(nameof(GameVegetation.r_GrowthStage), MatureTreeHarvestNative.Stage);
        }

        private static void ValidateField(string name, int offset)
        {
            if (Marshal.OffsetOf(typeof(GameVegetation), name).ToInt32() != offset - MatureTreeHarvestNative.ViewOffset)
                throw new InvalidOperationException("Mature tree vegetation field changed: " + name);
        }
    }
}
