# UnitLimit release status

**Status:** code newer

- Release: [v1.0.99](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.99)
- Release commit: [4e5980a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4e5980a3956ec75766d62f5e7771f93fe36be1bd)
- Current main commit: [d43d3d8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d43d3d831cc5f89e83ff1abdddb547a7fa579867)

## Relevant changed files

- `Shared/CrashBreadcrumbDiagnostics.cs`
- `Shared/CrashBreadcrumbRecorder.cs`
- `Shared/DebugLogHelper.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplaySessionLifecycle.cs`
- `Shared/SerpLocalization.cs`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/info.json`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ar.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/cs-CZ.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/de-DE.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/el-GR.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/en-US.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/es-ES.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/fr-FR.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/hu-HU.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/it-IT.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ja-JP.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ko-KR.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/nl-NL.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pl-PL.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pt-BR.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ru-RU.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/sv-SE.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/th-TH.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/tr-TR.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/uk-UA.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/zh-CN.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/zh-HK.txt`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/Override/ScriptExtenderUI/UnitLimitSettings.xaml`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/UnitLimit.dll`
- `UnitLimit/BepInEx/plugins/UnitLimit_Serp/UnitLimit.pdb`
- `UnitLimit/Locales/ar.txt`
- `UnitLimit/Locales/cs-CZ.txt`
- `UnitLimit/Locales/de-DE.txt`
- `UnitLimit/Locales/el-GR.txt`
- `UnitLimit/Locales/en-US.txt`
- `UnitLimit/Locales/es-ES.txt`
- `UnitLimit/Locales/fr-FR.txt`
- `UnitLimit/Locales/hu-HU.txt`
- `UnitLimit/Locales/it-IT.txt`
- `UnitLimit/Locales/ja-JP.txt`
- `UnitLimit/Locales/ko-KR.txt`
- `UnitLimit/Locales/nl-NL.txt`
- `UnitLimit/Locales/pl-PL.txt`
- `UnitLimit/Locales/pt-BR.txt`
- `UnitLimit/Locales/ru-RU.txt`
- `UnitLimit/Locales/sv-SE.txt`
- `UnitLimit/Locales/th-TH.txt`
- `UnitLimit/Locales/tr-TR.txt`
- `UnitLimit/Locales/uk-UA.txt`
- `UnitLimit/Locales/zh-CN.txt`
- `UnitLimit/Locales/zh-HK.txt`
- `UnitLimit/src/ActiveUnitCache.cs`
- `UnitLimit/src/UnitLimitIntegration.cs`
- `UnitLimit/src/UnitLimitLobbyViewModel.cs`
- `UnitLimit/src/UnitLimitPlugin.cs`
- `UnitLimit/src/UnitLimitRuntime.cs`
- `UnitLimit/src/UnitLimitRuntime.Integration.cs`
- `UnitLimit/src/UnitLimitRuntime.RecruitmentAvailability.cs`
- `UnitLimit/src/UnitLimitRuntime.Tooltips.cs`
- `UnitLimit/src/UnitLimitRuntime.UnitLimits.cs`
- `UnitLimit/UnitLimit.csproj`

Relevant localization keys: `Common.Limit`, `Common.Preset`, `UnitLimit.CrusaderDeTweakerWarning`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/Shared/CrashBreadcrumbDiagnostics.cs b/Shared/CrashBreadcrumbDiagnostics.cs
index f8a2e523..8f5ff9b4 100644
--- a/Shared/CrashBreadcrumbDiagnostics.cs
+++ b/Shared/CrashBreadcrumbDiagnostics.cs
@@ -21,7 +21,7 @@ namespace Shared
                 return;
 
             initialized = true;
