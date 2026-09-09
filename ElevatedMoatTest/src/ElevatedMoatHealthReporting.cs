namespace ElevatedMoatTest
{
    internal static class ElevatedMoatHealthReporting
    {
        internal const long ReportInterval = 4096;

        internal static bool ShouldReport(long successfulCorrectionCount) =>
            successfulCorrectionCount > 0 && successfulCorrectionCount % ReportInterval == 0;
    }
}
