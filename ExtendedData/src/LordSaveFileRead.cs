using System;
using System.IO;
using System.Security.Cryptography;

namespace ExtendedData
{
    internal static class LordSaveFileRead
    {
        internal static bool MatchesSource(string candidatePath, string candidateHash, string path, string hash)
            => !string.IsNullOrWhiteSpace(candidatePath) && !string.IsNullOrWhiteSpace(candidateHash) &&
                string.Equals(candidatePath, path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidateHash, hash, StringComparison.OrdinalIgnoreCase);

        // Keep a read-share lease across parsing as well as checking both hashes.
        // Author data can therefore never be paired with a later replacement file.
        internal static T Read<T>(string path, Func<string, T> parse)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                string before = Hash(stream);
                T value = parse(before);
                if (Hash(stream) != before) throw new InvalidDataException("Save changed while its Lord data was read.");
                return value;
            }
        }
        private static string Hash(Stream stream)
        {
            stream.Position = 0;
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
