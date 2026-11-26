using System;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using Shared;
using SHCDESE.API.LowLevel;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed class NativePlacementFeedback
    {
        private const string Feature = "Area construction feedback";
        private const int Sound = 0x2940, SpatialSound = 0x2960, Engineer = 0x3770;

        // Mapper input 0x90CD0 after Chore 28. The positional block starts before
        // the argument loads, so the silent branch at 0x92224 still lands outside it.
        private static readonly (int Start, int Call, int Target, string Hex)[] Sites =
        {
            (0x921A8, 0x921A8, Sound, "E89307F7FF8B05A9B201060550FFFFFF"),
            (0x9220B, 0x9221F, SpatialSound, "448B0592B20106488D0D772243068B1581B20106E83C07F7FF"),
            (0x9226B, 0x9226B, Engineer, "E80015F7FF488D0D89617506E8943E0F00"),
            (0x922AA, 0x922AA, Engineer, "E8C114F7FF488D0D4A617506E8553E0F00"),
            (0x922CC, 0x922CC, Engineer, "E89F14F7FF488D0D28617506E8333E0F00"),
            (0x922EE, 0x922EE, Engineer, "E87D14F7FF488D0D06617506E8113E0F00")
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Cue(IntPtr manager, int kind);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SpatialCue(IntPtr manager, int x, int y, int kind);

        // Keep callbacks and published executable storage rooted until process exit.
        private readonly X64InlineHook[] hooks = new X64InlineHook[Sites.Length];
        private readonly Cue sound, engineer;
        private readonly SpatialCue spatial;
        private Func<bool> intact;
        private static NativePlacementFeedback failedRollback;

        private NativePlacementFeedback(IntPtr image)
        {
            var vanillaSound = Marshal.GetDelegateForFunctionPointer<Cue>(IntPtr.Add(image, Sound));
            var vanillaSpatial = Marshal.GetDelegateForFunctionPointer<SpatialCue>(IntPtr.Add(image, SpatialSound));
            var vanillaEngineer = Marshal.GetDelegateForFunctionPointer<Cue>(IntPtr.Add(image, Engineer));
            sound = (manager, kind) => { if (PlacementFeedback.AllowSound()) vanillaSound(manager, kind); };
            spatial = (manager, x, y, kind) => { if (PlacementFeedback.AllowSound()) vanillaSpatial(manager, x, y, kind); };
            engineer = (manager, kind) => { if (PlacementFeedback.AllowVoice()) vanillaEngineer(manager, kind); };
        }

        internal bool Available { get; private set; }

        internal static NativePlacementFeedback Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            NativePlacementFeedback runtime = null;
            try
            {
                runtime = new NativePlacementFeedback(context.ModuleHandle);
                runtime.Publish(context);
            }
            catch (Exception error)
            {
                // Only an unpublished candidate is eligible for rollback.
                if (runtime != null)
                    foreach (var hook in runtime.hooks.Reverse())
                    {
                        try { hook?.Dispose(); }
                        catch (Exception rollbackError)
                        {
                            failedRollback = runtime;
                            log.LogError(AreaConstructionFeature.Stamp(Feature + " rollback failed: " + rollbackError.Message));
                        }
                    }
                log.LogError(AreaConstructionFeature.Stamp(Feature + " uses normal Vanilla audio: " + error.Message));
                return null;
            }
            log.LogInfo(AreaConstructionFeature.Stamp(Feature + " ready at " +
                string.Join(", ", Sites.Select(site => $"0x{site.Start:X}..0x{site.Start + site.Hex.Length / 2:X}"))));
            return runtime;
        }

        private void Publish(CrusaderLibraryLoadContext context)
        {
            ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
            for (int i = 0; i < Sites.Length; i++)
            {
                var site = Sites[i];
                byte[] expected = Enumerable.Range(0, site.Hex.Length / 2)
                    .Select(index => Convert.ToByte(site.Hex.Substring(index * 2, 2), 16)).ToArray();
                if (!context.Memory.Slice(site.Start, expected.Length).SequenceEqual(expected.AsSpan()))
                    throw new InvalidOperationException($"Unexpected reference code at RVA 0x{site.Start:X}.");
                ulong start = image + (uint)site.Start;
                string name = Feature + $" 0x{site.Start:X}";
                NativePatchGuard.RequireUnpatched(start, expected.Length, name, Feature);
                var hook = hooks[i] = new X64InlineHook(start, expected.Length, name: name);
                if (hook.DisplacedByteCount != expected.Length || hook.IsInstalled)
                    throw new InvalidOperationException("Unexpected instruction span for " + name);
                Delegate callback = site.Target == Sound ? (Delegate)sound :
                    site.Target == SpatialSound ? spatial : engineer;
                ulong pointer = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64());
                hook.Generate((asm, original, resume) =>
                {
                    ulong call = image + (uint)site.Call;
                    if (resume != start + (uint)expected.Length)
                        throw new InvalidOperationException("Unexpected continuation for " + name);
                    bool replaced = false;
                    foreach (var instruction in original)
                    {
                        if (instruction.IP != call)
                        {
                            asm.AddInstruction(instruction);
                            continue;
                        }
                        if (instruction.Mnemonic != Mnemonic.Call || instruction.NearBranchTarget != image + (uint)site.Target)
                            throw new InvalidOperationException("Unexpected call target for " + name);
                        // Same Windows x64 arguments and stack as the original call.
                        // RAX is volatile and the native callers discard the return value.
                        asm.mov(rax, pointer);
                        asm.call(rax);
                        replaced = true;
                    }
                    if (!replaced)
                        throw new InvalidOperationException("Missing placement audio call for " + name);
                });
            }

            foreach (var hook in hooks) NativePatchGuard.RequireUnpatched(hook, Feature);
            foreach (var hook in hooks)
            {
                hook.Enable();
                if (!hook.IsInstalled)
                    throw new InvalidOperationException("Could not enable " + hook.Name);
            }
            foreach (var hook in hooks) NativePatchGuard.Watch(hook, Feature);
            intact = NativePatchGuard.CaptureIntegrity(hooks, Feature);
            Available = true;
        }

        internal void Recheck() => Available = intact();
    }
}
