using BepInEx.Logging;
using ExtendedData;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace AIResourceReserveTest
{
    internal sealed unsafe class AIResourceReserveRuntime
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
        private const int RescanTicks = 16;
        private const int GoodCount = 3;

        private readonly ManualLogSource log;
        private readonly int[,] consumers = new int[LastPlayerId + 1, GoodCount];
        private readonly ReserveOverrides[] overridesByPlayer = new ReserveOverrides[LastPlayerId + 1];
        private readonly ResolutionState[] resolution = new ResolutionState[LastPlayerId + 1];
        private readonly bool[] hostWaitLogged = new bool[LastPlayerId + 1];
        private readonly int[,] priorStock = new int[LastPlayerId + 1, GoodCount];
        private readonly int[,] priorPending = new int[LastPlayerId + 1, GoodCount];
        private readonly bool[,] queuedByMod = new bool[LastPlayerId + 1, GoodCount];
        private readonly int[] priorGold = new int[LastPlayerId + 1];
        private readonly bool[] goldObserved = new bool[LastPlayerId + 1];
        private bool sessionActive;
        private bool firstTickLogged;
        private bool scanned;
        private int lastScanTick;

        private enum ResolutionState { Unknown, Ready, Disabled }

        internal AIResourceReserveRuntime(ManualLogSource log) { this.log = log; }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            Reset();
            sessionActive = true;
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_RESERVE_SESSION: id={session.SessionId}, kind={session.Kind}, active={sessionActive}.");
        }

        internal void OnSessionEnded() { Reset(); }

        internal void OnTick(int tick)
        {
            if (!sessionActive) return;
            if (!firstTickLogged)
            {
                firstTickLogged = true;
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_RESERVE_POST_STARTUP_TICK: tick={tick}, publisher=GameTimeManagerAPI.OnTick.");
            }
            try
            {
                if (!scanned || (uint)(tick - lastScanTick) >= RescanTicks)
                {
                    ScanBuildings();
                    scanned = true;
                    lastScanTick = tick;
                }

                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                GameAIManagerAPI ai = GameAIManagerAPI.Instance;
                for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
                {
                    if (!players.IsAIPlayer(playerId) ||
                        !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                        resources->r_IsPaused != 0 ||
                        resources->r_WinLossState != WinLossState.None ||
                        resources->r_LordUnitId == 0)
                    {
                        ClearObservation(playerId);
                        continue;
                    }

                    if (!ResolveOverrides(playerId))
                    {
                        ClearObservation(playerId);
                        continue;
                    }
                    int gold = players.GetPlayerGold(playerId);
                    int previousGold = goldObserved[playerId] ? priorGold[playerId] : gold;
                    for (int index = 0; index < GoodCount; index++)
                        CheckGood(players, ai, playerId, (ReserveGood)index, gold, previousGold);
                    priorGold[playerId] = gold;
                    goldObserved[playerId] = true;
                }
            }
            catch (Exception error)
            {
                // A broken runtime must not make repeated writes on later ticks.
                sessionActive = false;
                Shared.DebugLogHelper.LogError(log, "AI reserve session stopped after runtime error: " + error);
            }
        }

        private void ScanBuildings()
        {
            Array.Clear(consumers, 0, consumers.Length);
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                int ownerId = building.r_PlayerIdOwner;
                if (ownerId < FirstPlayerId || ownerId > LastPlayerId) continue;
                for (int index = 0; index < GoodCount; index++)
                    if (ReservePolicy.Consumes(in building, (ReserveGood)index))
                        consumers[ownerId, index]++;
            }
        }

        private bool ResolveOverrides(int playerId)
        {
            if (resolution[playerId] == ResolutionState.Ready) return true;
            if (resolution[playerId] == ResolutionState.Disabled) return false;

            ExtendedDataModDataReadResult result =
                ExtendedDataModDataApi.ReadSelectedLordNamespace(playerId, AIResourceReserveTestPlugin.Guid);
            switch (result.Status)
            {
                case ExtendedDataModDataReadStatus.Success:
                    if (!ReserveOverrides.TryParse(result.Data, out ReserveOverrides parsed,
                        out string error))
                    {
                        Disable(playerId, result.Source + ": " + error);
                        return false;
                    }
                    overridesByPlayer[playerId] = parsed;
                    resolution[playerId] = ResolutionState.Ready;
                    Shared.DebugLogHelper.LogInfo(log,
                        $"AI reserve parameters: player={playerId}, source={result.Source}, " +
                        $"MinWood={Describe(parsed.MinWood)}, MinIron={Describe(parsed.MinIron)}, MinAle={Describe(parsed.MinAle)}.");
                    return true;
                case ExtendedDataModDataReadStatus.FileNotFound:
                case ExtendedDataModDataReadStatus.NamespaceNotFound:
                    overridesByPlayer[playerId] = new ReserveOverrides(null, null, null);
                    resolution[playerId] = ResolutionState.Ready;
                    Shared.DebugLogHelper.LogInfo(log,
                        $"AI reserve parameters: player={playerId}, source=automatic, reason={result.Status}.");
                    return true;
                case ExtendedDataModDataReadStatus.HostDataUnavailable:
                    if (!hostWaitLogged[playerId])
                    {
                        hostWaitLogged[playerId] = true;
                        Shared.DebugLogHelper.LogWarning(log,
                            $"AI reserve waiting for Lord data: player={playerId}, reason={result.Diagnostic}");
                    }
                    return false;
                default:
                    Disable(playerId, result.Status + ": " + result.Diagnostic);
                    return false;
            }
        }

        private void CheckGood(GamePlayerManagerAPI players, GameAIManagerAPI ai,
            int playerId, ReserveGood good, int gold, int previousGold)
        {
            int index = (int)good;
            eGoods gameGood = ReservePolicy.Good(good);
            int stock = players.GetGoodAmount(playerId, gameGood);
            int pending = ai.GetGoodFromPendingPurchases(playerId, gameGood);
            if (queuedByMod[playerId, index] && priorPending[playerId, index] > 0 && pending == 0)
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_RESERVE_PENDING_CLEARED: player={playerId}, good={gameGood}, " +
                    $"stockBefore={priorStock[playerId, index]}, stockAfter={stock}, " +
                    $"goldBefore={previousGold}, goldAfter={gold}.");
            if (queuedByMod[playerId, index] && priorPending[playerId, index] > 0 &&
                stock > priorStock[playerId, index])
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_RESERVE_STOCK_INCREASE: player={playerId}, good={gameGood}, " +
                    $"before={priorStock[playerId, index]}, after={stock}, priorPending={priorPending[playerId, index]}, currentPending={pending}, " +
                    $"goldBefore={previousGold}, goldAfter={gold}.");
            if (pending == 0)
                queuedByMod[playerId, index] = false;

            int target = overridesByPlayer[playerId].GetTarget(good, consumers[playerId, index]);
            int proposal = ReservePolicy.Proposal(stock, target, pending);
            if (proposal > 0)
            {
                ai.SetGoodToPendingPurchases(playerId, gameGood, proposal);
                pending = proposal;
                queuedByMod[playerId, index] = true;
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_RESERVE_QUEUED: player={playerId}, good={gameGood}, stock={stock}, " +
                    $"target={target}, amount={proposal}, gold={gold}.");
            }
            priorStock[playerId, index] = stock;
            priorPending[playerId, index] = pending;
        }

        private void Disable(int playerId, string reason)
        {
            resolution[playerId] = ResolutionState.Disabled;
            Shared.DebugLogHelper.LogWarning(log,
                $"AI reserve disabled for player={playerId}: {reason}");
        }

        private static string Describe(int? value) => value?.ToString() ?? "automatic";

        private void ClearObservation(int playerId)
        {
            goldObserved[playerId] = false;
            for (int index = 0; index < GoodCount; index++)
            {
                priorPending[playerId, index] = 0;
                priorStock[playerId, index] = 0;
                queuedByMod[playerId, index] = false;
            }
        }

        private void Reset()
        {
            sessionActive = false;
            firstTickLogged = false;
            scanned = false;
            lastScanTick = 0;
            Array.Clear(consumers, 0, consumers.Length);
            Array.Clear(priorStock, 0, priorStock.Length);
            Array.Clear(priorPending, 0, priorPending.Length);
            Array.Clear(queuedByMod, 0, queuedByMod.Length);
            Array.Clear(priorGold, 0, priorGold.Length);
            Array.Clear(goldObserved, 0, goldObserved.Length);
            Array.Clear(overridesByPlayer, 0, overridesByPlayer.Length);
            Array.Clear(resolution, 0, resolution.Length);
            Array.Clear(hostWaitLogged, 0, hostWaitLogged.Length);
        }
    }
}
