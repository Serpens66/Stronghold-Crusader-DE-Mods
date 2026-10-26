using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BetterPathfindingTest
{
    internal sealed class ManagedEvidence
    {
        internal readonly string InstalledPath, LoadedName, LoadedMvid;
        internal string InstalledHash = "unavailable", MetadataError = "";
        internal ManagedEvidence(string gameRoot, Assembly loaded, Func<string, string> hashFile)
        {
            if (string.IsNullOrWhiteSpace(gameRoot)) throw new ArgumentException("Game root unavailable.");
            InstalledPath = Path.Combine(Path.GetFullPath(gameRoot), "Stronghold Crusader Definitive Edition_Data", "Managed", "Assembly-CSharp.dll");
            LoadedName = loaded.FullName; LoadedMvid = loaded.ManifestModule.ModuleVersionId.ToString("D");
            // Disk hash is report metadata, not authorization of the loaded/possibly patched IL.
            try { InstalledHash = hashFile(InstalledPath); }
            catch (Exception ex) { MetadataError = ex.GetType().Name + ": " + ex.Message; }
        }
        internal Dictionary<string, object> Data() => new Dictionary<string, object> {
            ["installedManagedPath"] = InstalledPath, ["installedManagedFileSha256"] = InstalledHash,
            ["loadedAssemblyName"] = LoadedName, ["loadedModuleMvid"] = LoadedMvid, ["optionalMetadataError"] = MetadataError
        };
    }
    internal static class ManagedContracts
    {
        internal static void ValidatePackedLengths(params int[] lengths)
        { foreach (int length in lengths) if (length != 320800) throw new CaptureContractException("SDK tile capacity changed: expected audited 320800, got " + length); }
        internal static void Validate()
        {
            if (Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N0000010E)).ToInt32() != 0x6C || Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N000001E6)).ToInt32() != 0x3CC) throw new NotSupportedException("Unit cadence layout changed.");
            if ((ushort)eStructs.STRUCT_KEEP_ONE != 0x28 || (ushort)eStructs.STRUCT_GATE_MAIN != 0x2D || (ushort)eStructs.STRUCT_TOWER5 != 0x4E) throw new NotSupportedException("Structure enum contract changed.");
        }
    }
}
