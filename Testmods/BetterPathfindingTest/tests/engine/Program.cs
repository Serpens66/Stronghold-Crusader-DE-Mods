using System;
using System.IO;
using System.Reflection;
using System.Threading;
using BetterPathfindingTest;

internal static class EngineContract
{
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
        {
            string name = new AssemblyName(request.Name).Name + ".dll";
            string path = Path.Combine(args[0], "Stronghold Crusader Definitive Edition_Data", "Managed", name);
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); Console.WriteLine("PASS: installed EngineInterface Monitor scope and nonblocking contention; no native method called."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Run()
    {
        var gate = new EngineGate(); if (!gate.TryEnter()) throw new Exception("Uncontended lock failed."); gate.Exit();
        using (var ready = new ManualResetEvent(false)) using (var release = new ManualResetEvent(false))
        {
            var thread = new Thread(() => { if (!gate.TryEnter()) throw new Exception("Test owner lock failed."); ready.Set(); release.WaitOne(); gate.Exit(); });
            thread.Start(); if (!ready.WaitOne(5000)) throw new Exception("Test owner unavailable.");
            try { if (gate.TryEnter()) { gate.Exit(); throw new Exception("Contended lock succeeded."); } }
            finally { release.Set(); thread.Join(); }
        }
    }
}
