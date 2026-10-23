using System;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared.Audio;
using System.Xml.Linq;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private static string game, workspace;
        private static int checks;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        private static int Main(string[] args)
        {
            game = args[0]; workspace = args[1];
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try { Run(); Console.WriteLine("PASS: Arabian idle volume (" + checks + " checks)."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            foreach (string dir in new[] {
                Environment.GetEnvironmentVariable("SHCDE_API_SHARED_DIR") ?? Path.Combine(workspace, "APIShared/BepInEx/plugins/APIShared_Serp"),
                Path.Combine(game, "BepInEx/plugins/000shcdese"), Path.Combine(game, "BepInEx/core"),
                Path.Combine(game, "Stronghold Crusader Definitive Edition_Data/Managed") })
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }
        private static void Check(bool condition, string message)
        { checks++; if (!condition) throw new Exception(message); }
        private static SoundPreEventArgs Request(SoundSituation situation = SoundSituation.ArabianIdleSwordDraw,
            int? type = 75, SituationEvidence evidence = SituationEvidence.InGameConfirmed, int effect = 180)
        {
            var context = (SoundContext)typeof(SoundContext).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(x => x.GetParameters().Length == 10).Invoke(new object[] {
                    effect, 2f, 0.3f, SoundPlaybackKind.Spatial, situation, (int?)10, (int?)12, evidence, type, "Unit75.Source04" });
            return (SoundPreEventArgs)typeof(SoundPreEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single().Invoke(new object[] { context, false, (Func<int, bool>)(id => true) });
        }
        private static void Run()
        {
            TestRealSetting();
            var settings = new BugfixesAndQoLViewModel();
            ArabianIdleSoundRuntime.Configure(null, settings); // No game: registration must fail closed.
            Check(Shared.DebugLogHelper.Failures == 1, "Unavailable API must log failure.");
            settings.Notify(nameof(settings.ArabianIdleSoundVolumePercent));
            var request = Request(); ArabianIdleSoundRuntime.Apply(request);
            Check(request.Volume == 2f, "Failed registration must remain inactive after a settings change.");
            var registration = (SoundRegistration)Activator.CreateInstance(typeof(SoundRegistration), true);
            typeof(ArabianIdleSoundRuntime).GetField("registration", Hidden).SetValue(null, registration);
            foreach (int percent in new[] { 0, 1, 25, 50, 99, 100 })
            {
                settings.ArabianIdleSoundVolumePercent = percent;
                settings.Notify(nameof(settings.ArabianIdleSoundVolumePercent));
                request = Request(); ArabianIdleSoundRuntime.Apply(request);
                Check(registration.Enabled == (percent < 100), "Demand gate for " + percent);
                Check(request.SkipOriginalFunction == (percent == 0), "Suppression for " + percent);
                Check(Math.Abs(request.Volume - (percent == 0 ? 2f : 2f * percent / 100f)) < 0.00001f, "Gain for " + percent);
                Check(request.SoundId == 180 && request.Pan == 0.3f, "Target and pan preserved.");
            }
            settings.ArabianIdleSoundVolumePercent = 25;
            settings.EnableClientFeatures = false; settings.Notify(nameof(settings.EnableClientFeatures));
            request = Request(); ArabianIdleSoundRuntime.Apply(request);
            Check(!registration.Enabled && request.Volume == 2f, "Client gate restores Vanilla.");
            settings.EnableClientFeatures = true; settings.Notify(nameof(settings.EnableClientFeatures));
            request = Request(); request.MultiplyVolume(0.5f); ArabianIdleSoundRuntime.Apply(request);
            Check(request.Volume == 0.25f, "Independent factors compose.");
            foreach (var other in new[] { Request(SoundSituation.Unknown), Request(type: 27), Request(type: null),
                Request(evidence: SituationEvidence.Inferred), Request(effect: 179),
                Request(SoundSituation.ArabianIdleSwordSheathe), Request(SoundSituation.MeleeWeaponSwing) })
            {
                ArabianIdleSoundRuntime.Apply(other);
                Check(other.Volume == 2f && !other.SkipOriginalFunction, "Unrelated/unconfirmed request changed.");
            }
            request = Request(); request.SkipOriginalFunction = true; ArabianIdleSoundRuntime.Apply(request);
            Check(request.SkipOriginalFunction && request.Volume == 2f, "Foreign suppression preserved.");
            for (int i = 0; i < 100; i++) { request = Request(); ArabianIdleSoundRuntime.Apply(request); }
            Check(Shared.DebugLogHelper.Messages == 1, "Confirmation logging is bounded.");
            foreach (int percent in new[] { -100, 999 })
            {
                settings.ArabianIdleSoundVolumePercent = percent; settings.Notify(nameof(settings.ArabianIdleSoundVolumePercent));
                request = Request(); ArabianIdleSoundRuntime.Apply(request);
                Check(percent < 0 ? request.SkipOriginalFunction : !registration.Enabled, "Corrupt input safely bounded.");
            }
            // Contexts are immutable per request; there is no retained unit/map pointer.
            settings.ArabianIdleSoundVolumePercent = 25; settings.Notify(nameof(settings.ArabianIdleSoundVolumePercent));
            request = Request(); ArabianIdleSoundRuntime.Apply(request);
            Check(request.Volume == 0.5f, "Fresh context after reactivation.");
        }
        private static string Member(string source, string anchor)
        {
            int start = source.IndexOf(anchor, StringComparison.Ordinal);
            if (start < 0) throw new Exception("Missing member " + anchor);
            int open = source.IndexOf('{', start), depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
            }
            throw new Exception("Unclosed member " + anchor);
        }
        private static void TestRealSetting()
        {
            string source = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs"));
            // Compile the actual field, property and bounded setter without constructing Noesis.
            string defaultValue = source.Split(new[] { '\r', '\n' }).Single(x => x.Contains("private const int DefaultArabianIdleSoundVolumePercent"));
            string field = source.Split(new[] { '\r', '\n' }).Single(x => x.Contains("private int arabianIdleSoundVolumePercent"));
            string property = Member(source, "[APIShared.ModSettings.PresetLocal]\r\n        public int ArabianIdleSoundVolumePercent");
            string setter = Member(source, "private void SetBoundedIntSetting(");
            string fixture = "using System; using System.Collections.Generic; public class Fixture { " + defaultValue + field + property + setter +
                " public event Action<string> SettingChanged; public string ArabianIdleSoundVolumeValueText => ArabianIdleSoundVolumePercent.ToString();" +
                " public bool Allowed = true; public int Notifications; bool CanMutateSettingWithDependents(string a,string b) => Allowed;" +
                " void OnPropertyChanged(string name) { Notifications++; } }";
            string directory = TestWorkDirectory.New("arabian-setting");
            Directory.CreateDirectory(directory);
            string input = Path.Combine(directory, "Fixture.cs"), output = Path.Combine(directory, "Fixture.dll");
            File.WriteAllText(input, fixture.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            string compiler = @"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe";
            var launch = new System.Diagnostics.ProcessStartInfo(compiler,
                "/nologo /target:library /langversion:latest /out:\"" + output + "\" /reference:\"" +
                typeof(APIShared.ModSettings.PresetLocalAttribute).Assembly.Location + "\" \"" + input + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(launch))
            {
                string diagnostics = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit(); Check(process.ExitCode == 0, diagnostics);
            }
            var type = Assembly.LoadFrom(output).GetType("Fixture"); var item = Activator.CreateInstance(type);
            var prop = type.GetProperty("ArabianIdleSoundVolumePercent");
            Check((int)prop.GetValue(item) == 25, "Missing persisted value defaults to 25.");
            Check(prop.IsDefined(typeof(APIShared.ModSettings.PresetLocalAttribute), false), "Setting is local.");
            foreach (int value in new[] { -1, 0, 25, 100, 101 }) {
                prop.SetValue(item, value); Check((int)prop.GetValue(item) == Math.Max(0, Math.Min(100, value)), "Real setter bounds.");
            }
            prop.SetValue(item, 37); type.GetField("Allowed").SetValue(item, false); prop.SetValue(item, 80);
            Check((int)prop.GetValue(item) == 37, "Real setter respects mode/preset authority.");
            type.GetField("Allowed").SetValue(item, true);
            int n = (int)type.GetField("Notifications").GetValue(item); prop.SetValue(item, 37);
            Check((int)type.GetField("Notifications").GetValue(item) == n, "Repeated value does not notify.");

            var xml = XDocument.Load(Path.Combine(workspace, "BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml"));
            XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var slider = xml.Descendants(ui + "Slider").Single(x => ((string)x.Attribute("Value") ?? "").Contains("ArabianIdleSoundVolumePercent"));
            Check((string)slider.Attribute("Minimum") == "0" && (string)slider.Attribute("Maximum") == "100" &&
                (string)slider.Attribute("TickFrequency") == "1" && (string)slider.Attribute("SmallChange") == "1" &&
                (string)slider.Attribute("LargeChange") == "1" && (string)slider.Attribute("IsSnapToTickEnabled") == "True", "Slider contract.");
        }
    }
}
