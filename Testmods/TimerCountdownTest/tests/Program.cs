using System;
using TimerCountdownTest;

internal static class Program
{
    private static int count;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        count++;
    }

    private static void Main()
    {
        Check(TimerCountdownPolicy.FormatTicks(-1) == "0:00", "negative value clamps to zero");
        Check(TimerCountdownPolicy.FormatTicks(0) == "0:00", "zero");
        Check(TimerCountdownPolicy.FormatTicks(1) == "0:01", "partial second rounds up");
        Check(TimerCountdownPolicy.FormatTicks(40) == "0:01", "exact second");
        Check(TimerCountdownPolicy.FormatTicks(41) == "0:02", "next second");
        Check(TimerCountdownPolicy.FormatTicks(2400) == "1:00", "one game minute");
        Check(TimerCountdownPolicy.FormatTicks(2401) == "1:01", "minute boundary");
        Check(TimerCountdownPolicy.FormatTicks(24000) == "10:00", "minutes are not capped");
        Check(TimerCountdownPolicy.SelectOstRemaining(7, 8, 9) == 7, "defeat has priority");
        Check(TimerCountdownPolicy.SelectOstRemaining(null, 8, 9) == 8, "victory has priority over peace");
        Check(TimerCountdownPolicy.SelectOstRemaining(null, null, 9) == 9, "peace is selected");
        Check(!TimerCountdownPolicy.SelectOstRemaining(null, null, null).HasValue, "no active timer");
        Console.WriteLine("Timer countdown policy: " + count + " checks passed.");
    }
}
