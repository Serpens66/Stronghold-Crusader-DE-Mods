using System;
using System.Collections.Generic;
using APIShared;
using CrusaderDE;
using Shared;
using System.Linq;
using SHCDESE.Interop;

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
        CoopAiSlotPlan assumed = CoopAiStartPlanFactory.Create(package, 4, custom, -1, null, true, 1);
        Check(complete.ConfirmedCandidateCount == 4 && prefix.ConfirmedCandidateCount == 1 &&
              assumed.CandidateCount == 4 && assumed.CandidateListComplete && assumed.ConfirmedCandidateCount == 1 &&
              CoopAiStartPlanFactory.Create(package, 4, custom, -1, null, true, 9).ConfirmedCandidateCount == 4,
            "a packaged list compared only through candidate zero confirms only that candidate");
        Check(Reject<InvalidOperationException>(() => CoopAiStartPlanFactory.Create(Info(custom, false, true, name: "Sir Otto"), 4, custom, -1, null, true)) != null &&
              Reject<InvalidOperationException>(() => CoopAiStartPlanFactory.Create(Info(custom, false, false, candidates: 1), 4, custom, -1, null, true)) != null,
            "named custom Lords need their own castles and name");

        // Selection identity used to decide between packaged and customized slots.
        Check(CoopAiStartPlanFactory.IsSameSelection(package, Info(custom, false, false, name: "Sir Otto", candidates: 1), false) &&
              !CoopAiStartPlanFactory.IsSameSelection(package, Info(custom, false, false, name: "Sir Otto", candidates: 1), true) &&
              !CoopAiStartPlanFactory.IsSameSelection(package, Info(custom, false, false, name: "Sir Otto", candidates: 4, marker: 8), true) &&
              CoopAiStartPlanFactory.IsSameSelection(Info(3, false, false, community: true), Info(3, false, false, community: true, candidates: 8), true),
            "selection identity compares AIC, castle source and the known candidates");

        TestLocalLaunchValidation();

        Console.WriteLine(failures == 0 ? "All Coop AI start plan tests passed." : failures + " Coop AI start plan test(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void TestLocalLaunchValidation()
    {
        int rat = checked((int)AILords.SK_RAT - 1), snake = checked((int)AILords.SK_SNAKE - 1);
        int abbot = checked((int)AILords.SK_ABBOT - 1), custom = CoopAiSelectionRules.FirstCustomLordType;
        var lobby = new Platform_Multiplayer.MPLobby();
        var partner = new Platform_Multiplayer.MPLobbyMember();
        var opponent = new Platform_Multiplayer.MPLobbyMember();
        partner.SetSkirmishPlayer(snake, 0);
        opponent.SetSkirmishPlayer(abbot, 0);
        lobby.members.Add(partner); lobby.members.Add(opponent);
        lobby.this_player_to_SteamID_mapping[1] = partner.GetSteamID();
        lobby.this_player_to_SteamID_mapping[2] = opponent.GetSteamID();
        var infos = Enumerable.Range(0, 8).Select(_ => Info(rat, true, true)).ToArray();
        infos[2] = Info(abbot, false, true); // The real Abbot report: empty name, custom AIC, standard castles.
        Check(Reject<Exception>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos)) == null,
            "production launch validation permits Snake's Vanilla placeholder and unnamed Abbot AIC");
        foreach (int lord in new[] { rat, snake, abbot })
            Check(!CoopAiStartPlanFactory.Validate(Info(rat, true, true), 2, lord, out bool needed)
                    .Equals(CoopAiCastleKind.Explicit) && !needed,
                "Vanilla placeholder stays unplanned for actual Lord " + lord);

        var remembered = Info(snake, false, false, candidates: 2);
        infos[1] = remembered;
        FRONT_Multiplayer.MPAIVInfo missionAi = infos[2];
        CoopAiStartPlanFactory.PrepareDirectPartner(lobby, infos);
        Check(!ReferenceEquals(infos[1], remembered) && infos[1].lordType == snake && infos[1].builtInLord &&
            infos[1].builtIn && infos[1].aivs.Count == 0 && remembered.aivs.Count == 2 &&
            !remembered.builtInLord && ReferenceEquals(infos[2], missionAi),
            "direct start replaces only the Vanilla partner snapshot without mutating remembered or mission data");

        partner.SetSkirmishPlayer(custom, 0);
        lobby.this_player_to_SteamID_mapping[1] = partner.GetSteamID();
        infos[1] = Info(custom, false, false, name: "Nox", candidates: 2);
        FRONT_Multiplayer.MPAIVInfo nox = infos[1];
        CoopAiStartPlanFactory.PrepareDirectPartner(lobby, infos);
        Check(ReferenceEquals(nox, infos[1]) && Reject<Exception>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos)) == null,
            "direct Custom partner keeps its configuration and candidates");

        Check(BugfixesAndQoL.CoopCustomLordSelectionPolicy.ShouldUseSelectedCoopAiv(true, false, true, true, true, 2),
            "Customize partner selections use the same importer as mission AI selections");
        foreach (var selection in new[] { Info(abbot, false, true), Info(abbot, false, false, community: true),
            Info(abbot, false, false, historical: true), Info(abbot, false, false, candidates: 2) })
        {
            infos[2] = selection;
            Check(Reject<Exception>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos)) == null,
                "production launch gate accepts unnamed Vanilla AIC with castle source " +
                CoopAiStartPlanFactory.Validate(selection, 3, abbot, out _));
        }

        infos[2] = Info(abbot, false, true); infos[2].lordConfig = null;
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos))?.Code == "MissingAic",
            "missing AIC is distinguished from a legitimate empty Lord name");
        infos[2] = Info(snake, false, true);
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos))?.Code == "LordTypeMismatch",
            "nonstandard data with a different actual Lord still blocks");
        infos[2] = null;
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos))?.Code == "MissingSelection",
            "missing mission selection never becomes a Vanilla fallback");
        infos[2] = Info(abbot, false, false, candidates: 2); infos[2].aivs[1].data = null;
        var badCandidate = Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos));
        Check(badCandidate?.Code == "InvalidCandidate" && badCandidate.Message.Contains("candidate=1"),
            "invalid selected candidate reports its slot and index");
        Check(CoopAiStartPlanFactory.Create(infos[2], 3, abbot, 1, null, false).CandidateCount == 1,
            "a known transmitted prefix does not inspect unused candidate data");
        infos[2].builtIn = true;
        Check(Reject<Exception>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos)) == null,
            "inactive candidate data does not invalidate an effective standard castle source");
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.Validate(Info(0, true, true), 2, -1, out _))?.Code == "InvalidLordType" &&
            Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.Validate(Info(0, true, true), 9, rat, out _))?.Code == "InvalidSlot",
            "invalid Lord types and player slots do not pass the Vanilla fast path");
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.Validate(
            Info(custom, false, false, candidates: 1), 2, custom, out _))?.Code == "MissingLordName",
            "named custom Lords still need a name");
        var expected = CoopAiStartPlanFactory.Create(nox, 2, custom, -1, null, true);
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.RequirePlan(expected, null, true))?.Code == "MissingImportPlan",
            "custom partner cannot silently start without an import provider");
        var different = CoopAiStartPlanFactory.Create(Info(custom, false, false, name: "Nox", candidates: 2, marker: 9), 2, custom, -1, null, true);
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.RequirePlan(expected, different, true))?.Code == "ImportPlanMismatch",
            "a contradictory winning plan is rejected before native start");
        var prefix = CoopAiStartPlanFactory.Create(nox, 2, custom, 1, null, false);
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.RequirePlan(expected, prefix, true)) != null &&
            Reject<Exception>(() => CoopAiStartPlanFactory.RequirePlan(prefix, expected, false)) == null,
            "local starts need all candidates while multiplayer prefixes retain their established contract");

        // The final gate has no cached success: a rejected selection can be repaired in the same lobby.
        infos[2] = Info(abbot, false, true); infos[2].lordConfig = null;
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos)) != null,
            "first launch rejects incomplete AIC");
        infos[2] = Info(abbot, false, true);
        Check(Reject<Exception>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos)) == null,
            "repaired selection passes a second launch without reopening the lobby");
        ulong[] mapping = lobby.this_player_to_SteamID_mapping;
        lobby.this_player_to_SteamID_mapping = null;
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos))?.Code == "InvalidSlot",
            "missing lobby mapping produces an actionable failure before a lookup");
        lobby.this_player_to_SteamID_mapping = mapping;
        lobby.members.Add(partner);
        Check(Reject<CoopAiSelectionException>(() => CoopAiStartPlanFactory.ValidateLobby(lobby, infos))?.Code == "InvalidSlot",
            "two AI entries cannot use the same mapped player slot");
        lobby.members.RemoveAt(lobby.members.Count - 1);
        var restart = new CrusaderDE.HUD_IngameMenu.RestartSkirmishMapInfo();
        restart.importAIVs(infos);
        CoopAiStartPlanFactory.ValidateLobby(lobby, restart.aivs);
        CoopAiSlotPlan restored = CoopAiStartPlanFactory.Create(restart.aivs[1], 2, custom, -1, null, true);
        Check(Reject<Exception>(() => CoopAiStartPlanFactory.RequirePlan(expected, restored, true)) == null,
            "Vanilla's actual restart snapshot preserves the effective Customize AIC and candidate list");
        var lifecycle = new ExtendedData.MissionPresetLifecycleState();
        lifecycle.Prepare(ExtendedData.MissionPresetLaunchKind.CoopTrail);
        lifecycle.ConfirmStarted(ExtendedData.MissionPresetLaunchKind.CoopTrail);
        lifecycle.Prepare(ExtendedData.MissionPresetLaunchKind.CoopTrail);
        lifecycle.CancelPendingLaunch();
        Check(lifecycle.PendingLaunch == ExtendedData.MissionPresetLaunchKind.None &&
            lifecycle.ActiveMission == ExtendedData.MissionPresetLaunchKind.CoopTrail,
            "frontend rejection clears only the pending attempt and preserves an active restart source");
    }
}
