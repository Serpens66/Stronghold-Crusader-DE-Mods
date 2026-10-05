using System;
using System.Reflection;

// Test publishers/settings only. AIKeepRangeRuntime is linked unchanged from production.
namespace Shared
{
    internal static class MissionEvents
    {
        internal static readonly R3.Subject<int> Initialization = new R3.Subject<int>();
        internal static readonly R3.Subject<int> Ended = new R3.Subject<int>();
        internal static void SetOwner(string owner) { }
    }
}
namespace BugfixesAndQoL
{
    internal sealed class BugfixesAndQoLViewModel
    {
        public bool EnableMod = true;
        public bool RemoveAIKeepRangeLimit = true;
    }
    internal static class BugfixesAndQoLPlugin { public const string PluginGuid = "BugfixesAndQoL_Serp"; }
    internal static class RuntimeHarness
    {
        private static FieldInfo Field(string name) => typeof(AIKeepRangeRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run()
        {
            var settings = new BugfixesAndQoLViewModel();
            var runtime = new AIKeepRangeRuntime(null, settings);
            runtime.Refresh();
            Check(!runtime.LobbyBypass, "Lobby cannot bypass before installation");
            Check((int)Field("enabled").GetValue(runtime) == 0, "No activation before successful installation");
            // Native installation is independently tested against the real backend in Program.
            Field("installed").SetValue(runtime, true);
            int originalCount = 0, classifications = 0;
            bool classificationFails = false;
            Func<int, bool> classifier = id =>
            {
                classifications++;
                if (classificationFails) throw new InvalidOperationException("Synthetic classifier failure");
                return id == 2;
            };
            AIKeepDistanceCheck original = (_, __, ___, ____, _____) => { originalCount++; return 17; };
            Field("isAI").SetValue(runtime, classifier);
            Field("original").SetValue(runtime, original);
            var callback = (AIKeepDistanceCheck)Field("callback").GetValue(runtime);
            runtime.SubscribeLifecycle(); runtime.SubscribeLifecycle();
            Shared.MissionEvents.Initialization.OnNext(0); // BeforeNativeStart/prebuild
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 0 && originalCount == 0, "AI active before prebuild");
            Check(callback(IntPtr.Zero, 1, 357, 518, 70) == 17 && originalCount == 1, "Human original once");
            Check(callback(IntPtr.Zero, 0, 357, 518, 70) == 17 && originalCount == 2 && classifications == 2, "Invalid ID not classified");
            settings.RemoveAIKeepRangeLimit = false; runtime.Refresh();
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 17 && originalCount == 3, "Feature disabled original once");
            settings.RemoveAIKeepRangeLimit = true; settings.EnableMod = false; runtime.Refresh();
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 17 && originalCount == 4, "Host activation disabled");
            settings.EnableMod = true;
            Shared.MissionEvents.Initialization.OnNext(1); // NativeLoaded/save
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 0 && originalCount == 4, "Save initialization restores logical state");
            Shared.MissionEvents.Ended.OnNext(0);
            Check(runtime.LobbyBypass, "Next-game lobby capability survives mission end");
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 17 && originalCount == 5, "Mission end disabled");
            Shared.MissionEvents.Initialization.OnNext(0);
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 0, "Next mission reactivates");
            classificationFails = true;
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 17 && originalCount == 6, "Classifier failure original once");
            classificationFails = false; runtime.Refresh(); Shared.MissionEvents.Initialization.OnNext(1);
            Check(!runtime.LobbyBypass, "Faulted hook cannot advertise lobby bypass");
            Check(callback(IntPtr.Zero, 2, 357, 518, 70) == 17 && originalCount == 7, "Fault stays disabled across refresh/save");
            Console.WriteLine("PASS: production AI runtime: settings, prebuild/save publishers, mission change, original counts, persistent fail-closed.");
        }
    }
}
