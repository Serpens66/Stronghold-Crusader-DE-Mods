namespace SpectatorEditorBuildTest
{
    public static class ActionPolicyTests
    {
        private static int checks;
        private static void Expect(bool condition, string name)
        { checks++; if (!condition) throw new System.Exception("Failed: " + name); }
        public static int Run()
        {
            Expect(ToolPolicy.PowerContextMatches(5, 5, 2, 2, 10, 10, true), "toolbar retains actor and session");
            Expect(!ToolPolicy.PowerContextMatches(5, 6, 2, 2, 10, 10, true), "toolbar request rejected after session change");
            Expect(!ToolPolicy.PowerContextMatches(5, 5, 2, 3, 10, 10, true), "toolbar request rejected after AI change");
            Expect(!ToolPolicy.PowerContextMatches(5, 5, 2, 2, 10, 11, true), "toolbar request rejected after cancel/view change");
            Expect(!ToolPolicy.PowerContextMatches(5, 5, 2, 2, 10, 10, false), "toolbar request rejected after disable");
            int amount;
            foreach (string invalid in new[] { null, "", "0", "-1", "+1", "1.5", "1,000", " 1", "2147483648" })
                Expect(!CreditMath.TryParse(invalid, out amount), "reject invalid amount " + invalid);
            Expect(CreditMath.TryParse("1", out amount) && amount == 1, "smallest credit");
            Expect(CreditMath.TryParse("2147483647", out amount) && amount == int.MaxValue, "largest credit");
            Expect(CreditMath.Fits(0, int.MaxValue), "empty reserve maximum");
            Expect(CreditMath.Fits(int.MaxValue - 1, 1), "exact capacity");
            Expect(!CreditMath.Fits(int.MaxValue, 1), "full reserve");
            Expect(!CreditMath.Fits(-1, 1), "corrupt negative reserve");
            Expect(!CreditMath.Fits(0, 0), "zero rejected");
            Expect(CreditInput.IsEditable(""), "empty editing state");
            Expect(CreditInput.IsEditable("0"), "zero editing state");
            Expect(CreditInput.IsEditable("2147483647"), "maximum input");
            foreach (string invalid in new[] { null, "abc", "100abc", "+1", "-1", "1.0", "1,000", " 100", "100\n", "١٢٣", "１２３", "2147483648", "100000000000" })
                Expect(!CreditInput.IsEditable(invalid), "reject full invalid edit or paste " + invalid);
            string candidate;
            Expect(CreditInput.TryReplace("0", 0, 1, "4120", out candidate) && candidate == "4120", "paste replaces selection");
            Expect(CreditInput.TryReplace("120", 1, 1, "99", out candidate) && candidate == "1990", "edit middle selection");
            Expect(CreditInput.TryReplace("4120", 0, 4, "", out candidate) && candidate == "", "delete entire number");
            Expect(!CreditInput.TryReplace("4120", 0, 4, "100abc", out candidate) && candidate == "4120", "invalid paste retains old value");
            Expect(!CreditInput.TryReplace("12", 1, 0, "x", out candidate) && candidate == "12", "invalid typed insertion");
            Expect(!CreditInput.TryReplace("2147483647", 10, 0, "0", out candidate), "overflow typing rejected");
            Expect(!CreditInput.TryReplace("1", 2, 0, "2", out candidate), "invalid selection rejected");
            Expect(CreditInput.Normalize("") == "0", "empty on blur becomes zero");
            Expect(CreditInput.Normalize("00041") == "41", "leading zeros normalized on blur");
            Expect(CreditInput.Normalize("2147483647") == "2147483647", "maximum remains intact");
            var ledger = new TestActionLedger();
            ledger.Reset(10);
            int balance = 0;
            if (ledger.TryBegin(10, 1)) balance += 100;
            if (ledger.TryBegin(10, 1)) balance += 100;
            Expect(balance == 100, "credit exactly once");
            Expect(!ledger.TryBegin(9, 2), "old session rejected");
            Expect(ledger.TryBegin(10, 2), "invalid old session does not consume new request");
            ledger.Reset(11);
            Expect(!ledger.TryBegin(10, 3), "queued action invalid after map replacement");
            Expect(ledger.TryBegin(11, 1), "new session starts fresh");
            ledger.Reset(0);
            Expect(!ledger.TryBegin(11, 2), "no active mission");
            var force = new ForcedOrderContext(TestActionKind.Attack, 2, 1, 3);
            Expect(force.Matches(TestActionKind.Attack, 2, 1, 3), "matching force context");
            Expect(!force.Matches(TestActionKind.Defend, 2, 1, 3), "different order untouched");
            Expect(!force.Matches(TestActionKind.Attack, 4, 1, 3), "different AI untouched");
            Expect(!force.Matches(TestActionKind.Attack, 2, 4, 3), "different issuer untouched");
            Expect(!force.Matches(TestActionKind.Attack, 2, 1, 4), "different attack target untouched");
            var defend = new ForcedOrderContext(TestActionKind.Defend, 2, 4, 4);
            Expect(defend.Matches(TestActionKind.Defend, 2, 4, 4), "protection target is explicit requester");
            Expect(ToolPolicy.SameBuilding(10, 10, 2, 2, 3, 3, 99, 99, true), "same gate identity accepted");
            Expect(!ToolPolicy.SameBuilding(10, 11, 2, 2, 3, 3, 99, 99, true), "gate from old session rejected");
            Expect(!ToolPolicy.SameBuilding(10, 10, 2, 4, 3, 3, 99, 99, true), "captured gate rejected");
            Expect(!ToolPolicy.SameBuilding(10, 10, 2, 2, 3, 3, 99, 100, true), "reused building slot rejected");
            Expect(!ToolPolicy.SameBuilding(10, 10, 2, 2, 3, 4, 99, 99, true), "different building rejected");
            Expect(!ToolPolicy.SameBuilding(10, 10, 2, 2, 3, 3, 99, 99, false), "destroyed building rejected");
            Expect(ToolPolicy.InRadius(50, 0, 0, 0, 2500), "spawn boundary inclusive");
            Expect(!ToolPolicy.InRadius(50, 1, 0, 0, 2500), "spawn outside radius rejected");
            Expect(ToolPolicy.InRadius(112, 0, 0, 0, 12544), "engineer boundary inclusive");
            Expect(!ToolPolicy.InRadius(25, 0, 0, 0, 624), "enemy boundary strictly less than 25");
            Expect(!ToolPolicy.InRadius(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue, 12544), "corrupt coordinates cannot overflow radius arithmetic");
            uint mana = 123;
            int calls = 0;
            int returned = ToolPolicy.WithoutCost(() => mana, value => mana = value, 1000000, () => { calls++; Expect(mana == 1000000, "custom high cost available only in own call"); mana -= 1000000; return 7; });
            Expect(mana == 123 && calls == 1 && returned == 7, "cost scope restores and executes once");
            try { ToolPolicy.WithoutCost<int>(() => mana, value => mana = value, 5088, () => { throw new System.Exception("expected fixture"); }); }
            catch (System.Exception error) { Expect(error.Message == "expected fixture", "power exception propagated"); }
            Expect(mana == 123, "cost restored after failure");
            bool rejected = ToolPolicy.WithoutCost(() => mana, value => mana = value, 636, () => false);
            Expect(!rejected && mana == 123, "cost restored after rejection");
            mana = uint.MaxValue;
            ToolPolicy.WithoutCost(() => mana, value => mana = value, 636, () => true);
            Expect(mana == uint.MaxValue, "raw prior mana restored without signed loss");
            return checks;
        }
    }
}
