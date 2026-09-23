using System;
using System.IO;

namespace AivLobbyPresetTest
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string path = Path.Combine(Path.GetTempPath(), "aiv-lobby-preset-" + Guid.NewGuid() + ".json");
            try
            {
                string sample = File.ReadAllText(args[0]);
                File.WriteAllText(path, sample);
                var first = LobbyPreset.Read(path);
                Require(first.Enabled && first.Players.Count == 3 &&
                    first.Players[1].Id == 2 && first.Players[1].AivDefault == 6 &&
                    first.Players[1].KeepSlot == 6, "initial Crater Lake setup");
                File.WriteAllText(path, sample.Replace("\"aivDefault\": 6", "\"aivDefault\": 5"));
                Require(LobbyPreset.Read(path).Players[1].AivDefault == 5,
                    "config reloaded on next lobby opening");
                Reject(path, sample.Replace("\"keepSlot\": 5", "\"keepSlot\": 6"), "duplicate Keep slot");
                Reject(path, sample.Replace("\"id\": 3", "\"id\": 4"), "player order gap");
                Reject(path, sample.Replace("\"aivDefault\": 6", "\"aivDefault\": 9"), "invalid AIV variant");
                File.WriteAllText(path, sample.Replace("\"enabled\": true", "\"enabled\": false"));
                Require(!LobbyPreset.Read(path).Enabled, "disabled preset yields control");
                Console.WriteLine("AIV lobby preset parser tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static void Reject(string path, string contents, string name)
        {
            File.WriteAllText(path, contents);
            try { LobbyPreset.Read(path); }
            catch (InvalidDataException) { return; }
            throw new Exception("Expected rejection: " + name);
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new Exception("Failed: " + name);
        }
    }
}
