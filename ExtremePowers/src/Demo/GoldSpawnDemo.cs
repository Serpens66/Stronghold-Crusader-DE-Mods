namespace ExtremePowers.Demo
{
    internal static class GoldSpawnDemo
    {
        internal static bool CanExecute(Settings.ExtremePowersSettings settings, int playerId, int targetTileId, out string reason)
        {
            bool valid = settings.EnableGoldReplacement && settings.DemoSpawnCount > 0 &&
                settings.DemoUnitType > (int)SHCDESE.Interop.eChimps.CHIMP_TYPE_NULL &&
                settings.DemoUnitType < (int)SHCDESE.Interop.eChimps.CHIMP_NUM_TYPES &&
                System.Enum.IsDefined(typeof(SHCDESE.Interop.eChimps), (ushort)settings.DemoUnitType) &&
                SHCDESE.API.GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                SHCDESE.API.GameTileManagerAPI.Instance.IsValidTileId(targetTileId);
            reason = valid ? null : "Gold spawn demo is disabled or has no valid player, target, or count.";
            return valid;
        }

        internal static int Execute(Settings.ExtremePowersSettings settings, int playerId, int targetTileId)
        {
            // The adapter invokes this only after the API validates and synchronizes the operation.
            SHCDESE.Interop.UnmanagedVector2<ushort> point = SHCDESE.API.GameTileManagerAPI.Instance.GetTileVectorFromId(targetTileId);
            int spawned = 0;
            for (int i = 0; i < settings.DemoSpawnCount; i++)
            {
                if (SHCDESE.API.GameUnitManagerAPI.Instance.CreateUnitLocal(playerId, playerId, point.X, point.Y, 0, (SHCDESE.Interop.eChimps)settings.DemoUnitType) <= 0) break;
                spawned++;
            }
            return spawned;
        }
    }
}
