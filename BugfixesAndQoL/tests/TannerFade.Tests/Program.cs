using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BugfixesAndQoL;

internal static class Program
{
    private static int Main()
    {
        try
        {
            TestNativeFadeModel();
            BenchmarkManyTanneries();
            TestInstalledNativeDetourBackend();
            Console.WriteLine("BugfixesAndQoL tannery native fade tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void TestNativeFadeModel()
    {
        var tracker = new NativeTannerFadeTracker();
        var firstIdentity = new NativeBuildingIdentity(101, 6001, 1);
        var secondIdentity = new NativeBuildingIdentity(202, 6002, 1);
        NativeFadeSample first = tracker.Observe(179, firstIdentity, 0, 12, 31);
        Assert(first.Started && first.EffectiveTransparency == 32 && first.ElapsedUpdates == 0,
            "The first tannery update must begin fully transparent.");
        Assert(tracker.TryGetVanillaTransparency(179, firstIdentity, 0, 32, out uint seed) && seed == 31,
            "The next Vanilla call must see its unmodified previous alpha.");
        Assert(!tracker.TryGetVanillaTransparency(179, firstIdentity, 0, 30, out _),
            "A concurrent native alpha writer must invalidate stale restore state.");
        first = tracker.Observe(179, firstIdentity, 0, 12, 31);
        Assert(first.Started && first.EffectiveTransparency == 32,
            "An externally changed native alpha must start a fresh cycle.");

        // No Observe call occurs during pause, regardless of how many render frames pass.
        NativeFadeSample second = tracker.Observe(180, secondIdentity, 0, 12, 31);
        Assert(second.Started && second.EffectiveTransparency == 32,
            "Two tanneries must keep independent update counts.");
        for (int update = 1; update <= 64; update++)
        {
            Assert(tracker.TryGetVanillaTransparency(179, firstIdentity, 0,
                (uint)(32 - (update - 1) / 2), out uint vanillaSeed),
                "Every update must restore the previous Vanilla alpha.");
            NativeFadeSample sample = tracker.Observe(179, firstIdentity,
                update < 20 ? 0 : 2, update < 20 ? 12 : 16, 30);
            Assert(sample.ElapsedUpdates == update &&
                sample.EffectiveTransparency == 32 - update / 2,
                "The fade must advance by one raw step every two native updates.");
        }
        Assert(tracker.Observe(179, firstIdentity, 5, 25, 30).EffectiveTransparency == 0,
            "The long initial phase must remain opaque.");
        Assert(tracker.Observe(180, secondIdentity, 0, 12, 30).ElapsedUpdates == 1,
            "A second tannery must retain its own count.");
        Assert(tracker.TryGetVanillaTransparency(180, secondIdentity, 0, 32,
            out uint disabledSeed) && disabledSeed == 30,
            "Disabling during a fade must restore Vanilla's saved transparency first.");
        Assert(tracker.Remove(180), "Disabling must discard the active building state.");
        Assert(!tracker.TryGetVanillaTransparency(180, secondIdentity, 0, 30, out _),
            "A disabled building must no longer restore old state.");
        Assert(tracker.Observe(180, secondIdentity, 0, 12, 30).Started,
            "Re-enabling can start a fresh fade from Vanilla's current value.");
        Assert(tracker.TryGetVanillaTransparency(179, firstIdentity, 5, 0,
            out uint exitSeed) && exitSeed == 30,
            "The last phase-5 update must restore Vanilla's previous raw value.");
        NativeFadeSample end = tracker.Observe(179, firstIdentity, 6, 25, 30);
        Assert(end.Ended && !end.Active &&
            NativeTannerFadeTracker.ShouldBridgeExitFrame(true, 5, 6, 25, 30, end),
            "Only the stale first phase-6 frame must remain opaque.");
        Assert(!NativeTannerFadeTracker.ShouldBridgeExitFrame(false, 5, 6, 25, 30, end) &&
            !NativeTannerFadeTracker.ShouldBridgeExitFrame(true, 4, 6, 25, 30, end) &&
            !NativeTannerFadeTracker.ShouldBridgeExitFrame(true, 5, 6, 12, 30, end) &&
            !NativeTannerFadeTracker.ShouldBridgeExitFrame(true, 5, 6, 25, 0, end),
            "Unconfirmed phase transitions must retain Vanilla's output.");
        var partial = new NativeTannerFadeTracker();
        partial.Observe(188, firstIdentity, 0, 12, 31);
        NativeFadeSample partialExit = partial.Observe(188, firstIdentity, 6, 25, 30);
        Assert(!NativeTannerFadeTracker.ShouldBridgeExitFrame(true, 5, 6, 25, 30, partialExit),
            "An interrupted fade must not receive the one-frame bridge.");
        Assert(!tracker.TryGetVanillaTransparency(179, firstIdentity, 6, 0, out _),
            "Vanilla's later fade must never restore an old alpha.");
        NativeFadeSample next = tracker.Observe(179, firstIdentity, 6, 12, 0);
        Assert(!NativeTannerFadeTracker.ShouldBridgeExitFrame(false, 6, 6, 12, 0, next),
            "The following phase-6 update must stay entirely under Vanilla control.");
        Assert(!tracker.Observe(179, firstIdentity, 6, 52, 31).Started,
            "Vanilla's exit fade must never restart the initial correction.");
        NativeFadeSample replaced = tracker.Observe(180,
            new NativeBuildingIdentity(303, 6002, 1), 0, 12, 31);
        Assert(replaced.Ended && replaced.Started && replaced.EffectiveTransparency == 32,
            "Replacement in the same building slot must restart independently.");
        Assert(tracker.Remove(180) && !tracker.Remove(180),
            "Building deletion must remove only the existing state.");
        tracker.Clear();
        Assert(tracker.Observe(181, new NativeBuildingIdentity(0, 6003, 2),
            3, 17, 16).EffectiveTransparency == 16,
            "A save loaded mid-phase must resume from its stored alpha.");
        Assert(tracker.Observe(182, new NativeBuildingIdentity(0, 6004, 2),
            4, 20, 0).EffectiveTransparency == 0,
            "A fully faded save must remain opaque on load.");
    }

    private delegate void TanneryProbeDelegate();

    private static void BenchmarkManyTanneries()
    {
        const int buildingCount = 500;
        var tracker = new NativeTannerFadeTracker();
        var identities = new NativeBuildingIdentity[buildingCount];
        var clock = Stopwatch.StartNew();
        for (int index = 0; index < buildingCount; index++)
        {
            identities[index] = new NativeBuildingIdentity((uint)(index + 1),
                (uint)(10000 + index), 1);
            NativeFadeSample start = tracker.Observe(index + 1, identities[index], 0, 12, 31);
            Assert(start.Started && start.EffectiveTransparency == 32,
                "Every tannery must begin independently under load.");
        }
        for (int update = 1; update <= NativeTannerFadeState.FadeUpdates; update++)
            for (int index = 0; index < buildingCount; index++)
            {
                int buildingId = index + 1;
                uint priorAlpha = (uint)(32 - (update - 1) / 2);
                Assert(tracker.TryGetVanillaTransparency(buildingId, identities[index], 0,
                    priorAlpha, out uint vanillaSeed) && vanillaSeed == (update == 1 ? 31u : 30u),
                    "The original alpha must remain independent under load.");
                NativeFadeSample sample = tracker.Observe(buildingId, identities[index], 0,
                    12 + update % 14, 30);
                Assert(sample.EffectiveTransparency == 32 - update / 2,
                    "Every tannery must reach the same alpha after the same update count.");
            }
        for (int index = 0; index < buildingCount; index++)
            Assert(tracker.Observe(index + 1, identities[index], 6, 52, 0).Ended,
                "Every tannery must yield to Vanilla at the phase transition.");
        clock.Stop();
        Console.WriteLine($"500-tannery state benchmark: {clock.Elapsed.TotalMilliseconds:F3} ms for " +
            $"{buildingCount * (NativeTannerFadeState.FadeUpdates + 2)} observations; excludes native detour and game rendering.");

    }

    private static void TanneryProbe() { }

    private static void TestInstalledNativeDetourBackend()
    {
        const string extender = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        ResolveEventHandler resolver = (sender, args) =>
        {
            string candidate = Path.Combine(extender, new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            foreach (string name in new[] { "Microsoft.Extensions.Logging.Abstractions", "Iced",
                "RedBird.Abstractions", "RedBird.Backends.NativeX64", "RedBird.Core", "RedBird.X64" })
                Assembly.LoadFrom(Path.Combine(extender, name + ".dll"));
            Assembly backendAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(
                item => item.GetName().Name == "RedBird.Backends.NativeX64");
            Assembly abstractions = AppDomain.CurrentDomain.GetAssemblies().Single(
                item => item.GetName().Name == "RedBird.Abstractions");
            Type backendType = backendAssembly.GetType("RedBird.Backends.NativeX64.NativeDetourBackend", true);
            Type requestType = abstractions.GetType("RedBird.Abstractions.Hooks.DetourRequest`1", true)
                .MakeGenericType(typeof(TanneryProbeDelegate));
            MethodInfo create = backendType.GetMethods().Single(method => method.Name == "CreateDetour" &&
                method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(TanneryProbeDelegate));
            byte[] entry = { 0x40, 0x53, 0x55, 0x41, 0x54, 0x41, 0x57,
                0x48, 0x83, 0xEC, 0x28 };
            IntPtr memory = Marshal.AllocHGlobal(128);
            try
            {
                for (int i = 0; i < 128; i++)
                    Marshal.WriteByte(memory, i, 0x90);
                Marshal.Copy(entry, 0, memory, entry.Length);
                object request = Activator.CreateInstance(requestType);
                TanneryProbeDelegate callback = TanneryProbe;
                requestType.GetProperty("Name").SetValue(request, "Tannery native entry probe");
                requestType.GetProperty("TargetAddress").SetValue(request, unchecked((ulong)memory.ToInt64()));
                requestType.GetProperty("Callback").SetValue(request, callback);
                object candidate = create.Invoke(backendType.GetProperty("Instance").GetValue(null),
                    new[] { request });
                GC.KeepAlive(callback);
                try
                {
                    Type type = candidate.GetType();
                    string scheme = type.GetProperty("Scheme").GetValue(candidate).ToString();
                    int displaced = (int)type.GetProperty("DisplacedByteCount").GetValue(candidate);
                    Assert(scheme == "Indirect" && displaced == 7,
                        $"Native detour scheme {scheme} displaces an unexpected span {displaced}.");
                    Assert((ulong)type.GetProperty("TargetAddress").GetValue(candidate) ==
                        unchecked((ulong)memory.ToInt64()), "Backend target must match the copied entry.");
                    Assert((IntPtr)type.GetProperty("PointerSlot").GetValue(candidate) != IntPtr.Zero &&
                        (IntPtr)type.GetProperty("HookEntryPointAddress").GetValue(candidate) != IntPtr.Zero,
                        "Indirect backend must allocate a pointer slot and hook entry.");
                    Assert(!(bool)type.GetProperty("IsInstalled").GetValue(candidate),
                        "The backend probe must remain uninstalled.");
                    type.GetMethod("Enable").Invoke(candidate, null);
                    Assert((bool)type.GetProperty("IsInstalled").GetValue(candidate),
                        "The backend probe must install on the copied entry.");
                    var patch = new byte[6];
                    Marshal.Copy(memory, patch, 0, patch.Length);
                    Assert(patch[0] == 0xFF && patch[1] == 0x25,
                        "The indirect backend patch must use an FF 25 jump.");
                    IntPtr slot = (IntPtr)type.GetProperty("PointerSlot").GetValue(candidate);
                    Assert(memory.ToInt64() + 6 + BitConverter.ToInt32(patch, 2) == slot.ToInt64(),
                        "The copied entry must point to the backend pointer slot.");
                    Assert(Marshal.ReadInt64(slot) ==
                        ((IntPtr)type.GetProperty("HookEntryPointAddress").GetValue(candidate)).ToInt64(),
                        "The pointer slot must point to the hook entry.");
                }
                finally { ((IDisposable)candidate).Dispose(); }
                byte[] after = new byte[entry.Length];
                Marshal.Copy(memory, after, 0, after.Length);
                Assert(after.SequenceEqual(entry), "The native backend probe must leave its copied bytes untouched.");
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolver; }
    }
}
