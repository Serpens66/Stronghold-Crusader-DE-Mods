namespace ExtremePowers.Demo
{
    internal static class GoldSpawnDemo
    {
        internal static bool CanExecute(Settings.ExtremePowersSettings settings, int playerId, int targetTileId, out string reason)
        {
            bool valid = settings.EnableGoldReplacement && settings.DemoSpawnCount > 0 &&
                SHCDESE.API.GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                SHCDESE.API.GameTileManagerAPI.Instance.IsValidTileId(targetTileId);
            reason = valid ? null : "Gold spawn demo is disabled or has no valid player, target, or count.";
            return valid;
        }

        internal static void Execute(Settings.ExtremePowersSettings settings, int playerId, int targetTileId)
        {
            // The adapter invokes this only after the API validates and synchronizes the operation.
            SHCDESE.Interop.UnmanagedVector2<ushort> point = SHCDESE.API.GameTileManagerAPI.Instance.GetTileVectorFromId(targetTileId);
            for (int i = 0; i < settings.DemoSpawnCount; i++)
                if (SHCDESE.API.GameUnitManagerAPI.Instance.CreateUnitLocal(playerId, playerId, point.X, point.Y, 0, (SHCDESE.Interop.eChimps)settings.DemoUnitType) <= 0) break;
        }
    }
}
