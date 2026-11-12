using System;
using System.Collections.Generic;
using APIShared;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace PathfindingLoadTest
{
    internal sealed partial class Fixture
    {
        private const int MaximumDeathDetails = 8;
        private static readonly object deathSync = new object();
        private static IDisposable deathSubscription;
        private static Fixture deathFixture;
        private readonly Dictionary<int, CreatedHealth> createdHealth = new Dictionary<int, CreatedHealth>();
        private readonly List<string> deletionDetails = new List<string>(MaximumDeathDetails);
        private int observedDeletes;
        private struct CreatedHealth { internal uint Global, Health, Maximum; internal string State; }

        private void EnableDeathObservation()
        {
            lock (deathSync)
            {
                // The published subscription survives Unity's startup cleanup. The callback
                // has no component/mission publisher and idles without a logically active run.
                if (deathSubscription == null)
                    deathSubscription = UnitR3EventHooks.OnUnitDelete.Observable.Subscribe(OnUnitDelete);
                deathFixture = this;
            }
        }

        private unsafe void TrackCreated(Identity identity)
        {
            Require(UnitAccess.TryGetById(identity.Id, out GameUnit* unit, out _) && unit->r_GlobalId == identity.Global,
                "Cannot snapshot created unit health");
            var health = new CreatedHealth { Global = identity.Global, Health = unit->r_CurrentHealth,
                Maximum = unit->r_MaxHealth, State = unit->r_AliveState.ToString() };
            lock (deathSync) createdHealth.Add(identity.Id, health);
        }

        private void LogInitialHealth()
        {
            uint min = uint.MaxValue, max = 0, minimumMaximum = uint.MaxValue, maximumMaximum = 0;
            int zero = 0, tracked;
            lock (deathSync)
            {
                tracked = createdHealth.Count;
                foreach (CreatedHealth health in createdHealth.Values)
                {
                    min = Math.Min(min, health.Health); max = Math.Max(max, health.Health);
                    minimumMaximum = Math.Min(minimumMaximum, health.Maximum); maximumMaximum = Math.Max(maximumMaximum, health.Maximum);
                    if (health.Health == 0 || health.Maximum == 0) zero++;
                }
            }
            log("initial-health count=" + tracked + " healthMin=" + min + " healthMax=" + max +
                " maxHealthMin=" + minimumMaximum + " maxHealthMax=" + maximumMaximum + " zeroHealth=" + zero);
        }

        private static unsafe void OnUnitDelete(UnitDeleteEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || args.UnitId == 0 || args.UnitId > int.MaxValue) return;
            lock (deathSync)
            {
                Fixture fixture = deathFixture;
                if (fixture == null || !fixture.createdHealth.TryGetValue((int)args.UnitId, out CreatedHealth created)) return;
                fixture.observedDeletes++;
                if (fixture.deletionDetails.Count >= MaximumDeathDetails) return;
                string actual;
                if (UnitAccess.TryGetById((int)args.UnitId, out GameUnit* unit, out var failure))
                    actual = "actualGlobal=" + unit->r_GlobalId + " owner=" + unit->r_ControllableForPlayerId +
                        " type=" + unit->r_UnitChimp + " alive=" + unit->r_AliveState + " killed=" + unit->r_IsKilledByProjectile +
                        " health=" + unit->r_CurrentHealth + " maxHealth=" + unit->r_MaxHealth +
                        " ai=" + unit->r_AIState + " resumeAi=" + unit->r_ResumeAIState +
                        " deathTicker=" + unit->r_TimeSinceDeathTicker + " tribe=" + unit->r_TribeId +
                        " pos=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY +
                        " target=" + unit->r_TargetTilePositionX + "," + unit->r_TargetTilePositionY;
                else actual = "lookup=" + failure + " actual=unavailable";
                // Snapshot only: never mutate/veto the event, retain a pointer, or log from Sim.
                fixture.deletionDetails.Add("delete-pre id=" + args.UnitId + " expectedGlobal=" + created.Global +
                    " initialHealth=" + created.Health + " initialMaxHealth=" + created.Maximum +
                    " initialState=" + created.State + " " + actual + " cause=unknown");
            }
        }

        internal void Complete()
        {
            string[] details;
            int seen;
            lock (deathSync)
            {
                if (ReferenceEquals(deathFixture, this)) deathFixture = null;
                details = deletionDetails.ToArray(); seen = observedDeletes;
            }
            // Automation completion executes on Unity for success/failure/cancellation alike.
            foreach (string detail in details) log(detail);
            if (seen != 0) log("deletion-observer matched=" + seen + " details=" + details.Length + " cause=unknown");
        }
    }
}
