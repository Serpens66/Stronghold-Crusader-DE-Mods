using APIShared;

internal static partial class Program
{
    private static void TestGateTransitions()
    {
        for (int direction = 0; direction < 8; direction++)
        {
            byte forward = (byte)(1 << direction), reverse = (byte)(1 << (direction ^ 4));
            Check(AssassinGateTransitionPolicy.Classify(direction, forward, 0, forward, reverse, 0, 0x100, true, true, true) == AssassinTransitionKind.Ground,
                "forward connection remains ground even with a roof endpoint");
            Check(AssassinGateTransitionPolicy.Classify(direction, 0, reverse, forward, reverse, 0, 0x100, true, true, true) == AssassinTransitionKind.Ground,
                "reverse neighbor connection precedes the physical climb branch");
            var up = AssassinGateTransitionPolicy.Classify(direction, 0, 0, forward, reverse, 0, 0x100, true, true, true);
            Check(up == ((direction & 1) == 0 ? AssassinTransitionKind.ClimbUp : AssassinTransitionKind.Unknown), "cardinal climb only");
            Check(AssassinGateTransitionPolicy.Classify(direction, 0, 0, forward, reverse, 0x100, 0, true, true, true) ==
                ((direction & 1) == 0 ? AssassinTransitionKind.ClimbDown : AssassinTransitionKind.Unknown), "descent branch");
            Check(AssassinGateTransitionPolicy.Classify(direction, 0, 0, forward, reverse, 0, 0x100, false, true, true) == AssassinTransitionKind.Unknown, "blocked or unproven surface");
            Check(AssassinGateTransitionPolicy.Classify(direction, 0, 0, forward, reverse, 0, 0x100, true, false, true) == AssassinTransitionKind.Unknown, "player climb disabled");
            Check(AssassinGateTransitionPolicy.Classify(direction, 0, 0, forward, reverse, 0, 0x100, true, true, false) == AssassinTransitionKind.Unknown, "building exception alone is insufficient");
        }
        Check(!AssassinGateTransitionPolicy.Allows(false, AssassinTransitionKind.Ground, true), "masked ground remains blocked");
        Check(!AssassinGateTransitionPolicy.Allows(false, AssassinTransitionKind.Unknown, true), "unknown cannot authorize passage");
        Check(!AssassinGateTransitionPolicy.Allows(false, AssassinTransitionKind.ClimbUp, false), "stale or reused identity cannot authorize climbing");
        Check(AssassinGateTransitionPolicy.Allows(false, AssassinTransitionKind.ClimbUp, true), "verified ascent crosses ground mask");
        Check(AssassinGateTransitionPolicy.Allows(false, AssassinTransitionKind.ClimbDown, true), "verified descent crosses ground mask");
        Check(AssassinGateTransitionPolicy.Allows(true, AssassinTransitionKind.Unknown, false), "unmasked/own access preserves the existing result");

        // Target-first field 12(d=3),22(d=2),11(d=1). Validated parent edges
        // are 11->22->12, but E1640 can reconstruct the direct 11->12 ground cut.
        int[] folded = { 12, 22, 11 };
        var inspected = new HashSet<(int, int)>();
        Check(!AssassinGateTransitionPolicy.ValidateReconstructionField(folded, folded.Length, 10,
            (a,b) => { inspected.Add((a,b)); return (a,b) != (11,12); }), "unsafe shortcut rejected before publication");
        Check(inspected.Contains((11,12)), "candidate direction is start toward target, not the reverse reconstruction walk");
        Check(AssassinGateTransitionPolicy.ValidateReconstructionField(folded, folded.Length, 10,
            (a,b) => (a,b) != (11,12) || AssassinGateTransitionPolicy.Allows(false, AssassinTransitionKind.ClimbUp, true)), "same folded field permits a proven climb");
        Check(!AssassinGateTransitionPolicy.ValidateReconstructionField(new[]{12,22,12},3,10,(_,_)=>true), "duplicate tile cannot produce a valid field");
        Check(AssassinGateTransitionPolicy.ValidateReconstructionField(new[]{12},1,10,(_,_)=>false), "stationary route has no transitions");
        // Independent native stamp/distance oracle over many folded and straight fields.
        var random = new Random(7041);
        for (int sample = 0; sample < 300; sample++)
        {
            var nodes = Enumerable.Range(11, 68).OrderBy(_=>random.Next()).Take(12).ToArray();
            var actual = new HashSet<(int,int)>();
            AssassinGateTransitionPolicy.ValidateReconstructionField(nodes,nodes.Length,10,(a,b)=>{ actual.Add((a,b)); return true; });
            var expected = new HashSet<(int,int)>();
            for (int current = 0; current < nodes.Length-1; current++)
                for (int candidate = 0; candidate < nodes.Length; candidate++)
                {
                    int currentDistance = nodes.Length-current, candidateDistance = nodes.Length-candidate;
                    int dx = Math.Abs(nodes[current]%10-nodes[candidate]%10), dy = Math.Abs(nodes[current]/10-nodes[candidate]/10);
                    if (candidateDistance >= currentDistance-2 && candidateDistance < currentDistance && dx<=1 && dy<=1)
                        expected.Add((nodes[candidate],nodes[current]));
                }
            Check(expected.SetEquals(actual), "all native distance candidates inspected exactly");
        }
        Console.WriteLine("PASS: physical movement classification and reconstruction distance-field oracle.");
    }
}
