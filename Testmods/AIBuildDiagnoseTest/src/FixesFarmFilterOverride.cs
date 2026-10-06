using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AIBuildDiagnoseTest
{
    // Startup-only compatibility control. No native hook is installed or removed here.
    internal sealed class FixesFarmFilterOverride
    {
        private const string NativeHash =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int CandidateGateRva = 0x58B12;
        private static readonly byte[] VanillaGate =
        {
            0x75, 0x1A, 0x44, 0x38, 0xB2, 0x37, 0xB8, 0x05,
            0x00, 0x75, 0x11, 0x44, 0x38, 0xB2, 0x38, 0xB8,
            0x05, 0x00, 0x7F, 0x08, 0x84, 0xDB, 0x0F, 0x84
        };

        private readonly ManualLogSource log;
        private readonly ConfigFile config;
        private readonly ConfigEntry<bool> entry;
        private readonly bool originalValue;
        private readonly bool originalSaveOnSet;
        private readonly string originalFileHash;
        private readonly bool changedInMemory;
        private readonly string startupStatus;
        private bool completed;

        private FixesFarmFilterOverride(ManualLogSource logger, ConfigFile source,
            ConfigEntry<bool> setting, bool value, bool saveOnSet, string fileHash,
            bool changed, string status)
        {
            log = logger;
            config = source;
            entry = setting;
            originalValue = value;
            originalSaveOnSet = saveOnSet;
            originalFileHash = fileHash;
            changedInMemory = changed;
            startupStatus = status;
        }

        internal static FixesFarmFilterOverride Begin(ManualLogSource logger)
        {
            if (!Chainloader.PluginInfos.TryGetValue("fixes", out PluginInfo info))
                return new FixesFarmFilterOverride(logger, null, null, false, false,
                    null, false, "fixes-absent");
            if (info.Instance == null)
                return new FixesFarmFilterOverride(logger, null, null, false, false,
                    null, false, "fixes-instance-unavailable");

            ConfigFile source = info.Instance.Config;
            if (source == null || !source.TryGetEntry<bool>(
                "FixPlacementSelectionNotAccountingForFarmland", "Enabled",
                out ConfigEntry<bool> setting))
                return new FixesFarmFilterOverride(logger, null, null, false, false,
                    null, false, "fixes-setting-unavailable");

            bool original = setting.Value;
            bool saveOnSet = source.SaveOnConfigSet;
            string hash;
            try { hash = HashFile(source.ConfigFilePath); }
            catch (Exception ex)
            {
                return new FixesFarmFilterOverride(logger, null, null, false, false,
                    null, false, "config-hash-unavailable:" + ex.GetType().Name);
            }
            if (!original)
                return new FixesFarmFilterOverride(logger, source, setting, false,
                    saveOnSet, hash, false, "already-disabled-before-diagnostic");

            try
            {
                source.SaveOnConfigSet = false;
                setting.Value = false;
                if (!string.Equals(HashFile(source.ConfigFilePath), hash,
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Fixes configuration file changed during in-memory override.");
                return new FixesFarmFilterOverride(logger, source, setting, true,
                    saveOnSet, hash, true, "memory-disabled-before-native-load");
            }
            catch (Exception ex)
            {
                // Restore the original in-memory state without saving a transient false value.
                try { source.SaveOnConfigSet = false; setting.Value = original; }
                finally { source.SaveOnConfigSet = saveOnSet; }
                return new FixesFarmFilterOverride(logger, source, setting, original,
                    saveOnSet, hash, false, "override-failed:" + ex.GetType().Name);
            }
        }

        internal bool Complete(IntPtr moduleHandle)
        {
            if (completed) return false;
            completed = true;
            bool restored = true;
            if (changedInMemory)
            {
                try
                {
                    config.SaveOnConfigSet = false;
                    entry.Value = originalValue;
                }
                catch (Exception ex)
                {
                    restored = false;
                    log.LogError("AI_BUILD_FIXES_FILTER: restore-failed=" + ex);
                }
                finally { config.SaveOnConfigSet = originalSaveOnSet; }
            }

            bool fileUnchanged = originalFileHash == null ||
                string.Equals(HashFile(config.ConfigFilePath), originalFileHash,
                    StringComparison.OrdinalIgnoreCase);
            bool nativeMatch = string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                    NativeHash, StringComparison.OrdinalIgnoreCase) &&
                IsVanillaCandidateGate(moduleHandle);
            bool ready = restored && fileUnchanged && nativeMatch &&
                startupStatus != "fixes-instance-unavailable" &&
                startupStatus != "fixes-setting-unavailable" &&
                !startupStatus.StartsWith("config-hash-unavailable:", StringComparison.Ordinal) &&
                !startupStatus.StartsWith("override-failed:", StringComparison.Ordinal);
            log.LogInfo("AI_BUILD_FIXES_FILTER: startup=" + startupStatus +
                "; nativeGateVanilla=" + nativeMatch +
                "; configFileUnchanged=" + fileUnchanged +
                "; memoryRestored=" + restored +
                "; activeProbesAllowed=" + ready +
                "; candidateGatePatched=" + !nativeMatch);
            return ready;
        }

        private static bool IsVanillaCandidateGate(IntPtr moduleHandle)
        {
            if (moduleHandle == IntPtr.Zero) return false;
            var live = new byte[VanillaGate.Length];
            Marshal.Copy(new IntPtr(checked(moduleHandle.ToInt64() + CandidateGateRva)),
                live, 0, live.Length);
            for (int i = 0; i < live.Length; i++)
                if (live[i] != VanillaGate[i]) return false;
            return true;
        }

        private static string HashFile(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }
    }
}
