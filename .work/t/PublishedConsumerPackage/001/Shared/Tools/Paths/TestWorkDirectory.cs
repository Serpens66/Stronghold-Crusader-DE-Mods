using System;
using System.IO;
using System.Threading;

// Test/tool infrastructure only: never included in a Unity plugin assembly.
internal static class TestWorkDirectory
{
    internal static bool IsOwned(string path)
    {
        try
        {
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!System.Text.RegularExpressions.Regex.IsMatch(path, @"[\\/]\.work[\\/]t[\\/][A-Za-z0-9_.-]+[\\/][0-9]+$")) return false;
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
            return path;
        }
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
