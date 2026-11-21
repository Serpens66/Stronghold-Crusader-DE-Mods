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
            GeneralObserverFixture.Run();
            Console.WriteLine("PASS: worker native guard assertions=" + native + "; production local rescue policy checked. No game hook installed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
