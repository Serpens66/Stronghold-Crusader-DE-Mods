namespace SurrenderDesyncDiagnostic
{
    internal static class SurrenderTracePolicy
    {
        internal static bool MapRestarted(int previousMapTick, int mapTick) =>
            previousMapTick >= 0 && mapTick < previousMapTick;

        internal static bool IsSurrenderLord(int surrenderPlayer, int surrenderUnit,
            int surrenderGlobal, int observedPlayer, int observedUnit, int observedGlobal) =>
            surrenderPlayer > 0 && surrenderPlayer == observedPlayer &&
            ((surrenderUnit > 0 && surrenderUnit == observedUnit) ||
             (surrenderGlobal > 0 && surrenderGlobal == observedGlobal));

        internal static bool FirstResyncFinished(bool started, bool before, bool now) =>
            started && before && !now;
    }
}
