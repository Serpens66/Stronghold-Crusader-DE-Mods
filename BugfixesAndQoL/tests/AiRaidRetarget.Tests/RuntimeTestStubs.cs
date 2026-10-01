using System;
using BepInEx.Logging;
namespace Shared {
 internal sealed class GameplaySessionStartedContext { internal long SessionId; internal bool IsEditor, IsReplay; }
 internal static class GameplaySessionLifecycle { internal static IDisposable SubscribeStarted(ManualLogSource log, Action<GameplaySessionStartedContext> start, Action end) { throw new NotSupportedException("No publisher in isolated runtime tests"); } }
}
