using System;
using System.IO;
using BepInEx.Configuration;
using Spectator;

internal static class ToolbarThemeTests
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    public static int Main(string[] args)
    {
        try
        {
            string path = Path.Combine(args[0], "Spectator.cfg");
            var config = new ConfigFile(path, false);
            config.Bind("Other", "Preserved", 17, "Unrelated preference").Value = 23;
            ToolbarThemePreference.Initialize(config);
            Check(!ToolbarThemePreference.Classic, "Missing preference defaults to parchment.");
            ToolbarThemePreference.Toggle();
            Check(ToolbarThemePreference.Classic, "Button selects the classic theme.");
            var restarted = new ConfigFile(path, false);
            ToolbarThemePreference.Initialize(restarted);
            Check(ToolbarThemePreference.Classic, "A fresh ConfigFile restores the saved classic choice.");
            Check(restarted.Bind("Other", "Preserved", 17).Value == 23, "Theme writes preserve unrelated preferences.");
            ToolbarThemePreference.Toggle();
            ToolbarThemePreference.Initialize(new ConfigFile(path, false));
            Check(!ToolbarThemePreference.Classic, "Switching back also survives restart.");
            Console.WriteLine("PASS: real BepInEx local theme persistence, defaults, repeated toggles and unrelated values.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
