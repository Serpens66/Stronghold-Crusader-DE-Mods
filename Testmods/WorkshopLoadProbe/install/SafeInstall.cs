using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WorkshopLoadProbe.Install
{
    // Prototype of the "safe complete installation" (plan WorkshopLoad.md):
    //   1. Stage: copy a package root into a separate staging folder, verify the copy by hash,
    //      then publish it with one rename (".tmp" -> final) and a completion marker.
    //   2. Swap (preloader, before any plugin is loaded): journal, carry over the files listed in
    //      apishared-keep.txt, rename installed folder -> backup, rename staged -> installed,
    //      delete backup. Everything not in the new package and not listed disappears.
    //   3. Recover (preloader, before any plugin is loaded): decide from the file system
    //      which side of the swap is complete and finish or undo it.
    // At no point is a plugin folder modified file by file, so BepInEx only ever sees the
    // complete old or the complete new version. No BepInEx/Unity types: the offline harness
    // compiles the same file.
    internal sealed class FileEntry
    {
        internal readonly string Path;
        internal readonly long Size;
        internal readonly string Sha256;
        internal FileEntry(string path, long size, string sha) { Path = path; Size = size; Sha256 = sha; }
    }

    internal static class Tree
    {
        internal const string CompleteName = ".wlp-complete";

        internal static string Sha256(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        // Relative '/'-separated paths of all files below root, excluding installer metadata.
        internal static List<string> Files(string root)
        {
            var result = new List<string>();
            if (!Directory.Exists(root)) return result;
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                foreach (string sub in Directory.GetDirectories(directory))
                {
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) throw new IOException("link inside package: " + sub);
                    pending.Push(sub);
                }
                foreach (string file in Directory.GetFiles(directory))
                {
                    string relative = file.Substring(root.Length + 1).Replace('\\', '/');
                    if (relative != CompleteName && relative != Keep.StateName) result.Add(relative);
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        internal static bool IsComplete(string root) => File.Exists(System.IO.Path.Combine(root, CompleteName));

        internal static string Native(string relative) => relative.Replace('/', System.IO.Path.DirectorySeparatorChar);

        internal static void WriteDurable(string path, string text)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        // Copies a tree over another (existing files are overwritten), without following links.
        internal static int CopyOver(string source, string target)
        {
            int count = 0;
            foreach (string relative in Files(source))
            {
                string to = System.IO.Path.Combine(target, Native(relative));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to));
                File.Copy(System.IO.Path.Combine(source, Native(relative)), to, true);
                count++;
            }
            return count;
        }

        // Deletes a tree without following links. Read-only attributes are cleared first.
        internal static void Delete(string root)
        {
            if (!Directory.Exists(root)) return;
            foreach (string sub in Directory.GetDirectories(root))
            {
                if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) Directory.Delete(sub, false);
                else Delete(sub);
            }
            foreach (string file in Directory.GetFiles(root))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            Directory.Delete(root, false);
        }
    }

    // apishared-keep.txt: the only thing a mod author maintains. Any folder of the package may
    // contain one; each line is a path relative to that folder: a file ("settings.json") or,
    // with a trailing '/', a folder ("config/"). An optional number after a space is the reset
    // revision ("presets.json 2"): raising it replaces the player's copy once with the shipped
    // one (or removes it if none is shipped). '#' starts a comment. No wildcards.
    internal static class Keep
    {
        internal const string ListName = "apishared-keep.txt";
        internal const string StateName = ".apishared-keep.state";

        internal sealed class Entry
        {
            internal string Path;      // relative to the package root, '/'-separated
            internal bool Folder;
            internal int Revision;
        }

        internal static List<Entry> Read(string packageRoot)
        {
            var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (string list in Tree.Files(packageRoot).Where(p => p == ListName || p.EndsWith("/" + ListName, StringComparison.OrdinalIgnoreCase)))
            {
                string prefix = list.Length == ListName.Length ? "" : list.Substring(0, list.Length - ListName.Length);
                int number = 0;
                foreach (string raw in File.ReadAllLines(System.IO.Path.Combine(packageRoot, Tree.Native(list))))
                {
                    number++;
                    string line = raw;
                    int comment = line.IndexOf('#');
                    if (comment >= 0) line = line.Substring(0, comment);
                    line = line.Trim();
                    if (line.Length == 0) continue;
                    var entry = new Entry();
                    int space = line.LastIndexOfAny(new[] { ' ', '\t' });
                    if (space > 0 && int.TryParse(line.Substring(space + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int revision))
                    {
                        entry.Revision = revision;
                        line = line.Substring(0, space).TrimEnd();
                    }
                    string path = line.Replace('\\', '/');
                    entry.Folder = path.EndsWith("/", StringComparison.Ordinal);
                    path = path.TrimEnd('/');
                    if (path.Length == 0 || path.StartsWith("/", StringComparison.Ordinal) || path.IndexOf(':') >= 0 || path.IndexOfAny(new[] { '*', '?' }) >= 0 ||
                        path.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                        throw new InvalidDataException(list + " line " + number + ": invalid path '" + raw.Trim() + "'");
                    entry.Path = prefix + path;
                    if (entries.ContainsKey(entry.Path)) throw new InvalidDataException(list + " line " + number + ": duplicate entry " + entry.Path);
                    entries[entry.Path] = entry;
                }
            }
            return entries.Values.OrderBy(e => e.Path, StringComparer.Ordinal).ToList();
        }

        internal static Dictionary<string, int> ReadState(string root)
        {
            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string path = System.IO.Path.Combine(root, StateName);
            if (!File.Exists(path)) return state; // first installation with this system: revision 0
            foreach (string line in File.ReadAllLines(path))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int revision)) state[parts[0]] = revision;
            }
            return state;
        }

        // Folder next to the staged package (never inside plugins): the shipped files a carry-over
        // overwrote and the list of carried files, so the next attempt starts from the pristine
        // package again.
        internal static string UndoFolder(string staged) => staged + ".undo";

        // Copies the listed player files from the installed folder into the staged (not yet
        // loaded) folder and writes the applied revisions. Every attempt first undoes the previous
        // attempt, so the result depends only on the current installed folder: newest player data
        // wins, and a file the player deleted meanwhile falls back to the shipped default.
        internal static string CarryOver(string target, string staged)
        {
            Undo(staged);
            var entries = Read(staged);
            var applied = ReadState(target);
            var state = new StringBuilder();
            var copies = new List<string>(); // relative file paths, root-based
            int kept = 0, reset = 0;
            foreach (var entry in entries)
            {
                state.Append(entry.Path).Append('\t').Append(entry.Revision.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                string from = System.IO.Path.Combine(target, Tree.Native(entry.Path));
                bool present = entry.Folder ? Directory.Exists(from) : File.Exists(from);
                if (!present) continue;
                applied.TryGetValue(entry.Path, out int old);
                if (entry.Revision > old) { reset++; continue; } // shipped version (or nothing) wins once
                if (entry.Folder)
                {
                    if ((File.GetAttributes(from) & FileAttributes.ReparsePoint) != 0) throw new IOException("link in kept folder " + from);
                    copies.AddRange(Tree.Files(from).Select(p => entry.Path + "/" + p));
                }
                else copies.Add(entry.Path);
                kept++;
            }
            if (copies.Count > 0)
            {
                string undo = UndoFolder(staged);
                foreach (string relative in copies)
                {
                    string shipped = System.IO.Path.Combine(staged, Tree.Native(relative));
                    if (!File.Exists(shipped)) continue;
                    string save = System.IO.Path.Combine(undo, "shipped", Tree.Native(relative));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(save));
                    File.Copy(shipped, save, true);
                }
                Directory.CreateDirectory(undo);
                // Written before the first overwrite: a crash afterwards is undone completely.
                Tree.WriteDurable(System.IO.Path.Combine(undo, "carried.txt"), string.Join("\r\n", copies.ToArray()) + "\r\n");
                foreach (string relative in copies)
                {
                    string to = System.IO.Path.Combine(staged, Tree.Native(relative));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to));
                    File.Copy(System.IO.Path.Combine(target, Tree.Native(relative)), to, true);
                }
            }
            if (entries.Count > 0) Tree.WriteDurable(System.IO.Path.Combine(staged, StateName), state.ToString());
            return "kept=" + kept + " reset=" + reset + " files=" + copies.Count;
        }

        // Restores the pristine staged package after an interrupted or failed attempt.
        internal static void Undo(string staged)
        {
            string undo = UndoFolder(staged);
            if (!Directory.Exists(undo)) return;
            string carried = System.IO.Path.Combine(undo, "carried.txt");
            if (File.Exists(carried))
                foreach (string relative in File.ReadAllLines(carried).Where(l => l.Length > 0))
                {
                    string file = System.IO.Path.Combine(staged, Tree.Native(relative));
                    if (!File.Exists(file)) continue;
                    File.Delete(file);
                    // Drop folders that only existed because of carried files.
                    for (string dir = System.IO.Path.GetDirectoryName(file);
                         dir.Length > staged.Length && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any();
                         dir = System.IO.Path.GetDirectoryName(dir))
                        Directory.Delete(dir);
                }
            string shipped = System.IO.Path.Combine(undo, "shipped");
            if (Directory.Exists(shipped)) Tree.CopyOver(shipped, staged);
            File.Delete(System.IO.Path.Combine(staged, StateName));
            Tree.Delete(undo);
        }
    }

    internal static class Staging
    {
        // Test hook: called after each copied file with its relative path.
        internal static Action<string> AfterCopy { get; set; }

        // Copies exactly the expected files from sourceRoot, verifies the written copies and
        // publishes <parent>/<version>. An existing complete stage is reused unchanged.
        internal static string Stage(string sourceRoot, IList<FileEntry> expected, string parent, string version, Action<string> log)
        {
            if (version.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || version.Contains(".")) throw new ArgumentException("bad version key " + version);
            string final = System.IO.Path.Combine(parent, version);
            if (Tree.IsComplete(final)) { log("STAGE_REUSED " + final); return final; }
            if (Directory.Exists(final)) Tree.Delete(final); // incomplete leftovers are never trusted
            string temporary = final + ".tmp";
            if (Directory.Exists(temporary)) { Tree.Delete(temporary); log("STAGE_DISCARDED_TMP " + temporary); }
            Directory.CreateDirectory(temporary);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            long bytes = 0;
            try
            {
                foreach (var entry in expected)
                {
                    string source = System.IO.Path.Combine(sourceRoot, Tree.Native(entry.Path));
                    string target = System.IO.Path.Combine(temporary, Tree.Native(entry.Path));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                    using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
                    {
                        input.CopyTo(output, 1 << 16);
                        output.Flush(true);
                    }
                    bytes += entry.Size;
                    AfterCopy?.Invoke(entry.Path);
                }
                // Verify the copy, not the source: whatever Steam does meanwhile, only bytes
                // that match the manifest are ever published.
                var present = Tree.Files(temporary);
                if (present.Count != expected.Count) throw new InvalidDataException("file count " + present.Count + " != " + expected.Count);
                foreach (var entry in expected)
                {
                    string copy = System.IO.Path.Combine(temporary, Tree.Native(entry.Path));
                    if (new FileInfo(copy).Length != entry.Size) throw new InvalidDataException("size mismatch " + entry.Path);
                    if (Tree.Sha256(copy) != entry.Sha256) throw new InvalidDataException("hash mismatch " + entry.Path);
                }
                Keep.Read(temporary); // a malformed keep list is rejected here, not at the swap
                Tree.WriteDurable(System.IO.Path.Combine(temporary, Tree.CompleteName), version + "\r\n");
                Directory.Move(temporary, final);
            }
            catch
            {
                try { Tree.Delete(temporary); } catch { }
                throw;
            }
            log("STAGED " + final + " files=" + expected.Count + " bytes=" + bytes + " ms=" + watch.ElapsedMilliseconds);
            return final;
        }
    }

    internal static class Swap
    {
        internal const string Protocol = "WLP-SWAP-1";
        // Test hook: called at each step; throwing simulates a crash at that point.
        internal static Action<string> Fault { get; set; }

        private static void Step(string name) => Fault?.Invoke(name);

        internal static void Apply(string target, string staged, string backup, string journal, Action<string> log)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (File.Exists(journal)) Recover(journal, log);
            if (!Tree.IsComplete(staged)) throw new InvalidOperationException("staged package incomplete: " + staged);
            // Without a journal a backup is a finished swap's leftover, never the only copy.
            if (Directory.Exists(backup)) { Tree.Delete(backup); log("SWAP_STALE_BACKUP_REMOVED " + backup); }
            // Directory.Move needs existing parents (found in game test G2).
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(journal));
            string temporary = journal + ".tmp";
            Tree.WriteDurable(temporary, Protocol + "\r\n" + target + "\r\n" + staged + "\r\n" + backup + "\r\n");
            File.Move(temporary, journal);
            Step("journal");

            string carried = Keep.CarryOver(target, staged);
            Step("carried");

            if (Directory.Exists(target))
            {
                try { Directory.Move(target, backup); }
                catch
                {
                    // Nothing moved (e.g. a file in the installed folder is held open): drop the
                    // journal so the old version stays as it is.
                    File.Delete(journal);
                    throw;
                }
                Step("old-moved");
            }
            try
            {
                Directory.Move(staged, target);
            }
            catch (Exception ex)
            {
                // Undo immediately; if that fails too, the journal makes the next start finish it.
                if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
                if (Directory.Exists(target)) File.Delete(journal);
                log("SWAP_ROLLED_BACK " + target + " " + ex.Message);
                throw;
            }
            Step("new-moved");
            // The new version is live now. A failing cleanup (e.g. a scanner holding a backup
            // file) must not report the swap as failed; the kept journal finishes it later.
            try
            {
                Tree.Delete(Keep.UndoFolder(staged));
                Tree.Delete(backup);
                Step("backup-deleted");
                File.Delete(journal);
                Step("done");
            }
            catch (IOException ex) { log("SWAP_CLEANUP_DEFERRED " + backup + " " + ex.Message); }
            catch (UnauthorizedAccessException ex) { log("SWAP_CLEANUP_DEFERRED " + backup + " " + ex.Message); }
            log("SWAPPED " + target + " " + carried + " ms=" + watch.ElapsedMilliseconds);
        }

        // Decides from the file system, not from journal progress lines, so a crash between
        // any rename and any journal write is covered.
        internal static string Recover(string journal, Action<string> log)
        {
            string[] lines = File.ReadAllLines(journal);
            if (lines.Length < 4 || lines[0] != Protocol) throw new InvalidDataException("unknown journal " + journal);
            string target = lines[1], staged = lines[2], backup = lines[3];
            bool t = Directory.Exists(target), b = Directory.Exists(backup), s = Directory.Exists(staged);
            string outcome;
            if (t && !b && s) outcome = "not-started";                 // nothing moved; staged stays for a new Apply
            else if (!t && b && s) { Directory.Move(staged, target); Step("recover-new-moved"); Tree.Delete(backup); outcome = "rolled-forward"; }
            else if (t && b && !s) { Tree.Delete(backup); outcome = "completed"; }
            else if (t && !b && !s) outcome = "completed";
            else if (!t && b && !s) { Directory.Move(backup, target); outcome = "rolled-back"; }
            else if (!t && !b && s) { Directory.Move(staged, target); outcome = "rolled-forward-fresh"; }
            else if (t && b && s) { Tree.Delete(backup); outcome = "not-started-stale-backup"; } // backup can only be a leftover here
            else outcome = "lost";
            // The undo record is only needed while the staged folder is still waiting.
            if (!Directory.Exists(staged)) Tree.Delete(Keep.UndoFolder(staged));
            if (outcome != "lost") File.Delete(journal);
            log("RECOVERED " + target + " " + outcome);
            return outcome;
        }
    }
}
