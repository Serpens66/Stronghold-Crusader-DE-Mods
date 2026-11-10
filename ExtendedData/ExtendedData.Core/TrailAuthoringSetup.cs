using APIShared.Maps;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ExtendedData.Core
{
    internal static class TrailAuthoringSetup
    {
        // Vanilla stores these values in a positional, versioned setup string. Keep its
        // version and every other token, including fields unknown to this mod.
        private const int StartingGameSpeedOffset = 1;
        private const int AutoSaveOffset = 6;
        internal const int AutoSaveOff = 0;

        internal static byte[] Neutralize(byte[] trail, int defaultGameSpeed)
        {
            TrailContainerLayout layout = TrailContainer.ReadLayout(trail);
            byte[] restart = TrailContainer.ReadRestartData(trail);
            TrailRestartMapReference.Read(restart, out int setupLengthOffset);
            int setupOffset = setupLengthOffset;
            int setupLength = TrailRestartMapReference.ReadInt32(restart, ref setupOffset);
            if (setupLength <= 0 || setupLength > restart.Length - setupOffset)
                throw new InvalidDataException("The Trail restart block has an invalid setup length.");

            var encoding = new UTF8Encoding(false, true);
            string[] fields = encoding.GetString(restart, setupOffset, setupLength).Split(',');
            if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int firstField))
                throw new InvalidDataException("The Trail restart block has an invalid multiplayer setup.");
            // A negative format version precedes fairness; the oldest format starts with fairness.
            int fairnessIndex = firstField < 0 ? 1 : 0;
            int speedIndex = fairnessIndex + StartingGameSpeedOffset;
            int autoSaveIndex = fairnessIndex + AutoSaveOffset;
            if (fields.Length <= autoSaveIndex || !TryReadInteger(fields[speedIndex]) ||
                !TryReadInteger(fields[autoSaveIndex]))
                throw new InvalidDataException("The Trail restart block has an invalid multiplayer setup.");
            string speed = defaultGameSpeed.ToString(CultureInfo.InvariantCulture);
            string autoSave = AutoSaveOff.ToString(CultureInfo.InvariantCulture);
            if (fields[speedIndex] == speed && fields[autoSaveIndex] == autoSave)
                return trail;

            fields[speedIndex] = speed;
            fields[autoSaveIndex] = autoSave;

            byte[] neutralSetup = encoding.GetBytes(string.Join(",", fields));
            int changeInLength = neutralSetup.Length - setupLength;
            int absoluteSetupLengthOffset = layout.RestartPayloadOffset + setupLengthOffset;
            int absoluteSetupOffset = layout.RestartPayloadOffset + setupOffset;
            int suffixOffset = absoluteSetupOffset + setupLength;
            var result = new byte[checked(trail.Length + changeInLength)];
            Buffer.BlockCopy(trail, 0, result, 0, absoluteSetupLengthOffset);
            BitConverter.GetBytes(layout.RestartSize + changeInLength).CopyTo(result, layout.RestartSizeFieldOffset);
            BitConverter.GetBytes(neutralSetup.Length).CopyTo(result, absoluteSetupLengthOffset);
            neutralSetup.CopyTo(result, absoluteSetupOffset);
            Buffer.BlockCopy(trail, suffixOffset, result, suffixOffset + changeInLength, trail.Length - suffixOffset);
            TrailContainer.ReadLayout(result);
            return result;
        }

        internal static void NeutralizeExportCopies(string staging, int defaultGameSpeed)
        {
            foreach (string path in Directory.GetFiles(staging, "*.trail"))
            {
                byte[] original = File.ReadAllBytes(path);
                byte[] neutral = Neutralize(original, defaultGameSpeed);
                if (!ReferenceEquals(original, neutral)) File.WriteAllBytes(path, neutral);
            }
        }

        internal static byte[] StageCoopMissionAssets(
            string sourceTrail, string bundledTrail, string bundledMap, int defaultGameSpeed)
        {
            byte[] trail = Neutralize(File.ReadAllBytes(sourceTrail), defaultGameSpeed);
            File.WriteAllBytes(bundledTrail, trail);
            File.WriteAllBytes(bundledMap, TrailContainer.ExtractMap(trail));
            return TrailContainer.ReadRestartData(trail);
        }

        internal static void PublishExportCopies(string staging, string destination)
        {
            // The caller creates staging inside the destination so both sets use same-volume renames.
            string backup = Path.Combine(staging, "previous");
            Directory.CreateDirectory(backup);
            var rollback = new Stack<(string Source, string Target)>();
            bool preserveBackup = false;
            try
            {
                foreach (string target in Directory.GetFiles(destination, "*.trail"))
                {
                    string previous = Path.Combine(backup, Path.GetFileName(target));
                    File.Move(target, previous);
                    rollback.Push((previous, target));
                }
                foreach (string source in Directory.GetFiles(staging, "*.trail"))
                {
                    string target = Path.Combine(destination, Path.GetFileName(source));
                    File.Move(source, target);
                    rollback.Push((target, source));
                }
            }
            catch (Exception publicationError)
            {
                var errors = new List<Exception> { publicationError };
                while (rollback.Count > 0)
                {
                    (string source, string target) = rollback.Pop();
                    try { File.Move(source, target); }
                    catch (Exception recoveryError) { errors.Add(recoveryError); }
                }
                if (errors.Count > 1)
                {
                    preserveBackup = true;
                    throw new IOException("Trail export failed and recovery was incomplete. Remaining backups: [" +
                        backup + "].", new AggregateException(errors));
                }
                throw;
            }
            finally
            {
                if (!preserveBackup)
                {
                    // The caller reports cleanup failures if the staging directory is still occupied.
                    try
                    {
                        foreach (string previous in Directory.GetFiles(backup, "*.trail"))
                            File.Delete(previous);
                        Directory.Delete(backup);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static bool TryReadInteger(string field) =>
            int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }
}
