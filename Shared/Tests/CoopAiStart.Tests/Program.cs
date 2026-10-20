using System;
using System.Collections.Generic;
using APIShared;
using CrusaderDE;
using Shared;

// Plans that BugfixesAndQoL and ExtendedData hand to APIShared's Coop AI start import. Both mods compile this
// one factory, so equal selections must produce equal plans; Vanilla selections must stay unplanned.
internal static class Program
{
    private static int failures;

    private static void Check(bool condition, string message)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    private static T Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        return null;
    }

    private static FRONT_Multiplayer.MPAIVInfo Info(int lordType, bool builtInLord, bool builtIn,
        bool community = false, bool historical = false, string name = "", int candidates = 0, int marker = 7)
    {
        var info = new FRONT_Multiplayer.MPAIVInfo
        {
            lordType = lordType, builtInLord = builtInLord, builtIn = builtIn, community = community,
            historical = historical, lordName = name,
            lordConfig = builtInLord ? null : new CustomisationFileManager.CustomLordConfig
            {
                name = "aic", checksum = 5,
                lordData = new EngineInterface.AILordConfigTransferData { opponent_type = marker },
            },
        };
        for (int index = 0; index < candidates; index++)
            info.aivs.Add(new CustomisationFileManager.CustomAIV { checksum = (ulong)(index + 1), data = new[] { (short)(index + 1) } });
        return info;
    }

    private static int Main()
    {
        int custom = CoopAiSelectionRules.FirstCustomLordType;

        // Vanilla Coop lobbies can carry the "0:0:0:0" placeholder; a fully Vanilla selection never aborts.
        Check(CoopAiStartPlanFactory.Create(Info(0, true, true), 3, 5, 1, null, false) == null &&
              CoopAiStartPlanFactory.Create(Info(5, true, false), 3, 5, -1, null, true) == null,
            "Vanilla AIC with standard castles keeps Vanilla's upload, also for a placeholder type");
        Check(Reject<InvalidOperationException>(() => CoopAiStartPlanFactory.Create(Info(0, true, false, community: true), 3, 5, 1, null, false)) != null,
            "a changed Lord type with non-standard castles still aborts");

        // F4: custom AIC with Vanilla castle sets.
        CoopAiSlotPlan standard = CoopAiStartPlanFactory.Create(Info(3, false, true), 3, 3, 1, null, false);
        CoopAiSlotPlan community = CoopAiStartPlanFactory.Create(Info(3, false, false, community: true), 3, 3, 1, null, false);
        CoopAiSlotPlan historical = CoopAiStartPlanFactory.Create(Info(3, true, false, historical: true), 3, 3, 1, null, false);
        Check(standard.CastleSource == CoopAiCastleSource.Standard && standard.HasLordConfiguration &&
              standard.LordConfiguration.opponent_type == 7 && standard.CandidateListComplete,
            "a standard Lord with custom AIC uses standard castles plus its AIC");
        Check(community.CastleSource == CoopAiCastleSource.Community && community.HasLordConfiguration &&
              historical.CastleSource == CoopAiCastleSource.Historical && !historical.HasLordConfiguration,
            "community and historical castle sets are kept independently of the AIC");

        // Explicit castles: lobby prefix versus complete package list.
        FRONT_Multiplayer.MPAIVInfo package = Info(custom, false, false, name: "Sir Otto", candidates: 4);
        CoopAiSlotPlan complete = CoopAiStartPlanFactory.Create(package, 4, custom, -1, null, true);
        CoopAiSlotPlan prefix = CoopAiStartPlanFactory.Create(package, 4, custom, 1, null, false);
        CoopAiSlotPlan extended = CoopAiStartPlanFactory.Create(package, 4, custom, 1,
            new List<short[]> { new short[] { 2 }, new short[] { 3 }, new short[] { 4 } }, true);
        Check(complete.CandidateCount == 4 && prefix.CandidateCount == 1 && !prefix.CandidateListComplete &&
              extended.CandidateCount == 4 && extended.CopyCandidate(3)[0] == 4,
            "complete, transmitted and synchronized candidate lists keep their order");
        Check(Reject<InvalidOperationException>(() => CoopAiStartPlanFactory.Create(Info(custom, false, true, name: "Sir Otto"), 4, custom, -1, null, true)) != null &&
              Reject<InvalidOperationException>(() => CoopAiStartPlanFactory.Create(Info(custom, false, false, candidates: 1), 4, custom, -1, null, true)) != null,
            "named custom Lords need their own castles and name");

        // Selection identity used to decide between packaged and customized slots.
        Check(CoopAiStartPlanFactory.IsSameSelection(package, Info(custom, false, false, name: "Sir Otto", candidates: 1), false) &&
              !CoopAiStartPlanFactory.IsSameSelection(package, Info(custom, false, false, name: "Sir Otto", candidates: 1), true) &&
              !CoopAiStartPlanFactory.IsSameSelection(package, Info(custom, false, false, name: "Sir Otto", candidates: 4, marker: 8), true) &&
              CoopAiStartPlanFactory.IsSameSelection(Info(3, false, false, community: true), Info(3, false, false, community: true, candidates: 8), true),
            "selection identity compares AIC, castle source and the known candidates");

        Console.WriteLine(failures == 0 ? "All Coop AI start plan tests passed." : failures + " Coop AI start plan test(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
