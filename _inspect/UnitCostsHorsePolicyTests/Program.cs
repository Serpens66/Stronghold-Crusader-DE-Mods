using System;
using System.IO;

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

        string root = FindWorkspaceRoot();
        string lobby = File.ReadAllText(Path.Combine(root, "UnitCosts", "src", "UnitCostsLobbyViewModel.cs"));
        string runtime = File.ReadAllText(Path.Combine(root, "UnitCosts", "src", "UnitCostsRuntime.cs"));
        Check(lobby.Contains("HumanExtraCostGoods.Length + 1"), "21-to-22 field migration");
        Check(lobby.Contains("case eChimps.CHIMP_TYPE_KNIGHT:"), "Vanilla knight horse exclusion");
        Check(lobby.Contains("case eChimps.CHIMP_TYPE_ARAB_BALLISTA:"), "siege horse exclusion");
        Check(runtime.Contains("stableIds.Sort();"), "deterministic stable ordering");
        Check(runtime.Contains("for (int slot = 0; slot < StableHorseSlotCount; slot++)"), "deterministic slot ordering");
        Check(!runtime.Contains("GetStablesUnitIdLink("), "unsafe Script Extender getter is not used");

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
}
