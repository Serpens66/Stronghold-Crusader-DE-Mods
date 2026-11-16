using System;
using System.Linq;
using System.Reflection;
using APIShared.Buildings;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace ConstructionCostsTest
{
    internal sealed partial class Fixture
    {
        // Observe actual accepted balance changes only inside the production authenticated
        // Chore reader. No native hook, memory write, manufactured frame or resource grant.
        private sealed class GoldPayoutWitness : IDisposable
        {
            private readonly IDisposable subscription;
            private readonly MethodInfo capture, matches;
            private readonly FieldInfo sender;
            private readonly byte opcode;
            private readonly int actor;
            private readonly Func<bool> objectMatches;
            private readonly bool subtract;
            private object pending;
            private int before;
            internal int PreCount { get; private set; }
            internal int PostCount { get; private set; }
            internal int ActualDelta { get; private set; }
            internal string Failure { get; private set; }
            internal GoldPayoutWitness(byte opcode, int actor, Func<bool> objectMatches, bool subtract = false)
            {
                this.opcode = opcode; this.actor = actor; this.objectMatches = objectMatches; this.subtract = subtract;
                Type context = typeof(HumanConstructionCosts).Assembly.GetType("APIShared.Networking.NativeChoreContext", true);
                capture = context.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(method =>
                    method.Name == "TryCaptureRaw" && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == typeof(byte));
                Type stamp = capture.GetParameters()[1].ParameterType.GetElementType();
                sender = stamp.GetField("Sender", BindingFlags.Instance | BindingFlags.NonPublic);
                matches = stamp.GetMethod("Matches", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(sender != null && sender.FieldType == typeof(int) && matches != null, "Production Chore identity reader contract changed");
                subscription = subtract
                    ? PlayerR3EventHooks.OnPlayerSubtractResource.Observable.Subscribe(args => Observe(args.Phase, args.PlayerId, args.Good, true))
                    : PlayerR3EventHooks.OnPlayerAddResource.Observable.Subscribe(args => Observe(args.Phase, args.PlayerId, args.Good, args.ReturnValue));
            }
            private void Observe(EventHookPhase phase, int player, eGoods good, bool accepted)
            {
                try
                {
                    if (player != actor || good != eGoods.STORED_GOLD || !objectMatches()) return;
                    object[] values = { opcode, null };
                    if (!(bool)capture.Invoke(null, values) || (int)sender.GetValue(values[1]) != actor) return;
                    if (phase == EventHookPhase.Pre)
                    {
                        if (pending != null) { Failure = "Nested matching Gold transaction"; return; }
                        PreCount++; pending = values[1];
                        before = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD);
                    }
                    else if (pending != null && (bool)matches.Invoke(pending, new[] { values[1] }))
                    {
                        PostCount++;
                        int delta = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD) - before;
                        ActualDelta += subtract ? -delta : delta;
                        if (!accepted) Failure = "Native resource addition was rejected";
                        pending = null;
                    }
                }
                catch (Exception error) { Failure = error.ToString(); }
            }
            public void Dispose() => subscription.Dispose();
        }

        private static unsafe bool BrazierMarked(int slot, uint generation, int player)
        {
            var span = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            return slot >= 25 && slot < span.Length && span[slot].r_GlobalId == generation &&
                span[slot].r_ProjectileType == ProjectileType.Brazier && (ushort)span[slot].r_PlayerSourceId == player &&
                span[slot].r_AliveState == AliveState.MarkedForDeletion;
        }
    }
}
