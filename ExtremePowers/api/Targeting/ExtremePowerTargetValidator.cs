namespace ExtremePowers.API
{
    public static class ExtremePowerTargetValidator
    {
        public static bool IsValid(ExtremePowerTarget target)
        {
            switch (target.Kind) { case ExtremePowerTargetKind.None: return target.TileIndex == -1 && target.UnitId == -1; case ExtremePowerTargetKind.MapPoint: return target.TileIndex >= 0 && target.UnitId == -1; case ExtremePowerTargetKind.Unit: return target.TileIndex == -1 && target.UnitId > 0; default: return false; }
        }
    }
}
