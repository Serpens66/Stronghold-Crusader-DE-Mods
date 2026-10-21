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

    private static string Expected(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
}
