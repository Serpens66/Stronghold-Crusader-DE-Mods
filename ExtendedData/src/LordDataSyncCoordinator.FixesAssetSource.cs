using System;
using System.Collections.Generic;
using System.IO;
using SHCDESE.API;

namespace ExtendedData
{
    internal sealed partial class LordDataSyncCoordinator
    {
        private readonly Dictionary<string, string> lastAssetDiagnostics =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private void LogFixesAssetSource(int playerId, int lordType, string lordName, string effectiveJson)
        {
            // The global asset registry contains Custom Lords only. An Extended
            // Lord can share its name with one; its typed preferences and inherited
            // sources are captured and verified separately by the Fixes bridge.
            if (lordType >= 0 || !fixes.Installed)
                return;
            string source;
            bool assetPresent = false;
            try
            {
                if (GameAIManagerAPI.Instance?.LordDataDict == null ||
                    !GameAIManagerAPI.Instance.LordDataDict.TryGetValue(
                        lordName.ToLowerInvariant(), out var entry))
                    source = "lord-entry=missing";
                else if (GameAssetManagerAPI.Instance == null)
                    source = "asset-provider=unavailable";
                else if (GameAssetManagerAPI.Instance.GetModFileTextContent(
                    entry.AssetProviderGuid, "override/fixes/preferences.json", out string assetJson))
                {
                    assetPresent = true;
                    source = "provider=" + LordDataSyncDiagnostics.SafeLabel(entry.AssetProviderGuid) +
                        ",asset=" + LordDataSyncDiagnostics.DescribeJson(assetJson, true);
                }
                else
                    source = "provider=" + LordDataSyncDiagnostics.SafeLabel(entry.AssetProviderGuid) +
                        ",asset=absent";
            }
            catch (Exception exception)
            {
                source = "asset-query-error=" + exception.GetType().Name;
            }
            string diagnostic = "player=" + playerId + ",lordType=" + lordType +
                ",lord=" + LordDataSyncDiagnostics.SafeLabel(lordName) +
                ",effective=" + LordDataSyncDiagnostics.DescribeJson(effectiveJson, true) +
                "," + source;
            string failure = "Fixes did not load the available preferences for selected Lord: " + diagnostic +
                ",problem=asset-present-but-effective-entry-missing.";
            if (lastAssetDiagnostics.TryGetValue(lordName, out string previous) &&
                string.Equals(diagnostic, previous, StringComparison.Ordinal))
            {
                if (assetPresent && effectiveJson == null)
                    throw new InvalidDataException(failure);
                return;
            }
            lastAssetDiagnostics[lordName] = diagnostic;
            if (assetPresent && effectiveJson == null)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/LordData/FixesAssetSource", "Lord-data Fixes source: " + diagnostic +
                    ",problem=asset-present-but-effective-entry-missing.");
                throw new InvalidDataException(failure);
            }
            Shared.DevDiag.Log(() => "Lord-data Fixes source: " + diagnostic);
        }
    }
}
