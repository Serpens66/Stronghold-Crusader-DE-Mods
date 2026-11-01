using System;
using BepInEx.Logging;
namespace Shared {
 internal static class UnityMainThreadDispatch { internal static void TryRunInlineOrEnqueue(Action action) => action(); }
 internal sealed class GameplaySessionStartedContext { internal long SessionId; internal bool IsEditor, IsReplay; }
 internal static class GameplaySessionLifecycle { internal static IDisposable SubscribeStarted(ManualLogSource log, Action<GameplaySessionStartedContext> start, Action end) { throw new NotSupportedException("No publisher in isolated runtime tests"); } }
}

namespace BugfixesAndQoL { internal static class BugfixesAndQoLPlugin { internal const string PluginGuid="BugfixesAndQoL_Serp"; } }
namespace APIShared.Simulation { internal static class SimulationTickEvents {
 internal static bool TryRegister(string owner,string id,Action<int> callback,out string reason,int order=0) {
  reason="No publisher in isolated runtime tests"; return false;
 }
} }
