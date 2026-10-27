using System;
using System.IO;

internal static class Program
{
    // --contracts: small exact contracts only. --capture <raw.gz>: contracts plus measurement.
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--capture") return GroupGate.Run(Path.GetFullPath(args[1]));
            if (args.Length == 0 || args.Length == 1 && args[0] == "--contracts") { GroupGate.Contracts(); return 0; }
            Console.WriteLine("Usage: [--contracts] | --capture <snapshot.raw.gz>"); return 64;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
