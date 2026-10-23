using System;
using System.Collections.Generic;
using System.IO;

namespace ExtendedData.Core
{
    internal interface ICoopTrailPackageFiles
    {
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void MoveDirectory(string source, string destination);
        void DeleteDirectory(string path);
        void WriteManifest(string path, CoopTrailPackageManifest manifest);
    }

    internal static class CoopTrailPackagePublication
    {
        internal const string MissionsDirectory = "CoopMissions";
        // Editable Trail Maker sources, staged beside the missions so both are replaced together.
        internal const string TrailMakerSourceDirectory = "TrailMakerSource";

        private sealed class SystemFiles : ICoopTrailPackageFiles
        {
            public bool DirectoryExists(string path) => Directory.Exists(path);
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);
            public void DeleteDirectory(string path) => Directory.Delete(path, true);
            public void WriteManifest(string path, CoopTrailPackageManifest manifest) =>
                CoopTrailPackageManifestJson.WriteAtomic(path, manifest);
        }

        private sealed class Replacement
        {
            internal string Target, Backup;
            internal bool BackedUp, Installed;
        }

        internal static void Publish(string stagingRoot, string destination,
            CoopTrailPackageManifest manifest, ICoopTrailPackageFiles files = null)
        {
            files = files ?? new SystemFiles();
            string targetRoot = Path.GetFullPath(destination);
            files.CreateDirectory(targetRoot);
            var replacements = new List<Replacement>();
            foreach (string name in new[] { MissionsDirectory, TrailMakerSourceDirectory })
            {
                string staged = Path.Combine(stagingRoot, name);
                // The missions are mandatory; staged Maker sources are optional for older callers.
                if (name != MissionsDirectory && !files.DirectoryExists(staged))
                    continue;
                replacements.Add(new Replacement
                {
                    Target = Path.Combine(targetRoot, name),
                    Backup = Path.Combine(targetRoot, "." + name + ".old-" + Guid.NewGuid().ToString("N")),
                });
            }
            try
            {
                foreach (Replacement replacement in replacements)
                {
                    string staged = Path.Combine(stagingRoot, Path.GetFileName(replacement.Target));
                    if (files.DirectoryExists(replacement.Target))
                    {
                        files.MoveDirectory(replacement.Target, replacement.Backup);
                        replacement.BackedUp = true;
                    }
                    files.MoveDirectory(staged, replacement.Target);
                    replacement.Installed = true;
                }
                files.WriteManifest(Path.Combine(targetRoot, "cooptrail.json"), manifest);
                // Atomic manifest replacement is the commit point. Cleanup below cannot undo it.
            }
            catch (Exception originalError)
            {
                var recoveryErrors = new List<Exception>();
                for (int index = replacements.Count - 1; index >= 0; index--)
                {
                    Replacement replacement = replacements[index];
                    try
                    {
                        // A failed backup rename leaves the OLD directory at this path.
                        // Only this transaction's successfully installed directory may be deleted.
                        if (replacement.Installed && files.DirectoryExists(replacement.Target))
                            files.DeleteDirectory(replacement.Target);
                        if (replacement.BackedUp)
                            files.MoveDirectory(replacement.Backup, replacement.Target);
                    }
                    catch (Exception recoveryError)
                    {
                        recoveryErrors.Add(recoveryError);
                    }
                }
                if (recoveryErrors.Count > 0)
                {
                    var all = new List<Exception> { originalError };
                    all.AddRange(recoveryErrors);
                    var preserved = new List<string>();
                    foreach (Replacement replacement in replacements)
                        if (replacement.BackedUp) preserved.Add(replacement.Backup);
                    throw new IOException("Coop Trail publication failed: " + originalError.Message +
                        ". Recovery failed: " + recoveryErrors[0].Message +
                        ". Preserved recovery directory: [" + string.Join("], [", preserved) + "].",
                        new AggregateException(all));
                }
                throw;
            }
            foreach (Replacement replacement in replacements)
            {
                if (!replacement.BackedUp) continue;
                try { files.DeleteDirectory(replacement.Backup); }
                catch { /* The committed package stays valid; preserve an undeletable backup. */ }
            }
        }
    }
}
