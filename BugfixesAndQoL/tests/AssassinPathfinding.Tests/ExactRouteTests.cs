using APIShared;
using BugfixesAndQoL;

internal static partial class Program
{
    private static void TestExactRouteHandoff()
    {
        // T(1,1,d4), R(2,1,d3), P(2,2,d2), S(1,2,d1).
        // E1640 chooses R at direction 2, then S at direction 4 using the
        // already reduced distance 3. S->T is a climb, unlike S->P->R->T.
        int[] nodes = { 11, 12, 22, 21 };
        int distance = 4, chosen = -1;
        foreach (var candidate in new[] { (Node: 12, Distance: 3), (Node: 21, Distance: 1) })
            if (candidate.Distance >= distance - 2 && candidate.Distance < distance)
            { chosen = candidate.Node; distance = candidate.Distance; }
        Check(chosen == 21, "native mutable-distance reconstruction selects the expensive climb shortcut");
        int walking = 3 * AssassinClimbCostPolicy.GetCardinalMovementTicks(2);
        int climbing = AssassinClimbCostPolicy.GetCardinalMovementTicks(2) +
            AssassinClimbCostPolicy.GetAdditionalTicks(true, 90, false, true, false);
        Check(walking < climbing, "regular gate roof entrance wins by estimated travel time");
        byte[] encoded = AssassinRouteEncoding.EncodeTargetFirst(nodes, 10);
        Check(encoded.Length == 2 && encoded[0] == 0x02 && encoded[1] == 6,
            "exact forward route encodes east,north,west low nibble first");
        Check(AssassinRouteEncoding.EncodeTargetFirst(new[] { 10, 9 }, 10) == null,
            "row wrapping is not an adjacent route");
        Check(AssassinRouteEncoding.EncodeTargetFirst(new[] { 11, 11 }, 10) == null,
            "duplicate direction rejected");
        Check(AssassinRouteEncoding.EncodeTargetFirst(new int[2002], 10) == null,
            "route buffer capacity enforced");
        int published = 0;
        bool identity = true, permitted = true;
        IntPtr context = new IntPtr(123);
        var frame = new AssassinRouteHandoff(context, 1, 2, 1, 1, 2, () => identity,
            (bytes, count) => { Check(bytes.SequenceEqual(encoded), "publisher receives exact entrance bytes"); published++; return count; });
        try
        {
            Check(!AssassinRouteHandoff.Stage(context, 1, 2, 1, 1, 3, encoded, 3, () => true), "wrong player rejected");
            Check(!AssassinRouteHandoff.Stage(context, 1, 2, 2, 1, 2, encoded, 3, () => true), "wrong target rejected");
            Check(!AssassinRouteHandoff.Stage(new IntPtr(124), 1, 2, 1, 1, 2, encoded, 3, () => true), "wrong manager rejected");
            Check(!AssassinRouteHandoff.Stage(context, 1, 2, 1, 1, 2, new byte[] { 15 }, 1, () => true), "invalid packed direction rejected");
            Check(AssassinRouteHandoff.Stage(context, 1, 2, 1, 1, 2, encoded, 3, () => permitted), "prepared entrance route staged");
            var nested = new AssassinRouteHandoff(context, 0, 0, 0, 0, 0, null, null);
            try
            {
                Check(!AssassinRouteHandoff.Stage(context, 1, 2, 1, 1, 2, encoded, 3, () => true), "unqualified nested builder shadows outer route");
                Check(frame.Complete(8) == 8 && published == 0, "outer route cannot publish within nested frame");
            }
            finally { nested.Leave(); }
            permitted = false;
            Check(frame.Complete(8) == 8 && published == 0, "changed gate or climb policy retains native result");
            permitted = true; identity = false;
            Check(frame.Complete(8) == 8 && published == 0, "changed unit or buffer retains native result");
            identity = true;
            Check(frame.Complete(0) == 3 && published == 1, "exact route replaces reconstruction failure before unit consumption");
        }
        finally { frame.Leave(); }
        Check(!AssassinRouteHandoff.Stage(context, 1, 2, 1, 1, 2, encoded, 3, () => true), "route cannot leak beyond builder lifetime");
        for (int d = 0; d < 8; d++)
        {
            byte reverse = (byte)(1 << (d ^ 4));
            Check(AssassinGateTransitionPolicy.HasOrdinaryConnection(0, reverse, (byte)(1 << d), reverse),
                "reverse-only connection is ordinary with climbing disabled");
        }
    }
}
