using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

internal static class BackendEvidence
{
    internal static void Save(string directory)
    {
        string library = Path.Combine(AppContext.BaseDirectory, "BetterPathfindingNative.dll");
        string proof = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.local/native/build.json"));
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(proof));
        string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(library)));
        if (!string.Equals(actual, record.RootElement.GetProperty("dllSha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Native benchmark binary does not match its compilation proof.");
        File.Copy(proof, Path.Combine(directory, "native-build.json"));
        File.WriteAllText(Path.Combine(directory, "host.json"), JsonSerializer.Serialize(new { timestampUtc = DateTime.UtcNow, runtime = Environment.Version.ToString(), is64Bit = Environment.Is64BitProcess, managedTestAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(BackendEvidence).Assembly.Location))) }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
