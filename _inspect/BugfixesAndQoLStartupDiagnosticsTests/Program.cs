using System;
using BugfixesAndQoL;

internal static class Program
{
    private static int Main()
    {
        AssertFalse(
            StartupDiagnosticPolicy.ShouldReportFixedLayoutFailure(false, false, true, false),
            "Native startup deferral must not be reported.");
        AssertFalse(
            StartupDiagnosticPolicy.ShouldReportFixedLayoutFailure(true, true, true, false),
            "A validated native layout must not be reported.");
        AssertTrue(
            StartupDiagnosticPolicy.ShouldReportFixedLayoutFailure(true, false, true, false),
            "An initialized but unknown native layout must be reported once.");
        AssertFalse(
            StartupDiagnosticPolicy.ShouldReportFixedLayoutFailure(true, false, true, true),
            "An unknown native layout must not be reported repeatedly.");
        AssertFalse(
            StartupDiagnosticPolicy.ShouldReportFixedLayoutFailure(true, false, false, false),
            "A disabled option must not be reported.");

        Console.WriteLine("BugfixesAndQoL startup diagnostic policy tests passed.");
        return 0;
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }
}
