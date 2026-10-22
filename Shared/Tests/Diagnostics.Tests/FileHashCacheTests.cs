using Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Security.Cryptography;

[TestClass]
public sealed class FileHashCacheTests
{
    [TestMethod]
    public void CachedHashFollowsFileContentAndMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), "serp-hash-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "native.bin");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            string first = DebugLogHelper.GetFileSha256(path);
            Assert.AreEqual(Expected(path), first);
            Assert.AreEqual(first.ToUpperInvariant(), first);
            Assert.AreEqual(first, DebugLogHelper.GetFileSha256(Path.Combine(directory, ".", "native.bin")));

            // Same length with an explicit new write time: the cache must not report the old hash.
            File.WriteAllBytes(path, new byte[] { 3, 2, 1 });
            File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(5));
            Assert.AreEqual(Expected(path), DebugLogHelper.GetFileSha256(path));
            Assert.AreNotEqual(first, DebugLogHelper.GetFileSha256(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void PersistentStoreSkipsHashingOnlyForUnchangedMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), "serp-hash-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(directory);
        string previous = DebugLogHelper.PersistentHashDirectory;
        try
        {
            DebugLogHelper.PersistentHashDirectory = directory;
            string path = Path.GetFullPath(Path.Combine(directory, "persisted.bin"));
            File.WriteAllBytes(path, new byte[] { 7, 8, 9 });
            var info = new FileInfo(path);
            string marker = new string('A', 64);
            // A store entry with matching path, size and write time is trusted without reading the file.
            File.WriteAllText(Path.Combine(directory, "Serp.file-hashes.tsv"),
                path + "\t" + info.Length + "\t" + info.LastWriteTimeUtc.Ticks + "\t" + marker + "\r\n");
            Assert.AreEqual(marker, DebugLogHelper.GetFileSha256(path));

            File.SetLastWriteTimeUtc(path, info.LastWriteTimeUtc.AddSeconds(5));
            string real = DebugLogHelper.GetFileSha256(path);
            Assert.AreEqual(Expected(path), real);
            StringAssert.Contains(File.ReadAllText(Path.Combine(directory, "Serp.file-hashes.tsv")), real);
            Assert.IsFalse(File.ReadAllText(Path.Combine(directory, "Serp.file-hashes.tsv")).Contains(marker));

            File.WriteAllText(Path.Combine(directory, "Serp.file-hashes.tsv"), "damaged\tline");
            File.SetLastWriteTimeUtc(path, info.LastWriteTimeUtc.AddSeconds(10));
            Assert.AreEqual(Expected(path), DebugLogHelper.GetFileSha256(path));
        }
        finally
        {
            DebugLogHelper.PersistentHashDirectory = previous;
            Directory.Delete(directory, true);
        }
    }

    private static string Expected(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
}
