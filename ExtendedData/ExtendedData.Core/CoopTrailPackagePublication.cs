using System;
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
        private sealed class SystemFiles : ICoopTrailPackageFiles
        {
            public bool DirectoryExists(string path) => Directory.Exists(path);
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);
            public void DeleteDirectory(string path) => Directory.Delete(path, true);
            public void WriteManifest(string path, CoopTrailPackageManifest manifest) =>
                CoopTrailPackageManifestJson.WriteAtomic(path, manifest);
        }

        internal static void Publish(string stagingRoot, string destination,
            CoopTrailPackageManifest manifest, ICoopTrailPackageFiles files = null)
        {
            files = files ?? new SystemFiles();
            string targetRoot = Path.GetFullPath(destination);
            string targetMissions = Path.Combine(targetRoot, "CoopMissions");
            string backupMissions = Path.Combine(targetRoot, ".CoopMissions.old-" + Guid.NewGuid().ToString("N"));
            files.CreateDirectory(targetRoot);
            bool backedUp = false;
            bool newMissionsInstalled = false;
            try
            {
                if (files.DirectoryExists(targetMissions))
                {
                    files.MoveDirectory(targetMissions, backupMissions);
                    backedUp = true;
                }
                files.MoveDirectory(Path.Combine(stagingRoot, "CoopMissions"), targetMissions);
                newMissionsInstalled = true;
                files.WriteManifest(Path.Combine(targetRoot, "cooptrail.json"), manifest);
                // Atomic manifest replacement is the commit point. Cleanup below cannot undo it.
            }
            catch (Exception originalError)
            {
                try
                {
                    // A failed backup rename leaves the OLD directory at this path.
                    // Only this transaction's successfully installed directory may be deleted.
                    if (newMissionsInstalled && files.DirectoryExists(targetMissions))
                        files.DeleteDirectory(targetMissions);
                    if (backedUp)
                        files.MoveDirectory(backupMissions, targetMissions);
                }
                catch (Exception recoveryError)
                {
                    throw new IOException("Coop Trail publication failed: " + originalError.Message +
                        ". Recovery failed: " + recoveryError.Message +
                        ". Preserved recovery directory: [" + backupMissions + "].",
                        new AggregateException(originalError, recoveryError));
                }
                throw;
            }
            if (backedUp)
            {
                try { files.DeleteDirectory(backupMissions); }
                catch { /* The committed package stays valid; preserve an undeletable backup. */ }
            }
        }
    }
}
