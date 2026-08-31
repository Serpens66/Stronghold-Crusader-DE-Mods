using System;
using System.IO;
using System.Security.Cryptography;

namespace ExtremePowers.API
{
    internal static class NativeBuildGuard
    {
        internal const string SupportedSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal static bool IsSupported(string path, out string status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { status = "CrusaderDE.dll was not found; Vanilla fallback is active."; return false; }
                using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                    if (!string.Equals(actual, SupportedSha256, StringComparison.OrdinalIgnoreCase)) { status = "Unsupported CrusaderDE.dll SHA-256 " + actual + "; Vanilla fallback is active."; return false; }
                }
                status = "Supported Steam build 24816905 recognized; native mutation remains disabled until all effect signatures pass validation.";
                return true;
            }
            catch (Exception ex) { status = "DLL validation failed: " + ex.Message + "; Vanilla fallback is active."; return false; }
        }
        internal static bool IsSupportedImage(byte[] image)
        {
            if (image == null) return false;
            using (var sha = SHA256.Create()) return string.Equals(BitConverter.ToString(sha.ComputeHash(image)).Replace("-", string.Empty), SupportedSha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
