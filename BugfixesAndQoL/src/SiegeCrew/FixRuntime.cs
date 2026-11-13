using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using APIShared.NativeSites;
using BepInEx;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using static BugfixesAndQoL.SiegeCrew.NativeContract;

namespace BugfixesAndQoL.SiegeCrew
{
    internal sealed class FixRuntime
    {
        private readonly ManualLogSource log;
        private readonly List<HookTransaction> published = new List<HookTransaction>();

        private IntPtr timeoutActivation;
        private IntPtr fireActivation;
        private IntPtr aiReadinessActivation;

        private delegate void EmitSite(Assembler assembler, Site site, ulong activation, ReadOnlySpan<Instruction> original);

        internal FixRuntime(ManualLogSource log) => this.log = log;

        internal void Install(CrusaderLibraryLoadContext context, bool enableMod, bool enableTimeout, bool enableFire)
        {
            string path = Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            if (context.ModuleHandle == IntPtr.Zero ||
                !string.Equals(Shared.DebugLogHelper.GetFileSha256(path), NativeHash, StringComparison.OrdinalIgnoreCase))
            {
                Shared.DebugLogHelper.LogError(log,
                    "SIEGE_CREW_TIMEOUT_FIX_INACTIVE / SIEGE_CREW_FIRE_FIX_INACTIVE / AI_SIEGE_CREW_READY_FIX_INACTIVE: the native layout requires the audited image.");
                return;
            }

            timeoutActivation = InstallGroup(context, "siege-crew-timeout-reset", "SIEGE_CREW_TIMEOUT_FIX",
                TimeoutSites, EmitTimeout, enableMod && enableTimeout);
            ulong manager = unchecked((ulong)context.ModuleHandle.ToInt64()) + SiegeCrewFirePatch.UnitManagerRva;
            fireActivation = InstallGroup(context, "siege-crew-fire-loss", "SIEGE_CREW_FIRE_FIX",
                new[] { SiegeCrewFirePatch.FireSite },
                (assembler, site, activation, original) => SiegeCrewFirePatch.Emit(assembler, site, activation, manager, original),
                enableMod && enableFire);
            aiReadinessActivation = InstallGroup(context, "ai-harassment-crew-readiness", "AI_SIEGE_CREW_READY_FIX",
                new[] { AiCrewReadinessPatch.Site },
                (assembler, site, activation, original) => AiCrewReadinessPatch.Emit(assembler, activation,
                    unchecked((ulong)context.ModuleHandle.ToInt64())), enableMod && enableTimeout);
        }

        internal void SetEnabled(bool enableMod, bool enableTimeout, bool enableFire)
        {
            SetActivation(timeoutActivation, enableMod, enableTimeout);
            SetActivation(fireActivation, enableMod, enableFire);
            SetActivation(aiReadinessActivation, enableMod, enableTimeout);
        }

        private IntPtr InstallGroup(CrusaderLibraryLoadContext context, string name, string marker,
            Site[] sites, EmitSite emit, bool enabled)
        {
            IntPtr activation = IntPtr.Zero;
            HookTransaction transaction = null;
            bool claimed = false;
            bool publicationStarted = false;
            try
            {
                var spans = new NativeSiteSpan[sites.Length];
                for (int index = 0; index < sites.Length; index++)
                {
                    Site site = sites[index];
                    ReadOnlySpan<byte> pristine = context.Memory.Slice(site.Rva, site.Original.Length);
                    ReadOnlySpan<byte> continuation = context.Memory.Slice(site.End, site.Continuation.Length);
                    var live = new byte[site.Original.Length];
                    Marshal.Copy(IntPtr.Add(context.ModuleHandle, site.Rva), live, 0, live.Length);
                    site.Verify(pristine, continuation, live);
                    spans[index] = new NativeSiteSpan(site.Rva, live.Length, NativeSiteMode.Exclusive);
                }

                var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, name, spans);
                if (!claim.Succeeded)
                    throw new InvalidOperationException(claim.Reason);
                claimed = true;

                activation = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(activation, 0);
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region, installOnCommit: false);

                ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
                ulong gate = unchecked((ulong)activation.ToInt64());
                var handles = new HookHandle<X64InlineHook>[sites.Length];
                for (int index = 0; index < sites.Length; index++)
                {
                    Site site = sites[index];
                    var handle = new HookHandle<X64InlineHook>();
                    handles[index] = handle;
                    transaction.AddInline(handle, HookTarget.FromAddress(module + (uint)site.Rva),
                        (assembler, original, _) => emit(assembler, site, gate, original),
                        hookSize: site.Original.Length, name: name + " " + site.Name);
                }

                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess)
                    throw new InvalidOperationException(name + " stub preparation failed: " + result);

                for (int index = 0; index < sites.Length; index++)
                {
                    Site site = sites[index];
                    var handle = handles[index];
                    if (!handle.Success || handle.Hook.IsInstalled ||
                        handle.Hook.TargetAddress != module + (uint)site.Rva ||
                        handle.Hook.DisplacedByteCount != site.Original.Length)
                        throw new InvalidOperationException($"Unexpected prepared span at RVA 0x{site.Rva:X}.");
                }

                Publish(activation, () =>
                {
                    // Enable can write code before throwing. Root the transaction and
                    // leave the zeroed gate allocated after publication starts.
                    published.Add(transaction);
                    publicationStarted = true;
                    foreach (IHook hook in result.Installed)
                        hook.Enable();
                    for (int index = 0; index < sites.Length; index++)
                        VerifyInstalled(context.ModuleHandle, sites[index], handles[index].Hook);
                }, enabled);
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, $"{marker}_INACTIVE: {error}");
                if (!publicationStarted)
                {
                    try
                    {
                        transaction?.Dispose();
                    }
                    catch (Exception cleanupError)
                    {
                        Shared.DebugLogHelper.LogError(log, $"{marker}_ROLLBACK_FAILED: {cleanupError}");
                    }
                    finally
                    {
                        if (activation != IntPtr.Zero)
                            Marshal.FreeHGlobal(activation);
                        if (claimed)
                            NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, name);
                    }
                }
                return IntPtr.Zero;
            }

            string addresses = string.Join(", ", sites.Select(site => $"0x{site.Rva:X}+{site.Original.Length}"));
            Shared.DebugLogHelper.LogInfo(log, $"{marker}_READY: reference-rva {addresses}.");
            return activation;
        }

        private static void VerifyInstalled(IntPtr module, Site site, X64InlineHook hook)
        {
            IntPtr entry = IntPtr.Add(module, site.Rva);
            if (!hook.IsInstalled || Marshal.ReadInt16(entry) != 0x25FF ||
                Marshal.ReadInt32(entry, 2) != 0 || Marshal.ReadInt64(entry, 6) != hook.StubAddress.ToInt64())
                throw new InvalidOperationException($"Unexpected installed jump at RVA 0x{site.Rva:X}.");
            for (int index = 14; index < site.Original.Length; index++)
                if (Marshal.ReadByte(entry, index) != 0x90)
                    throw new InvalidOperationException($"Unexpected installed padding at RVA 0x{site.Rva + index:X}.");
        }
    }
}
