using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;

namespace CastlePlanner
{
    internal sealed partial class BlueprintRuntimeController
    {
        private readonly Dictionary<int, string[]> spectatorAivNames = new Dictionary<int, string[]>();
        private string spectatorAivFileName;
        private bool spectatorSaveLoaded;
        private bool spectatorNameErrorLogged;

        private void RefreshSpectatorAivName(BlueprintLivePlan plan)
        {
            spectatorAivFileName = null;
            if (plan == null) return;
            try
            {
                if (!spectatorAivNames.TryGetValue(plan.Player, out string[] names))
                {
                    var info = GetSpectatorAivInfo(plan.Player);
                    if (info == null) return; // Metadata may still be restored by Vanilla.
                    names = CaptureSpectatorAivNames(info);
                    spectatorAivNames.Add(plan.Player, names);
                }
                spectatorAivFileName = BlueprintAivNames.At(names, plan.Variant);
            }
            catch (Exception error)
            {
                // Name provenance is optional. Its failure never disables the projection.
                if (!spectatorNameErrorLogged)
                {
                    spectatorNameErrorLogged = true;
                    Shared.DebugLogHelper.LogWarningLimited(log, "BlueprintAivName",
                        "AI Blueprint source name unavailable: " + error.GetBaseException().Message);
                }
            }
        }

        private FRONT_Multiplayer.MPAIVInfo GetSpectatorAivInfo(int player)
        {
            var menu = MainViewModel.Instance?.HUDIngameMenu;
            var game = GameData.Instance;
            if (game != null && game.coopTrailID > 0) return null;
            var slots = menu?.restartSkirmishMapInfo?.aivs;
            if (slots != null && player >= 1 && player <= slots.Length)
                return slots[player - 1];
            // A saved MP game does not carry the original AIV list. Never borrow
            // an old frontend/lobby list for such a save.
            var lobby = Platform_Multiplayer.Instance?.activeLobby;
            if (spectatorSaveLoaded || menu?.restartMPInfo == null || lobby == null || lobby.coopTrailGame)
                return null;
            string encoded;
            switch (player)
            {
                case 2: encoded = lobby.AIVDataPlayer2; break;
                case 3: encoded = lobby.AIVDataPlayer3; break;
                case 4: encoded = lobby.AIVDataPlayer4; break;
                case 5: encoded = lobby.AIVDataPlayer5; break;
                case 6: encoded = lobby.AIVDataPlayer6; break;
                case 7: encoded = lobby.AIVDataPlayer7; break;
                case 8: encoded = lobby.AIVDataPlayer8; break;
                default: return null;
            }
            if (string.IsNullOrEmpty(encoded)) return null;
            string[] fields = encoded.Split(':');
            if (fields.Length < 2 || !int.TryParse(fields[1], out int lordType) || lordType < 0)
                return null;
            bool encodedCustom = fields[0].Length > 0 && fields[0] != "0" && fields[0] != "1" && fields[0] != "2";
            // Decode the same imported candidate as Vanilla, excluding unrelated
            // Lord configuration and portrait payloads (no texture work here).
            var info = new FRONT_Multiplayer.MPAIVInfo();
            info.decode(string.Join(":", fields, 0, Math.Min(3, fields.Length)));
            if (info.lordType != lordType || (encodedCustom && info.aivs.Count != 1)) return null;
            return info;
        }

        private static string[] CaptureSpectatorAivNames(FRONT_Multiplayer.MPAIVInfo info)
        {
            FieldInfo tableField = typeof(AIVLoader).GetField("AIVs", BindingFlags.NonPublic | BindingFlags.Static);
            if (tableField?.FieldType != typeof(string[,]))
                throw new MissingFieldException("AIVLoader.AIVs filename table contract changed.");
            var defaults = (string[,])tableField.GetValue(null);
            // The registered resource index also covers archives. Reading it does
            // not extract/read an asset, unlike GetModifiedFileTextContent.
            var assets = GameAssetManagerAPI.Instance;
            FieldInfo indexField = typeof(GameAssetManagerAPI).GetField("_fileIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            var index = assets == null ? null : indexField?.GetValue(assets) as IDictionary;
            if (index == null) throw new MissingFieldException("GameAssetManagerAPI resource-index contract changed.");
            string FindOverride(int lord, int variant)
            {
                string lordName = Enum.GetName(typeof(AILords), (AILords)(lord + 1));
                if (lordName == null) return null;
                string key = "AIV/" + lordName + "_" + variant + ".aivjson";
                return index.Contains(key) ? key : null;
            }
            if (info.builtIn || info.aivs == null || info.aivs.Count == 0 || info.community || info.historical)
            {
                BlueprintAivSet set = info.builtIn || info.aivs == null || info.aivs.Count == 0
                    ? BlueprintAivSet.Default : info.community ? BlueprintAivSet.Community : BlueprintAivSet.Historical;
                int count = set == BlueprintAivSet.Historical ? 1 : 8;
                var names = new string[count];
                for (int i = 0; i < count; i++)
                    names[i] = BlueprintAivNames.BuiltIn(info.lordType, i, set, defaults, FindOverride);
                return names;
            }
            var selected = new string[info.aivs.Count];
            for (int i = 0; i < selected.Length; i++)
            {
                var candidate = info.aivs[i];
                if (candidate != null)
                    selected[i] = BlueprintAivNames.Candidate(candidate.AIVName, candidate.builtIn,
                        candidate.checksum, candidate.lordType, defaults, FindOverride);
            }
            return selected;
        }
    }
}
