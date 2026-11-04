using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using BetterPathfindingTest.Api;
using BetterPathfindingTest.Service;

internal static partial class Program
{
    private static int ExternalClient(string path)
    {
        using var work = new BlockingCollection<Action>(); Exception failure = null;
        var thread = new Thread(() => { foreach (Action job in work.GetConsumingEnumerable()) { try { job(); } catch (Exception ex) { failure = ex; } } }) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
        thread.Start(); var service = new ResidentNavigation(work.Add, 256L * 1024 * 1024); NavigationApi.Bind(service);
        try
        {
            service.Begin(1, 1); service.Offer(Frame(1, Grid(64, 16)));
            var clock = Stopwatch.StartNew(); while (!service.GetStatus(1).IsReady && clock.ElapsedMilliseconds < 10000) Thread.Sleep(1);
            if (!service.GetStatus(1).IsReady) throw new InvalidOperationException("External host was not ready: " + service.GetStatus(1).Reason, failure);
            Assembly client = Assembly.LoadFrom(System.IO.Path.GetFullPath(path));
            client.GetType("NavigationClientProbe", true).GetMethod("CheckActive").Invoke(null, null);
            if (failure != null) throw failure;
            Console.WriteLine("PASS: independently compiled external consumer exercised readiness, contexts, async batches, immutable prefixes, full steps and release on a real background worker.");
            return 0;
        }
        catch (TargetInvocationException ex) { Console.Error.WriteLine(ex.InnerException ?? ex); return 1; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            service.End(1); using var drained = new ManualResetEvent(false); work.Add(() => drained.Set()); drained.WaitOne(10000);
            work.CompleteAdding(); thread.Join(10000); NavigationApi.Bind(null);
        }
    }
}
