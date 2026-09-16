using System;
using System.Collections.Generic;
using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace OutpostTest
{
    // Passive comparison run: no native hooks, allocation, assignment or orders.
    internal sealed unsafe class VanillaObserver
    {
        private readonly ManualLogSource log;
        private readonly IDisposable subscription;
        private readonly List<Watch> watches = new List<Watch>();
        private bool active, failed, confirmed, capped;
        private int lastTick = int.MinValue, captured, ended;
        private sealed class Watch
        {
            internal int Unit, Building, Born, LastLog, Lines, ExpectedTribe;
            internal uint Global, BuildingGlobal, ExpectedTribeGlobal;
            internal string Signature, Source = "exit-candidate";
        }
        internal VanillaObserver(ManualLogSource log)
        {
            this.log = log;
            OutpostNative.ValidateLayouts();
            subscription = UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(Created);
        }
        internal void Begin(MissionLifecycleNotification n)
        {
            Disable("new mission");
            var m = n.Context.Mode;
            active = !failed && !m.IsRealMultiplayer && !m.IsMapEditor &&
                !m.HasConflictingCustomizedOrigin && m.Kind != Shared.GameModeKind.Unknown && m.Kind != Shared.GameModeKind.Tutorial;
            Info($"vanilla-session={n.Context.SessionId} save={n.Context.IsSave} observing={active} customSpawn=False suppression=False; {m.ToDiagnosticString()}");
        }
        internal void End(MissionLifecycleNotification n) => Disable("mission ended");
        internal void Disable(string reason)
        {
            active = false;
            Info($"vanilla-stop reason={reason} captured={captured} ended={ended} remaining={watches.Count} invariant={captured == ended + watches.Count}");
            watches.Clear(); captured = ended = 0; lastTick = int.MinValue; confirmed = capped = false;
        }
        private void Created(UnitCreateEventArgs args)
        {
            if (!active || args.Phase != EventHookPhase.Post) return;
            try
            {
                int tick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
                var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length && spanIndex < 3999; spanIndex++)
                {
                    fixed (GameBuilding* b = &buildings[spanIndex])
                    {
                        if (b->r_AliveState != AliveState.IsAlive || !OutpostSchedule.IsOutpost((int)b->r_BuildingType) ||
                            b->r_PlayerIdOwner != args.PlayerOwnerId || args.WorldTileX != b->r_TilePositionXEnd * 8 ||
                            args.WorldTileY != b->r_TilePositionYEnd * 8) continue;
                        if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                        { Info($"vanilla-create-failed tick={tick} candidateBuilding={spanIndex+1}/{b->r_GlobalId} return={args.ReturnValue}"); continue; }
                        if (watches.Count >= 2048)
                        { if (!capped) { capped = true; Info("vanilla-cap active=2048; further candidates omitted until space is available"); } return; }
                        int unitId = (int)args.ReturnValue;
                        if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out var u))
                            throw new InvalidOperationException("Create post returned an unresolved unit ID.");
                        var w = new Watch { Unit = unitId, Global = u->r_GlobalId, Building = spanIndex+1,
                            BuildingGlobal = b->r_GlobalId, Born = tick, LastLog = tick };
                        w.ExpectedTribe = *(short*)((byte*)b+0x302);
                        w.ExpectedTribeGlobal = *(uint*)((byte*)b+0x304);
                        watches.Add(w); captured++;
                        Info($"vanilla-create tick={tick} unit={unitId}/{w.Global} candidateBuilding={w.Building}/{w.BuildingGlobal} buildingType={(int)b->r_BuildingType} owner={args.PlayerOwnerId} requestedType={(int)args.UnitType} phase=post-before-outpost-assignment");
                        Snapshot(w, u, tick, "create-post");
                        if (!confirmed) { confirmed = true; Info("vanilla-callback confirmed; exit match is a candidate, not proof of caller identity"); }
                    }
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        internal void Tick(int tick)
        {
            if (!active || tick == lastTick) return;
            lastTick = tick;
            try
            {
                for (int i = watches.Count-1; i >= 0; i--)
                {
                    var w = watches[i];
                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(w.Unit, out var u) || u->r_GlobalId != w.Global)
                    { Finish(i, tick, "identity-retired"); continue; }
                    string previousSource = w.Source;
                    if (GameBuildingManagerAPI.Instance.TryGetBuildingById(w.Building, out var b) && b->r_GlobalId == w.BuildingGlobal)
                    {
                        byte* bytes = (byte*)b;
                        for (int slot = 0; slot < 8; slot++)
                        {
                            int idOffset = slot < 4 ? 0x19E + slot*2 : 0x2E0 + (slot-4)*2;
                            int globalOffset = slot < 4 ? 0x1A8 + slot*4 : 0x2E8 + (slot-4)*4;
                            if (*(short*)(bytes+idOffset) == w.Unit && *(uint*)(bytes+globalOffset) == w.Global)
                                w.Source = "guard-identity";
                        }
                        int tribeId = *(short*)(bytes+0x302);
                        if (tribeId > 0 && u->r_TribeId == tribeId &&
                            GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out var t) && t->r_GlobalId == *(uint*)(bytes+0x304))
                            w.Source = "production-tribe-identity";
                    }
                    if (w.ExpectedTribe > 0 && u->r_TribeId == w.ExpectedTribe &&
                        GameTribeManagerAPI.Instance.TryGetTribeById(w.ExpectedTribe, out var expected) && expected->r_GlobalId == w.ExpectedTribeGlobal)
                        w.Source = "production-tribe-identity";
                    string signature = Signature(u);
                    int age = unchecked(tick-w.Born);
                    if (signature != w.Signature || previousSource != w.Source || unchecked(tick-w.LastLog) >= 40 || age <= 2)
                        Snapshot(w, u, tick, signature != w.Signature ? "transition" : "sample");
                    if (age >= 2400 || w.Lines >= 400) Finish(i, tick, "observation-limit");
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static string Signature(GameUnit* u) => $"type={(int)u->r_UnitChimp},alive={(int)u->r_AliveState},state={u->r_AIState},tribe={u->r_TribeId},health={u->r_CurrentHealth}/{u->r_MaxHealth},role={(int)u->r_AITribeRole}";
        private void Snapshot(Watch w, GameUnit* u, int tick, string phase)
        {
            string signature = Signature(u), tribe = "none";
            if (u->r_TribeId > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(u->r_TribeId, out var t))
                tribe = $"{u->r_TribeId}/{t->r_GlobalId},alive={(int)t->r_AliveState},members={t->r_UnitsInGroup},stance={t->r_TribeStance}";
            Info($"vanilla-state tick={tick} age={unchecked(tick-w.Born)} phase={phase} unit={w.Unit}/{w.Global} building={w.Building}/{w.BuildingGlobal} source={w.Source} previous=[{w.Signature ?? "none"}] current=[{signature}] world={u->r_CurrentWorldPositionX},{u->r_CurrentWorldPositionY} group=[{tribe}]");
            w.Signature = signature; w.LastLog = tick; w.Lines++;
        }
        private void Finish(int index, int tick, string reason)
        {
            var w = watches[index];
            Info($"vanilla-end tick={tick} unit={w.Unit}/{w.Global} age={unchecked(tick-w.Born)} reason={reason} lines={w.Lines} source={w.Source}");
            watches.RemoveAt(index); ended++;
        }
        private void Fail(Exception ex)
        {
            failed = true; Disable("observer failure; Vanilla unchanged");
            Shared.DebugLogHelper.LogError(log, "OutpostTest observer error: " + ex);
        }
        private void Info(string text) => Shared.DebugLogHelper.LogInfo(log, "OutpostTest " + text);
    }
}
