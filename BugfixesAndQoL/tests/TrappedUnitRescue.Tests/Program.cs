global using BugfixesAndQoL.TrappedUnitRescue;
using System;
using TrappedUnitRescueTest.Tests;
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length!=1)throw new ArgumentException("Pass the explicitly selected absolute APIShared plugin package directory.");
            BugfixesAndQoL.TrappedUnitRescue.Tests.MountedScannerGatedFixture.Run(args[0]);
            CandidateOwnerGateFixture.Run();
            CandidateQueueFixture.Run();
            CivilianRemovalPolicyFixture.Run();
            CommandJourneyFixture.Run();
            CompactArchiveFixture.Run();
            DiscoveryFixture.Run();
            FixesManagedContractFixture.Run();
            GeneralObserverFixture.Run();
            IdleEscapeFixture.Run();
            LocalFieldFixture.Run();
            MutationFootprintsFixture.Run();
            NativeMutationFootprintsFixture.Run();
            NativeVegetationAccessFixture.Run();
            PendingRescueFixture.Run();
            ReplanCoordinatorFixture.Run();
            RescueGeometryPersistenceFixture.Run();
            RescueGeometryWitnessFixture.Run();
            RescueOrderFixture.Run();
            RescuePolicyFixture.Run();
            RescueReplanIntentFixture.Run();
            StartedStepFixture.Run();
            WorkerRecoveryServiceFixture.Run();
            WorkerResumeLedgerFixture.Run();
            WorkerResumeSupervisorFixture.Run();
            WorkerTravelFixture.Run();
            Console.WriteLine("PASS: actual BugfixesAndQoL rescue production sources; linked existing pure fixtures; no game hooks.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
