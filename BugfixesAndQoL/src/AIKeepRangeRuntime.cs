using System;
using System.Threading;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL
{
    // Statically rooted by the parent runtime, outside its generic Dispose path.
    internal sealed class AIKeepRangeRuntime
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly AIKeepDistanceCheck callback;
        private readonly Func<int, bool> isAI = id => GamePlayerManagerAPI.Instance.IsAIPlayer(id);
        private NativeDetour<AIKeepDistanceCheck> hook;
        private AIKeepDistanceCheck original;
        private IDisposable initialization, ended;
        private bool installed;
        private int enabled, firstCallback, faulted;

        internal AIKeepRangeRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            callback = OnDistanceCheck;
        }

        internal void Initialize(CrusaderLibraryLoadContext context)
        {
            if (installed) return;
            if (Chainloader.PluginInfos.ContainsKey("AIKeepRangeLimitTest_Serp"))
                throw new InvalidOperationException("AI_KEEP_RANGE_INACTIVE: remove AIKeepRangeLimitTest_Serp and restart; no competing detour installed.");
            ulong target = AIKeepRangeNativeContract.Resolve(context, out string resolution);
            AIKeepRangeNativeContract.ProbeBackend(target);
            SubscribeLifecycle();
            NativeDetour<AIKeepDistanceCheck> candidate = null;
            try
            {
                var request = new DetourRequest<AIKeepDistanceCheck>
                {
                    Name = "AI keep distance predicate", TargetAddress = target, Callback = callback
                };
                candidate = AIKeepRangeNativeContract.Backend.CreateDetour(in request) as NativeDetour<AIKeepDistanceCheck>;
                AIKeepRangeNativeContract.ValidateDetour(candidate, target, false);
                original = candidate.Original;
                candidate.Enable();
                AIKeepRangeNativeContract.ValidateDetour(candidate, target, true);
                hook = candidate;
                installed = true; // Published permanently; only logical activation changes from here.
            }
            catch
            {
                candidate?.Dispose(); // Rollback of the unpublished installation candidate only.
                throw;
            }
            Refresh();
            try { Shared.DebugLogHelper.LogDebug(log, "AI_KEEP_RANGE_READY: " + resolution + ", scheme=Indirect, displaced=10; AI-only distance exception."); } catch { }
        }

        internal void SubscribeLifecycle()
        {
            Shared.MissionEvents.SetOwner(BugfixesAndQoLPlugin.PluginGuid);
            if (initialization == null)
                initialization = Shared.MissionEvents.Initialization.Subscribe(_ => Refresh());
            if (ended == null)
                ended = Shared.MissionEvents.Ended.Subscribe(_ => Volatile.Write(ref enabled, 0));
        }

        // Mission end clears the live mask, not the capability for the next lobby start.
        internal bool LobbyBypass => installed && Volatile.Read(ref faulted) == 0 &&
            settings.EnableMod && settings.RemoveAIKeepRangeLimit;

        internal void Refresh() => Volatile.Write(ref enabled,
            installed && Volatile.Read(ref faulted) == 0 && settings.EnableMod && settings.RemoveAIKeepRangeLimit ? 1 : 0);

        private long OnDistanceCheck(IntPtr manager, int playerId, uint x, uint y, int range)
        {
            bool bypass = false;
            try
            {
                bypass = AIKeepRangeDecision.Bypass(Volatile.Read(ref enabled) != 0, playerId, isAI);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref enabled, 0);
                if (Interlocked.Exchange(ref faulted, 1) == 0)
                    try { Shared.DebugLogHelper.LogError(log, "AI_KEEP_RANGE_INACTIVE: classification failed; Vanilla preserved until restart: " + ex); } catch { }
            }
            // One post-startup marker, not per-cell diagnostics or continually incremented counters.
            if (Volatile.Read(ref firstCallback) == 0 && Interlocked.Exchange(ref firstCallback, 1) == 0)
                try { Shared.DebugLogHelper.LogDebug(log, "AI_KEEP_RANGE_CALLBACK: permanent runtime reached native distance check after startup."); } catch { }
            return AIKeepRangeDecision.Execute(bypass, original, manager, playerId, x, y, range);
        }
    }
}
