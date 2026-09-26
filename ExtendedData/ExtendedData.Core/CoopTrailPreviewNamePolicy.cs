namespace ExtendedData.Core
{
    public static class CoopTrailPreviewNamePolicy
    {
        public static string Resolve(bool activeMissionAndSlot, bool aiMember,
            int playerId, int lordType, string customLordName)
        {
            if (!activeMissionAndSlot || !aiMember || playerId < 2 || playerId > 8 ||
                lordType < 29 || lordType > 37 || string.IsNullOrWhiteSpace(customLordName))
                return null;
            return customLordName;
        }
    }
}
