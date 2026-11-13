using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

// Test/tool infrastructure only: never included in a Unity plugin assembly.
internal static class TestWorkDirectory
{
    private static readonly List<string> Allocated = new List<string>();
    static TestWorkDirectory()
    {
        AppDomain.CurrentDomain.ProcessExit += delegate
        {
            lock (Allocated)
                for (int i = Allocated.Count - 1; i >= 0; i--)
                    try { Remove(Allocated[i]); }
                    catch (Exception error) { Console.Error.WriteLine("WARNING: test fixture retained: " + Allocated[i] + ": " + error.Message); }
        };
    }
    internal static bool IsOwned(string path)
    {
        try
        {
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!System.Text.RegularExpressions.Regex.IsMatch(path, @"[\\/]\.work[\\/]t[\\/][A-Za-z0-9_.-]+[\\/][0-9]+$")) return false;
            if ((File.GetAttributes(path + ".run.json") & FileAttributes.ReparsePoint) != 0) return false;
            if (File.ReadAllText(path + ".run.json") != Record(path)) return false;
            for (var item = new DirectoryInfo(path); item != null; item = item.Parent)
                if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (ArgumentException) { return false; }
    }
    private static string Record(string path)
    {
        return "{\"SchemaVersion\":1,\"Root\":\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
    }
    internal static string New(string name, bool useTempRoot = false)
    {
        // Exact-length regression cases need a short root even in a deeply nested checkout.
        string root = useTempRoot ? Path.GetTempPath() : AppDomain.CurrentDomain.BaseDirectory;
        while (!useTempRoot && !IsWorkspaceRoot(root))
        {
            var parent = Directory.GetParent(root);
            if (parent == null) throw new InvalidOperationException("Test workspace root was not found.");
            root = parent.FullName;
        }
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        if (name.Length > 24) name = name.Substring(0, 24);
        string folder = Path.Combine(root, ".work", "t", name);
        Directory.CreateDirectory(folder);
        using (FileStream gate = AcquireGate(folder))
        {
            int number = 1;
            string path;
            do { path = Path.Combine(folder, (number++).ToString("D3")); }
            while (Directory.Exists(path) || File.Exists(path) || File.Exists(path + ".run.json"));
            Directory.CreateDirectory(path);
            File.WriteAllText(path + ".run.json", Record(path));
            lock (Allocated) Allocated.Add(path);
            return path;
        }
    }
    internal static void Remove(string path)
    {
        path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(path) && !File.Exists(path + ".run.json")) return;
        if (!IsOwned(path)) throw new InvalidOperationException("Test fixture ownership mismatch.");
        if (Directory.Exists(path))
        {
            AssertNoWorktreePointers(new DirectoryInfo(path));
            DeleteTree(new DirectoryInfo(path));
        }
        File.Delete(path + ".run.json");
    }
    private static void AssertNoWorktreePointers(DirectoryInfo directory)
    {
        foreach (FileSystemInfo entry in directory.GetFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            if (entry.Name == ".git" && entry is FileInfo)
                throw new InvalidOperationException("Git worktree cleanup must go through Git: " + entry.FullName);
            if (entry is DirectoryInfo) AssertNoWorktreePointers((DirectoryInfo)entry);
        }
    }
    private static void DeleteTree(DirectoryInfo directory)
    {
        foreach (FileSystemInfo entry in directory.GetFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                if (entry is DirectoryInfo) Directory.Delete(entry.FullName, false);
                else File.Delete(entry.FullName);
            }
            else if (entry is DirectoryInfo) DeleteTree((DirectoryInfo)entry);
            else { ((FileInfo)entry).IsReadOnly = false; entry.Delete(); }
        }
        directory.Delete();
    }
    private static bool IsWorkspaceRoot(string root)
    {
        // Source ZIPs must remain testable without Git metadata or a surrounding modpack.
        return File.Exists(Path.Combine(root, ".git")) || Directory.Exists(Path.Combine(root, ".git")) ||
            (File.Exists(Path.Combine(root, "Directory.Build.targets")) &&
             (Directory.Exists(Path.Combine(root, "tools", "Paths")) ||
              Directory.Exists(Path.Combine(root, "Shared", "Tools", "Paths"))));
    }
    private static FileStream AcquireGate(string folder)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try { return new FileStream(Path.Combine(folder, ".allocate.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { if (DateTime.UtcNow >= until) throw; Thread.Sleep(20); }
        }
    }
}