-            bool enabled = DebugLogHelper.IsDebugEnabled();
+            bool enabled = DebugLogHelper.IsDiskDebugEnabled();
             recorder = new CrashBreadcrumbRecorder(
                 enabled,
                 BepInEx.Paths.BepInExRootPath,
@@ -36,7 +36,7 @@ namespace Shared
             DebugLogHelper.LogDebug(
                 log,
                 $"Crash breadcrumb diagnostics enabled: plugin={pluginGuid}, " +
-                "ringCapacity=256, snapshotIntervalSeconds=1, retainedSessions=3.");
+                "ringCapacity=256, snapshotIntervalSeconds=1, retainedGameStarts=5.");
         }
 
         internal static CrashBreadcrumbScope Enter(

diff --git a/Shared/CrashBreadcrumbRecorder.cs b/Shared/CrashBreadcrumbRecorder.cs
index 857b763a..1bb3486e 100644
--- a/Shared/CrashBreadcrumbRecorder.cs
+++ b/Shared/CrashBreadcrumbRecorder.cs
@@ -13,7 +13,8 @@ namespace Shared
     internal sealed class CrashBreadcrumbRecorder : IDisposable
     {
         private const int RingCapacity = 256;
-        private const int RetainedSessions = 3;
+        private const int RetainedGameStarts = 5;
+        private const string PersistenceMutexName = @"Local\SerpsModsDiagnostics.Persistence";
         private readonly object syncRoot = new object();
         private readonly object snapshotWriteRoot = new object();
         private readonly BreadcrumbRecord[] ring = new BreadcrumbRecord[RingCapacity];
@@ -30,6 +31,7 @@ namespace Shared
         private readonly string directory;
         private readonly string filePrefix;
         private readonly int processId;
+        private readonly long processStartedUtcTicks;
         private readonly DateTime startedUtc;
         private readonly long startedTimestamp;
         private readonly long summaryIntervalTicks;
@@ -58,7 +60,11 @@ namespace Shared
             this.pluginName = pluginName ?? string.Empty;
             this.pluginVersion = pluginVersion ?? string.Empty;
             this.statusLogger = statusLogger;
-            processId = Process.GetCurrentProcess().Id;
+            using (Process process = Process.GetCurrentProcess())
+            {
+                processId = process.Id;
+                processStartedUtcTicks = process.StartTime.ToUniversalTime().Ticks;
+            }
             startedUtc = DateTime.UtcNow;
             startedTimestamp = Stopwatch.GetTimestamp();
             summaryIntervalTicks = Math.Max(
@@ -71,7 +77,8 @@ namespace Shared
 
             directory = Path.Combine(rootDirectory ?? string.Empty, "SerpsModsDiagnostics");
             filePrefix = SanitizeFileName(this.pluginGuid) + "-pid" +
-                processId.ToString(CultureInfo.InvariantCulture);
+                processId.ToString(CultureInfo.InvariantCulture) + "-start" +
+                processStartedUtcTicks.ToString(CultureInfo.InvariantCulture);
             TryPrepareDirectory();
 
             TimeSpan interval = snapshotInterval ?? TimeSpan.FromSeconds(1);
@@ -305,7 +312,6 @@ namespace Shared
             try
             {
                 Directory.CreateDirectory(directory);
-                TrimOldSessions();
             }
             catch (Exception exception)
             {
@@ -313,22 +319,68 @@ namespace Shared
             }
         }
 
-        private void TrimOldSessions()
+        private void TrimOldGameStarts()
         {
-            string safeGuid = SanitizeFileName(pluginGuid);
             FileInfo[] files = new DirectoryInfo(directory)
-                .GetFiles(safeGuid + "-pid*-*.txt", SearchOption.TopDirectoryOnly);
+                .GetFiles("*.txt", SearchOption.TopDirectoryOnly);
             var sessions = files
-                .GroupBy(file => GetSessionPrefix(file.Name), StringComparer.OrdinalIgnoreCase)
-                .OrderByDescending(group => group.Max(file => file.LastWriteTimeUtc))
-                .Skip(RetainedSessions - 1)
+                .Select(file => TryGetGameStart(file, out string key, out DateTime startedUtc)
+                    ? new { File = file, Key = key, StartedUtc = startedUtc }
+                    : null)
+                .Where(item => item != null)
+                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
+                .OrderByDescending(group => group.Max(item => item.StartedUtc))
+                .ThenByDescending(group => group.Key, StringComparer.Ordinal)
+                .Skip(RetainedGameStarts)
                 .ToArray();
 
             foreach (var session in sessions)
             {
-                foreach (FileInfo file in session)
-                    file.Delete();
+                foreach (var item in session)
+                    item.File.Delete();
+            }
+        }
+
+        private static bool TryGetGameStart(FileInfo file, out string key, out DateTime startedUtc)
+        {
+            key = null;
+            startedUtc = default(DateTime);
+            string name = Path.GetFileNameWithoutExtension(file.Name);
+            int slotSeparator = name.LastIndexOf('-');
+            if (slotSeparator < 1 || slotSeparator != name.Length - 2 ||
+                (name[name.Length - 1] != '0' && name[name.Length - 1] != '1'))
+            {
+                return false;
+            }
+
+            string prefix = name.Substring(0, slotSeparator);
+            int pidMarker = prefix.LastIndexOf("-pid", StringComparison.OrdinalIgnoreCase);
+            if (pidMarker < 1)
+                return false;
+
+            string identity = prefix.Substring(pidMarker + 4);
+            int startMarker = identity.IndexOf("-start", StringComparison.OrdinalIgnoreCase);
+            string pidText = startMarker < 0 ? identity : identity.Substring(0, startMarker);
+            if (!int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
+                return false;
+
+            if (startMarker < 0)
+            {
+                key = "legacy-pid" + pid.ToString(CultureInfo.InvariantCulture);
+                startedUtc = file.LastWriteTimeUtc;
+                return true;
+            }
+
+            string ticksText = identity.Substring(startMarker + 6);
+            if (!long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) ||
+                ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
+            {
+                return false;
             }
+
+            key = "pid" + pid.ToString(CultureInfo.InvariantCulture) + "-start" + ticksText;
+            startedUtc = new DateTime(ticks, DateTimeKind.Utc);
+            return true;
         }
 
         private void TryWriteSnapshot(bool finalSnapshot)
@@ -355,7 +407,32 @@ namespace Shared
                     string text = FormatSnapshot(snapshot, finalSnapshot);
                     int slot = (int)(snapshot.SnapshotSequence & 1L);
                     string path = Path.Combine(directory, filePrefix + "-" + slot + ".txt");
-                    File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
+                    using (Mutex mutex = new Mutex(false, PersistenceMutexName))
+                    {
+                        bool acquired = false;
+                        try
+                        {
+                            try
+                            {
+                                acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
+                            }
+                            catch (AbandonedMutexException)
+                            {
+                                acquired = true;
+                            }
+
+                            if (!acquired)
+                                throw new TimeoutException("Crash diagnostics persistence lock timed out.");
+
+                            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
+                            TrimOldGameStarts();
+                        }
+                        finally
+                        {
+                            if (acquired)
+                                mutex.ReleaseMutex();
+                        }
+                    }
                     // Publish only the sequence captured in this file. A concurrent writer that
                     // advances the ring remains dirty and is persisted by the next timer pass.
                     Interlocked.Exchange(
@@ -533,12 +610,6 @@ namespace Shared
             return string.IsNullOrWhiteSpace(result) ? "unknown-mod" : result;
         }
 
-        private static string GetSessionPrefix(string fileName)
-        {
-            int slotSeparator = fileName.LastIndexOf('-');
-            return slotSeparator > 0 ? fileName.Substring(0, slotSeparator) : fileName;
-        }
-
         [DllImport("kernel32.dll")]
         private static extern int GetCurrentThreadId();
 

diff --git a/Shared/DebugLogHelper.cs b/Shared/DebugLogHelper.cs
index fbc4eef0..71f41dd1 100644
--- a/Shared/DebugLogHelper.cs
+++ b/Shared/DebugLogHelper.cs
@@ -27,6 +27,26 @@ namespace Shared
             return debugEnabledCache;
         }
 
+        public static bool IsDiskDebugEnabled()
+        {
+            try
+            {
+                foreach (ILogListener listener in Logger.Listeners)
+                {
+                    if (listener is DiskLogListener diskLogListener &&
+                        HasDebugFlag(diskLogListener.DisplayedLogLevel))
+                    {
+                        return true;
+                    }
+                }
+            }
+            catch
+            {
+            }
+
+            return false;
+        }
+
         public static void LogDebug(ManualLogSource log, params object[] parts)
         {
             if (log == null || !IsDebugEnabled())

diff --git a/Shared/GameModeHelper.cs b/Shared/GameModeHelper.cs
index e1e3dca3..24d03f31 100644
--- a/Shared/GameModeHelper.cs
+++ b/Shared/GameModeHelper.cs
@@ -5,7 +5,7 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Reflection;
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
 using Steamworks;
 #endif
 
@@ -134,7 +134,7 @@ namespace Shared
                 $"Steam identity {senderSteamId} belongs to final slot {resolution.PlayerId}.");
         }
 
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
         internal static PlayerIdentityResolution CaptureLocalPlayerId(
             bool preferInGameRoster) =>
             CaptureLocalPlayerId(

diff --git a/Shared/GameplayModActivationGate.cs b/Shared/GameplayModActivationGate.cs
index 7e0182e5..102d723c 100644
--- a/Shared/GameplayModActivationGate.cs
+++ b/Shared/GameplayModActivationGate.cs
@@ -1,6 +1,6 @@
 using BepInEx.Logging;
 using System;
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
 using R3;
 using SHCDESE.EventAPI;
 using SHCDESE.EventAPI.MapLoader;
@@ -20,6 +20,7 @@ namespace Shared
         private static GameModeSnapshot snapshot;
         private static volatile bool isAllowed;
         private static bool initialized;
+        private static bool routineLoggingEnabled = true;
         internal static event Action<bool> StateChanged;
 
         internal static bool IsAllowed => isAllowed;
@@ -30,7 +31,8 @@ namespace Shared
             ManualLogSource logger,
             string modGuid,
             string displayName,
-            Func<bool> isConfiguredEnabled)
+            Func<bool> isConfiguredEnabled,
+            bool logRoutineActivity = true)
         {
             if (initialized)
                 return;
@@ -38,6 +40,7 @@ namespace Shared
             log = logger;
             profile = GameplayModModePolicy.GetProfile(modGuid, displayName);
             configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));
