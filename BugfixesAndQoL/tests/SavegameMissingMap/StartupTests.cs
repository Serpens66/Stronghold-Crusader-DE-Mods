using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BugfixesAndQoL.SavegameMissingMap
{
    internal static class StartupTests
    {
        private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
        internal static void Run(string game)
        {
            // Chainloader's static initializer needs BepInEx paths even in a desktop test.
            // Keep its config/cache writes in the fixture, never in the game installation.
            string host = TestWorkDirectory.New("SavegameMissingMapHost");
            typeof(BepInEx.Paths).GetMethod("SetExecutablePath", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Path.Combine(host, "Fixture.exe"), Path.Combine(host, "BepInEx"),
                    Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed"), new string[0] });
            // Exercise the real entry, including the Chainloader lookup and one-shot guard.
            // A null logger deliberately checks that a failed skip diagnostic cannot escape.
            var plugins = BepInEx.Bootstrap.Chainloader.PluginInfos;
            plugins.Add(SavegameMissingMapFeature.LegacyGuid, null);
            try
            {
                SavegameMissingMapFeature.Initialize(null, () => { throw new Exception("Recovery predicate evaluated during skip."); });
                Require(!SavegameFix.Enabled && SavegameFix.RestartConfirmation == null, "Legacy conflict reached hook initialization.");
            }
            finally { plugins.Remove(SavegameMissingMapFeature.LegacyGuid); }
            SavegameMissingMapFeature.Initialize(null, () => true);
            Require(!SavegameFix.Enabled && SavegameFix.RestartConfirmation == null, "Skipped feature retried installation in the same process.");
            Console.WriteLine("PASS: actual feature entry skips legacy plugin once and survives logger failure.");
            int installs = 0, skipLogs = 0;
            SavegameMissingMapFeature.InitializeUnlessLegacy(true, () => installs++, message => { skipLogs++; throw new IOException("logger"); });
            Require(installs == 0 && skipLogs == 1, "Legacy plugin conflict installed duplicate hooks or failed on logger.");
            SavegameMissingMapFeature.InitializeUnlessLegacy(false, () => installs++, message => skipLogs++);
            Require(installs == 1 && skipLogs == 1, "Feature without legacy plugin was skipped.");
            Console.WriteLine("PASS: legacy testplugin skips installation even with a failing logger; absence permits initialization.");
            string managed = Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed");
            var memoryAssembly = Assembly.Load(File.ReadAllBytes(Path.Combine(managed, "Assembly-CSharp.dll")));
            Require(string.IsNullOrEmpty(memoryAssembly.Location), "Regression fixture must have an empty assembly location.");
            Require(File.Exists(RuntimeStartup.ValidateGameAssembly(managed)), "Installed assembly validation failed with a byte-loaded game assembly.");
            string scratch = TestWorkDirectory.New("SavegameMissingMapFixTes");
            Directory.CreateDirectory(scratch);
            try
            {
                bool missing = false;
                try { RuntimeStartup.ValidateGameAssembly(scratch); }
                catch (FileNotFoundException ex) { missing = ex.FileName == Path.Combine(scratch, "Assembly-CSharp.dll"); }
                Require(missing, "Missing assembly was not rejected with its path.");
                File.WriteAllBytes(Path.Combine(scratch, "Assembly-CSharp.dll"), new byte[] { 1, 2, 3 });
                bool mismatch = false;
                try { RuntimeStartup.ValidateGameAssembly(scratch); }
                catch (InvalidOperationException ex) { mismatch = ex.Message.Contains("SHA256=") && ex.Message.Contains(scratch); }
                Require(mismatch, "Mismatched assembly hash was not rejected with diagnostics.");
            }
            finally
            {
                File.Delete(Path.Combine(scratch, "Assembly-CSharp.dll"));
                Directory.Delete(scratch);
            }
            foreach (bool immediate in new[] { false, true })
            foreach (int failureAt in new[] { -1, 0, 1, 2, 3, 4 })
            {
                var startup = new RuntimeStartup();
                var messages = new List<string>();
                var published = new List<object>();
                Action callback = null;
                bool active = false;
                int attempts = 0, errors = 0;
                startup.Subscribe(handler => { callback = handler; if (immediate) handler(); }, () =>
                {
                    attempts++;
                    Require(!active, "Fix activated before installation completed.");
                    for (int stage = 0; stage <= 4; stage++)
                    {
                        if (stage == failureAt) throw new InvalidOperationException("injected failure " + stage);
                        if (stage > 0) published.Add(new object());
                        Require(!active, "Fix active during partial hook installation.");
                    }
                }, value => active = value, messages.Add, ex => { errors++; messages.Add("FIX_DISABLED: " + ex.Message); });
                if (!immediate) Require(attempts == 0 && !active && messages.Count == 1, "Deferred library event initialized early.");
                callback(); callback();
                Require(attempts == 1, "Repeated library event initialized twice.");
                Require(messages.Count == 2 && messages[0].StartsWith("WAITING:"), "Startup log order is incorrect.");
                Require(active == (failureAt == -1) && errors == (failureAt == -1 ? 0 : 1), "Failure did not keep fix disabled.");
                Require(published.Count == (failureAt == -1 ? 4 : Math.Max(0, failureAt - 1)), "Published hook roots were removed after failure.");
                Require(messages[1].StartsWith(failureAt == -1 ? "READY:" : "FIX_DISABLED:"), "Final startup status is incorrect.");
            }
            foreach (bool failInitialization in new[] { false, true })
            {
                bool active = false;
                int attempts = 0, infoCalls = 0, errorCalls = 0;
                var failure = new InvalidOperationException("real initialization failure");
                Exception reported = null;
                new RuntimeStartup().Subscribe(callback => { callback(); callback(); }, () =>
                {
                    attempts++;
                    if (failInitialization) throw failure;
                }, value => active = value,
                message => { infoCalls++; throw new IOException("info sink failed"); },
                error => { errorCalls++; reported = error; throw new IOException("error sink failed"); });
                Require(attempts == 1 && active == !failInitialization, "Logger failure changed initialization result.");
                Require(infoCalls == (failInitialization ? 1 : 2), "WAITING/READY logging path not exercised.");
                Require(errorCalls == (failInitialization ? 1 : 0) && (!failInitialization || ReferenceEquals(reported, failure)), "Real startup failure was lost or replaced.");
            }
            Console.WriteLine("PASS: throwing WAITING/READY/error loggers preserve success, real failure and one-time initialization.");
            Console.WriteLine("PASS: byte-loaded assembly with empty Location, installed path/hash validation, immediate/deferred/repeated events and failures before each hook.");
        }
    }
}
