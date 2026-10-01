using System;
using System.Runtime.InteropServices;
using BugfixesAndQoL;
using static BugfixesAndQoL.RaidAttackFieldEvaluation;

internal static unsafe class Program
{
    private static int checkedCases;
    private static int BuildingAt(int tile) => tile == 101 ? 119 : 120;
    private static bool Cardinal(int stand, int building) => stand == 100 && building == 101;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        checkedCases++;
    }

    private static void Expect(AttackCandidateSnapshot snapshot, AttackResult expected,
        string reason, string freshness = "nativeSearchObserved", bool matched = true,
        bool identity = true, long commandReturn = 1)
    {
        AttackResult actual = Evaluate(snapshot, matched, freshness, commandReturn,
            identity, 119, 1000, BuildingAt, Cardinal, out string validation);
        Check(actual == expected && validation == reason,
            $"Expected {expected}/{reason}; got {actual}/{validation}");
    }

    private static int Main()
    {
        string game = Environment.GetEnvironmentVariable("RAID_TEST_GAME_DIR");
        AppDomain.CurrentDomain.AssemblyResolve += (sender,args) => {
            string name = new System.Reflection.AssemblyName(args.Name).Name + ".dll";
            foreach (string directory in new[] {
                System.IO.Path.Combine(game,"BepInEx","plugins","000shcdese"),
                System.IO.Path.Combine(game,"BepInEx","core"),
                System.IO.Path.Combine(game,"Stronghold Crusader Definitive Edition_Data","Managed") }) {
                string file=System.IO.Path.Combine(directory,name);
                if(System.IO.File.Exists(file)) return System.Reflection.Assembly.LoadFrom(file);
            }
            return null;
        };
        IntPtr memory = Marshal.AllocHGlobal((Capacity + 1) * RecordSize);
        try
        {
            int* words = (int*)memory;
            for (int i = 0; i < (Capacity + 1) * 3; i++) words[i] = 0;
            words[1] = 101;
            words[2] = 77;
            words[3] = 100;
            words[4] = 101;
            var empty = Read(memory, 0);
            Check(empty.Complete && empty.Records.Length == 0 && empty.TerminatorAt == 0,
                "A null approach terminates despite stale building field and later pair");
            Expect(empty, AttackResult.NoAttackPoint, "emptyList");
            words[1] = 500;
            words[2] = -42;
            words[3] = 300;
            Check(empty.SameRecords(Read(memory, 0)), "Ignored tail does not change freshness");
            Expect(empty, AttackResult.Unknown, "notEvaluatedWithoutFreshness", "searchCompletionNotObserved");
            Expect(empty, AttackResult.Unknown, "notEvaluatedWithoutFreshness", "missingPre", false);
            Expect(empty, AttackResult.Unknown, "notEvaluatedWithoutFreshness", "unmatchedPrePost", false);
            Expect(Read(IntPtr.Zero, 0), AttackResult.Unknown, "contextUnavailable");

            words[0] = 100; words[1] = 0; words[2] = 10;
            words[3] = 100; words[4] = 101; words[5] = 11;
            words[6] = 0; words[7] = 101; words[8] = 123;
            var gated = Read(memory, 0);
            Check(gated.Records.Length == 2 && gated.TerminatorAt == 2, "Correct prefix length");
            Expect(gated, AttackResult.NoAttackPoint, "nativeFirstBuildingGateFailed");
            words[1] = 101;
            var valid = Read(memory, 0);
            Expect(valid, AttackResult.AttackPoint, "validFirstMeleePair");
            Expect(valid, AttackResult.Unknown, "targetIdentityChanged", identity: false);
            Expect(valid, AttackResult.Unknown, "commandReturnNotSuccess", commandReturn: 0);
            Expect(valid, AttackResult.Unknown, "notEvaluatedWithoutFreshness", "searchCompletionNotObserved");
            words[1] = 102;
            Expect(Read(memory, 0), AttackResult.Unknown, "firstPairWrongBuilding");
            words[1] = -1;
            Expect(Read(memory, 0), AttackResult.Unknown, "buildingTileOutOfRange");
            words[1] = 1000;
            Expect(Read(memory, 0), AttackResult.Unknown, "buildingTileOutOfRange");
            words[1] = 101; words[0] = -1;
            Expect(Read(memory, 0), AttackResult.Unknown, "approachTileOutOfRange");
            words[0] = 1000;
            Expect(Read(memory, 0), AttackResult.Unknown, "approachTileOutOfRange");
            words[0] = 100; words[2] = UnreachableScore;
            Expect(Read(memory, 0), AttackResult.Unknown, "firstScoreInvalid");
            words[0] = 99; words[2] = 10;
            Expect(Read(memory, 0), AttackResult.Unknown, "firstPairNotCardinalOrTileMappingInvalid");

            for (int i = 0; i < Capacity; i++)
            {
                words[i * 3] = 100;
                words[i * 3 + 1] = 101;
                words[i * 3 + 2] = 10;
            }
            var full = Read(memory, 0);
            Check(!full.Complete && full.Records.Length == 500, "Bounded 500-record read");
            Expect(full, AttackResult.Unknown, "listEndNotFoundInFirst500");
            words[499 * 3] = 0;
            Check(Read(memory, 0).TerminatorAt == 499, "Last permitted terminator");
            Expect(Read(memory, 0), AttackResult.AttackPoint, "validFirstMeleePair");

            // Eight players with all six roles, retaining stable identities and a
            // detailed U token, exercise the longest common compact retry format.
            long bytes = 0;
            string detail = "U{freshness=searchCompletionNotObserved,prePostMatch=True," +
                "validation=notEvaluatedWithoutFreshness,return=1," + empty.DescribeCompact() + "," +
                new RaidSearchEvidence(1,2,3,4,4390,1375870,808,1373353,new IntPtr(123)).Describe(true,empty) +
                ",result=Unknown}";
            for (int player = 1; player <= 8; player++)
                for (int role = 0; role < 6; role++)
                    for (int cycle = 0; cycle < 40; cycle++)
                    {
                        string line = $"RAID_FIX_RETRY: session=1, player={player}, role={role}, " +
                            $"tribe={4000 + player * 6 + role}/{60000 + cycle}, " +
                            $"original=119/6450, attempts=[120/7000/11:N;121/7001/12:{detail}], " +
                            "selected=0/0, storedTargetAtAbort=121/7001, outcome=uncertain;vanillaPreserved." +
                            Environment.NewLine;
                        Check(line.Contains($"player={player}, role={role}, tribe="), "Retry identity retained");
                        bytes += System.Text.Encoding.UTF8.GetByteCount(line);
                    }
            Check(bytes < 5000000, "Compact U details bounded in representative 1920-retry stream");
            Console.WriteLine($"PASS: {checkedCases} assertions; 1920 detailed retries={bytes} bytes.");
            NativeSearchTests.Run();
            ActivationTests.Run();
            return 0;
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
}
