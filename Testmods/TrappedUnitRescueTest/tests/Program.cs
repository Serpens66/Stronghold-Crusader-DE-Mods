using System;
using TrappedUnitRescueTest;
using TrappedUnitRescueTest.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length!=1)throw new ArgumentException("Pass the explicitly selected APIShared plugin package directory.");
            FixesManagedContractFixture.Run();
            MountedScannerRuntimeFixture.Run(args[0]);
            int native = WorkerGuardFixture.Run();
            RescuePolicyFixture.Run();
            CivilianRemovalPolicyFixture.Run();
            CandidateOwnerGateFixture.Run();
            IdleEscapeFixture.Run();
            SeriesFixture.Run();
            LocalFieldFixture.Run();
            CompactArchiveFixture.Run();
            GeneralObserverFixture.Run();
            RescueGeometryWitnessFixture.Run();
            RescueReplanIntentFixture.Run();
            RescueGeometryPersistenceFixture.Run();
            ReplanCoordinatorFixture.Run();
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
