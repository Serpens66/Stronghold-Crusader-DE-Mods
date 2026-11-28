using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData
{
    internal static class TrailLordRequirementSource
    {
        internal static IReadOnlyList<TrailLordSlot> CaptureForSave(
            string sourceTrailPath, HUD_IngameMenu.RestartSkirmishMapInfo restart,
            ISet<int> includedPlayerIds, out IReadOnlyList<string> warnings)
        {
            var problems = new List<string>();
            warnings = problems;
            TrailLordRequirements stored = null;
            bool readStored = false;
            var fixes = new FixesLordPreferencesBridge();
            var slots = new List<TrailLordSlot>();
            foreach (int playerId in TrailLordPackageRuntime.CustomLordPlayerIds(restart, includedPlayerIds))
            {
                var info = restart.aivs[playerId - 1];
                TrailLordSlot current = TrailLordPackageRuntime.SavedIdentity(info, playerId);
                try
                {
                    var source = TrailLordPackageRuntime.FindMakerSource(info);
                    if (source != null)
                    {
                        slots.Add(TrailLordPackageRuntime.CaptureSlot(info, playerId, fixes, source));
                        continue;
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is InvalidDataException || exception is ArgumentException ||
                    exception is FormatException || exception is InvalidOperationException)
                {
                    problems.Add(current.LordName + " (player " + playerId + "): " + exception.Message);
                }

                if (!readStored)
                {
                    stored = string.IsNullOrWhiteSpace(sourceTrailPath) ? null :
                        TrailLordRequirements.ReadUnbound(sourceTrailPath, out _);
                    readStored = true;
                }
                TrailLordSlot fallback = stored?.Slots.FirstOrDefault(slot => SameSelection(slot, current));
                if (fallback?.Presentation?.MediaId != null)
                {
                    try
                    {
                        TrailLordMediaStore.BindAndVerify(new[] { fallback.Presentation },
                            Path.GetDirectoryName(Path.GetFullPath(sourceTrailPath)));
                    }
                    catch (Exception exception) when (exception is IOException ||
                        exception is UnauthorizedAccessException || exception is InvalidDataException)
                    {
                        problems.Add(current.LordName + " (player " + playerId + "): " + exception.Message);
                        fallback.Presentation = TrailLordMediaStore.SelectMedia(fallback.Presentation, false);
                    }
                }
                slots.Add(WithStoredExtras(current, fallback));
            }
            return slots;
        }

        internal static IReadOnlyList<TrailLordSlot> ReadForExport(string trailPath,
            HUD_IngameMenu.RestartSkirmishMapInfo restart, ISet<int> includedPlayerIds)
        {
            TrailLordRequirements stored = TrailLordRequirements.ReadUnbound(trailPath, out bool bound);
            var slots = new List<TrailLordSlot>();
            foreach (int playerId in TrailLordPackageRuntime.CustomLordPlayerIds(restart, includedPlayerIds))
            {
                TrailLordSlot current = TrailLordPackageRuntime.SavedIdentity(restart.aivs[playerId - 1], playerId);
                TrailLordSlot saved = stored?.Slots.FirstOrDefault(slot => slot.PlayerId == playerId);
                if (saved != null && !SameSelection(saved, current))
                {
                    if (bound)
                        throw new InvalidDataException("Stored Lord requirements do not match player " + playerId +
                            " of [" + Path.GetFileName(trailPath) + "].");
                    // Older Maker saves could rewrite the mission without renewing its sidecar.
                    saved = null;
                }
                slots.Add(WithStoredExtras(current, saved));
            }
            TrailLordMediaStore.BindAndVerify(slots.Select(slot => slot.Presentation),
                Path.GetDirectoryName(Path.GetFullPath(trailPath)));
            return slots;
        }

        private static bool SameSelection(TrailLordSlot stored, TrailLordSlot current) =>
            stored.PlayerId == current.PlayerId &&
            (!stored.LordType.HasValue || stored.LordType == current.LordType) &&
            string.Equals(stored.LordName, current.LordName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(stored.ConfigName, current.ConfigName, StringComparison.OrdinalIgnoreCase);

        private static TrailLordSlot WithStoredExtras(TrailLordSlot current, TrailLordSlot saved)
        {
            if (saved == null)
            {
                current.EmbeddedValuesOnly = true;
                return current;
            }
            current.Presentation = saved.Presentation;
            current.RequiresInstalledPackage = saved.RequiresInstalledPackage;
            current.PackageDigest = saved.PackageDigest;
            current.ModLordJson = saved.ModLordJson;
            current.FixesJson = saved.FixesJson;
            current.FixesSources = saved.FixesSources;
            current.EmbeddedValuesOnly = saved.EmbeddedValuesOnly;
            return current;
        }
    }
}
