using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using WorkshopLoadProbe.Install;

namespace WorkshopLoadProbe
{
    // Game side of the safe-install prototype. Runs in the patcher's Finish, i.e. before the
    // chainloader has opened or loaded anything from BepInEx/plugins:
    //   1. recover every interrupted swap (journals),
    //   2. apply every pending request (staged package -> plugins/<folder>),
    //   3. optionally replace this patcher's own DLL (APIShared self-update question).
    // Layout below BepInEx/cache/WLPI: s/ staging, b/ backups, j/ journals, r/ requests, p/ patcher.
    internal static class InstallRunner
    {
        internal const string RequestProtocol = "WLP-INSTALL-1";

        internal static void Run()
        {
            var watch = Stopwatch.StartNew();
            string root = Path.Combine(BepInEx.Paths.CachePath, "WLPI");
            string plugins = Path.GetFullPath(BepInEx.Paths.PluginPath);
            Action<string> log = line => ProbeLog.Info("INSTALL", line);
            try
            {
                LogIdentity(root);
                string journals = Path.Combine(root, "j");
                if (Directory.Exists(journals))
                    foreach (string journal in Directory.GetFiles(journals, "*.journal"))
                    {
                        try { Swap.Recover(journal, log); }
                        catch (Exception ex) { ProbeLog.Error("INSTALL_ERROR", "recover " + journal + " " + ex.Message); }
                    }
                string requests = Path.Combine(root, "r");
                if (Directory.Exists(requests))
                    foreach (string request in Directory.GetFiles(requests, "*.request"))
                        Apply(request, root, plugins, log);
                ReplaceOwnFile(root);
            }
            catch (Exception ex) { ProbeLog.Error("INSTALL_ERROR", ex.ToString()); }
            ProbeLog.Info("INSTALL_DONE", "ms=" + watch.ElapsedMilliseconds);
        }

        private static void Apply(string request, string root, string plugins, Action<string> log)
        {
            try
            {
                string[] lines = File.ReadAllLines(request);
                if (lines.Length < 3 || lines[0] != RequestProtocol) throw new InvalidDataException("unknown request");
                string folder = lines[1];
                if (folder.Length == 0 || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || folder.StartsWith(".", StringComparison.Ordinal))
                    throw new InvalidDataException("bad folder " + folder);
                string staged = Path.GetFullPath(lines[2]);
                if (!staged.StartsWith(Path.Combine(root, "s") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("staged outside staging root " + staged);
                string target = Path.Combine(plugins, folder);
                Swap.Apply(target, staged, Path.Combine(Path.Combine(root, "b"), folder), Path.Combine(Path.Combine(root, "j"), folder + ".journal"), log);
                File.Delete(request);
                ProbeLog.Info("INSTALL_APPLIED", folder + " files=" + Tree.Files(target).Count);
            }
            catch (Exception ex) { ProbeLog.Error("INSTALL_ERROR", "request " + Path.GetFileName(request) + " kept: " + ex.Message); }
        }

        // The patcher DLL is loaded right now. File.Replace swaps the file in one step and keeps
        // the old one as .old; the new code runs from the next start.
        private static void ReplaceOwnFile(string root)
        {
            string own = typeof(InstallRunner).Assembly.Location;
            string old = own + ".old";
            if (File.Exists(old)) { try { File.Delete(old); ProbeLog.Info("INSTALL", "removed previous " + Path.GetFileName(old)); } catch (Exception ex) { ProbeLog.Warning("INSTALL", "old patcher kept: " + ex.Message); } }
            string staged = Path.Combine(Path.Combine(root, "p"), Path.GetFileName(own));
            if (!File.Exists(staged)) return;
            try
            {
                File.Replace(staged, own, old);
                ProbeLog.Info("INSTALL_PATCHER_REPLACED", "file now " + Tree.Sha256(own).Substring(0, 16) + "; takes effect at next start");
            }
            catch (Exception ex) { ProbeLog.Error("INSTALL_ERROR", "patcher replace " + ex.GetType().Name + ": " + ex.Message); }
        }

        private static void LogIdentity(string root)
        {
            var assembly = typeof(InstallRunner).Assembly;
            ProbeLog.Info("PATCHER_IDENTITY", "mvid=" + assembly.ManifestModule.ModuleVersionId + " file=" + Tree.Sha256(assembly.Location).Substring(0, 16) + " root=" + root);
        }
    }
}
