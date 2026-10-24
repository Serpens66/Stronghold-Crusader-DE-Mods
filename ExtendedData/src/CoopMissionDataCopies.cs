using System.Collections.Generic;
using CrusaderDE;

namespace ExtendedData
{
    internal static class CoopMissionDataCopies
    {
        internal static FRONT_Multiplayer.CoopMissionSetupData CopySetup(FRONT_Multiplayer.CoopMissionSetupData source)
        {
            return new FRONT_Multiplayer.CoopMissionSetupData
            {
                mapName = source.mapName, header = source.header,
                keepOrder = (int[])source.keepOrder?.Clone(), teams = (int[])source.teams?.Clone(),
                AIs = (int[])source.AIs?.Clone(), AIVs = (int[])source.AIVs?.Clone(),
                fairness = source.fairness, starting_level = source.starting_level,
                allowBarracksPlayer1 = source.allowBarracksPlayer1,
                allowMercPostPlayer1 = source.allowMercPostPlayer1,
                allowStockadePlayer1 = source.allowStockadePlayer1,
                allowBarracksPlayer2 = source.allowBarracksPlayer2,
                allowMercPostPlayer2 = source.allowMercPostPlayer2,
                allowStockadePlayer2 = source.allowStockadePlayer2,
            };
        }

        // Independent copy: later lobby changes must never mutate the decoded package data.
        internal static FRONT_Multiplayer.MPAIVInfo CopyLord(FRONT_Multiplayer.MPAIVInfo saved)
        {
            var copy = new FRONT_Multiplayer.MPAIVInfo
            {
                lordType = saved.lordType,
                lordName = saved.lordName ?? string.Empty,
                builtIn = saved.builtIn,
                community = saved.community,
                historical = saved.historical,
                rotation = saved.rotation,
                builtInLord = saved.builtInLord,
                lordConfig = saved.lordConfig == null ? null : new CustomisationFileManager.CustomLordConfig
                {
                    name = saved.lordConfig.name,
                    path = saved.lordConfig.path,
                    lordType = saved.lordConfig.lordType,
                    checksum = saved.lordConfig.checksum,
                    workshop = saved.lordConfig.workshop,
                    workshopUploadInfoAvailable = saved.lordConfig.workshopUploadInfoAvailable,
                    lordData = saved.lordConfig.lordData,
                },
                imageData = (byte[])saved.imageData?.Clone(),
                image = saved.image,
            };
            copy.aivs.Clear();
            foreach (CustomisationFileManager.CustomAIV aiv in saved.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                copy.aivs.Add(new CustomisationFileManager.CustomAIV
                {
                    lordType = aiv.lordType,
                    AIVName = aiv.AIVName,
                    builtIn = aiv.builtIn,
                    workshopUploadInfoAvailable = aiv.workshopUploadInfoAvailable,
                    workshop = aiv.workshop,
                    path = aiv.path,
                    checksum = aiv.checksum,
                    data = (short[])aiv.data?.Clone(),
                });
            return copy;
        }

    }
}
