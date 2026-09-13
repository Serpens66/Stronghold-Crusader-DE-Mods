using System;
using System.IO;
using System.Text.RegularExpressions;

internal static class Program
{
    private static int failures;

    private static void Main()
    {
        Check(!UnitCosts.UnitExtraHorseCostPolicy.NormalizeHorseRequirement(0, true), "zero horse flag");
        Check(UnitCosts.UnitExtraHorseCostPolicy.NormalizeHorseRequirement(1, true), "one horse flag");
        Check(UnitCosts.UnitExtraHorseCostPolicy.NormalizeHorseRequirement(100, true), "positive horse flag normalization");
        Check(!UnitCosts.UnitExtraHorseCostPolicy.NormalizeHorseRequirement(1, false), "unsupported horse flag");
        Check(UnitCosts.UnitExtraHorseCostPolicy.CalculateAvailableHorseSlots(4, 1, 3) == 3, "normal stable availability");
        Check(UnitCosts.UnitExtraHorseCostPolicy.CalculateAvailableHorseSlots(4, 0, 2) == 2, "free slots cap availability");
        Check(UnitCosts.UnitExtraHorseCostPolicy.CalculateAvailableHorseSlots(1, 3, 4) == 0, "invalid used count fails closed");
        Check(UnitCosts.UnitExtraHorseCostPolicy.ApplyHorseAffordabilityLimit(5, 3) == 3, "horse Ctrl/Shift ceiling");
        CheckConsumedHorseTotal(4, 4, true, 3, "first consume before recount");
        CheckConsumedHorseTotal(3, 4, true, 2, "second consume with stale used count");
        CheckConsumedHorseTotal(2, 4, true, 1, "third consume with stale used count");
        CheckConsumedHorseTotal(1, 4, true, 0, "fourth consume with stale used count");
        CheckConsumedHorseTotal(1, 0, true, 0, "consume before used-horse recount");
        CheckConsumedHorseTotal(0, 0, false, 0, "zero total fails closed");
        CheckConsumedHorseTotal(5, 0, false, 5, "total above stable capacity fails closed");
        CheckConsumedHorseTotal(1, -1, false, 1, "negative used count fails closed");
        CheckConsumedHorseTotal(1, 5, false, 1, "used count above stable capacity fails closed");
        Check(UnitCosts.UnitExtraHorseCostPolicy.IsStableHorseSlotPairComplete(0, 0), "empty slot pair is complete");
        Check(UnitCosts.UnitExtraHorseCostPolicy.IsStableHorseSlotPairComplete(12, 34), "occupied slot pair is complete");
        Check(!UnitCosts.UnitExtraHorseCostPolicy.IsStableHorseSlotPairComplete(12, 0), "unit-only slot is incomplete");
        Check(!UnitCosts.UnitExtraHorseCostPolicy.IsStableHorseSlotPairComplete(0, 34), "global-only slot is incomplete");
        Check(UnitCosts.UnitExtraHorseCostPolicy.IsOccupiedStableHorseSlotCountValid(4, 4), "full stable topology");
        Check(UnitCosts.UnitExtraHorseCostPolicy.IsOccupiedStableHorseSlotCountValid(3, 3), "batch topology with stale used count");
        Check(!UnitCosts.UnitExtraHorseCostPolicy.IsOccupiedStableHorseSlotCountValid(2, 3), "occupied slots above total fail closed");

        string root = FindWorkspaceRoot();
        string lobby = File.ReadAllText(Path.Combine(root, "UnitCosts", "src", "UnitCostsLobbyViewModel.cs"));
        string runtime = File.ReadAllText(Path.Combine(root, "UnitCosts", "src", "UnitCostsRuntime.cs"));
        Check(lobby.Contains("HumanExtraCostGoods.Length + 1"), "21-to-22 field migration");
        Check(lobby.Contains("case eChimps.CHIMP_TYPE_KNIGHT:"), "Vanilla knight horse exclusion");
        Check(lobby.Contains("case eChimps.CHIMP_TYPE_ARAB_BALLISTA:"), "siege horse exclusion");
        Check(runtime.Contains("stableIds.Sort();"), "deterministic stable ordering");
        Check(runtime.Contains("for (int slot = 0; slot < StableHorseSlotCount; slot++)"), "deterministic slot ordering");
        Check(!runtime.Contains("GetStablesUnitIdLink("), "unsafe Script Extender getter is not used");
        Check(runtime.Contains("stable->r_TotalHorses = (byte)totalAfter;"), "disband consumes exactly one total horse");
        Check(runtime.Contains("TryCountOccupiedStableHorseSlots"), "disband validates all stable horse slots");
        Check(!Regex.IsMatch(runtime, @"stable->r_UsedHorses\s*=(?!=)"), "disband does not write Vanilla used-horse accounting");
        Check(!Regex.IsMatch(runtime, @"stable->r_HorseRechargeTimer\s*=(?!=)"), "disband does not reset Vanilla recharge timer");

        if (failures != 0)
            Environment.Exit(1);
    }

    private static string FindWorkspaceRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "UnitCosts")))
            directory = directory.Parent;

        if (directory == null)
            throw new DirectoryNotFoundException("Could not locate the workspace root.");

        return directory.FullName;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
            return;

        Console.Error.WriteLine("FAILED: " + name);
        failures++;
    }

    private static void CheckConsumedHorseTotal(
        int totalBefore,
        int usedBefore,
        bool expectedSuccess,
        int expectedTotalAfter,
        string name)
    {
        bool success = UnitCosts.UnitExtraHorseCostPolicy.TryCalculateConsumedHorseTotal(
            totalBefore,
            usedBefore,
            out int totalAfter);
        Check(success == expectedSuccess && totalAfter == expectedTotalAfter, name);
    }
}
