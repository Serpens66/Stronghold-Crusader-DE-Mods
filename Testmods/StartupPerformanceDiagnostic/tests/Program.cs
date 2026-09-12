using System;
using System.Collections.Generic;
using System.Linq;

namespace StartupPerformanceDiagnostic.Tests
{
    internal static class Program
    {
        private static int checks;

        private static int Main()
        {
            try
            {
                TestCompleteTimeline();
                TestLastPluginEndsAtChainloaderCompletion();
                TestMissingMarkersFailVisible();
                TestDuplicateAndOutOfOrderMarkers();
                TestPercentageGuards();
                TestStaticFieldReader();
                TestStaticFieldReaderRejectsInvalidContracts();
                Console.WriteLine("StartupPerformanceDiagnostic tests passed: " + checks + " checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void TestCompleteTimeline()
        {
            StartupTimingAnalysis result = TimingAnalysis.Analyze(new[]
            {
                Event(200, "Preloader finished"),
                Event(220, "Chainloader ready"),
                Event(250, "Chainloader started"),
                Event(300, "2 plugins to load"),
                Event(320, "Loading [First 1.0.0]"),
                Event(420, "Loading [Second 2.0.0]"),
                Event(620, "Chainloader startup complete")
            }, 0, 100, 1000);

            Check(result.Plugins.Count == 2, "plugin count");
            Check(result.Plugins[0].Label == "First 1.0.0", "first label");
            Check(result.Plugins[0].DurationTicks == 100, "first duration");
            Check(result.Plugins[1].DurationTicks == 200, "second duration");
            Check(result.PluginTotalTicks == 300, "plugin sum");
            Check(result.Warnings.Count == 0, "unexpected complete-timeline warning");
        }

        private static void TestLastPluginEndsAtChainloaderCompletion()
        {
            StartupTimingAnalysis result = TimingAnalysis.Analyze(StandardEvents(), 0, 100, 1000);
            Check(result.Plugins.Last().CompletedTimestamp == 620, "last plugin did not end at chainloader completion");
        }

        private static void TestMissingMarkersFailVisible()
        {
            StartupTimingAnalysis result = TimingAnalysis.Analyze(new[]
            {
                Event(320, "Loading [Only 1.0.0]")
            }, 0, 100, 1000);
            Check(result.Plugins.Count == 0, "incomplete plugin was treated as complete");
            Check(result.Warnings.Any(item => item.Contains("final plugin timing window")), "missing final-window warning");
            Check(result.Warnings.Any(item => item.Contains("Chainloader completion")), "missing chainloader warning");
        }

        private static void TestDuplicateAndOutOfOrderMarkers()
        {
            var events = new List<TimelineEvent>(StandardEvents())
            {
                Event(230, "Chainloader ready"),
                Event(150, "Chainloader startup complete")
            };
            StartupTimingAnalysis result = TimingAnalysis.Analyze(events, 0, 100, 1000);
            Check(result.Warnings.Any(item => item.Contains("Duplicate marker")), "duplicate marker warning");
            Check(result.Warnings.Any(item => item.Contains("chronological order")), "ordering warning");
        }

        private static void TestPercentageGuards()
        {
            Check(Math.Abs(TimingAnalysis.Percentage(25, 100) - 25.0) < 0.0001, "percentage calculation");
            Check(TimingAnalysis.Percentage(25, 0) == 0, "zero percentage denominator");
            Check(Math.Abs(TimingAnalysis.Milliseconds(500, 1000) - 500.0) < 0.0001, "milliseconds calculation");
        }

        private static void TestStaticFieldReader()
        {
            bool created = StaticFieldReader<bool>.TryCreate(
                typeof(FieldFixture),
                "flag",
                out StaticFieldReader<bool> reader,
                out string error);
            Check(created, "private static field was not resolved: " + error);
            FieldFixture.SetFlag(false);
            Check(!reader.Read(), "private static false value");
            FieldFixture.SetFlag(true);
            Check(reader.Read(), "private static true value");
        }

        private static void TestStaticFieldReaderRejectsInvalidContracts()
        {
            Check(!StaticFieldReader<bool>.TryCreate(
                typeof(FieldFixture), "missing", out _, out string missingError) &&
                missingError.Contains("was not found"), "missing field validation");
            Check(!StaticFieldReader<bool>.TryCreate(
                typeof(FieldFixture), "instanceFlag", out _, out string instanceError) &&
                instanceError.Contains("not static"), "instance field validation");
            Check(!StaticFieldReader<bool>.TryCreate(
                typeof(FieldFixture), "wrongType", out _, out string typeError) &&
                typeError.Contains("instead of"), "field type validation");
        }

        private static TimelineEvent[] StandardEvents() => new[]
        {
            Event(200, "Preloader finished"),
            Event(220, "Chainloader ready"),
            Event(250, "Chainloader started"),
            Event(320, "Loading [First 1.0.0]"),
            Event(420, "Loading [Second 2.0.0]"),
            Event(620, "Chainloader startup complete")
        };

        private static TimelineEvent Event(long timestamp, string message) => new TimelineEvent(timestamp, message);

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("Check failed: " + message);
        }

        private sealed class FieldFixture
        {
            private static bool flag;
            private static int wrongType = 0;
            private bool instanceFlag = false;

            internal static void SetFlag(bool value)
            {
                flag = value;
            }

            internal static int ReadWrongType()
            {
                return wrongType;
            }

            internal bool ReadInstanceFlag()
            {
                return instanceFlag;
            }
        }
    }
}