+            routineLoggingEnabled = logRoutineActivity;
 
             MissionEvents.SetOwner(modGuid);
             MissionEvents.SetGate(e =>
@@ -100,6 +103,9 @@ namespace Shared
         private static void LogTransition(string source, bool policyChanged)
         {
             bool configuredEnabled = ReadConfiguredEnabled();
+            if (!routineLoggingEnabled)
+                return;
+
             bool effectiveEnabled = configuredEnabled && IsAllowed;
             GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
             string action = effectiveEnabled
@@ -128,7 +134,7 @@ namespace Shared
             }
         }
 
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
         internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
         internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => Update(next, "test-load");
         internal static void SetStartSnapshotForTests(GameModeSnapshot next) => Update(next, "test-start");

diff --git a/Shared/GameplaySessionLifecycle.cs b/Shared/GameplaySessionLifecycle.cs
index c519e9a2..81aabbad 100644
--- a/Shared/GameplaySessionLifecycle.cs
+++ b/Shared/GameplaySessionLifecycle.cs
@@ -31,7 +31,7 @@ namespace Shared
         private static Action<MissionLifecycleNotification> priority;
         private static MissionLifecycleNotification latest;
         private static string ownerGuid;
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
         private static readonly ManualLogSource log = null;
 #else
         private static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Mission adapter");
