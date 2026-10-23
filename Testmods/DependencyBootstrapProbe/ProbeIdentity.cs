using System;
using System.IO;
using System.Security.Cryptography;

namespace DependencyBootstrapProbe
{
    internal static class ProbeIdentity
    {
        internal static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
