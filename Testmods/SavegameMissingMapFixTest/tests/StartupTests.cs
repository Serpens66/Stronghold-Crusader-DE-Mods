using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SavegameMissingMapFixTest
{
    internal static class StartupTests
    {
        private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
        internal static void Run(string game)
        {
            string managed = Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed");
            var memoryAssembly = Assembly.Load(File.ReadAllBytes(Path.Combine(managed, "Assembly-CSharp.dll")));
            Require(string.IsNullOrEmpty(memoryAssembly.Location), "Regression fixture must have an empty assembly location.");
            Require(File.Exists(RuntimeStartup.ValidateGameAssembly(managed)), "Installed assembly validation failed with a byte-loaded game assembly.");
            string scratch = Path.Combine(Path.GetTempPath(), "SavegameMissingMapFixTest-" + Guid.NewGuid().ToString("N"));
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
            Console.WriteLine("PASS: byte-loaded assembly with empty Location, installed path/hash validation, immediate/deferred/repeated events and failures before each hook.");
        }
    }
}
