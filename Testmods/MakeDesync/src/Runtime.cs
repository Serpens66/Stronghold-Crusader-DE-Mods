using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using APIShared.Multiplayer;
using APIShared.Networking;
using APIShared.Simulation;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace MakeDesync
{
    internal sealed unsafe class Runtime
    {
        private sealed class Attempt
        {
            internal Attempt(long session, bool deposit) { Session = session; Deposit = deposit; }
            internal readonly long Session;
            internal readonly bool Deposit;
            internal string Mode => Deposit ? "deposit" : "vanilla";
            internal bool Applied;
            internal int Lord, Tile = -1;
            internal uint LordGlobal, OriginalHealth;
            internal byte OriginalDeposit;
        }

        private readonly ManualLogSource log;
        private ModNetworkClient presentation;
        private IMissionLifecycleCapability missions;
        private object engineGate;
        private DepositHistory history;
        private IDisposable inputSubscription;
        private Attempt current;
        private long resyncStart;
        private bool firstTickLogged;

        internal Runtime(ManualLogSource log) => this.log = log;

        internal void Initialize(CrusaderLibraryLoadContext context)
        {
            try
            {
                if (inputSubscription != null) return;
                if (!MultiplayerResyncData.CheckIntegrity())
                    throw new InvalidOperationException("The audited native resync service is unavailable.");
                var api = ApiShared.ForMod(Plugin.Guid);
                presentation = api.Network;
                if (!api.TryGetMissionLifecycle(out missions, out var diagnostic))
                    throw new InvalidOperationException(diagnostic?.Reason);
                var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null || !field.IsPrivate || field.FieldType != typeof(object))
                    throw new MissingFieldException("EngineInterface.threadLock");
                engineGate = field.GetValue(null) ?? throw new InvalidOperationException("Engine monitor unavailable.");
                history = new DepositHistory();
                if (!missions.TryRegisterObserver("make-desync", null, EndMission, null, out diagnostic))
                    throw new InvalidOperationException(diagnostic?.Reason);
                if (!MultiplayerResyncEvents.TryRegister(Plugin.Guid, "observe", RepairStarted, RepairCompleted, out string reason, order: 100) ||
                    !SimulationTickEvents.TryRegister(Plugin.Guid, "apply", OnTick, out reason))
                    throw new InvalidOperationException(reason);
                inputSubscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(KeyDown);
                Info("MAKE_DESYNC_READY keys=Ctrl+Shift+F9/F10");
            }
            catch (Exception error) { Error("MAKE_DESYNC_REJECTED initialize " + error); }
        }

        private void KeyDown(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Pre || (args.Key != KeyCode.F9 && args.Key != KeyCode.F10) ||
                (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) ||
                (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))) return;

            lock (engineGate)
            {
                var mission = missions.Current;
                if (mission == null || !AttemptPolicy.CanQueue(mission.Mode.IsRealMultiplayer, mission.IsHost,
                    mission.LocalPlayerId, MultiplayerResyncEvents.IsInProgress, current != null))
                {
                    Info("MAKE_DESYNC_REJECTED requires a ready multiplayer client with no active attempt or repair");
                    return;
                }
                current = new Attempt(mission.SessionId, args.Key == KeyCode.F10);
                Info($"MAKE_DESYNC_ARMED session={current.Session} mode={current.Mode}");
            }
        }

        private void OnTick(int tick)
        {
            if (firstTickLogged && current == null) return;
            var mission = missions.Current;
            if (!firstTickLogged && mission?.Mode.IsRealMultiplayer == true)
            {
                firstTickLogged = true;
                Info($"MAKE_DESYNC_FIRST_TICK session={mission.SessionId} tick={tick} host={mission.IsHost}");
            }
            if (current == null) return;
            // The normal game tick publisher is inside the admitted engine run.
            if (!Monitor.IsEntered(engineGate))
            {
                lock (engineGate)
                    if (current?.Applied == false) Reject("simulation tick did not hold the engine monitor");
                return;
            }
            var attempt = current;
            if (attempt == null || attempt.Applied) return;
            if (mission?.SessionId != attempt.Session || !AttemptPolicy.CanQueue(mission.Mode.IsRealMultiplayer,
                mission.IsHost, mission.LocalPlayerId, MultiplayerResyncEvents.IsInProgress, false))
            {
                Reject("the queued multiplayer session changed or a native repair started");
                return;
            }

            try { Apply(attempt, mission.LocalPlayerId.Value, tick); }
            catch (Exception error) { Reject(error.Message); }
        }

        private void Apply(Attempt attempt, int player, int tick)
        {
            int lordId = GamePlayerManagerAPI.Instance.GetLordUnitId(player);
            bool lordReady = UnitAccess.TryGetById(lordId, out GameUnit* lord, out _) &&
                UnitAccess.IsReallyAlive(lord) && lord->r_UnitChimp == eChimps.CHIMP_TYPE_LORD &&
                lord->r_ControllableForPlayerId == player && lord->r_GlobalId != 0 &&
                lord->r_CurrentHealth >= 2 && lord->r_CurrentHealth == lord->r_MaxHealth;
            IntPtr depositAddress = IntPtr.Zero;
            bool depositReady = attempt.Deposit && lordReady &&
                history.TryFind(attempt.Session, out depositAddress, out attempt.Tile, out attempt.OriginalDeposit);
            if (!AttemptPolicy.CanApply(attempt.Deposit, lordReady, depositReady))
            {
                Reject(lordReady ? "no tracked stone or iron entry under a live wall" : "the local Lord is unavailable or lacks full health");
                return;
            }

            attempt.Lord = lordId;
            attempt.LordGlobal = lord->r_GlobalId;
            attempt.OriginalHealth = lord->r_CurrentHealth;
            if (attempt.Deposit) Marshal.WriteByte(depositAddress, 0);
            lord->r_CurrentHealth--;
            attempt.Applied = true;
            Info($"MAKE_DESYNC_APPLIED session={attempt.Session} mode={attempt.Mode} tick={tick} lord={lordId}/{attempt.LordGlobal} " +
                $"hp={attempt.OriginalHealth}->{lord->r_CurrentHealth} tile={attempt.Tile} deposit={attempt.OriginalDeposit}->0");
        }

        private void Reject(string reason)
        {
            current = null;
            Error("MAKE_DESYNC_REJECTED " + reason);
        }

        private void EndMission(MissionLifecycleNotification note)
        {
            lock (engineGate)
            {
                if (current?.Session == note.Context.SessionId) current = null;
                resyncStart = 0;
                firstTickLogged = false;
            }
        }

        private void RepairStarted()
        {
            if (!Monitor.IsEntered(engineGate) || missions.Current?.Mode.IsRealMultiplayer != true) return;
            if (current?.Applied == false) Reject("a repair started before the queued test could run");
            resyncStart = Stopwatch.GetTimestamp();
            Info($"MAKE_DESYNC_RESYNC_STARTED mode={current?.Mode ?? "observe"} tick={Tick()}");
        }

        private void RepairCompleted()
        {
            // The test observer follows the Deposit Restoration completion observer.
            if (!Monitor.IsEntered(engineGate) || resyncStart == 0) return;
            var attempt = current;
            current = null;
            long elapsed = (Stopwatch.GetTimestamp() - resyncStart) * 1000 / Stopwatch.Frequency;
            resyncStart = 0;
            string result = string.Empty;
            if (attempt?.Applied == true)
            {
                try
                {
                    uint? hp = UnitAccess.TryGetById(attempt.Lord, out GameUnit* lord, out _) &&
                        lord->r_GlobalId == attempt.LordGlobal ? lord->r_CurrentHealth : (uint?)null;
                    byte? deposit = attempt.Deposit ? history.Read(attempt.Session, attempt.Tile) : null;
                    result = $" lord={attempt.Lord}/{attempt.LordGlobal} hp={hp} expectedHp={attempt.OriginalHealth}" +
                        $" tile={attempt.Tile} deposit={deposit} restored={hp == attempt.OriginalHealth && (!attempt.Deposit || deposit == attempt.OriginalDeposit)}";
                }
                catch (Exception error) { Error("MAKE_DESYNC_REJECTED could not read repaired test state: " + error.Message); }
            }
            Info($"MAKE_DESYNC_RESYNC_COMPLETED mode={attempt?.Mode ?? "observe"} tick={Tick()} elapsedMs={elapsed}{result}");
        }

        private static string Tick()
        {
            try { return SimulationTickEvents.TryGetCurrentTick(out int tick) ? tick.ToString() : "unknown"; }
            catch { return "unknown"; } // Diagnostic failure must not fault the world.
        }

        private void Info(string message) => Write(LogLevel.Info, message);
        private void Error(string message) => Write(LogLevel.Error, message);

        private void Write(LogLevel level, string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (presentation == null) log.Log(level, line);
            else presentation.TryPostPresentation(() => log.Log(level, line));
        }
    }
}
