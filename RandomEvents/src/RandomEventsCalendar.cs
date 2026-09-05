using System;

namespace RandomEvents
{
    internal static class RandomEventsCalendar
    {
        internal const uint MonthsPerYear = 12;

        internal static int ToAbsoluteMonth(uint currentYear, uint currentMonth)
        {
            if (currentMonth >= MonthsPerYear)
                throw new InvalidOperationException($"Unsupported Vanilla calendar values year={currentYear}, month={currentMonth}.");

            ulong absoluteMonth = (ulong)currentYear * MonthsPerYear + currentMonth;
            if (absoluteMonth > int.MaxValue)
                throw new InvalidOperationException($"Vanilla calendar value exceeds the Random Events state range: year={currentYear}, month={currentMonth}.");
            return (int)absoluteMonth;
        }
    }
}