@@ -46,7 +46,7 @@ namespace Shared
         }
         private static void EnsureConnected()
         {
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
             if (capability != null) return;
             string owner = ownerGuid ?? typeof(MissionEvents).Assembly.GetTypes()
                 .SelectMany(t => t.GetCustomAttributes(typeof(BepInPlugin), false).Cast<BepInPlugin>())
@@ -58,7 +58,7 @@ namespace Shared
             { capability = null; throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason); }
 #endif
         }
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
         internal static void ResetForTests() { observers.Clear(); latest = null; priority = null; capability = null; }
         internal static void PublishForTests(MissionLifecycleNotification e) => Deliver(e);
 #endif

diff --git a/Shared/SerpLocalization.cs b/Shared/SerpLocalization.cs
index f1f972e6..611b2d4d 100644
--- a/Shared/SerpLocalization.cs
+++ b/Shared/SerpLocalization.cs
@@ -28,11 +28,13 @@ public static class SerpLocalization
     public const string Max = "Common.Max";
     public const string UnitLimitsTitle = "UnitLimit.Title";
     public const string UnitLimitsHelp = "UnitLimit.Help";
+    public const string UnitLimitCrusaderDeTweakerWarning = "UnitLimit.CrusaderDeTweakerWarning";
     public const string BuildingsProductionTitle = "SomeSettings.BuildingsProductionTitle";
     public const string CampfirePeasants = "SomeSettings.CampfirePeasants";
     public const string CampfirePeasantsHelp = "SomeSettings.CampfirePeasantsHelp";
     public const string BuildingLimitsTitle = "BuildingLimit.Title";
     public const string BuildingLimitsHelp = "BuildingLimit.Help";
