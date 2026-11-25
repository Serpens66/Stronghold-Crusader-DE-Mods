using System;
using TrappedUnitRescueTest;
using TrappedUnitRescueTest.Tests;

internal static class Program
{
    private static int Main()
    {
        try
        {
            FixesManagedContractFixture.Run();
            int native = WorkerGuardFixture.Run();
            RescuePolicyFixture.Run();
            IdleEscapeFixture.Run();
            GeneralObserverFixture.Run();
            RescueGeometryWitnessFixture.Run();
            RescueReplanIntentFixture.Run();
            RescueOrderFixture.Run();
            CandidateQueueFixture.Run();
            PendingRescueFixture.Run();
            DiscoveryFixture.Run();
            StartedStepFixture.Run();
            WorkerTravelFixture.Run();
            NativeVegetationAccessFixture.Run();
            CommandJourneyFixture.Run();
            WorkerResumeLedgerFixture.Run();
            WorkerResumeSupervisorFixture.Run();
            WorkerRecoveryServiceFixture.Run();
            MutationFootprintsFixture.Run();
            NativeMutationFootprintsFixture.Run();
            Console.WriteLine("PASS: worker native guard assertions=" + native + "; production local rescue policy checked. No game hook installed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
