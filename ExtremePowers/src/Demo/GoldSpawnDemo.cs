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

    }
}
