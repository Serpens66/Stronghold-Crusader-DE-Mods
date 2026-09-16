using System;
using System.IO;

namespace CastlePlanner
{
    internal static class AivCandidateFilePolicy
    {
        private const int HeaderLength = 8;

        internal static bool IsKnownNonJsonContainer(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            byte[] header = new byte[HeaderLength];
            int length;
            using (FileStream stream = File.OpenRead(path))
                length = stream.Read(header, 0, header.Length);
            return IsKnownNonJsonContainer(header, length);
        }

        internal static bool IsKnownNonJsonContainer(byte[] header)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));
            return IsKnownNonJsonContainer(header, header.Length);
        }

        private static bool IsKnownNonJsonContainer(byte[] header, int length)
        {
            bool zip = length >= 4 &&
                header[0] == 0x50 && header[1] == 0x4B &&
                ((header[2] == 0x03 && header[3] == 0x04) ||
                 (header[2] == 0x05 && header[3] == 0x06) ||
                 (header[2] == 0x07 && header[3] == 0x08));
            bool rar = length >= 7 &&
                header[0] == 0x52 && header[1] == 0x61 &&
                header[2] == 0x72 && header[3] == 0x21 &&
                header[4] == 0x1A && header[5] == 0x07 &&
                (header[6] == 0x00 || header[6] == 0x01);
            return zip || rar;
        }
    }
}
