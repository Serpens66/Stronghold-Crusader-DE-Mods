using CrusaderDE;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Pure selection repair. Callers own lobby authority, publication and diagnostics.
    // Compute both independent defaults before changing anything: a missing package must
    // not leave half of a repaired selection installed. Package order is Vanilla order.
    internal static class AiSelectionRepair
    {
        internal static bool IsCustom(FRONT_Multiplayer.MPAIVInfo info) => info != null &&
            (!string.IsNullOrEmpty(info.lordName) || info.lordType >= Shared.CoopAiSelectionRules.FirstCustomLordType);

        internal static bool ValidAiv(CustomisationFileManager.CustomAIV aiv) =>
            aiv?.data != null && aiv.data.Length > 0;

        internal static bool CanRemember(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null) return false;
            if (!IsCustom(info)) return true;
            if (string.IsNullOrWhiteSpace(info.lordName) || info.builtIn || info.community || info.historical ||
                info.builtInLord || info.lordConfig == null || info.aivs == null || info.aivs.Count == 0) return false;
            foreach (var aiv in info.aivs) if (!ValidAiv(aiv) || aiv.builtIn) return false;
            return true;
        }

        internal static bool TryRepair(FRONT_Multiplayer.MPAIVInfo info,
            IList<CustomisationFileManager.CustomAIV> availableAivs,
            IList<CustomisationFileManager.CustomLordConfig> availableAics,
            out AiSelectionRepairResult result)
        {
            if (info?.aivs == null)
            { result = new AiSelectionRepairResult(false, false, false, "Missing selection data."); return false; }
            bool custom = IsCustom(info);
            if (custom && string.IsNullOrWhiteSpace(info.lordName))
            { result = new AiSelectionRepairResult(false, false, false, "Missing custom Lord identity."); return false; }
            bool explicitCastles = !info.builtIn && !info.community && !info.historical;
            var candidates = new List<CustomisationFileManager.CustomAIV>();
            if (explicitCastles)
                foreach (var aiv in info.aivs)
                    if (ValidAiv(aiv) && (!custom || !aiv.builtIn)) candidates.Add(aiv);
            bool castleFallback = custom ? !explicitCastles || candidates.Count == 0
                : explicitCastles && candidates.Count == 0;
            if (castleFallback && custom)
            {
                if (availableAivs == null || availableAivs.Count == 0 ||
                    !ValidAiv(availableAivs[0]) || availableAivs[0].builtIn)
                { result = new AiSelectionRepairResult(false, false, false, "Missing usable first package castle."); return false; }
                candidates.Clear();
                candidates.Add(availableAivs[0]);
            }
            bool aicFallback = custom ? info.builtInLord || info.lordConfig == null
                : !info.builtInLord && info.lordConfig == null;
            var configuration = info.lordConfig;
            if (aicFallback)
            {
                configuration = custom && availableAics != null && availableAics.Count > 0 ? availableAics[0] : null;
                if (custom && configuration == null)
                { result = new AiSelectionRepairResult(false, false, false, "Missing first package Lord configuration."); return false; }
            }
            bool changed = castleFallback || aicFallback || explicitCastles && candidates.Count != info.aivs.Count;
            if (castleFallback || explicitCastles && candidates.Count != info.aivs.Count)
            {
                info.aivs.Clear();
                info.aivs.AddRange(candidates);
            }
            if (castleFallback)
            {
                info.builtIn = !custom;
                info.community = false;
                info.historical = false;
            }
            if (aicFallback)
            {
                info.builtInLord = !custom;
                info.lordConfig = configuration;
            }
            result = new AiSelectionRepairResult(changed, castleFallback, aicFallback, null);
            return true;
        }

        internal static FRONT_Multiplayer.MPAIVInfo Copy(FRONT_Multiplayer.MPAIVInfo source)
        {
            var copy = new FRONT_Multiplayer.MPAIVInfo { lordType = source.lordType, lordName = source.lordName };
            CopySelection(source, copy);
            return copy;
        }

        // Never replace the live slot identity or package presentation with persisted values.
        internal static void CopySelection(FRONT_Multiplayer.MPAIVInfo source, FRONT_Multiplayer.MPAIVInfo target)
        {
            target.builtIn = source.builtIn;
            target.community = source.community;
            target.historical = source.historical;
            target.rotation = source.rotation;
            target.builtInLord = source.builtInLord;
            target.lordConfig = source.lordConfig;
            target.aivs.Clear();
            target.aivs.AddRange(source.aivs);
        }
    }

    internal readonly struct AiSelectionRepairResult
    {
        internal AiSelectionRepairResult(bool changed, bool castleFallback, bool aicFallback, string failure)
        { Changed = changed; CastleFallback = castleFallback; AicFallback = aicFallback; Failure = failure; }
        internal bool Changed { get; }
        internal bool CastleFallback { get; }
        internal bool AicFallback { get; }
        internal string Failure { get; }
    }
}
