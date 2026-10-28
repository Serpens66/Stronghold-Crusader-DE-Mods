using System;
using System.IO;

internal static class Program
{
    // --contracts: small exact contracts only.
    // --capture <raw.gz> [--with-exact] [--only name,name]: contracts plus measurement;
    // variant A only on request; --only restricts the measured loads by name.
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "--capture")
            {
                bool withExact = false; string[] only = null;
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--with-exact") withExact = true;
                    else if (args[i] == "--only" && i + 1 < args.Length) only = args[++i].Split(',');
                    else { Console.WriteLine("Unknown option " + args[i]); return 64; }
                }
                return GroupGate.Run(Path.GetFullPath(args[1]), withExact, only);
            }
            if (args.Length == 0 || args.Length == 1 && args[0] == "--contracts") { GroupGate.Contracts(); return 0; }
            Console.WriteLine("Usage: [--contracts] | --capture <snapshot.raw.gz> [--with-exact] [--only name,name]"); return 64;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
