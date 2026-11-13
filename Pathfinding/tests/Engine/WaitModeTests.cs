using System;
using System.Collections.Generic;
using APIShared.Pathfinding;

namespace EngineHarness
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        // Production wait/restore methods, native route probes and the same one-row terrain as
        // AnchorIntegrationTests. No duplicate wait algorithm and no game/installation required.
        private void WaitModeTests(ref int ticks)
        {
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            GameTribeManagerAPI tribes = GameTribeManagerAPI.Instance;
            GameUnit* walker = units.Units + 7;
            GameUnit saved = *walker;
            bool single = settings.AnchorSingleMoves, fireForever = settings.FireWaitIndefinitely, plagueForever = settings.PlagueWaitIndefinitely;
            RouteReplanPolicy oldFire = fireAbortPolicy, oldPlague = plagueAbortPolicy;
            GameTribe* table = InstallTribeTable(8);
            TribeOrder plain = tribeOrders[3];
            int now = ticks, assertions = 0;
            void Assert(bool value, string message) { assertions++; Check(value, "wait modes: " + message); }
            Assert(RouteTileRules.TryRegister("tests.pathfinding", "wait-modes", out RouteTileRuleSet rules, out _), "rule registration");
            RouteReplanPolicy policy = null;
            void Step(int advance)
            {
                now += advance;
                ProcessReplanWaits(now);
            }
            void Begin(bool plague, bool unlimited)
            {
                replanWaits.Clear(); replanSettled.Clear(); tribeOrders[3] = plain;
                table[3].r_GlobalId = plain.Global; table[3].r_PlayerIdOwner = 1;
                walker->r_GlobalId = 9007; walker->r_ControllableForPlayerId = 1; walker->r_TribeId = 3;
                walker->r_AliveState = AliveState.IsAlive; walker->r_IsKilledByProjectile = 0;
                walker->r_SelectionAllowed = 1; walker->r_UnitChimp = 0;
                Route(7, 140, 10, new int[0], 140, 10);
                policy = new RouteReplanPolicy(3, 50, 10, 8);
                if (plague) { plagueAbortPolicy = policy; settings.PlagueWaitIndefinitely = unlimited; settings.FireWaitIndefinitely = !unlimited; }
                else { fireAbortPolicy = policy; settings.FireWaitIndefinitely = unlimited; settings.PlagueWaitIndefinitely = !unlimited; }
                rules.ReplanPolicy = policy; rules.Set(160, 10, RouteTileEffect.Penalty, 254); NoteReplanTile(160, 10);
                StartReplanWait(7, walker, 1, policy, 20, 160, 10, 140, 10, now);
                units.Moves.Clear(); tribes.Moves.Clear();
            }
            try
            {
                settings.AnchorSingleMoves = true;
                units.MoveHandler = (id, x, y) => Route(id, x, y, new int[0], x, y);
                foreach (bool plague in new[] { false, true })
                {
                    Begin(plague, false);
                    long aborted = replanAborted;
                    Step(16);
                    Assert(!replanWaits.ContainsKey(7) && replanAborted == aborted + 1 && tribes.Moves.Count == 0,
                        "bounded hazard wait aborts with no command through danger; other hazard's unlimited flag is irrelevant");

                    Begin(plague, true);
                    int until = replanWaits[7].Until;
                    aborted = replanAborted;
                    Step(48); Step(1000);
                    Assert(replanWaits.ContainsKey(7) && replanAborted == aborted && tribes.Moves.Count == 0,
                        "unlimited hazard wait survives its saved deadline while route remains dangerous");
                    Assert(policy.MaximumWaitTicks == 8 && replanWaits[7].Until == until,
                        "unlimited route waiting never changes APIShared or escape move-resume deadlines");

                    // Real save encoder/decoder: mission settings choose the mode; v1 wire fields stay intact.
                    var record = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                    CaptureMovementCases((name, data) => record.Add(name, data));
                    ResetReplanWatch();
                    NoteReplanTile(160, 10);
                    StageMovementCases(record); ApplyLoadedMovementCases();
                    Assert(replanWaits.TryGetValue(7, out ReplanWait restored) && restored.Until == until && IsIndefiniteHazardWait(restored.Policy),
                        "unlimited wait survives v1 capture/restore without changing its deadline");
                    Step(48);
                    Assert(replanWaits.ContainsKey(7), "restored unlimited wait still rejects a dangerous route");

                    // No rule tile and ready core: even an elapsed unlimited wait resumes immediately at its next probe.
                    rules.ClearAll(); NoteReplanTile(160, 10); AnchorTick(++now);
                    Step(48);
                    Assert(!replanWaits.ContainsKey(7) && tribes.Moves.Count == 1 && walker->r_TargetTilePositionX2 == 160,
                        "unlimited waiting resumes once a safe route exists");

                    Begin(plague, true);
                    replanWaits[7].NextProbe = now;
                    replanProbeTick = now; replanProbeLeft = 0;
                    long deferred = replanProbesDeferred;
                    ProcessReplanWaits(now);
                    Assert(replanWaits.ContainsKey(7) && replanProbesDeferred == deferred + 1,
                        "unlimited waiting obeys the shared probe budget");

                    Begin(plague, true);
                    ObserveTribeOrder(3, true, 170, 10, 0, true, TribeMoveType.DefaultInSync);
                    Step(1);
                    Assert(!replanWaits.ContainsKey(7) && tribes.Moves.Count == 0,
                        "a newer observed order cancels immediately even while unit still has old stopped fields");

                    Begin(plague, true); walker->r_TribeId = 0; Step(1);
                    Assert(!replanWaits.ContainsKey(7) && tribes.Moves.Count == 0, "regrouping cancels the old wait");
                    Begin(plague, true); walker->r_ControllableForPlayerId = 2; Step(1);
                    Assert(!replanWaits.ContainsKey(7) && tribes.Moves.Count == 0, "ownership transfer cancels the old wait");
                    Begin(plague, true); table[3].r_GlobalId++; Step(1);
                    Assert(!replanWaits.ContainsKey(7) && tribes.Moves.Count == 0, "tribe slot reuse cancels the old wait");
                    Begin(plague, true); walker->r_GlobalId++; Step(1);
                    Assert(!replanWaits.ContainsKey(7) && tribes.Moves.Count == 0, "unit slot reuse cancels the old wait");
                    Begin(plague, true); walker->r_AliveState = AliveState.Dead; Step(1);
                    Assert(!replanWaits.ContainsKey(7) && tribes.Moves.Count == 0, "death cancels the old wait");
                    Begin(plague, true); ResetReplanWatch();
                    Assert(replanWaits.Count == 0, "mission reset clears unlimited waits");
                }
                // Other owners retain the APIShared contract, even while both mod-local flags are enabled.
                settings.FireWaitIndefinitely = settings.PlagueWaitIndefinitely = true;
                Assert(!IsIndefiniteHazardWait(new RouteReplanPolicy(3, 50, 10, 0)),
                    "foreign MaximumWaitTicks=0 still means no stop, not unlimited waiting");
                Console.WriteLine("PASS: " + assertions + " production bounded/unlimited wait, cancellation and v1 restore checks.");
            }
            finally
            {
                rules.ClearAll(); rules.ReplanPolicy = null; NoteReplanTile(160, 10); AnchorTick(++now);
                ResetReplanWatch(); loadedCases = null;
                settings.AnchorSingleMoves = single; settings.FireWaitIndefinitely = fireForever; settings.PlagueWaitIndefinitely = plagueForever;
                fireAbortPolicy = oldFire; plagueAbortPolicy = oldPlague;
                units.MoveHandler = null; units.Moves.Clear();
                RemoveTribeTable(table); *walker = saved; ticks = now;
            }
        }
    }
}
