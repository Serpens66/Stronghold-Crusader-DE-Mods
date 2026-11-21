using System;
using APIShared;

namespace TrappedUnitRescueTest.Tests
{
    internal static class GeneralObserverFixture
    {
        internal static void Run()
        {
            var context = new AivBuildStepContext(123, 2, 7, 0, 1);
            int calls = 0;
            long activeSession = 9;
            Func<long, bool> current = id => id == activeSession;
            Action after = () => calls++;
            var receipt = new GeneralRescueObserver.Completion(context, 9, current, after);
            receipt.Complete(new AivBuildStepCompletion(context, true, 0));
            receipt.Complete(new AivBuildStepCompletion(context, true, 1));
            Require(calls == 1, "Normal zero-result completion must be topology-checked exactly once");
            new GeneralRescueObserver.Completion(context, 9, current, after).Complete(new AivBuildStepCompletion(context, false, 0));
            Require(calls == 1, "Exceptional original must not perform recovery");
            new GeneralRescueObserver.Completion(context, 9, current, after).Complete(
                new AivBuildStepCompletion(new AivBuildStepContext(123, 2, 7, 0, 1), true, 1));
            Require(calls == 1, "A different invocation with identical arguments is not this receipt");
            var stale = new GeneralRescueObserver.Completion(context, 9, current, after);
            activeSession = 10;
            stale.Complete(new AivBuildStepCompletion(context, true, 1));
            Require(calls == 1, "A replaced or ended mission must invalidate pending work");
            activeSession = 9;
            var throwing = new GeneralRescueObserver.Completion(context, 9, current, () => { calls++; throw new InvalidOperationException("after"); });
            try { throwing.Complete(new AivBuildStepCompletion(context, true, 1)); }
            catch (InvalidOperationException) { }
            throwing.Complete(new AivBuildStepCompletion(context, true, 1));
            Require(calls == 2, "Failure in recovery must not replay a consumed receipt");
            Console.WriteLine("PASS: production AIV observer receipt contracts=6; no native call or hook publication");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
