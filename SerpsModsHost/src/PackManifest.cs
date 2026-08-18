using System.Collections.Generic;

namespace SerpsModsHost
{
    public sealed class PackManifest
    {
        public int SchemaVersion { get; set; }
        public string PackGuid { get; set; }
        public string PackVersion { get; set; }
        public string HostVersion { get; set; }
        public string CreatedUtc { get; set; }
        public string RepositoryCommit { get; set; }
        public List<PackModRecord> Mods { get; set; } = new List<PackModRecord>();
    }

    public sealed class PackModRecord
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public string Version { get; set; }
        public string State { get; set; }
        public string RelativePath { get; set; }
        public string ReleaseUrl { get; set; }
        public string ReleaseTag { get; set; }
        public string SourceCommit { get; set; }
        public string PackageSha256 { get; set; }
        public string ExpectedSoftDependency { get; set; }
        public List<PackFileRecord> Files { get; set; } = new List<PackFileRecord>();
    }

    public sealed class PackFileRecord
    {
        public string Path { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }
    }
}
