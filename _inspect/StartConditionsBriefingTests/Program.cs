using StartConditions;
using System;
using System.IO;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Check(StartGoldPolicy.CalculateGold(2000, -1, 0) == 2000,
            "unchanged settings retain Vanilla gold");
        Check(StartGoldPolicy.CalculateGold(0, -1, 500) == 500,
            "positive Add follows human No Starting Gold");
        Check(StartGoldPolicy.CalculateGold(2000, -1, -2500) == 0,
            "negative Add is clamped at zero without Set");
        Check(StartGoldPolicy.CalculateGold(2000, 0, 0) == 0 &&
              StartGoldPolicy.CalculateGold(0, 3000, 0) == 3000,
            "Set replaces both Vanilla and No Starting Gold bases");
        Check(StartGoldPolicy.CalculateGold(2000, 20000, 123) == 20123,
            "positive Add follows Set");
        Check(StartGoldPolicy.CalculateGold(2000, 20000, -123) == 19877,
            "negative Add follows Set");
        Check(StartGoldPolicy.CalculateGold(2000, 0, 123) == 123 &&
              StartGoldPolicy.CalculateGold(2000, 0, -123) == 0,
            "Set zero applies signed Add and clamps at zero");
        Check(StartGoldPolicy.CalculateGold(2000, 100, -500) == 0,
            "negative Add cannot reduce Set below zero");
        Check(StartGoldPolicy.CalculateGold(6000, -1, 1000) == 7000,
            "AI uses its unchanged effective Vanilla base");

        var state = new StartConditionsMapSessionState();
        Check(!state.IsNewGame, "unstarted sessions do not adjust the briefing");
        Check(state.TryBeginNewMap() && state.IsNewGame,
            "new maps enable the briefing adjustment");
        state.Reset();
        state.MarkSaveLoaded();
        Check(!state.IsNewGame && !state.TryBeginNewMap(),
            "save loads and lifecycle replays remain excluded");

        string root = FindRoot();
        string runtime = File.ReadAllText(Path.Combine(
            root, "StartConditions", "src", "StartConditionsRuntime.cs"));
        string registration = File.ReadAllText(Path.Combine(
            root, "StartConditions", "src", "StartConditionsBriefingGoldRegistration.cs"));
        string resources = File.ReadAllText(Path.Combine(
            root, "StartConditions", "src", "StartConditionsRuntime.StartResources.cs"));
        Check(runtime.Contains("settings.EnableMod || StartConditionsIntegration.HasMissionOverride") &&
              runtime.Contains("!mapSessionState.IsNewGame"),
            "runtime supports regular activation, mission override, and new-game gating");
        Check(registration.Contains("BriefingGoldAdjustmentStage.ModAdjustment") &&
              runtime.Contains("static StartConditionsBriefingGoldRegistration") &&
              !runtime.Contains("processBriefingGoldRegistration?.Dispose"),
            "Start Conditions registration uses the shared mod stage and is process rooted");
        Check(resources.Contains("addGold < 0") &&
              resources.Contains("StartGoldPolicy.CalculateGold(0, setGold, addGold)") &&
              resources.Contains("addGold < 0 && setGold < 0") &&
              resources.Contains("AddIncomingGood(playerId, eGoods.STORED_GOLD, addGold)"),
            "runtime applies negative Add directly after Set while preserving the positive and no-Set incoming paths");

        foreach (string localePath in Directory.GetFiles(
            Path.Combine(root, "StartConditions", "Locales"), "*.txt"))
        {
            string locale = File.ReadAllText(localePath);
            Check(locale.Contains("StartConditions.SetStartGoldHelp=") &&
                  locale.Contains("effective Vanilla") ||
                  Path.GetFileName(localePath) == "de-DE.txt" &&
                  locale.Contains("effektive Vanilla"),
                "updated Set tooltip exists in " + Path.GetFileName(localePath));
            Check(locale.Contains("StartConditions.AddStartGoldHelp=") &&
                  (locale.Contains("signed adjustment") || locale.Contains("vorzeichenbehaftete Anpassung")),
                "updated Add tooltip exists in " + Path.GetFileName(localePath));
        }

        if (failures == 0)
        {
            Console.WriteLine("StartConditions briefing-gold policy tests passed.");
            return 0;
        }
        return 1;
    }

    private static string FindRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "StartConditions")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Workspace root not found.");
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
            return;
        failures++;
        Console.Error.WriteLine("FAIL: " + message);
    }
}