+    public const string BuildingLimitCrusaderDeTweakerWarning = "BuildingLimit.CrusaderDeTweakerWarning";
     public const string UnitCostsTitle = "UnitCosts.Title";
     public const string UnitCostsHelp = "UnitCosts.Help";
     public const string UnitCostsExtraTitle = "UnitCosts.ExtraTitle";
@@ -246,6 +279,12 @@ public static class SerpLocalization
     private static string cachedSteamLanguage;
     private static string cachedSteamLanguageSource;
     private static BepInEx.Logging.ManualLogSource localizationLog;
+    private static bool routineLoggingEnabled = true;
+
+    internal static void SetRoutineLoggingEnabled(bool enabled)
+    {
+        routineLoggingEnabled = enabled;
+    }
 
     private static readonly Dictionary<string, string> SteamLanguageLocales =
         new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
@@ -331,8 +379,8 @@ public static class SerpLocalization
         { EnableTrailCustomizationButtons, "Customize buttons for Custom and Coop Trails" },
         { EnableTrailCustomizationButtonsHelp, "Shows Customize for Custom Trails and adds it to all four Coop Trails. The host can open the normal skirmish setup before starting the selected mission." },
         { "ExtendedData.HostOptions", "HOST OPTIONS" },
-        { "ExtendedData.SupportedTrailSettings", "MOD SETTINGS IN CUSTOM TRAILS" },
-        { "ExtendedData.SupportedTrailSettingsHelp", "Select which compatible mods are saved with newly created Custom Trail missions. Enabled by default. Unselected mods remain unchanged when the Trail is played." },
+        { "ExtendedData.SupportedTrailSettings", "MOD SETTINGS IN MAPS AND CUSTOM TRAILS" },
+        { "ExtendedData.SupportedTrailSettingsHelp", "Choose whether each host-controlled setting uses the mod default, the normal player/host preset, or a fixed creator value when Maps and Custom Trail missions are saved." },
         { "ExtendedData.IncompatibleTrailMods", "Installed mods with incompatible mod settings:" },
         { "ExtendedData.CompatibilityGuide", "How can mod authors add compatibility?" },
         { "ExtendedData.CompatibilityGuideHelp", "Opens the Extended Data compatibility guide in your browser." },
@@ -376,7 +428,10 @@ public static class SerpLocalization
         { "CastlePlanner.Blueprints", "Blueprints" },
         { "CastlePlanner.BlueprintsHelp", "Displays the selected castle as a local blueprint without changing the game simulation." },
         { "CastlePlanner.SpawnCastle", "Spawn Castle" },
-        { "CastlePlanner.SpawnCastleHelp", "Host setting: spawns the host's selected castle when a new supported singleplayer game starts. Enabling this restores the default castle-content choices." },
+        { "CastlePlanner.SpawnCastleHelp", "Host setting: pauses each new supported skirmish or Trail so every human player can choose Nothing, rotate only their Keep, or preview, rotate, and confirm one free castle. Enabling this restores the default castle-content choices." },
+        { "CastlePlanner.CastleSelectionTimeout", "Castle selection time" },
+        { "CastlePlanner.CastleSelectionTimeoutHelp", "Host setting: real-time limit for choosing a castle at game start, from 60 to 600 seconds." },
+        { "CastlePlanner.CastleSelectionTimeoutValue", "{0} s" },
         { "CastlePlanner.SpawnFortifications", "Also spawn fortifications" },
         { "CastlePlanner.SpawnFortificationsHelp", "Spawns walls, crenels, towers, gates, drawbridges, and stairs from the selected AIV." },
         { "CastlePlanner.SpawnBuildings", "Also spawn buildings" },
@@ -488,11 +546,13 @@ public static class SerpLocalization
         { Max, "Max" },
         { UnitLimitsTitle, "Unit Limits (Human)" },
         { UnitLimitsHelp, "Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Existing living units count against the limit." },
