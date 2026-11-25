using System;
using MakeDesync;

var queueCases = new (string Name, bool Multiplayer, bool? Host, int? Slot, bool Resync, bool Busy, bool Expected)[]
{
    ("Ready client", true, false, 2, false, false, true),
    ("Host", true, true, 1, false, false, false),
    ("Single-player", false, false, 2, false, false, false),
    ("Unresolved role", true, null, 2, false, false, false),
    ("Unresolved slot", true, false, null, false, false, false),
    ("Invalid slot zero", true, false, 0, false, false, false),
    ("Invalid slot nine", true, false, 9, false, false, false),
    ("Repair in progress", true, false, 2, true, false, false),
    ("Attempt already pending or applied", true, false, 2, false, true, false)
};
foreach (var test in queueCases)
{
    Assert(test.Name, test.Expected,
        AttemptPolicy.CanQueue(test.Multiplayer, test.Host, test.Slot, test.Resync, test.Busy));
}

var applyCases = new (string Name, bool DepositMode, bool LordReady, bool DepositReady, bool Expected)[]
{
    ("Vanilla attempt without a deposit target", false, true, false, true),
    ("Vanilla attempt without a Lord target", false, false, false, false),
    ("Deposit attempt with both targets", true, true, true, true),
    ("Deposit attempt without a Lord target", true, false, true, false),
    ("Deposit attempt without a deposit target prevents the Lord write", true, true, false, false),
    ("Deposit attempt without either target", true, false, false, false)
};
foreach (var test in applyCases)
{
    Assert(test.Name, test.Expected, AttemptPolicy.CanApply(test.DepositMode, test.LordReady, test.DepositReady));
}

Console.WriteLine($"PASS: {queueCases.Length + applyCases.Length} MakeDesync policy cases.");

static void Assert(string name, bool expected, bool actual)
{
    if (actual != expected)
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}.");
}
