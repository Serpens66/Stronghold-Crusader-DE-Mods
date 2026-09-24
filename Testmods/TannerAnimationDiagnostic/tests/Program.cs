using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using TannerAnimationDiagnostic;

internal static class Program
{
    private static int Main()
    {
        try
        {
            var lines = new List<string>();
            var first = new VisualTimeline("UNIT id=1", lines.Add);
            var second = new VisualTimeline("UNIT id=2", lines.Add);
            long secondTicks = Stopwatch.Frequency;
            first.Observe(0, Sample(100, 24, 0.25f));
            first.Observe(secondTicks / 2, Sample(101, 24, 0.25f));
            first.Observe(secondTicks, Sample(102, 24, 0.25f));
            Assert(lines.Count(x => x.Contains("UNIT id=1 ALPHA_END")) == 0,
                "Image changes must not end the current alpha span.");
            second.Observe(secondTicks, Sample(200, 0, 1f));
            first.Observe(secondTicks * 2, Sample(103, 0, 1f));
            Assert(lines.Any(x => x.Contains("UNIT id=1 ALPHA_END raw=24") && x.Contains("duration_ms=2000.000")),
                "The first alpha interval must span both image changes.");
            Assert(!lines.Any(x => x.Contains("UNIT id=2 ALPHA_END")),
                "A second unit must keep its own interval.");
            first.Observe(secondTicks * 3, Sample(103, 0, 1f));
            Assert(lines.Count(x => x.Contains("UNIT id=1 FRAME")) == 3,
                "Repeated frames must not produce transition logs.");
            first.Close(secondTicks * 4, "visual_removed");
            second.Close(secondTicks * 4, "visual_removed");
            Assert(lines.Any(x => x.Contains("UNIT id=1 ALPHA_END raw=0") && x.Contains("duration_ms=2000.000")),
                "Visual removal must close the active alpha interval.");
            Assert(lines.Any(x => x.Contains("UNIT id=2 ALPHA_END raw=0") && x.Contains("duration_ms=3000.000")),
                "Each unit must retain its own timing origin.");
            first.Observe(secondTicks * 5, Sample(100, 32, 0f, false));
            Assert(lines.Count(x => x.Contains("UNIT id=1 START")) == 2,
                "Reappearing visuals must begin a fresh timeline.");
            first.Observe(secondTicks * 6, Sample(100, 0, 1f, true));
            Assert(lines.Any(x => x.Contains("UNIT id=1 VISIBLE")),
                "First visible frame must be marked separately from visual creation.");
            TestNativeFadeModel();
            TestInstalledNativeDetourBackend();
            Console.WriteLine("TannerAnimationDiagnostic timeline tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static VisualSample Sample(int image, int raw, float alpha, bool visible = true) =>
        new VisualSample(image, raw, alpha, visible, 1, 2, 3);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void TestNativeFadeModel()
    {
        var tracker = new NativeTannerFadeTracker();
        NativeFadeSample first = tracker.Observe(179, 101, 12, 31, 0);
        Assert(first.Started && first.EffectiveTransparency == 32,
            "The first native packet must begin fully transparent.");
        NativeFadeSample paused = tracker.Observe(179, 101, 13, 30, 0);
        Assert(paused.EffectiveTransparency == 32 && paused.ElapsedTicks == 0,
            "Rendering new images during pause must not advance the fade.");
        NativeFadeSample faster = tracker.Observe(179, 101, 14, 30, 8);
        Assert(faster.EffectiveTransparency == 23,
            "Eight unpaused ticks must advance the fade regardless of wall-clock time.");
        NativeFadeSample second = tracker.Observe(180, 202, 12, 31, 8);
        Assert(second.Started && second.EffectiveTransparency == 32,
            "Two tanneries must keep independent fade clocks.");
        Assert(tracker.Observe(179, 101, 20, 30, 15).EffectiveTransparency == 16,
            "Half the simulation ticks must produce half opacity.");
        Assert(tracker.Observe(179, 101, 1, 30, 30).EffectiveTransparency == 0,
            "Thirty unpaused ticks must reach full opacity.");
        Assert(tracker.Observe(179, 101, 25, 30, 100).EffectiveTransparency == 0,
            "The long phase must remain opaque across image changes.");
        NativeFadeSample end = tracker.Observe(179, 101, 26, 0, 101);
        Assert(end.Ended && end.EffectiveTransparency == 0,
            "The next native phase must receive its unchanged Vanilla alpha.");
        Assert(!tracker.Observe(179, 101, 26, 31, 102).Started,
            "A later Vanilla fade must not restart the initial correction.");
        Assert(tracker.Observe(180, 202, 13, 30, 23).EffectiveTransparency == 16,
            "The second tannery must retain its own start tick.");
        NativeFadeSample replaced = tracker.Observe(180, 303, 12, 31, 24);
        Assert(replaced.Ended && replaced.Started && replaced.EffectiveTransparency == 32,
            "A new generation in the same building slot must restart independently.");
        tracker.Clear();
        Assert(tracker.Observe(180, 303, 14, 30, 200).Started,
            "A map reset must discard all prior fade state.");
        Assert(tracker.Observe(181, 404, 17, 30, 200).Started,
            "A save loaded mid-phase must start its own fade.");
    }

    private delegate void TanneryProbeDelegate();
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
            byte[] entry = { 0x4C, 0x8B, 0xDC, 0x53, 0x55, 0x57, 0x41, 0x56,
                0x41, 0x57, 0x48, 0x83, 0xEC, 0x60 };
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
                    Assert(scheme == "Indirect" && displaced == 6,
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