+        { UnitLimitCrusaderDeTweakerWarning, "Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies." },
         { BuildingsProductionTitle, "Buildings and Production" },
         { CampfirePeasants, "Peasants waiting at the campfire" },
         { CampfirePeasantsHelp, "-1 = unchanged. Allowed range: -1 to 500. Sets the maximum peasants waiting at the campfire." },
         { BuildingLimitsTitle, "Building Limits (Human)" },
         { BuildingLimitsHelp, "Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together." },
+        { BuildingLimitCrusaderDeTweakerWarning, "Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies." },
         { UnitCostsTitle, "Base Costs (Human and AI)" },
         { UnitCostsHelp, "Good slots apply to European units. unchanged keeps the vanilla slot; gold -1 stays unchanged." },
         { UnitCostsExtraTitle, "Additional Costs (Human only)" },
@@ -948,6 +1030,9 @@ public static class SerpLocalization
         string englishPath,
         string localePath)
     {
+        if (!routineLoggingEnabled)
+            return;
+
         try
         {
             if (localizationLog == null)

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/info.json b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/info.json
index c472e453..d2ec8b7f 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/info.json
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/info.json
@@ -3,13 +3,19 @@
   "Author": "Serpens66",
   "Name": "Unit Limit",
   "Description": "Limits the number of active soldier units per type for human players in Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.99",
+  "Version": "1.0.100",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "MinimumScriptExtenderVersion": "2.3.0",
   "MaximumScriptExtenderVersion": "",
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.100",
+      "Changes": [
+        "Shows a settings warning when Crusader DE Tweaker is loaded because unit limits should be configured in only one mod."
+      ]
+    },
     {
       "Version": "1.0.99",
       "Changes": [

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ar.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ar.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ar.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ar.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/cs-CZ.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/cs-CZ.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/cs-CZ.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/cs-CZ.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/de-DE.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/de-DE.txt
index 414bf6e3..7a4bea29 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/de-DE.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/de-DE.txt
@@ -1,6 +1,49 @@
+Common.PresetLoad=Preset laden
+Common.PresetSave=Preset speichern
+Common.PresetBasedOn=Basiert auf
+Common.PresetModified=geändert
+Common.PresetLoadConfirm=Laden
+Common.PresetLoadSelectionHelp=Die Auswahl ändert noch nichts. Erst Laden wendet das Preset an.
+Common.PresetLoadCancel=Abbrechen
+Common.PresetDelete=Löschen
+Common.PresetDeleteTitle=Eigenes Preset löschen
+Common.PresetDeleteConfirm=Das eigene Preset wird endgültig gelöscht. Fortfahren?
+Common.PresetDeleteFailedTitle=Preset konnte nicht gelöscht werden
+Common.PresetSaveTarget=Speichern als
+Common.PresetSaveNew=Neues persönliches Preset
+Common.PresetSourcePersonal=Eigene Presets
+Common.PresetSourceBundled=Mit diesem Mod geliefert
+Common.PresetSourceExternal=Externe Presets
+Common.PresetSaveName=Presetname
+Common.PresetSaveDescription=Beschreibung (optional)
+Common.PresetSaveBulkMode=Alle Modi setzen
+Common.PresetSaveBulkModeHelp=Standard: Mod-Standard verwenden. Spieler: aktuellen Spielerwert behalten. Fest: gespeicherten Wert anwenden. Host fest: Hostwerte festlegen, Spieler-/lokale Werte behalten.
+Common.PresetSaveConfirm=Speichern
+Common.PresetSaveNameRequired=Vor dem Speichern einen Presetnamen eingeben.
+Common.PresetSaveCancel=Abbrechen
+Common.PresetSaveOverwriteTitle=Eigenes Preset überschreiben
+Common.PresetSaveOverwrite=Das gewählte eigene Preset wird vollständig ersetzt. Fortfahren?
+Common.PresetSaveFailedTitle=Preset konnte nicht gespeichert werden
+Common.SettingsSource=Einstellungen zurücksetzen auf
+Common.SettingsSourceLoad=Zurücksetzen
+Common.SettingsSourceDefaults=Mod-Standards
+Common.SettingsSourceTrail=Trail-Einstellungen
+Common.SettingsSourceMap=Map-Einstellungen
+Common.SettingsSourceLoadFailed=Einstellungen konnten nicht zurückgesetzt werden
+Common.PresetConfirm=Bestätigen
+Common.PresetStatusDismiss=Schließen
+Common.PresetModeDefault=Standard
+Common.PresetModePlayer=Spieler
+Common.PresetModeFixed=Fest
+Common.PresetModeHostFixed=Host fest
+Common.PresetModeMixed=Gemischt
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Spieler
+Common.PresetScopeLocal=Lokal
+
 # Serp mod localization
+
 # Format: key=value
-Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client-Settings)
@@ -10,16 +53,16 @@ Common.Limit=Begrenzung
 Common.Max=Maximum
 UnitLimit.Title=Einheitenlimits (Mensch)
 UnitLimit.Help=Nur für Menschen! -1 = unbegrenzt. Erlaubter Bereich: -1 bis 5000. Vorhandene lebende Einheiten zaehlen gegen das Limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker ist geladen. Konfiguriere Einheitenlimits nur in einem der beiden Mods; wenn beide Limits festlegen, gilt das strengere Limit.
 
 Common.HostOptions=HOST-OPTIONEN
 Common.ClientOptions=LOKALE CLIENT-OPTIONEN
 Common.HostReadOnly=Werte vom Host – schreibgeschützt
-Common.ResetToDefaultHelp=Setzt die Einstellungen zurück, die du im aktuellen Kontext ändern kannst.
 Common.EnableModHelp=Aktiviert oder deaktiviert diese Mod für die Partie.
 Common.PresetHelp=Wählt ein gespeichertes Preset. Clients ändern damit nur ihre persönlichen Einstellungen.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset und Zurücksetzen betreffen Host-Einstellungen und deine lokalen Client-Optionen.
-Common.ActionsScopeClient=Preset und Zurücksetzen betreffen nur deine lokalen Client-Optionen.
+Common.ActionsScopeHost=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft Host-Einstellungen und deine lokalen Client-Optionen.
+Common.ActionsScopeClient=Das Laden eines Presets oder Zurücksetzen der Einstellungen betrifft nur deine lokalen Client-Optionen.
 Common.ModSettingsSearchLabel=Suche
 Common.ModSettingsSearchHelp=Durchsucht die Titel der Einstellungen. Optional können Tooltips einbezogen werden.
 Common.ModSettingsSearchToggleHelp=Blendet die Einstellungssuche ein oder aus.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Tooltips durchsuchen
 Common.ModSettingsSearchIncludeToolTipsHelp=Durchsucht zusätzlich die erklärenden Tooltips der Einstellungen.
 Common.ModSettingsSearchClearHelp=Leert den Einstellungsfilter.
 Common.ModSettingsSearchNoResults=Keine passenden Einstellungen gefunden.
+Common.SettingsSourceHelp=Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.
+Common.PresetLoadFailedTitle=Preset konnte nicht geladen werden

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/el-GR.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/el-GR.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/el-GR.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/el-GR.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/en-US.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/en-US.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/en-US.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/en-US.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/es-ES.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/es-ES.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/es-ES.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/es-ES.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/fr-FR.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/fr-FR.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/fr-FR.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/fr-FR.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/hu-HU.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/hu-HU.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/hu-HU.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/hu-HU.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/it-IT.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/it-IT.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/it-IT.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/it-IT.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ja-JP.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ja-JP.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ja-JP.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ja-JP.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ko-KR.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ko-KR.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ko-KR.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ko-KR.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/nl-NL.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/nl-NL.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/nl-NL.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/nl-NL.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pl-PL.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pl-PL.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pl-PL.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pl-PL.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pt-BR.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pt-BR.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pt-BR.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/pt-BR.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ru-RU.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ru-RU.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ru-RU.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/ru-RU.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/sv-SE.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/sv-SE.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/sv-SE.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/sv-SE.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/th-TH.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/th-TH.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/th-TH.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/th-TH.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/tr-TR.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/tr-TR.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/tr-TR.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/tr-TR.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
+Common.PresetModeDefault=Default
+Common.PresetModePlayer=Player
+Common.PresetModeFixed=Fixed
+Common.PresetModeHostFixed=Host Fixed
+Common.PresetModeMixed=Mixed
+Common.PresetScopeHost=Host
+Common.PresetScopePlayer=Player
+Common.PresetScopeLocal=Local
+
 # Serp mod localization
+
 # Format: key=value
 Common.EnableMod=Enable Mod
 Common.HostActivationLabel=(Host-)
 Common.ClientActivationLabel=(Client settings)
 Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
 Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
-Common.ResetToDefault=Reset to Default
 Common.Limit=Limit
 Common.Max=Max
 UnitLimit.Title=Unit Limits (Human)
 UnitLimit.Help=Only for Human! -1 = unlimited. Allowed range: -1 to 5000. Existing living units count against the limit.
+UnitLimit.CrusaderDeTweakerWarning=Crusader DE Tweaker is loaded. Configure unit limits in only one of the two mods; if both define limits, the stricter limit applies.
 
 Common.HostOptions=HOST OPTIONS
 Common.ClientOptions=LOCAL CLIENT OPTIONS
 Common.HostReadOnly=Values from host - read-only
-Common.ResetToDefaultHelp=Resets the settings you can control in the current context.
 Common.EnableModHelp=Enables or disables this mod for the match.
 Common.PresetHelp=Selects a saved preset. Clients change only their personal settings.
 Common.Preset=Preset
-Common.ActionsScopeHost=Preset and reset affect host settings and your local client settings.
-Common.ActionsScopeClient=Preset and reset affect only your local client settings.
+Common.ActionsScopeHost=Loading a preset or resetting settings affects host settings and your local client settings.
+Common.ActionsScopeClient=Loading a preset or resetting settings affects only your local client settings.
 Common.ModSettingsSearchLabel=Search
 Common.ModSettingsSearchHelp=Search setting titles. Optionally include tooltips.
 Common.ModSettingsSearchToggleHelp=Show or hide the settings search.
@@ -27,3 +70,5 @@ Common.ModSettingsSearchIncludeToolTips=Search tooltips
 Common.ModSettingsSearchIncludeToolTipsHelp=Also search the explanatory tooltips of settings.
 Common.ModSettingsSearchClearHelp=Clear the settings filter.
 Common.ModSettingsSearchNoResults=No matching settings found.
+Common.SettingsSourceHelp=Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.
+Common.PresetLoadFailedTitle=Preset load failed

diff --git a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/uk-UA.txt b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/uk-UA.txt
index 7c27259b..07f7f32d 100644
--- a/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/uk-UA.txt
+++ b/UnitLimit/BepInEx/plugins/UnitLimit_Serp/Locales/uk-UA.txt
@@ -1,25 +1,68 @@
+Common.PresetLoad=Load preset
+Common.PresetSave=Save preset
+Common.PresetBasedOn=Based on
+Common.PresetModified=modified
+Common.PresetLoadConfirm=Load
+Common.PresetLoadSelectionHelp=Selecting a preset changes nothing until you choose Load.
+Common.PresetLoadCancel=Cancel
+Common.PresetDelete=Delete
+Common.PresetDeleteTitle=Delete personal preset
+Common.PresetDeleteConfirm=The personal preset will be permanently deleted. Continue?
+Common.PresetDeleteFailedTitle=Preset deletion failed
+Common.PresetSaveTarget=Save as
+Common.PresetSaveNew=New personal preset
+Common.PresetSourcePersonal=Personal presets
+Common.PresetSourceBundled=Bundled with this mod
+Common.PresetSourceExternal=External presets
+Common.PresetSaveName=Preset name
+Common.PresetSaveDescription=Description (optional)
+Common.PresetSaveBulkMode=Set all modes
+Common.PresetSaveBulkModeHelp=Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.
+Common.PresetSaveConfirm=Save
+Common.PresetSaveNameRequired=Enter a preset name before saving.
+Common.PresetSaveCancel=Cancel
+Common.PresetSaveOverwriteTitle=Overwrite personal preset
+Common.PresetSaveOverwrite=The selected personal preset will be completely replaced. Continue?
+Common.PresetSaveFailedTitle=Preset save failed
+Common.SettingsSource=Reset settings to
+Common.SettingsSourceLoad=Reset
+Common.SettingsSourceDefaults=Mod defaults
+Common.SettingsSourceTrail=Trail settings
+Common.SettingsSourceMap=Map settings
+Common.SettingsSourceLoadFailed=Could not reset settings
+Common.PresetConfirm=Confirm
+Common.PresetStatusDismiss=Close
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/UnitLimit.diff).
