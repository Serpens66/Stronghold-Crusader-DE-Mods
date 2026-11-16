using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace GameStateProbe
{
    /// <summary>
    /// Line-based JSON request/response server on 127.0.0.1. Each request line is a JSON object with "cmd".
    /// The socket thread only parses and waits; every command runs on the game's own threads:
    /// context "tick" = inside GameTimeManagerAPI.OnTick (consistent between simulation ticks),
    /// context "main" = Unity main thread via UnityMainThreadDispatcher.EnqueueStatic (works while paused),
    /// context "auto" (default) = tick if the simulation ticked recently, else main.
    /// All commands are read-only.
    /// </summary>
    internal static class ProbeServer
    {
        private sealed class Pending
        {
            public Dictionary<string, object> Request;
            public string Context;
            public long MinTick;
            public object Response;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static ManualLogSource log;
        private static Thread listenerThread;
        private static readonly ConcurrentQueue<Pending> TickQueue = new ConcurrentQueue<Pending>();
        private static readonly List<Pending> Deferred = new List<Pending>();
        private static long lastTick = -1;
        private static long lastTickUtcTicks;
        private static int tickSubscribed;
        private static bool firstTickLogged;

        public static void Start(ManualLogSource logger, int port)
        {
            log = logger;
            listenerThread = new Thread(() => Listen(port)) { IsBackground = true, Name = "GameStateProbe" };
            listenerThread.Start();

            // OnTick is only usable once the native library is loaded (Extender globals resolved).
            if (CrusaderLibrary.Instance.IsLibraryLoaded()) SubscribeTick();
            else CrusaderLibrary.Instance.LibraryLoaded += _ => SubscribeTick();
        }

        private static void SubscribeTick()
        {
            if (Interlocked.Exchange(ref tickSubscribed, 1) != 0) return;
            GameTimeManagerAPI.Instance.OnTick += OnTick;
            Plugin.Log(log, "tick hook subscribed");
        }

        internal static long LastTick => Interlocked.Read(ref lastTick);

        private static void OnTick(int tick)
        {
            Interlocked.Exchange(ref lastTick, tick);
            Interlocked.Exchange(ref lastTickUtcTicks, DateTime.UtcNow.Ticks);
            if (!firstTickLogged) { firstTickLogged = true; Plugin.Log(log, "first tick " + tick); }

            if (TickQueue.IsEmpty && Deferred.Count == 0) return; // nothing requested: no per-tick cost
            while (TickQueue.TryDequeue(out var p)) Deferred.Add(p);
            for (int i = Deferred.Count - 1; i >= 0; i--)
            {
                var p = Deferred[i];
                if (tick < p.MinTick) continue;
                Deferred.RemoveAt(i);
                Run(p, "tick");
            }
        }

        private static void Run(Pending p, string context)
        {
            try { p.Response = ProbeCommands.Execute(p.Request, context); }
            catch (Exception ex) { p.Response = Error(ex.GetType().Name + ": " + ex.Message); }
            p.Done.Set();
        }

        private static void Listen(int port)
        {
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                Plugin.Log(log, "listening on 127.0.0.1:" + port);
            }
            catch (Exception ex)
            {
                log.LogError("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] GAMESTATE_PROBE listen failed: " + ex.Message);
                return;
            }
            while (true)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    var t = new Thread(() => Serve(client)) { IsBackground = true, Name = "GameStateProbe.client" };
                    t.Start();
                }
                catch (ThreadAbortException)
                {
                    return; // process shutdown (seen after "quit"); nothing to report
                }
                catch (Exception ex)
                {
                    log.LogWarning("GAMESTATE_PROBE accept: " + ex.Message);
                    Thread.Sleep(200);
                }
            }
        }

        private static void Serve(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
            {
                string line;
                try
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Trim().Length == 0) continue;
                        Handle(line, writer);
                    }
                }
                catch (IOException) { /* client went away */ }
            }
        }

        private static void Handle(string line, StreamWriter writer)
        {
            Dictionary<string, object> req;
            try
            {
                req = Shared.DependencyFreeJson.Parse(line) as Dictionary<string, object>;
                if (req == null) throw new FormatException("request must be a JSON object");
            }
            catch (Exception ex)
            {
                Write(writer, Error("bad request: " + ex.Message));
                return;
            }

            string context = Str(req, "context") ?? "auto";
            int timeoutMs = Int(req, "timeoutMs", 5000);

            if (Str(req, "cmd") == "watch")
            {
                // {"cmd":"watch","request":{...},"samples":N,"everyTicks":K} -> N response lines, K ticks apart
                var inner = req.TryGetValue("request", out var r) ? r as Dictionary<string, object> : null;
                if (inner == null) { Write(writer, Error("watch needs \"request\"")); return; }
                int samples = Math.Max(1, Math.Min(Int(req, "samples", 10), 100000));
                int every = Math.Max(1, Int(req, "everyTicks", 1));
                long next = LastTick + 1;
                for (int i = 0; i < samples; i++)
                {
                    var resp = Submit(inner, "tick", next, timeoutMs);
                    if (resp is Dictionary<string, object> d) { d["sample"] = i; }
                    Write(writer, resp);
                    if (resp is Dictionary<string, object> e && e.TryGetValue("tick", out var tk) && tk is long tl) next = tl + every;
                    else next = LastTick + every;
                }
                return;
            }

            Write(writer, Submit(req, context, 0, timeoutMs));
        }

        private static object Submit(Dictionary<string, object> req, string context, long minTick, int timeoutMs)
        {
            if (context == "auto")
            {
                long ageMs = (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastTickUtcTicks)) / TimeSpan.TicksPerMillisecond;
                context = tickSubscribed == 1 && lastTick >= 0 && ageMs < 300 ? "tick" : "main";
            }
            var p = new Pending { Request = req, Context = context, MinTick = minTick };
            if (context == "tick") TickQueue.Enqueue(p);
            else if (context == "main") UnityMainThreadDispatcher.EnqueueStatic(() => Run(p, "main"));
            else return Error("context must be auto, tick or main");

            if (!p.Done.Wait(timeoutMs))
                return Error("timeout after " + timeoutMs + " ms in context " + context
                             + (context == "tick" ? " (simulation paused or not running? use \"context\":\"main\")" : ""));
            return p.Response;
        }

        // The serializer indents; valid JSON never holds raw line breaks inside strings, so stripping them
        // yields exactly one response line per request (the protocol framing).
        private static void Write(StreamWriter writer, object value) =>
            writer.WriteLine(Shared.DependencyFreeJson.Serialize(value).Replace("\r", "").Replace("\n", ""));

        internal static Dictionary<string, object> Error(string message) =>
            new Dictionary<string, object> { ["ok"] = false, ["error"] = message };

        internal static string Str(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

        internal static int Int(Dictionary<string, object> d, string key, int fallback) =>
            d.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback;
    }
}
