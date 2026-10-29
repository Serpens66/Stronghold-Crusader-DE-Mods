using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;

namespace PerfProbe
{
    // Scripted AI orders during the automated run phase (for example many Assassins sent to attack),
    // so a stress run reproduces the same burst every time. Config Automation.Actions:
    // "seconds:Attack:actor:target[:force]" entries separated by '|', seconds after the run started.
    // Same native order and forced acceptance as SpectatorEditorBuildTest's AI order buttons:
    // CrusaderDE.dll 0xD7580 (attack target, ordered AI, requester, order), hash-bound to FBCB9319
    // (see UpdateToNewDLL.md); force accepts exactly the scripted (AI, requester, target) through
    // the Extender's OnPlayerAIEvaluateAttackOrder Pre event. Runs on the automation's static render
    // callback under Vanilla's EngineInterface.threadLock; the event subscription is process-lived.
    internal static class PerfProbeAutomationActions
    {
        private const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int OrderRva = 0xD7580, OrderLength = 587, AttackOrder = 1;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeOrderCall(int attackTarget, int orderedAi, int requester, int order);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);

        private sealed class ScriptedOrder { internal double At; internal int Actor, Target; internal bool Force, Done; }
        private static readonly List<ScriptedOrder> orders = new List<ScriptedOrder>();
        private static NativeOrderCall nativeOrder;
        private static object engineLock;
        private static IDisposable forceSubscription;
        private static ScriptedOrder forced;
        private static ManualLogSource log;

        internal static void Parse(string text, ManualLogSource logger)
        {
            log = logger; orders.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (string entry in text.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] part = entry.Trim().Split(':');
                if (part.Length < 4 || part[1] != "Attack" ||
                    !double.TryParse(part[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double at) ||
                    !int.TryParse(part[2], out int actor) || !int.TryParse(part[3], out int target) ||
                    actor < 1 || actor > 8 || target < 1 || target > 8 || actor == target)
                { Log("ACTION_INVALID entry=" + entry); continue; }
                orders.Add(new ScriptedOrder { At = at, Actor = actor, Target = target, Force = part.Length > 4 && part[4] == "force" });
            }
            orders.Sort((a, b) => a.At.CompareTo(b.At));
            Log("actions parsed=" + orders.Count);
        }

        // At the start of the run phase: resolves the native order once (fail-closed) and subscribes the
        // forced acceptance for the process lifetime.
        internal static void Prepare()
        {
            if (orders.Count == 0 || nativeOrder != null) return;
            try
            {
                FieldInfo monitor = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
                if (monitor == null || monitor.FieldType != typeof(object) || (engineLock = monitor.GetValue(null)) == null)
                    throw new MissingFieldException("EngineInterface", "private static object threadLock");
                string file = Path.Combine(BepInEx.Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
                byte[] image = File.ReadAllBytes(file);
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(image)).Replace("-", "") != NativeSha256)
                        throw new InvalidOperationException("unknown native image; the AI order contract is hash-bound");
                int pe = BitConverter.ToInt32(image, 60), table = pe + 24 + BitConverter.ToUInt16(image, pe + 20), count = BitConverter.ToUInt16(image, pe + 6);
                int raw = -1, sectionRva = 0;
                for (int i = 0; i < count; i++)
                {
                    int header = table + i * 40, rva = BitConverter.ToInt32(image, header + 12), length = BitConverter.ToInt32(image, header + 16);
                    if (OrderRva >= rva && OrderRva + OrderLength <= (long)rva + length && (BitConverter.ToUInt32(image, header + 36) & 0x20000000) != 0)
                    { raw = BitConverter.ToInt32(image, header + 20); sectionRva = rva; break; }
                }
                if (raw < 0) throw new InvalidDataException("executable section of the order function missing");
                IntPtr module = GetModuleHandle("CrusaderDE.dll");
                if (module == IntPtr.Zero) throw new InvalidOperationException("CrusaderDE.dll is not loaded");
                var live = new byte[OrderLength];
                Marshal.Copy(new IntPtr(module.ToInt64() + OrderRva), live, 0, OrderLength);
                for (int i = 0; i < OrderLength; i++)
                    if (live[i] != image[raw + OrderRva - sectionRva + i]) throw new InvalidDataException("order function bytes differ at +" + i);
                forceSubscription = PlayerR3EventHooks.OnPlayerAIEvaluateAttackOrder.Observable.Subscribe(args =>
                {
                    ScriptedOrder current = forced;
                    if (args.Phase != EventHookPhase.Pre || current == null || args.TargetPlayerId != current.Actor ||
                        args.SourcePlayerId != current.Actor || args.ContextPlayerId != current.Target) return;
                    args.ReturnValue = true;
                    args.SkipOriginalFunction = true;
                });
                nativeOrder = (NativeOrderCall)Marshal.GetDelegateForFunctionPointer(new IntPtr(module.ToInt64() + OrderRva), typeof(NativeOrderCall));
                Log("actions ready rva=0x" + OrderRva.ToString("X") + " method=reference-rva");
            }
            catch (Exception error) { orders.Clear(); Log("ACTIONS_DISABLED " + error.Message); }
        }

        // Each render frame of the run phase: issues every order whose time has come.
        internal static void Step(double runSeconds)
        {
            if (nativeOrder == null) return;
            foreach (ScriptedOrder order in orders)
            {
                if (order.Done || order.At > runSeconds) continue;
                order.Done = true;
                string result;
                try
                {
                    var players = GamePlayerManagerAPI.Instance;
                    if (!players.IsAIPlayer(order.Actor)) result = "actor-not-ai";
                    else
                    {
                        lock (engineLock)
                        {
                            forced = order.Force ? order : null;
                            // The ordered AI also requests: the same fallback the HUD buttons use.
                            try { nativeOrder(order.Target, order.Actor, order.Actor, AttackOrder); }
                            finally { forced = null; }
                        }
                        result = "issued";
                    }
                }
                catch (Exception error) { result = "error " + error.Message; }
                Log("action at=" + order.At.ToString("0.0", CultureInfo.InvariantCulture) + "s kind=Attack actor=" + order.Actor +
                    " target=" + order.Target + " force=" + order.Force + " result=" + result);
            }
        }

        private static void Log(string text) =>
            log?.LogMessage("[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] AUTOMATION " + text);
    }
}
