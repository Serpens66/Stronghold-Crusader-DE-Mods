namespace ExtendedData.Core
{
    public static class CoopTrailPreviewTeamPolicy
    {
        // Row order is the order Vanilla actually draws, not the Lord/player slot order.
        public static void FillBoundaries(int[] displayedTeams, bool[] boundaries)
        {
            if (displayedTeams == null || boundaries == null ||
                displayedTeams.Length != 8 || boundaries.Length != 8)
                throw new System.ArgumentException("The Coop preview requires eight rows and boundaries.");

            int previousVisibleTeam = -1;
            for (int row = 0; row < 8; row++)
            {
                int team = displayedTeams[row];
                boundaries[row] = team >= 0 && previousVisibleTeam >= 0 &&
                    team != previousVisibleTeam;
                if (team >= 0)
                    previousVisibleTeam = team;
            }
        }
    }
}
